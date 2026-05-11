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
}
