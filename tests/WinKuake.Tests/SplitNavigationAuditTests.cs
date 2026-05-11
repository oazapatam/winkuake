using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 7: Alt+ArrowUp/Down/Left/Right enfoca pane vecino,
/// y Fase 15: atajos de split adicionales (Ctrl+Shift+D / Ctrl+Shift+E)
/// además de Alt+Shift+= / Alt+Shift+-.
/// </summary>
public class SplitNavigationAuditTests
{
    private static string ResourcesDir
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir is not null; i++)
            {
                var c = Path.Combine(dir, "src", "WinKuake", "Resources", "terminal");
                if (Directory.Exists(c)) return c;
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("Resources/terminal no encontrado");
        }
    }

    private static string ReadHtml() => File.ReadAllText(Path.Combine(ResourcesDir, "terminal.html"));

    [Fact]
    public void Html_AltArrows_PostsFocusPane_WithDirection()
    {
        var html = ReadHtml();
        // Alt+Arrow* → focusPane con direction "left|right|up|down".
        Assert.Matches(@"type\s*:\s*['""]focusPane['""]", html);
        Assert.Contains("ArrowUp",    html);
        Assert.Contains("ArrowDown",  html);
        Assert.Contains("ArrowLeft",  html);
        Assert.Contains("ArrowRight", html);
        // Direction extraído del code (replace 'Arrow').
        Assert.Matches(@"replace\s*\(\s*['""]Arrow['""]\s*,", html);
    }

    [Fact]
    public void Html_AltArrows_OnlyTriggersWhenAltIsTheOnlyModifier()
    {
        // Sin esta guarda, Alt+Ctrl+Arrow (selección de palabra) sería robado.
        var html = ReadHtml();
        Assert.Matches(@"altKey[^{]*!ev\.ctrlKey[^{]*!ev\.shiftKey[^{]*ArrowUp", html);
    }

    [Fact]
    public void Html_SplitVertical_AcceptsAltShiftEqual_AndCtrlShiftD()
    {
        var html = ReadHtml();
        // Alt+Shift+= o Ctrl+Shift+D → splitVertical.
        Assert.Matches(@"type\s*:\s*['""]splitVertical['""]", html);
        Assert.Contains("KeyD", html);
        // Detección por code Equal/NumpadAdd y por key '+' (layouts no-US).
        Assert.Matches(@"Equal|NumpadAdd", html);
        Assert.Matches(@"ev\.key\s*===\s*['""]\+['""]", html);
    }

    [Fact]
    public void Html_SplitHorizontal_AcceptsAltShiftMinus_AndCtrlShiftE()
    {
        var html = ReadHtml();
        Assert.Matches(@"type\s*:\s*['""]splitHorizontal['""]", html);
        Assert.Contains("KeyE", html);
        Assert.Matches(@"Minus|NumpadSubtract", html);
        // Acepta también ev.key '-' o '_' para layouts no-US.
        Assert.Matches(@"ev\.key\s*===\s*['""]-['""]", html);
    }

    [Fact]
    public void Html_BindsCtrlShiftW_ToClosePane()
    {
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*KeyW", html);
        Assert.Matches(@"type\s*:\s*['""]closePane['""]", html);
    }

    [Fact]
    public void Html_BindsCtrlShiftP_ToOpenPalette()
    {
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*KeyP", html);
        Assert.Matches(@"type\s*:\s*['""]openPalette['""]", html);
    }

    [Fact]
    public void Html_BindsCtrlShiftB_ToToggleBroadcast()
    {
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*KeyB", html);
        Assert.Matches(@"type\s*:\s*['""]toggleBroadcast['""]", html);
    }

    [Fact]
    public void Html_BindsCtrlShiftS_ToSaveBuffer()
    {
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*KeyS", html);
        Assert.Matches(@"type\s*:\s*['""]saveBuffer['""]", html);
    }
}
