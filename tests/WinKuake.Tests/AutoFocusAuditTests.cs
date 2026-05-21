using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría textual del wiring "auto-focus al terminal" sin tener que ejecutar
/// la app (la animación + WebView2 + xterm.focus son lo que se valida manualmente).
/// </summary>
public class AutoFocusAuditTests
{
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

    private static string ReadFile(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { SrcDir }.Concat(parts).ToArray()));

    [Fact]
    public void WindowAnimator_Show_AcceptsOnCompletedCallback()
    {
        // Para enfocar el terminal cuando termina la animación de slide,
        // Show necesita recibir un callback. Antes solo Hide lo tenía.
        var src = ReadFile("Services", "WindowAnimator.cs");
        // Firma esperada: Show(double topTarget, int durationMs, Action? onCompleted = null)
        Assert.Matches(new Regex(@"public\s+void\s+Show\s*\(\s*double[^,]+,\s*int[^,]+,\s*Action\??\s+\w+(\s*=\s*null)?\s*\)"), src);
    }

    [Fact]
    public void MainWindow_ToggleVisibility_FocuseaTerminalDespuesDeShow()
    {
        // ToggleVisibility debe pasar un callback a animator.Show que enfoque
        // el terminal activo. Sin esto, al abrir con F12 hay que clickear el
        // pane para tipear.
        var src = ReadFile("MainWindow.xaml.cs");
        // Busca el bloque del Show y exige que mencione FocusActiveTerminal.
        var match = Regex.Match(src, @"_animator\.Show\s*\([^)]*\)", RegexOptions.Singleline);
        Assert.True(match.Success, "No encontré la llamada _animator.Show(...)");
        // El método helper existe.
        Assert.Matches(new Regex(@"FocusActiveTerminal\s*\("), src);
    }

    [Fact]
    public void MainWindow_OnActiveChanged_FocuseaTerminalDelNuevoTab()
    {
        // Al cambiar de tab (Ctrl+Tab, click en tab bar, Ctrl+Shift+arrows…)
        // el foco debe ir al pane activo del nuevo tab automáticamente.
        var src = ReadFile("MainWindow.xaml.cs");
        var match = Regex.Match(src, @"private\s+void\s+OnActiveChanged\s*\([^)]*\)\s*\{(?<body>[\s\S]*?)\n\s*\}",
            RegexOptions.Multiline);
        Assert.True(match.Success, "No encontré OnActiveChanged");
        Assert.Contains("FocusActiveTerminal", match.Groups["body"].Value);
    }

    [Fact]
    public void TerminalPane_ExposeEvento_Ready_QueDisparaAlRecibirReadyDelJS()
    {
        // Cuando se crea una sesión nueva (Ctrl+Shift+T), el WebView todavía
        // se está cargando, así que FocusTerminal() llamado de inmediato no
        // surte efecto (CoreWebView2 == null). El pane debe exponer un evento
        // Ready que el host puede usar para enfocar cuando el JS termina init.
        var src = ReadFile("Views", "TerminalPane.xaml.cs");
        Assert.Matches(new Regex(@"public\s+event\s+Action\??\s+Ready"), src);
        // El handler 'ready' del bridge debe disparar Ready.
        var caseReady = Regex.Match(src, @"case\s+""ready"":\s*(?<body>[\s\S]*?)break\s*;", RegexOptions.Multiline);
        Assert.True(caseReady.Success, "No encontré case \"ready\" en OnWebMessage");
        Assert.Matches(new Regex(@"Ready\?\.Invoke\s*\("), caseReady.Groups["body"].Value);
    }

    [Fact]
    public void TerminalControl_CreatePane_SuscribePaneReady_ParaEnfocarSiEsActivo()
    {
        // En CreatePane, cuando se conectan los handlers del pane, debe
        // suscribirse a pane.Ready y enfocar si el pane es _activePane al
        // momento del Ready. Sin esto, el primer F12 o Ctrl+Shift+T deja
        // el cursor sin foco hasta que el user clickea.
        var src = ReadFile("Views", "TerminalControl.xaml.cs");
        var body = Regex.Match(src, @"private\s+TerminalPane\s+CreatePane\s*\(\)\s*\{(?<body>[\s\S]*?)\n\s*return\s+pane;",
            RegexOptions.Multiline);
        Assert.True(body.Success, "No encontré CreatePane");
        Assert.Matches(new Regex(@"pane\.Ready\s*\+="), body.Groups["body"].Value);
        Assert.Contains("FocusTerminal", body.Groups["body"].Value);
    }
}
