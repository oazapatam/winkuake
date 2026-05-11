using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 9 — Broadcast input. La lógica vive en TerminalControl
/// (que requiere WebView2 para instanciar) así que verificamos los
/// invariantes accesibles: el handler JS Ctrl+Shift+B existe y postea el
/// mensaje correcto, y el switch del bridge en TerminalPane.cs lo recibe.
/// </summary>
public class BroadcastAuditTests
{
    private static string ResourcesDir
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir, "src", "WinKuake", "Resources", "terminal");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("No encontré Resources/terminal");
        }
    }

    private static string SrcDir
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir, "src", "WinKuake");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("No encontré src/WinKuake");
        }
    }

    private static string ReadHtml() => File.ReadAllText(Path.Combine(ResourcesDir, "terminal.html"));

    [Fact]
    public void Html_DeclaraHandlerCtrlShiftB()
    {
        var html = ReadHtml();
        // Buscamos el chequeo Ctrl+Shift+B con KeyB (no Bslash).
        Assert.Matches(@"ev\.ctrlKey\s*&&\s*ev\.shiftKey\s*&&\s*ev\.code\s*===\s*['""]KeyB['""]", html);
    }

    [Fact]
    public void Html_HandlerCtrlShiftB_PosteaToggleBroadcast()
    {
        var html = ReadHtml();
        Assert.Contains("'toggleBroadcast'", html);
    }

    [Fact]
    public void Pane_BridgeReconoceMensajeToggleBroadcast()
    {
        // El switch en TerminalPane.OnWebMessage debe tener "case toggleBroadcast".
        var pane = File.ReadAllText(Path.Combine(SrcDir, "Views", "TerminalPane.xaml.cs"));
        Assert.Contains("case \"toggleBroadcast\"", pane);
        Assert.Contains("ToggleBroadcastRequested", pane);
    }

    [Fact]
    public void Control_ToggleBroadcastEsNoOpConUnSoloPane()
    {
        // PLAN Fase 9: broadcast solo tiene sentido con 2+ panes. Verificamos
        // que TerminalControl.cs implementa el guard.
        var ctrl = File.ReadAllText(Path.Combine(SrcDir, "Views", "TerminalControl.xaml.cs"));
        Assert.Contains("if (_panes.Count < 2) return", ctrl);
    }

    [Fact]
    public void Control_BroadcastReplicaInputPaneAPane()
    {
        var ctrl = File.ReadAllText(Path.Combine(SrcDir, "Views", "TerminalControl.xaml.cs"));
        // El handler InputReceived debe iterar sobre todos los panes y
        // saltarse el originador.
        Assert.Matches(@"if\s*\(p\s*!=\s*pane\)\s*p\.InjectInput\(text\)", ctrl);
    }

    [Fact]
    public void Status_BarMuestraGlyphBroadcast()
    {
        var mw = File.ReadAllText(Path.Combine(SrcDir, "MainWindow.xaml.cs"));
        // PLAN Fase 9: status bar muestra "📡 BROADCAST" cuando on.
        Assert.Contains("📡 BROADCAST", mw);
    }

    [Fact]
    public void GlobalUsings_ResuelveTextBoxAFavorDeWpf()
    {
        // PLAN Fase 12: GlobalUsings resuelve homónimos a favor de WPF.
        // Verificamos el caso más usado (TextBox, MessageBox, Application).
        var gu = File.ReadAllText(Path.Combine(SrcDir, "GlobalUsings.cs"));
        Assert.Contains("global using TextBox = System.Windows.Controls.TextBox;", gu);
        Assert.Contains("global using MessageBox = System.Windows.MessageBox;", gu);
        Assert.Contains("global using Application = System.Windows.Application;", gu);
    }
}
