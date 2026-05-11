using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 5: regresión textual sobre terminal.html para el handler
/// OSC 7 (CWD del shell) y los atajos Ctrl+Tab / Ctrl+Shift+Tab.
/// </summary>
public class OscHandlerAuditTests
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
    public void Html_RegistersOsc7Handler()
    {
        var html = ReadHtml();
        // Spec PLAN.md: term.parser.registerOscHandler(7, ...)
        Assert.Matches(@"registerOscHandler\s*\(\s*7\s*,", html);
    }

    [Fact]
    public void Html_Osc7_ParsesFileUriHostnamePath()
    {
        // El handler debe parsear "file://hostname/path". La regex JS literal
        // es /^file:\/\/[^\/]*(\/.*)$/, así que en el HTML hay esa secuencia.
        var html = ReadHtml();
        Assert.Contains(@"file:\/\/", html);
    }

    [Fact]
    public void Html_Osc7_PostsCwdToHost()
    {
        var html = ReadHtml();
        // El handler debe postMessage type:'cwd' con la ruta detectada.
        Assert.Matches(@"type\s*:\s*['""]cwd['""]", html);
    }

    [Fact]
    public void Html_Osc7_DecodesUriComponent()
    {
        // pwsh emite paths URL-encoded (espacios → %20). El handler debe
        // decodeURIComponent para que el path llegue limpio.
        var html = ReadHtml();
        Assert.Contains("decodeURIComponent", html);
    }

    [Fact]
    public void Html_BindsCtrlTab_NextTab()
    {
        var html = ReadHtml();
        // Ctrl+Tab (sin shift) → nextTab.
        Assert.Matches(@"ctrlKey[^{]*!ev\.shiftKey[^{]*Tab", html);
        Assert.Matches(@"type\s*:\s*['""]nextTab['""]", html);
    }

    [Fact]
    public void Html_BindsCtrlShiftTab_PrevTab()
    {
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*Tab", html);
        Assert.Matches(@"type\s*:\s*['""]prevTab['""]", html);
    }

    [Fact]
    public void Html_BindsCtrlShiftDigits_ActivateAt()
    {
        var html = ReadHtml();
        // Ctrl+Shift+1..9 → jump-to-tab via activateAt.
        Assert.Matches(@"type\s*:\s*['""]activateAt['""]", html);
        Assert.Contains("Digit", html);
    }

    [Fact]
    public void Html_BindsCtrlShiftPageUpDown_MoveActiveBy()
    {
        var html = ReadHtml();
        Assert.Matches(@"type\s*:\s*['""]moveActiveBy['""]", html);
        Assert.Matches(@"PageUp", html);
        Assert.Matches(@"PageDown", html);
    }
}
