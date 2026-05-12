using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Regresión de dos bugs reportados sobre TerminalPane / TerminalControl:
///
/// 1) Duplicación de teclas tras split. WPF re-dispara Loaded en cada
///    reparenting (un split mueve el pane existente entre slots). En HEAD,
///    OnLoaded → InitializeWebViewAsync hacía `WebView.CoreWebView2.WebMessageReceived += OnWebMessage`
///    en cada invocación, acumulando subscripciones; cada tecla terminaba
///    escrita al PTY N veces. Fix: guard en OnLoaded con un flag de instancia.
///
/// 2) Click sobre un pane no captura teclado al primer intento. El handler
///    PreviewMouseDown invoca FocusReceived → SetActivePane → pane.Focus()
///    en HEAD; como TerminalPane.xaml declara Focusable="True", el .Focus()
///    se llevaba el foco al UserControl, robándoselo al WebView2 que el
///    click acababa de enfocar. Fix:
///      - método público FocusTerminal() que enfoca WebView (HWND) + term.focus().
///      - SetActivePane recibe `moveFocus` (default true); el suscriptor de
///        FocusReceived pasa moveFocus:false para no pelear con el grab que
///        xterm hace en su propio mousedown.
///
/// Estos invariantes no se pueden testear end-to-end (requieren WebView2
/// real + interacción de mouse); auditamos los archivos fuente como hace
/// el resto del proyecto (BroadcastAuditTests, ContextMenuTests, etc.).
/// </summary>
public class TerminalPaneInitAuditTests
{
    private static string SrcDir
    {
        get
        {
            var dir = System.AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir, "src", "WinKuake");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("No encontré src/WinKuake");
        }
    }

    private static string PaneSrc =>
        File.ReadAllText(Path.Combine(SrcDir, "Views", "TerminalPane.xaml.cs"));

    private static string ControlSrc =>
        File.ReadAllText(Path.Combine(SrcDir, "Views", "TerminalControl.xaml.cs"));

    // ---- Bug 1: duplicación de teclas tras split ---------------------------

    [Fact]
    public void TerminalPane_OnLoaded_TieneGuardContraReinicializacion()
    {
        // OnLoaded debe verificar un flag de instancia ANTES de llamar al
        // init del WebView. Sin esto cada reparenting del split re-suscribe
        // WebMessageReceived.
        var src = PaneSrc;
        // Hay un campo bool de instancia que actúa como guard.
        Assert.Matches(new Regex(@"private\s+bool\s+_\w*[Ii]nit\w*"), src);
        // OnLoaded chequea el guard y hace return temprano.
        var onLoaded = ExtractMethodBody(src, "OnLoaded");
        Assert.NotNull(onLoaded);
        Assert.Matches(new Regex(@"if\s*\(\s*_\w*[Ii]nit\w*\s*\)\s*return"), onLoaded!);
    }

    [Fact]
    public void TerminalPane_InitializeWebView_NoEstaCondicionadoFueraDeOnLoaded()
    {
        // El Navigate("https://winkuake.local/terminal.html") DEBE quedar
        // dentro del flujo síncrono post-Ensure en InitializeWebViewAsync.
        // Moverlo a un handler de evento (CoreWebView2InitializationCompleted)
        // introduce un path donde Navigate puede no dispararse y el WebView
        // queda en about:blank (fondo negro). Esto es regresión del turno 4.
        var src = PaneSrc;
        var init = ExtractMethodBody(src, "InitializeWebViewAsync");
        Assert.NotNull(init);
        Assert.Contains("Navigate(", init!);
        Assert.Contains("winkuake.local/terminal.html", init);
    }

    [Fact]
    public void TerminalPane_WebMessageReceived_SeSuscribeUnaVez_PorInitGuard()
    {
        // += OnWebMessage solo aparece una vez en el código, dentro del
        // bloque protegido por el guard. Multiplicidad = bug de duplicación.
        var src = PaneSrc;
        var count = Regex.Matches(src, @"WebMessageReceived\s*\+=\s*OnWebMessage").Count;
        Assert.Equal(1, count);
    }

    // ---- Bug 2: click no captura teclado al primer intento ------------------

    [Fact]
    public void TerminalPane_TieneMetodoFocusTerminal_QueEnfocaWebView()
    {
        var src = PaneSrc;
        // Método público FocusTerminal que delega al WebView (HWND nativo).
        Assert.Matches(new Regex(@"public\s+void\s+FocusTerminal\s*\("), src);
        var body = ExtractMethodBody(src, "FocusTerminal");
        Assert.NotNull(body);
        Assert.Contains("WebView.Focus()", body!);
    }

    [Fact]
    public void TerminalControl_SetActivePane_NoLlamaUserControlFocus()
    {
        // SetActivePane ya no debe usar pane.Focus() (UserControl) — ese .Focus()
        // robaba el foco al WebView. Debe enfocar el WebView vía FocusTerminal,
        // y solo cuando moveFocus está activo.
        var src = ControlSrc;
        var body = ExtractMethodBody(src, "SetActivePane");
        Assert.NotNull(body);
        Assert.DoesNotContain("pane.Focus()", body!);
        Assert.Contains("FocusTerminal", body);
    }

    [Fact]
    public void TerminalControl_SetActivePane_AceptaParametroMoveFocus()
    {
        // La firma debe tener un parámetro bool moveFocus (default true)
        // para distinguir activaciones por click (no mover foco; xterm ya
        // lo agarra) vs activaciones programáticas (Alt+flecha, F12, etc).
        var src = ControlSrc;
        Assert.Matches(
            new Regex(@"SetActivePane\s*\(\s*TerminalPane\s+\w+\s*,\s*bool\s+moveFocus"),
            src);
    }

    [Fact]
    public void TerminalControl_FocusReceived_NoMueveFoco_DesdeClicks()
    {
        // El suscriptor de pane.FocusReceived (que se dispara por click /
        // GotFocus) debe pasar moveFocus:false. El click ya enfocó el WebView
        // por su cuenta; forzar Focus() encima rompe el grab de xterm.
        var src = ControlSrc;
        Assert.Matches(
            new Regex(@"pane\.FocusReceived\s*\+=.*SetActivePane\s*\(\s*pane\s*,\s*moveFocus\s*:\s*false",
                RegexOptions.Singleline),
            src);
    }

    // ---- helpers -----------------------------------------------------------

    /// <summary>
    /// Extrae el cuerpo (entre la primera { hasta la } emparejada del mismo
    /// nivel) del método indicado. Devuelve null si no se encuentra. Pensado
    /// para auditorías de archivos fuente — no es un parser de C# completo.
    /// </summary>
    private static string? ExtractMethodBody(string src, string methodName)
    {
        // Buscamos la DECLARACIÓN (con modificador o tipo retorno antes),
        // no las llamadas. Acepta `private/public/...`, `async`, tipo y nombre.
        var sig = new Regex(
            @"(?:public|private|protected|internal)\s+(?:async\s+)?(?:\w+(?:<[^>]+>)?\??\s+)+" +
            Regex.Escape(methodName) + @"\s*\([^)]*\)");
        var sm = sig.Match(src);
        if (!sm.Success) return null;
        int i = sm.Index + sm.Length;
        // Saltar firma hasta encontrar {
        while (i < src.Length && src[i] != '{') i++;
        if (i >= src.Length) return null;
        int depth = 0, start = i;
        for (; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            else if (src[i] == '}') { depth--; if (depth == 0) return src.Substring(start, i - start + 1); }
        }
        return null;
    }
}
