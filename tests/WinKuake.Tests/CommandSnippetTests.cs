using System.Linq;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class CommandSnippetTests
{
    [Fact]
    public void Defaults_ContainCommonDevCommands()
    {
        var snippets = CommandSnippetService.Defaults();
        Assert.NotEmpty(snippets);
        // Cuando menos: git status, ls/dir, clear.
        Assert.Contains(snippets, s => s.Command.Contains("git status"));
        Assert.Contains(snippets, s => s.Command == "clear" || s.Command == "cls");
    }

    [Fact]
    public void Filter_EmptyQuery_ReturnsAll()
    {
        var snippets = new[]
        {
            new CommandSnippet("Status", "git status"),
            new CommandSnippet("List", "ls -la"),
        };
        var r = CommandSnippetService.Filter(snippets, "");
        Assert.Equal(2, r.Count);
    }

    [Fact]
    public void Filter_MatchesNameOrCommand_CaseInsensitive()
    {
        var snippets = new[]
        {
            new CommandSnippet("Status",      "git status"),
            new CommandSnippet("List files",  "ls -la"),
            new CommandSnippet("Docker ps",   "docker ps"),
        };
        Assert.Single(CommandSnippetService.Filter(snippets, "list"));
        Assert.Single(CommandSnippetService.Filter(snippets, "DOCKER"));
        // "s" matches "Status", "List files" y "docker ps" (todos contienen 's')
        var sMatches = CommandSnippetService.Filter(snippets, "s");
        Assert.Equal(3, sMatches.Count);
    }

    [Fact]
    public void Filter_NoMatch_ReturnsEmpty()
    {
        var snippets = new[] { new CommandSnippet("X", "x") };
        Assert.Empty(CommandSnippetService.Filter(snippets, "xyzzy"));
    }

    [Fact]
    public void Filter_MultipleTokens_AllMustMatch()
    {
        var snippets = new[]
        {
            new CommandSnippet("Git log",     "git log --oneline"),
            new CommandSnippet("Git status",  "git status"),
            new CommandSnippet("Docker logs", "docker logs"),
        };
        // "git log" → solo el primero
        var r = CommandSnippetService.Filter(snippets, "git log");
        Assert.Single(r);
        Assert.Equal("Git log", r[0].Name);
    }

    [Fact]
    public void Snippet_ToStringShowsName()
    {
        var s = new CommandSnippet("Status", "git status");
        Assert.Equal("Status", s.ToString());
    }

    // -- Variable expansion --------------------------------------------------

    [Fact]
    public void Expand_ReplacesCwd()
    {
        var ctx = new SnippetContext { Cwd = "/home/foo" };
        Assert.Equal("cd /home/foo", CommandSnippetService.Expand("cd {cwd}", ctx));
    }

    [Fact]
    public void Expand_ReplacesMultipleOccurrences()
    {
        var ctx = new SnippetContext { Cwd = "/x" };
        Assert.Equal("cp /x/a /x/b", CommandSnippetService.Expand("cp {cwd}/a {cwd}/b", ctx));
    }

    [Fact]
    public void Expand_UnknownPlaceholder_LeavesAsIs()
    {
        var ctx = new SnippetContext { Cwd = "/x" };
        Assert.Equal("hola {desconocido}", CommandSnippetService.Expand("hola {desconocido}", ctx));
    }

    [Fact]
    public void Expand_NullContext_LeavesPlaceholdersAsIs()
    {
        Assert.Equal("cd {cwd}", CommandSnippetService.Expand("cd {cwd}", null));
    }

    [Fact]
    public void Expand_NoPlaceholders_ReturnsSameString()
    {
        Assert.Equal("git status", CommandSnippetService.Expand("git status", new SnippetContext()));
    }

    [Fact]
    public void Expand_IsCaseInsensitiveForPlaceholders()
    {
        var ctx = new SnippetContext { Cwd = "/x" };
        Assert.Equal("/x", CommandSnippetService.Expand("{CWD}", ctx));
        Assert.Equal("/x", CommandSnippetService.Expand("{Cwd}", ctx));
    }

    [Fact]
    public void Expand_DatePlaceholder_UsesIsoDate()
    {
        var ctx = new SnippetContext { Date = new System.DateTime(2026, 5, 10) };
        Assert.Equal("backup-2026-05-10.tar", CommandSnippetService.Expand("backup-{date}.tar", ctx));
    }

    [Fact]
    public void Expand_BranchPlaceholder()
    {
        var ctx = new SnippetContext { Branch = "feature/login" };
        Assert.Equal("git push origin feature/login",
            CommandSnippetService.Expand("git push origin {branch}", ctx));
    }

    [Fact]
    public void Expand_SelectionPlaceholder()
    {
        var ctx = new SnippetContext { Selection = "hello world" };
        Assert.Equal("echo 'hello world'",
            CommandSnippetService.Expand("echo '{selection}'", ctx));
    }

    [Fact]
    public void Expand_NullBranchOrSelection_LeavesPlaceholderLiteral()
    {
        var ctx = new SnippetContext();
        Assert.Equal("git checkout {branch}", CommandSnippetService.Expand("git checkout {branch}", ctx));
        Assert.Equal("echo {selection}", CommandSnippetService.Expand("echo {selection}", ctx));
    }

    // -- Combine defaults + user snippets -----------------------------------

    [Fact]
    public void LoadAll_AppendsUserSnippetsAfterDefaults()
    {
        var user = new[]
        {
            new CommandSnippet("Mi script", "./scripts/run.sh"),
        };
        var all = CommandSnippetService.LoadAll(user);
        Assert.True(all.Count > user.Length);
        // El último elemento es el del usuario.
        Assert.Equal("Mi script", all[^1].Name);
    }

    [Fact]
    public void LoadAll_NullUser_ReturnsDefaults()
    {
        var all = CommandSnippetService.LoadAll(null);
        Assert.Equal(CommandSnippetService.Defaults().Count, all.Count);
    }
}
