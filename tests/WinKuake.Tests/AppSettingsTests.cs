using System.IO;
using System.Text.Json;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_AreReasonable()
    {
        var s = new AppSettings();
        Assert.Equal("F12", s.HotkeyKey);
        Assert.Empty(s.HotkeyModifiers);
        Assert.InRange(s.HeightRatio, 0.1, 1.0);
        Assert.InRange(s.WidthRatio,  0.1, 1.0);
        Assert.InRange(s.Opacity,     0.5, 1.0);
        Assert.True(s.AnimationMs >= 0);
    }

    [Fact]
    public void DefaultSkin_IsDarkYakuakeStyle()
    {
        var s = new AppSettings();
        Assert.Equal("#1E1E1E", s.ChromeBackgroundHex);
        Assert.Equal("#3C3C3C", s.ChromeBorderHex);
        Assert.Equal("#E6E6E6", s.ChromeForegroundHex);
        Assert.Equal("#0E7AB5", s.AccentHex);
    }

    [Fact]
    public void Json_Roundtrip_PreservesAllFields()
    {
        var src = new AppSettings
        {
            HotkeyModifiers = new() { "Win", "Ctrl" },
            HotkeyKey = "OemTilde",
            HeightRatio = 1.0,
            WidthRatio  = 0.75,
            Opacity     = 0.9,
            DefaultProfile = "PowerShell",
            AutoHideOnFocusLost = true,
            StartWithWindows = true,
            AnimationMs = 220,
            MonitorIndex = 1,
            ChromeBackgroundHex = "#101010",
            ChromeBorderHex     = "#202020",
            ChromeForegroundHex = "#FFFFFF",
            AccentHex           = "#FF8800"
        };

        var opts = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        var json = JsonSerializer.Serialize(src, opts);
        var dst  = JsonSerializer.Deserialize<AppSettings>(json, opts)!;

        Assert.Equal(src.HotkeyModifiers, dst.HotkeyModifiers);
        Assert.Equal(src.HotkeyKey, dst.HotkeyKey);
        Assert.Equal(src.HeightRatio, dst.HeightRatio);
        Assert.Equal(src.WidthRatio,  dst.WidthRatio);
        Assert.Equal(src.Opacity, dst.Opacity);
        Assert.Equal(src.DefaultProfile, dst.DefaultProfile);
        Assert.Equal(src.AutoHideOnFocusLost, dst.AutoHideOnFocusLost);
        Assert.Equal(src.StartWithWindows, dst.StartWithWindows);
        Assert.Equal(src.AnimationMs, dst.AnimationMs);
        Assert.Equal(src.MonitorIndex, dst.MonitorIndex);
        Assert.Equal(src.ChromeBackgroundHex, dst.ChromeBackgroundHex);
        Assert.Equal(src.ChromeBorderHex,     dst.ChromeBorderHex);
        Assert.Equal(src.ChromeForegroundHex, dst.ChromeForegroundHex);
        Assert.Equal(src.AccentHex,           dst.AccentHex);
    }

    [Fact]
    public void ScrollbackLines_DefaultsToUnlimited()
    {
        // Convención: -1 significa "ilimitado" (en xterm se traduce a MAX_SAFE_INTEGER).
        var s = new AppSettings();
        Assert.Equal(-1, s.ScrollbackLines);
    }

    [Fact]
    public void ScrollbackLines_PersistsThroughJson()
    {
        var src = new AppSettings { ScrollbackLines = 50000 };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(src, opts);
        var dst  = JsonSerializer.Deserialize<AppSettings>(json, opts)!;
        Assert.Equal(50000, dst.ScrollbackLines);
    }

    [Fact]
    public void AllFields_PersistThroughJson()
    {
        var src = new AppSettings
        {
            HotkeyModifiers     = new() { "Ctrl", "Shift" },
            HotkeyKey           = "OemTilde",
            HeightRatio         = 0.66,
            WidthRatio          = 0.88,
            Opacity             = 0.91,
            DefaultProfile      = "Ubuntu",
            AutoHideOnFocusLost = true,
            StartWithWindows    = true,
            AnimationMs         = 250,
            ScrollbackLines     = 100000,
            TerminalThemeName   = "Dracula",
            TerminalFontSize    = 16,
            MonitorIndex        = 2,
            ChromeBackgroundHex = "#0A0A0A",
            ChromeBorderHex     = "#222222",
            ChromeForegroundHex = "#EEEEEE",
            AccentHex           = "#FF8800",
            UserSnippets        = new()
            {
                new UserSnippet { Name = "Mi build", Command = "make all" },
                new UserSnippet { Name = "Logs", Command = "tail -f {cwd}/log" },
            }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var dst  = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(src, opts), opts)!;

        Assert.Equal(src.HotkeyModifiers, dst.HotkeyModifiers);
        Assert.Equal(src.HotkeyKey, dst.HotkeyKey);
        Assert.Equal(src.HeightRatio, dst.HeightRatio);
        Assert.Equal(src.WidthRatio, dst.WidthRatio);
        Assert.Equal(src.Opacity, dst.Opacity);
        Assert.Equal(src.DefaultProfile, dst.DefaultProfile);
        Assert.Equal(src.AutoHideOnFocusLost, dst.AutoHideOnFocusLost);
        Assert.Equal(src.StartWithWindows, dst.StartWithWindows);
        Assert.Equal(src.AnimationMs, dst.AnimationMs);
        Assert.Equal(src.ScrollbackLines, dst.ScrollbackLines);
        Assert.Equal(src.TerminalThemeName, dst.TerminalThemeName);
        Assert.Equal(src.TerminalFontSize, dst.TerminalFontSize);
        Assert.Equal(src.MonitorIndex, dst.MonitorIndex);
        Assert.Equal(src.ChromeBackgroundHex, dst.ChromeBackgroundHex);
        Assert.Equal(src.ChromeBorderHex, dst.ChromeBorderHex);
        Assert.Equal(src.ChromeForegroundHex, dst.ChromeForegroundHex);
        Assert.Equal(src.AccentHex, dst.AccentHex);
        Assert.Equal(2, dst.UserSnippets.Count);
        Assert.Equal("Mi build", dst.UserSnippets[0].Name);
        Assert.Equal("make all", dst.UserSnippets[0].Command);
        Assert.Equal("Logs", dst.UserSnippets[1].Name);
        Assert.Equal("tail -f {cwd}/log", dst.UserSnippets[1].Command);
    }


    [Fact]
    public void Json_LoadingPartialFile_FillsMissingWithDefaults()
    {
        // Sólo dos campos en el JSON: el resto debe quedar con defaults.
        var json = "{\"hotkeyKey\":\"F11\",\"opacity\":0.85}";
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var dst  = JsonSerializer.Deserialize<AppSettings>(json, opts)!;

        Assert.Equal("F11", dst.HotkeyKey);
        Assert.Equal(0.85, dst.Opacity);
        Assert.Equal("#1E1E1E", dst.ChromeBackgroundHex); // default preservado
        Assert.Equal(180, dst.AnimationMs);               // default preservado
    }
}
