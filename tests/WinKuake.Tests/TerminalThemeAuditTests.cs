using System.Linq;
using System.Text.Json;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 4: confirma que las 5 paletas exigidas por PLAN.md existen
/// con valores razonables, que ToXtermJson emite todas las claves esperadas
/// por xterm.js y que el JSON resultante es parseable.
/// </summary>
public class TerminalThemeAuditTests
{
    [Theory]
    [InlineData("VSCode Dark+")]
    [InlineData("Dracula")]
    [InlineData("Nord")]
    [InlineData("Gruvbox Dark")]
    [InlineData("Monokai")]
    public void Audit_Each_Required_Palette_Is_Present(string name)
    {
        var t = TerminalTheme.Find(name);
        Assert.NotNull(t);
        Assert.Equal(name, t!.Name);
    }

    [Fact]
    public void Audit_All_Five_Palettes_AreUnique()
    {
        // Paranoia: que ningún tema haya quedado duplicado por copy/paste.
        var names = TerminalTheme.All.Select(t => t.Name).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
    }

    [Fact]
    public void Audit_All_Palettes_HaveValidHexColors()
    {
        // Cada uno de los 19 colores debe ser hex #RRGGBB válido.
        var hex = new System.Text.RegularExpressions.Regex("^#[0-9a-fA-F]{6}$");
        foreach (var t in TerminalTheme.All)
        {
            Assert.Matches(hex, t.Background);
            Assert.Matches(hex, t.Foreground);
            Assert.Matches(hex, t.Cursor);
            Assert.Matches(hex, t.Black);   Assert.Matches(hex, t.Red);
            Assert.Matches(hex, t.Green);   Assert.Matches(hex, t.Yellow);
            Assert.Matches(hex, t.Blue);    Assert.Matches(hex, t.Magenta);
            Assert.Matches(hex, t.Cyan);    Assert.Matches(hex, t.White);
            Assert.Matches(hex, t.BrightBlack);   Assert.Matches(hex, t.BrightRed);
            Assert.Matches(hex, t.BrightGreen);   Assert.Matches(hex, t.BrightYellow);
            Assert.Matches(hex, t.BrightBlue);    Assert.Matches(hex, t.BrightMagenta);
            Assert.Matches(hex, t.BrightCyan);    Assert.Matches(hex, t.BrightWhite);
        }
    }

    [Fact]
    public void Audit_ToXtermJson_HasAll19RequiredKeys()
    {
        // xterm.js espera exactamente estas claves; si renombramos una el tema rompe.
        var expected = new[]
        {
            "background","foreground","cursor",
            "black","red","green","yellow","blue","magenta","cyan","white",
            "brightBlack","brightRed","brightGreen","brightYellow",
            "brightBlue","brightMagenta","brightCyan","brightWhite",
        };
        var doc = JsonDocument.Parse(TerminalTheme.Default.ToXtermJson()).RootElement;
        foreach (var k in expected)
            Assert.True(doc.TryGetProperty(k, out _), $"Falta clave xterm '{k}'");
    }

    [Fact]
    public void Audit_AppSettings_DefaultsForFase4_AreSet()
    {
        // Defaults documentados en PLAN.md fase 4: -1 ilimitado, "VSCode Dark+", 14 pt.
        var s = new AppSettings();
        Assert.Equal(-1, s.ScrollbackLines);
        Assert.Equal("VSCode Dark+", s.TerminalThemeName);
        Assert.Equal(14, s.TerminalFontSize);
    }

    [Fact]
    public void Audit_ResolveCurrent_DefaultPath_FallsBackToVSCodeDarkPlus()
    {
        var s = new AppSettings();
        var t = TerminalTheme.ResolveCurrent(s);
        Assert.Equal("VSCode Dark+", t.Name);
    }
}
