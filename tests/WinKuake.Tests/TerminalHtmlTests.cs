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
    public void TerminalHtml_BindsCtrlShiftC_ToCopySelection()
    {
        var html = ReadHtml();
        Assert.Matches(@"ctrlKey[^{]*shiftKey[^{]*KeyC", html);
        Assert.Contains("getSelection", html);
        Assert.Contains("clipboard.writeText", html);
    }

    [Fact]
    public void TerminalHtml_DoesNotHandle_CtrlShiftV_Manually_To_AvoidDoublePaste()
    {
        // Regresión: xterm.js intercepta nativamente el evento DOM 'paste' en su
        // textarea helper y ya respeta bracketed paste. Si además agregamos un
        // handler manual de keydown Ctrl+Shift+V que llame a term.paste(...),
        // el texto se pega dos veces (devolver false de attachCustomKeyEventHandler
        // no hace preventDefault sobre el paste event del browser).
        // Por eso el bloque manual NO debe existir en attachCustomKeyEventHandler.
        var html = ReadHtml();

        // Aislamos el cuerpo de attachCustomKeyEventHandler para no chocar con
        // PasteFromClipboard() (que vive en TerminalPane.xaml.cs y se invoca
        // desde el menú contextual; ese path sí usa term.paste y es intencional).
        var match = Regex.Match(
            html,
            @"attachCustomKeyEventHandler\s*\(\s*ev\s*=>\s*\{(?<body>[\s\S]*?)\n\s*\}\s*\)\s*;",
            RegexOptions.Multiline);
        Assert.True(match.Success, "No encontré attachCustomKeyEventHandler(ev => { ... })");

        var body = match.Groups["body"].Value;
        Assert.DoesNotMatch(@"KeyV", body);
        Assert.DoesNotContain("clipboard.readText", body);
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
