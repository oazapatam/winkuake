using System.Collections.Generic;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class GlobalFindServiceTests
{
    private static IReadOnlyList<string> Lines(params string[] xs) => xs;

    [Fact]
    public void Search_EmptyQuery_ReturnsNoResults()
    {
        var src = new[]
        {
            new GlobalFindSource(SessionId: 1, SessionLabel: "main", PaneIndex: 0, Lines: Lines("hola mundo")),
        };
        var r = GlobalFindService.Search(src, "");
        Assert.Empty(r);
    }

    [Fact]
    public void Search_WhitespaceQuery_ReturnsNoResults()
    {
        var src = new[]
        {
            new GlobalFindSource(1, "main", 0, Lines("error: foo")),
        };
        Assert.Empty(GlobalFindService.Search(src, "   "));
    }

    [Fact]
    public void Search_CaseInsensitiveSubstring_Matches()
    {
        var src = new[]
        {
            new GlobalFindSource(1, "main", 0, Lines("Hello World", "no match here")),
        };
        var r = GlobalFindService.Search(src, "hello");
        Assert.Single(r);
        Assert.Equal(0, r[0].LineNumber);
        Assert.Equal("Hello World", r[0].LinePreview);
    }

    [Fact]
    public void Search_ReportsCorrectLineNumberPerPane()
    {
        var src = new[]
        {
            new GlobalFindSource(1, "main", 0, Lines("a", "b", "match aqui", "c")),
        };
        var r = GlobalFindService.Search(src, "match");
        Assert.Single(r);
        Assert.Equal(2, r[0].LineNumber);
    }

    [Fact]
    public void Search_MultipleSessionsAndPanes_AllScanned()
    {
        var src = new[]
        {
            new GlobalFindSource(1, "main",  0, Lines("error 1")),
            new GlobalFindSource(1, "main",  1, Lines("error 2")),
            new GlobalFindSource(2, "build", 0, Lines("error 3", "ok")),
        };
        var r = GlobalFindService.Search(src, "error");
        Assert.Equal(3, r.Count);
    }

    [Fact]
    public void Search_PreservesSessionAndPaneMetadata()
    {
        var src = new[]
        {
            new GlobalFindSource(42, "ssh-prod", 3, Lines("err: connection lost")),
        };
        var r = GlobalFindService.Search(src, "connection");
        Assert.Single(r);
        Assert.Equal(42, r[0].SessionId);
        Assert.Equal("ssh-prod", r[0].SessionLabel);
        Assert.Equal(3, r[0].PaneIndex);
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var src = new[]
        {
            new GlobalFindSource(1, "main", 0, Lines("aaa", "bbb")),
        };
        Assert.Empty(GlobalFindService.Search(src, "zzz"));
    }

    [Fact]
    public void Search_TrimsTrailingWhitespaceFromPreview()
    {
        // xterm getLine.translateToString(true) puede dejar trailing spaces
        // según versión. El preview debe estar limpio.
        var src = new[]
        {
            new GlobalFindSource(1, "main", 0, Lines("found x    ")),
        };
        var r = GlobalFindService.Search(src, "found");
        Assert.Single(r);
        Assert.Equal("found x", r[0].LinePreview);
    }

    [Fact]
    public void Search_SkipsEmptyLines()
    {
        // Líneas vacías del scrollback no son matches útiles (incluso si query coincide
        // con cadena vacía — no se puede dar porque IsNullOrWhiteSpace ya filtra).
        var src = new[]
        {
            new GlobalFindSource(1, "main", 0, Lines("", "", "real line")),
        };
        var r = GlobalFindService.Search(src, "real");
        Assert.Single(r);
        Assert.Equal(2, r[0].LineNumber);
    }

    [Fact]
    public void Search_MultipleMatchesInSamePane_AllReported()
    {
        var src = new[]
        {
            new GlobalFindSource(1, "main", 0, Lines("foo", "foobar", "foo again")),
        };
        var r = GlobalFindService.Search(src, "foo");
        Assert.Equal(3, r.Count);
    }
}
