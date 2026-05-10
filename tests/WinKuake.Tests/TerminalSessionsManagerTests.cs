using System.Linq;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class TerminalSessionsManagerTests
{
    private static TerminalProfile P(string name = "x") => new(name, name)
    {
        CommandLine = "cmd.exe"
    };

    [Fact]
    public void Empty_StartsWithNoSessions()
    {
        var m = new TerminalSessionsManager();
        Assert.Empty(m.Sessions);
        Assert.Null(m.Active);
    }

    [Fact]
    public void Create_AddsSessionAndActivates()
    {
        var m = new TerminalSessionsManager();
        var s = m.Create(P("Ubuntu"));
        Assert.Single(m.Sessions);
        Assert.Equal(s, m.Active);
        Assert.Equal("Ubuntu", s.Profile?.DisplayName);
    }

    [Fact]
    public void Create_AssignsIncrementalIds()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P());
        var b = m.Create(P());
        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(a.Id + 1, b.Id);
    }

    [Fact]
    public void Close_RemovesSession()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P());
        Assert.True(m.Close(a.Id));
        Assert.Empty(m.Sessions);
    }

    [Fact]
    public void Close_NonExistent_ReturnsFalse()
    {
        var m = new TerminalSessionsManager();
        Assert.False(m.Close(999));
    }

    [Fact]
    public void Close_Active_ActivatesPrevious()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P("a"));
        var b = m.Create(P("b"));
        // b queda activo tras Create
        Assert.Equal(b, m.Active);
        m.Close(b.Id);
        Assert.Equal(a, m.Active);
    }

    [Fact]
    public void Close_Inactive_KeepsCurrentActive()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P("a"));
        var b = m.Create(P("b"));
        // b activo; cierro a (no activa).
        m.Close(a.Id);
        Assert.Equal(b, m.Active);
    }

    [Fact]
    public void Close_Last_LeavesNoActive()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P());
        m.Close(a.Id);
        Assert.Null(m.Active);
        Assert.Empty(m.Sessions);
    }

    [Fact]
    public void SetActive_ChangesActiveSession()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P("a"));
        var b = m.Create(P("b"));
        Assert.True(m.SetActive(a.Id));
        Assert.Equal(a, m.Active);
    }

    [Fact]
    public void SetActive_AlreadyActive_ReturnsFalse()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P());
        Assert.False(m.SetActive(a.Id));
    }

    [Fact]
    public void SetActive_NonExistent_ReturnsFalse()
    {
        var m = new TerminalSessionsManager();
        m.Create(P());
        Assert.False(m.SetActive(999));
    }

    [Fact]
    public void Rename_SetsCustomLabel()
    {
        var m = new TerminalSessionsManager();
        var s = m.Create(P("Ubuntu"));
        m.Rename(s.Id, "my build");
        Assert.Equal("my build", s.Label);
    }

    [Fact]
    public void Label_FallsBackToProfileName()
    {
        var m = new TerminalSessionsManager();
        var s = m.Create(P("Ubuntu"));
        Assert.Equal("Ubuntu", s.Label);
    }

    [Fact]
    public void Label_FallsBackToShellIfNoProfile()
    {
        var m = new TerminalSessionsManager();
        var s = m.Create(null);
        Assert.Equal("Shell", s.Label);
    }

    [Fact]
    public void Events_AreFiredOnCreateCloseActiveChange()
    {
        var m = new TerminalSessionsManager();
        int added = 0, closed = 0, activeChanged = 0;
        m.SessionAdded += _ => added++;
        m.SessionClosed += _ => closed++;
        m.ActiveChanged += _ => activeChanged++;

        var a = m.Create(P("a"));
        Assert.Equal(1, added);
        Assert.Equal(1, activeChanged);

        var b = m.Create(P("b"));
        Assert.Equal(2, added);
        Assert.Equal(2, activeChanged);

        m.SetActive(a.Id);
        Assert.Equal(3, activeChanged);

        m.Close(a.Id);
        Assert.Equal(1, closed);
        // a era activo, ahora vuelve a b.
        Assert.Equal(4, activeChanged);
    }

    [Fact]
    public void Sessions_PreserveCreationOrder()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P("a"));
        var b = m.Create(P("b"));
        var c = m.Create(P("c"));
        Assert.Equal(new[] { a, b, c }, m.Sessions.ToArray());
    }
}
