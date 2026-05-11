using System.Linq;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class TerminalThemeTests
{
    [Fact]
    public void All_ContainsExpectedThemes()
    {
        var names = TerminalTheme.All.Select(t => t.Name).ToArray();
        Assert.Contains("VSCode Dark+", names);
        Assert.Contains("Dracula", names);
        Assert.Contains("Nord", names);
        Assert.Contains("Gruvbox Dark", names);
        Assert.Contains("Monokai", names);
    }

    [Fact]
    public void Default_IsVSCodeDarkPlus()
    {
        Assert.Equal("VSCode Dark+", TerminalTheme.Default.Name);
    }

    [Fact]
    public void Find_ReturnsTheme()
    {
        var t = TerminalTheme.Find("Dracula");
        Assert.NotNull(t);
        Assert.Equal("Dracula", t!.Name);
    }

    [Fact]
    public void Find_UnknownReturnsNull()
    {
        Assert.Null(TerminalTheme.Find("ThisDoesNotExist"));
    }

    [Fact]
    public void Find_IsCaseInsensitive()
    {
        Assert.NotNull(TerminalTheme.Find("dracula"));
        Assert.NotNull(TerminalTheme.Find("DRACULA"));
    }

    [Fact]
    public void FindOrDefault_FallsBackToDefault()
    {
        Assert.Equal(TerminalTheme.Default, TerminalTheme.FindOrDefault(null));
        Assert.Equal(TerminalTheme.Default, TerminalTheme.FindOrDefault(""));
        Assert.Equal(TerminalTheme.Default, TerminalTheme.FindOrDefault("unknown"));
        Assert.Equal("Dracula", TerminalTheme.FindOrDefault("Dracula")!.Name);
    }

    [Fact]
    public void Theme_HasAllRequiredColors()
    {
        foreach (var t in TerminalTheme.All)
        {
            Assert.False(string.IsNullOrEmpty(t.Background), $"{t.Name} Background");
            Assert.False(string.IsNullOrEmpty(t.Foreground), $"{t.Name} Foreground");
            Assert.False(string.IsNullOrEmpty(t.Cursor),     $"{t.Name} Cursor");
            // ANSI: 8 base + 8 bright = 16 colores.
            Assert.False(string.IsNullOrEmpty(t.Black),       $"{t.Name} Black");
            Assert.False(string.IsNullOrEmpty(t.Red),         $"{t.Name} Red");
            Assert.False(string.IsNullOrEmpty(t.Green),       $"{t.Name} Green");
            Assert.False(string.IsNullOrEmpty(t.Yellow),      $"{t.Name} Yellow");
            Assert.False(string.IsNullOrEmpty(t.Blue),        $"{t.Name} Blue");
            Assert.False(string.IsNullOrEmpty(t.Magenta),     $"{t.Name} Magenta");
            Assert.False(string.IsNullOrEmpty(t.Cyan),        $"{t.Name} Cyan");
            Assert.False(string.IsNullOrEmpty(t.White),       $"{t.Name} White");
            Assert.False(string.IsNullOrEmpty(t.BrightBlack), $"{t.Name} BrightBlack");
            Assert.False(string.IsNullOrEmpty(t.BrightWhite), $"{t.Name} BrightWhite");
        }
    }

    [Fact]
    public void ToXtermJson_HasExpectedKeys()
    {
        var json = TerminalTheme.Default.ToXtermJson();
        Assert.Contains("\"background\"", json);
        Assert.Contains("\"foreground\"", json);
        Assert.Contains("\"cursor\"", json);
        Assert.Contains("\"black\"", json);
        Assert.Contains("\"brightBlack\"", json);
        Assert.Contains("\"brightWhite\"", json);
    }

    [Fact]
    public void ColorValues_AreHexFormat()
    {
        var t = TerminalTheme.Find("Dracula")!;
        Assert.Matches("^#[0-9a-fA-F]{6}$", t.Background);
        Assert.Matches("^#[0-9a-fA-F]{6}$", t.Foreground);
    }

    [Fact]
    public void FromCustom_MapsAllColors()
    {
        var c = new TerminalThemeColors
        {
            Name = "MiTema",
            Background = "#111111", Foreground = "#eeeeee", Cursor = "#ff00ff",
            Black = "#010101", Red = "#020202", Green = "#030303", Yellow = "#040404",
            Blue = "#050505", Magenta = "#060606", Cyan = "#070707", White = "#080808",
            BrightBlack = "#0a0a0a", BrightRed = "#0b0b0b", BrightGreen = "#0c0c0c",
            BrightYellow = "#0d0d0d", BrightBlue = "#0e0e0e", BrightMagenta = "#0f0f0f",
            BrightCyan = "#101010", BrightWhite = "#121212",
        };
        var t = TerminalTheme.FromCustom(c);
        Assert.Equal("MiTema", t.Name);
        Assert.Equal("#111111", t.Background);
        Assert.Equal("#eeeeee", t.Foreground);
        Assert.Equal("#ff00ff", t.Cursor);
        Assert.Equal("#010101", t.Black);
        Assert.Equal("#020202", t.Red);
        Assert.Equal("#030303", t.Green);
        Assert.Equal("#040404", t.Yellow);
        Assert.Equal("#050505", t.Blue);
        Assert.Equal("#060606", t.Magenta);
        Assert.Equal("#070707", t.Cyan);
        Assert.Equal("#080808", t.White);
        Assert.Equal("#0a0a0a", t.BrightBlack);
        Assert.Equal("#0b0b0b", t.BrightRed);
        Assert.Equal("#0c0c0c", t.BrightGreen);
        Assert.Equal("#0d0d0d", t.BrightYellow);
        Assert.Equal("#0e0e0e", t.BrightBlue);
        Assert.Equal("#0f0f0f", t.BrightMagenta);
        Assert.Equal("#101010", t.BrightCyan);
        Assert.Equal("#121212", t.BrightWhite);
    }

    [Fact]
    public void FromCustom_EmptyName_DefaultsToCustom()
    {
        var c = new TerminalThemeColors { Name = "" };
        var t = TerminalTheme.FromCustom(c);
        Assert.Equal("Custom", t.Name);
    }

    [Fact]
    public void FromCustom_WhitespaceName_DefaultsToCustom()
    {
        var c = new TerminalThemeColors { Name = "   " };
        var t = TerminalTheme.FromCustom(c);
        Assert.Equal("Custom", t.Name);
    }

    [Fact]
    public void ResolveCurrent_CustomNameWithColors_UsesCustom()
    {
        var s = new AppSettings
        {
            TerminalThemeName = "Custom",
            CustomTerminalTheme = new TerminalThemeColors
            {
                Name = "MiTema",
                Background = "#abcdef",
                Foreground = "#123456",
            }
        };
        var t = TerminalTheme.ResolveCurrent(s);
        Assert.Equal("MiTema", t.Name);
        Assert.Equal("#abcdef", t.Background);
    }

    [Fact]
    public void ResolveCurrent_NamedThemeIgnoresCustom()
    {
        var s = new AppSettings
        {
            TerminalThemeName = "Dracula",
            CustomTerminalTheme = new TerminalThemeColors { Background = "#abcdef" }
        };
        var t = TerminalTheme.ResolveCurrent(s);
        Assert.Equal("Dracula", t.Name);
        Assert.NotEqual("#abcdef", t.Background);
    }

    [Fact]
    public void ResolveCurrent_CustomNameButNoColors_FallsBackToDefault()
    {
        var s = new AppSettings
        {
            TerminalThemeName = "Custom",
            CustomTerminalTheme = null
        };
        var t = TerminalTheme.ResolveCurrent(s);
        Assert.Equal(TerminalTheme.Default.Name, t.Name);
    }
}
