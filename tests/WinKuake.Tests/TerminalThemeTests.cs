using System.Linq;
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
}
