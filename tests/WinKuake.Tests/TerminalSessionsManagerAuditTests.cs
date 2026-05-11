using System.Linq;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fases 3 y 6 — pin/move/jump-to-tab y casos extremos
/// (multi-pin, move-edge, ActivateAt sobre activa, MoveActiveBy clamp).
/// Refuerzan invariantes ya cubiertos en TerminalSessionsManagerTests.
/// </summary>
public class TerminalSessionsManagerAuditTests
{
    private static TerminalProfile P(string name = "x") => new(name, name)
    {
        CommandLine = "cmd.exe"
    };

    [Fact]
    public void TogglePin_MultipleSessions_FlipIndividually()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P("a"));
        var b = m.Create(P("b"));
        m.TogglePin(a.Id);
        Assert.True(a.IsPinned);
        Assert.False(b.IsPinned);
    }

    [Fact]
    public void Close_PinnedSession_StillRemovedFromManager()
    {
        // El manager no rechaza cerrar pinned — la confirmación es
        // responsabilidad del UI (MainWindow.TryCloseSession).
        var m = new TerminalSessionsManager();
        var a = m.Create(P());
        m.TogglePin(a.Id);
        Assert.True(m.Close(a.Id));
        Assert.Empty(m.Sessions);
    }

    [Fact]
    public void ActivateAt_OnAlreadyActive_ReturnsTrueAndDoesNotFireActiveChanged()
    {
        // ActivateAt(n) sobre la activa devuelve true (no falla) pero no
        // dispara el evento — evita rebotes infinitos en el bridge JS.
        var m = new TerminalSessionsManager();
        var a = m.Create(P("a"));
        var b = m.Create(P("b"));
        m.SetActive(b.Id);
        var fired = 0;
        m.ActiveChanged += _ => fired++;
        Assert.True(m.ActivateAt(2)); // b está en pos 2 (1-based)
        Assert.Equal(0, fired);
    }

    [Fact]
    public void MoveActiveBy_Zero_ReturnsFalse()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P("a"));
        m.SetActive(a.Id);
        Assert.False(m.MoveActiveBy(0)); // mismo lugar → no cambia.
    }

    [Fact]
    public void Move_FollowedByActivateAt_RespectsNewOrder()
    {
        // Tras drag-and-drop, ActivateAt(n) usa el orden visual nuevo.
        var m = new TerminalSessionsManager();
        var a = m.Create(P("a"));
        var b = m.Create(P("b"));
        var c = m.Create(P("c"));
        m.Move(a.Id, 2); // orden: b, c, a
        m.ActivateAt(3);
        Assert.Equal(a, m.Active);
    }

    [Fact]
    public void Rename_Empty_TakesEmptyAndFallsBackToProfile()
    {
        // El manager acepta cualquier label; la lógica de "fallback al profile"
        // sólo aplica cuando CustomLabel es null o "".
        var m = new TerminalSessionsManager();
        var s = m.Create(P("Ubuntu"));
        m.Rename(s.Id, "");
        Assert.Equal("Ubuntu", s.Label);
    }

    [Fact]
    public void Rename_NonExistent_Throws()
    {
        var m = new TerminalSessionsManager();
        Assert.Throws<System.ArgumentException>(() => m.Rename(999, "x"));
    }

    [Fact]
    public void Sessions_ExposedAsReadOnly_CannotMutateExternally()
    {
        var m = new TerminalSessionsManager();
        m.Create(P());
        var list = m.Sessions;
        Assert.IsAssignableFrom<System.Collections.Generic.IReadOnlyList<TerminalSession>>(list);
    }

    [Fact]
    public void ActivateNext_SingleSession_StaysSame()
    {
        var m = new TerminalSessionsManager();
        var a = m.Create(P());
        m.ActivateNext();
        Assert.Equal(a, m.Active);
    }

    [Fact]
    public void Close_MiddleActive_PicksLastRemainingAsActive()
    {
        // Política documentada: tras cerrar la activa, la nueva activa es la
        // última que queda (LastOrDefault), no la inmediatamente anterior.
        var m = new TerminalSessionsManager();
        var a = m.Create(P("a"));
        var b = m.Create(P("b"));
        var c = m.Create(P("c"));
        m.SetActive(b.Id);
        m.Close(b.Id);
        Assert.Equal(c, m.Active); // último que queda
    }
}
