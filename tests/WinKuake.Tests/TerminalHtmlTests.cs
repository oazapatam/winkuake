using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Tests de regresión sobre el frontend del terminal
/// (<c>src/WinKuake/Resources/terminal/terminal.html</c> y sus addons).
///
/// La estrategia es leer el HTML y los .js como texto y hacer aserciones por
/// regex sobre los script tags, los <c>loadAddon</c> y los handlers de teclado.
/// No ejecutamos JS — solo verificamos que las features de Fase 1 estén
/// declaradas. La validación funcional real es manual con la app corriendo.
/// </summary>
public class TerminalHtmlTests
{
    private static string ResourcesDir
    {
        get
        {
            // Subimos desde bin/Debug/netX/ hasta el repo y bajamos a src/WinKuake/Resources/terminal.
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir, "src", "WinKuake", "Resources", "terminal");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("No encontré src/WinKuake/Resources/terminal desde " + AppContext.BaseDirectory);
        }
    }

    private static string ReadHtml() => File.ReadAllText(Path.Combine(ResourcesDir, "terminal.html"));

    // ---- 1.A Addons xterm.js -----------------------------------------------

    [Theory]
    [InlineData("xterm.js")]
    [InlineData("addon-fit.js")]
    [InlineData("addon-web-links.js")]
    [InlineData("addon-search.js")]
    [InlineData("addon-unicode11.js")]
    [InlineData("addon-webgl.js")]
    [InlineData("addon-clipboard.js")]
    public void Addon_File_ExistsInResourcesDir(string fileName)
    {
        var path = Path.Combine(ResourcesDir, fileName);
        Assert.True(File.Exists(path), $"Falta el archivo {fileName} en Resources/terminal/");
        Assert.True(new FileInfo(path).Length > 0, $"{fileName} está vacío");
    }

    [Theory]
    [InlineData("xterm.js")]
    [InlineData("addon-fit.js")]
    [InlineData("addon-web-links.js")]
    [InlineData("addon-search.js")]
    [InlineData("addon-unicode11.js")]
    [InlineData("addon-webgl.js")]
    [InlineData("addon-clipboard.js")]
    public void TerminalHtml_References_AddonScript(string fileName)
    {
        var html = ReadHtml();
        var pattern = @"<script[^>]+src\s*=\s*[""']" + Regex.Escape(fileName) + @"[""']";
        Assert.True(Regex.IsMatch(html, pattern, RegexOptions.IgnoreCase),
            $"terminal.html no incluye <script src=\"{fileName}\">");
    }

    [Theory]
    [InlineData("FitAddon")]
    [InlineData("WebLinksAddon")]
    [InlineData("SearchAddon")]
    [InlineData("Unicode11Addon")]
    [InlineData("WebglAddon")]
    [InlineData("ClipboardAddon")]
    public void TerminalHtml_Instantiates_Addon(string globalName)
    {
        var html = ReadHtml();
        // Debe instanciar el constructor en la forma "new <Global>.<Global>(".
        var pattern = $@"new\s+{Regex.Escape(globalName)}\s*\.\s*{Regex.Escape(globalName)}\s*\(";
        Assert.True(Regex.IsMatch(html, pattern),
            $"terminal.html no instancia 'new {globalName}.{globalName}(...)'");
    }

    [Fact]
    public void TerminalHtml_LoadsEveryAddon_ViaTermLoadAddon()
    {
        var html = ReadHtml();
        // Al menos 6 invocaciones a term.loadAddon (fit, web-links, search, unicode11, clipboard, webgl).
        var matches = Regex.Matches(html, @"term\.loadAddon\s*\(");
        Assert.True(matches.Count >= 6,
            $"Se esperaban >= 6 llamadas a term.loadAddon, encontradas {matches.Count}");
    }

    [Fact]
    public void TerminalHtml_ActivatesUnicode11()
    {
        var html = ReadHtml();
        // unicode.activeVersion debe quedar en '11' (Unicode11 addon registra esa versión).
        Assert.Matches(@"term\.unicode\.activeVersion\s*=\s*['""]11['""]", html);
    }

    [Fact]
    public void TerminalHtml_WebglAddon_FallsBackOnError()
    {
        var html = ReadHtml();
        // El addon WebGL debe estar dentro de un try/catch para caer a canvas si falla.
        Assert.Matches(@"try\s*\{[^}]*WebglAddon[^}]*\}\s*catch", html);
    }

    // ---- 1.B Tema y tipografía ---------------------------------------------

    [Fact]
    public void TerminalHtml_UsesVSCodeDarkPlusPalette()
    {
        var html = ReadHtml();
        // Background y algunos ANSI específicos de VSCode Dark+.
        Assert.Contains("#0c0c0c", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("#c50f1f", html, StringComparison.OrdinalIgnoreCase); // red
        Assert.Contains("#13a10e", html, StringComparison.OrdinalIgnoreCase); // green
        Assert.Contains("#3a96dd", html, StringComparison.OrdinalIgnoreCase); // cyan
    }

    [Fact]
    public void TerminalHtml_UsesCascadiaCodeWithFallback()
    {
        var html = ReadHtml();
        Assert.Contains("Cascadia Code", html);
        Assert.Contains("Consolas", html);
    }

    [Fact]
    public void TerminalHtml_HasReasonableLineHeightAndLetterSpacing()
    {
        var html = ReadHtml();
        Assert.Matches(@"lineHeight\s*:\s*[0-9]+\.?[0-9]*", html);
        Assert.Matches(@"letterSpacing\s*:\s*[0-9]+", html);
    }

    [Fact]
    public void TerminalHtml_CursorIsBlockAndBlinking()
    {
        var html = ReadHtml();
        Assert.Matches(@"cursorStyle\s*:\s*['""]block['""]", html);
        Assert.Matches(@"cursorBlink\s*:\s*true", html);
    }

    // ---- 1.C Quality of life ------------------------------------------------

    [Fact]
    public void TerminalHtml_HasScrollbackOption()
    {
        var html = ReadHtml();
        // El default initial puede ser cualquier número; el host lo reconfigura
        // vía mensaje "config" después. Verificamos sólo presencia.
        Assert.Matches(@"scrollback\s*:\s*\d+", html);
        // Y verificamos que el handler de "config" honore m.scrollback.
        Assert.Contains("m.scrollback", html);
    }

    [Fact]
    public void TerminalHtml_BindsCtrlC_ToCopySelection()
    {
        // Estilo Windows Terminal: Ctrl+C (sin shift) copia la selección.
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey\s*&&\s*!ev\.shiftKey[^)]*KeyC", html);
        Assert.Contains("getSelection", html);
        Assert.Contains("clipboard.writeText", html);
    }

    [Fact]
    public void TerminalHtml_ConsumesCtrlShiftC_SoItDoesNotOpenDevTools()
    {
        // Regresión: Ctrl+Shift+C es "inspeccionar elemento" en Edge/Chromium.
        // Con AreDevToolsEnabled=true, dejar pasar la tecla (return true) abría
        // la ventana de depuración. La rama Ctrl+Shift+C debe consumirla siempre.
        var html = ReadHtml();
        var m = Regex.Match(
            html,
            @"ev\.ctrlKey && ev\.shiftKey[^\n]*KeyC'\)\s*\{(?<body>[\s\S]*?)\n    \}",
            RegexOptions.Multiline);
        Assert.True(m.Success, "No encontré la rama Ctrl+Shift+C en el handler");
        Assert.Contains("return false", m.Groups["body"].Value);
        Assert.DoesNotContain("return true", m.Groups["body"].Value);
    }

    [Fact]
    public void TerminalHtml_BindsCtrlV_ToPaste_WithoutDoublePaste()
    {
        // Ctrl+V (sin shift, sin alt) pega. El handler intercepta la tecla y
        // devuelve false para que xterm NO mande ^V (0x16) al shell; el evento
        // 'paste' nativo del browser hace el pegado real (xterm lo intercepta en
        // su textarea helper y respeta bracketed paste).
        //
        // Regresión: la rama Ctrl+V NO debe llamar term.paste()/clipboard.readText()
        // dentro del handler. Devolver false de attachCustomKeyEventHandler no hace
        // preventDefault sobre el paste event del browser, así que pegar también a
        // mano duplicaría el texto. El pegado manual sólo es correcto donde NO hay
        // paste event nativo (botón central del ratón y PasteFromClipboard del menú).
        var html = ReadHtml();

        var match = Regex.Match(
            html,
            @"attachCustomKeyEventHandler\s*\(\s*ev\s*=>\s*\{(?<body>[\s\S]*?)\n\s*\}\s*\)\s*;",
            RegexOptions.Multiline);
        Assert.True(match.Success, "No encontré attachCustomKeyEventHandler(ev => { ... })");

        var body = match.Groups["body"].Value;
        // Hay una rama Ctrl+V (sin shift) que captura KeyV.
        Assert.Matches(@"ctrlKey\s*&&\s*!ev\.shiftKey[^)]*KeyV", body);
        // Pero NO pega a mano dentro del handler (causaría doble pegado).
        Assert.DoesNotContain("clipboard.readText", body);
        Assert.DoesNotContain("term.paste", body);
    }

    [Fact]
    public void TerminalHtml_MiddleClick_Pastes()
    {
        // Botón central del ratón pega (estilo primary-selection de Unix). El
        // botón central NO dispara evento 'paste' nativo, así que acá sí leemos
        // el clipboard y llamamos term.paste() a mano (un solo pegado).
        var html = ReadHtml();
        Assert.Matches(@"button\s*===\s*1", html);
        Assert.Contains("clipboard.readText", html);
        Assert.Contains("term.paste", html);
    }

    [Fact]
    public void TerminalHtml_BindsCtrlShiftF_ToSearchOverlay()
    {
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*KeyF", html);
        Assert.Contains("openSearch", html);
    }

    [Fact]
    public void TerminalHtml_BindsCtrlShiftArrowRight_ToNextTab()
    {
        // Ctrl+Shift+→ : siguiente tab. Equivalente teclas-flechas al Ctrl+Tab
        // existente, sin chocar con la selección de texto (que sería Shift+→ sola).
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*ArrowRight[\s\S]*?nextTab", html);
    }

    [Fact]
    public void TerminalHtml_BindsCtrlShiftArrowLeft_ToPrevTab()
    {
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*ArrowLeft[\s\S]*?prevTab", html);
    }

    // ---- Yakuake defaults --------------------------------------------------

    [Fact]
    public void TerminalHtml_BindsCtrlParenOpen_ToSplitVertical()
    {
        // Yakuake default: Ctrl+( para split vertical.
        // Usamos ev.key === '(' para que funcione en cualquier layout
        // (en US es Ctrl+Shift+9, en LATAM es Ctrl+Shift+8).
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*ev\.key\s*===\s*['""]\(['""][\s\S]*?splitVertical", html);
    }

    [Fact]
    public void TerminalHtml_BindsCtrlParenClose_ToSplitHorizontal()
    {
        // Yakuake default: Ctrl+) para split horizontal.
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*ev\.key\s*===\s*['""]\)['""][\s\S]*?splitHorizontal", html);
    }

    [Fact]
    public void TerminalHtml_CtrlShiftDigitJumpToTab_ExcluyeParentesis()
    {
        // Regresión: en layouts no-US el split Yakuake (Ctrl+( / Ctrl+)) es
        // físicamente Ctrl+Shift+8 / Ctrl+Shift+9. El handler de jump-to-tab por
        // Ctrl+Shift+Dígito corre ANTES que el de split; si no excluye '(' y ')'
        // se traga la tecla (intenta saltar a una tab inexistente) y el split
        // nunca dispara. El guard debe excluir ambos caracteres.
        var html = ReadHtml();
        Assert.Matches(
            @"ctrlKey[^{]*shiftKey[^{]*Digit[^{]*ev\.key\s*!==\s*['""]\(['""][^{]*ev\.key\s*!==\s*['""]\)['""][\s\S]*?activateAt",
            html);
    }

    [Fact]
    public void TerminalHtml_BindsCtrlShiftR_ToClosePane()
    {
        // Yakuake default: Ctrl+Shift+R = close-active-terminal (cerrar pane).
        // Coexiste con Ctrl+Shift+W (close pane) que ya existe en el menú.
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*KeyR[\s\S]*?closePane", html);
    }

    [Fact]
    public void TerminalHtml_BindsAltDigit_ToJumpToTabN()
    {
        // Yakuake default: Alt+1..9 = switch-to-session-N.
        // El handler cataloga digits con `code.startsWith('Digit')`.
        var html = ReadHtml();
        Assert.Matches(@"altKey[^{]*Digit[\s\S]*?activateAt", html);
    }

    [Fact]
    public void TerminalHtml_BindsAlt0_ToJumpToTab10()
    {
        // Yakuake default: Alt+0 = switch-to-session-10.
        var html = ReadHtml();
        Assert.Matches(@"index\s*:\s*10", html);
    }

    [Fact]
    public void TerminalHtml_BindsCtrlL_ToClear()
    {
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*!ev\.shiftKey[^{]*KeyL", html);
        // Se acepta term.clear() o el escape \x0c.
        Assert.True(html.Contains("term.clear()") || html.Contains(@"\x0c"),
            "Ctrl+L no llama term.clear() ni envía \\x0c al PTY");
    }

    [Fact]
    public void TerminalHtml_BindsCtrlPlus_AndCtrlMinus_AndCtrl0_ForZoom()
    {
        var html = ReadHtml();
        // Zoom in: Ctrl+= o Ctrl++ (numpad).
        Assert.Matches(@"ctrlKey[^{]*Equal[^{]*NumpadAdd", html);
        // Zoom out: Ctrl+- o Ctrl+- (numpad).
        Assert.Matches(@"ctrlKey[^{]*Minus[^{]*NumpadSubtract", html);
        // Reset: Ctrl+0.
        Assert.Matches(@"ctrlKey[^{]*Digit0[^{]*Numpad0", html);
    }

    [Fact]
    public void TerminalHtml_HasSearchOverlayDom()
    {
        var html = ReadHtml();
        Assert.Contains("id=\"search\"", html);
        Assert.Contains("id=\"search-input\"", html);
        Assert.Contains("search.findNext", html);
        Assert.Contains("search.findPrevious", html);
    }

    // ---- 1.D Diagnóstico (checkpoints) -------------------------------------

    [Theory]
    [InlineData("checkpoint:html_parsed")]
    [InlineData("checkpoint:scripts_loaded")]
    [InlineData("checkpoint:term_created")]
    [InlineData("checkpoint:term_opened")]
    [InlineData("checkpoint:ready_posted")]
    public void TerminalHtml_EmitsDiagnosticCheckpoint(string marker)
    {
        // Si el pane sale 100% negro sin cursor, el log del host nos dice hasta
        // qué punto llegó la inicialización JS. Sin esta migaja diagnosticar
        // es ciego.
        var html = ReadHtml();
        Assert.Contains(marker, html);
    }

    [Fact]
    public void TerminalHtml_RegistersUnhandledRejectionListener()
    {
        // window.addEventListener('error', ...) ya existe; agregamos
        // 'unhandledrejection' para no perder errores de promesas en init
        // (p.ej. ClipboardAddon, WebGL, fetch a un recurso roto).
        var html = ReadHtml();
        Assert.Matches(@"addEventListener\(\s*['""]unhandledrejection['""]", html);
    }
}
