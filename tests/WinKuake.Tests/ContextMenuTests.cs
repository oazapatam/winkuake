using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Tests del menú contextual: builder C# (función pura sobre estado) +
/// regresión textual sobre el handler JS de <c>contextmenu</c> en
/// <c>terminal.html</c>.
/// </summary>
public class ContextMenuTests
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
            throw new DirectoryNotFoundException("No encontré src/WinKuake/Resources/terminal");
        }
    }

    private static string ReadHtml() => File.ReadAllText(Path.Combine(ResourcesDir, "terminal.html"));

    // ---- Builder de items (función pura) ----------------------------------

    [Fact]
    public void Build_ContieneTodosLosItemsEsperados()
    {
        var items = TerminalContextMenuBuilder.Build(hasSelection: true, isInSplit: true);
        var ids = items.Where(i => !i.IsSeparator).Select(i => i.ActionId).ToArray();
        Assert.Equal(new[]
        {
            "copy", "paste", "find",
            "splitVertical", "splitHorizontal", "closePane",
            "openPalette", "clearBuffer"
        }, ids);
    }

    [Fact]
    public void Build_ContieneSeparadoresEntreGrupos()
    {
        var items = TerminalContextMenuBuilder.Build(hasSelection: true, isInSplit: true);
        // Tres grupos => dos separadores.
        Assert.Equal(2, items.Count(i => i.IsSeparator));
    }

    [Fact]
    public void Build_CopiarDeshabilitadoCuandoSinSeleccion()
    {
        var items = TerminalContextMenuBuilder.Build(hasSelection: false, isInSplit: true);
        var copy = items.First(i => i.ActionId == "copy");
        Assert.False(copy.Enabled);
    }

    [Fact]
    public void Build_CopiarHabilitadoCuandoHaySeleccion()
    {
        var items = TerminalContextMenuBuilder.Build(hasSelection: true, isInSplit: true);
        var copy = items.First(i => i.ActionId == "copy");
        Assert.True(copy.Enabled);
    }

    [Fact]
    public void Build_CerrarPaneDeshabilitadoCuandoSoloHayUno()
    {
        var items = TerminalContextMenuBuilder.Build(hasSelection: true, isInSplit: false);
        var close = items.First(i => i.ActionId == "closePane");
        Assert.False(close.Enabled);
    }

    [Fact]
    public void Build_CerrarPaneHabilitadoEnSplit()
    {
        var items = TerminalContextMenuBuilder.Build(hasSelection: true, isInSplit: true);
        var close = items.First(i => i.ActionId == "closePane");
        Assert.True(close.Enabled);
    }

    [Theory]
    [InlineData("copy",            "Copiar selección",   "Ctrl+Shift+C")]
    [InlineData("paste",           "Pegar",              "Ctrl+Shift+V")]
    [InlineData("find",            "Buscar",             "Ctrl+Shift+F")]
    [InlineData("splitVertical",   "Dividir vertical",   "Alt+Shift+=")]
    [InlineData("splitHorizontal", "Dividir horizontal", "Alt+Shift+-")]
    [InlineData("closePane",       "Cerrar pane",        "Ctrl+Shift+W")]
    [InlineData("openPalette",     "Paleta de comandos", "Ctrl+Shift+P")]
    [InlineData("clearBuffer",     "Limpiar buffer",     "Ctrl+L")]
    public void Build_CadaItemTieneLabelYShortcutEsperados(string id, string label, string shortcut)
    {
        var items = TerminalContextMenuBuilder.Build(hasSelection: true, isInSplit: true);
        var item = items.First(i => i.ActionId == id);
        Assert.Equal(label, item.Label);
        Assert.Equal(shortcut, item.Shortcut);
    }

    [Fact]
    public void Build_PegarSiempreHabilitado()
    {
        // El clipboard puede estar vacío; en ese caso term.paste("") es no-op.
        // No bloqueamos el item por adelantado porque comprobar el clipboard
        // de Windows es asíncrono y no vale la pena para sólo ahorrar un click.
        var a = TerminalContextMenuBuilder.Build(hasSelection: false, isInSplit: false);
        var b = TerminalContextMenuBuilder.Build(hasSelection: true,  isInSplit: true);
        Assert.True(a.First(i => i.ActionId == "paste").Enabled);
        Assert.True(b.First(i => i.ActionId == "paste").Enabled);
    }

    // ---- Bridge JS en terminal.html ---------------------------------------

    [Fact]
    public void Html_RegistraListenerContextmenu()
    {
        var html = ReadHtml();
        Assert.Matches(new Regex(@"addEventListener\(\s*['""]contextmenu['""]"), html);
    }

    [Fact]
    public void Html_PreviewContextmenuHaceDefault()
    {
        var html = ReadHtml();
        // Tiene que evitar el menú nativo del WebView2.
        Assert.Contains("preventDefault", ReadContextMenuHandler(html));
    }

    [Fact]
    public void Html_PostMessage_TieneTipoContextMenu()
    {
        var handler = ReadContextMenuHandler(ReadHtml());
        Assert.Matches(new Regex(@"type\s*:\s*['""]contextMenu['""]"), handler);
    }

    [Fact]
    public void Html_PostMessage_IncluyeHasSelection()
    {
        var handler = ReadContextMenuHandler(ReadHtml());
        Assert.Contains("hasSelection", handler);
    }

    [Fact]
    public void Html_PostMessage_IncluyeCoordenadas()
    {
        var handler = ReadContextMenuHandler(ReadHtml());
        // Mandamos x e y para posicionar el menú en pantalla.
        Assert.Matches(new Regex(@"\bx\s*:"),  handler);
        Assert.Matches(new Regex(@"\by\s*:"), handler);
    }

    private static string ReadContextMenuHandler(string html)
    {
        // Extrae el cuerpo de la lambda registrada en addEventListener('contextmenu', ev => { ... })
        // o function(ev) { ... }. Busca desde el listener hasta el cierre de su bloque.
        var m = Regex.Match(
            html,
            @"addEventListener\(\s*['""]contextmenu['""]\s*,\s*[^{]*\{(?<body>.*?)\}\s*\)",
            RegexOptions.Singleline);
        Assert.True(m.Success, "No encontré el handler de contextmenu en terminal.html");
        return m.Groups["body"].Value;
    }

    // ---- Bridge C# en TerminalPane.xaml.cs --------------------------------

    [Fact]
    public void TerminalPaneCs_TieneCaseContextMenu()
    {
        var path = Path.Combine(SrcDir(), "Views", "TerminalPane.xaml.cs");
        var src = File.ReadAllText(path);
        Assert.Matches(new Regex(@"case\s+""contextMenu"""), src);
    }

    [Fact]
    public void TerminalPaneCs_DeclaraEventoContextMenuRequested()
    {
        var path = Path.Combine(SrcDir(), "Views", "TerminalPane.xaml.cs");
        var src = File.ReadAllText(path);
        Assert.Contains("ContextMenuRequested", src);
    }

    private static string SrcDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "WinKuake");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("No encontré src/WinKuake desde " + AppContext.BaseDirectory);
    }
}
