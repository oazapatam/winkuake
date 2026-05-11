using System.Collections.Generic;
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
    public void LastSessionTabs_PersistThroughJson()
    {
        var src = new AppSettings
        {
            LastSessionTabs = new()
            {
                new PersistedTab { ProfileGuid = "{abc}", ProfileName = "Ubuntu", Cwd = @"C:\foo", CustomLabel = "bld", IsPinned = true },
                new PersistedTab { ProfileGuid = "{xyz}", ProfileName = "pwsh" },
            }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var dst = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(src, opts), opts)!;
        Assert.Equal(2, dst.LastSessionTabs.Count);
        Assert.Equal("Ubuntu", dst.LastSessionTabs[0].ProfileName);
        Assert.Equal(@"C:\foo", dst.LastSessionTabs[0].Cwd);
        Assert.Equal("bld", dst.LastSessionTabs[0].CustomLabel);
        Assert.True(dst.LastSessionTabs[0].IsPinned);
        Assert.Null(dst.LastSessionTabs[1].Cwd);
    }

    [Fact]
    public void PersistedSplitNode_RoundtripsLeafThroughJson()
    {
        var src = new PersistedTab
        {
            ProfileName = "Ubuntu",
            Layout = new PersistedSplitNode
            {
                ProfileGuid = "{abc}",
                ProfileName = "pwsh",
                Cwd = @"C:\code"
            }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(src, opts);
        var dst = JsonSerializer.Deserialize<PersistedTab>(json, opts)!;
        Assert.NotNull(dst.Layout);
        Assert.Null(dst.Layout!.Orientation);
        Assert.Equal("pwsh", dst.Layout.ProfileName);
        Assert.Equal(@"C:\code", dst.Layout.Cwd);
    }

    [Fact]
    public void PersistedSplitNode_RoundtripsBranchTree()
    {
        var src = new PersistedTab
        {
            Layout = new PersistedSplitNode
            {
                Orientation = "Vertical",
                First  = new PersistedSplitNode { ProfileName = "A" },
                Second = new PersistedSplitNode
                {
                    Orientation = "Horizontal",
                    First  = new PersistedSplitNode { ProfileName = "B" },
                    Second = new PersistedSplitNode { ProfileName = "C" }
                }
            }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(src, opts);
        var dst = JsonSerializer.Deserialize<PersistedTab>(json, opts)!;
        Assert.Equal("Vertical", dst.Layout!.Orientation);
        Assert.Equal("A", dst.Layout.First!.ProfileName);
        Assert.Equal("Horizontal", dst.Layout.Second!.Orientation);
        Assert.Equal("B", dst.Layout.Second.First!.ProfileName);
        Assert.Equal("C", dst.Layout.Second.Second!.ProfileName);
    }

    [Fact]
    public void Workspaces_PersistThroughJson()
    {
        var src = new AppSettings
        {
            Workspaces = new()
            {
                new Workspace
                {
                    Name = "dev",
                    Tabs = new()
                    {
                        new PersistedTab { ProfileName = "Ubuntu", Cwd = @"C:\code" }
                    }
                }
            }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var dst = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(src, opts), opts)!;
        Assert.Single(dst.Workspaces);
        Assert.Equal("dev", dst.Workspaces[0].Name);
        Assert.Single(dst.Workspaces[0].Tabs);
        Assert.Equal(@"C:\code", dst.Workspaces[0].Tabs[0].Cwd);
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

    [Fact]
    public void CustomTerminalTheme_DefaultsToNull()
    {
        var s = new AppSettings();
        Assert.Null(s.CustomTerminalTheme);
    }

    [Fact]
    public void TerminalThemeColors_Defaults_AreCustomAndBlack()
    {
        var c = new TerminalThemeColors();
        Assert.Equal("Custom", c.Name);
        Assert.Equal("#000000", c.Background);
        Assert.Equal("#000000", c.Foreground);
        Assert.Equal("#000000", c.Cursor);
        Assert.Equal("#000000", c.Black);
        Assert.Equal("#000000", c.Red);
        Assert.Equal("#000000", c.Green);
        Assert.Equal("#000000", c.Yellow);
        Assert.Equal("#000000", c.Blue);
        Assert.Equal("#000000", c.Magenta);
        Assert.Equal("#000000", c.Cyan);
        Assert.Equal("#000000", c.White);
        Assert.Equal("#000000", c.BrightBlack);
        Assert.Equal("#000000", c.BrightRed);
        Assert.Equal("#000000", c.BrightGreen);
        Assert.Equal("#000000", c.BrightYellow);
        Assert.Equal("#000000", c.BrightBlue);
        Assert.Equal("#000000", c.BrightMagenta);
        Assert.Equal("#000000", c.BrightCyan);
        Assert.Equal("#000000", c.BrightWhite);
    }

    [Fact]
    public void CustomTerminalTheme_PersistsThroughJson()
    {
        var src = new AppSettings
        {
            TerminalThemeName = "Custom",
            CustomTerminalTheme = new TerminalThemeColors
            {
                Name = "MiTema",
                Background = "#111111",
                Foreground = "#eeeeee",
                Cursor = "#ff00ff",
                Black = "#010101", Red = "#020202", Green = "#030303", Yellow = "#040404",
                Blue = "#050505", Magenta = "#060606", Cyan = "#070707", White = "#080808",
                BrightBlack = "#0a0a0a", BrightRed = "#0b0b0b", BrightGreen = "#0c0c0c",
                BrightYellow = "#0d0d0d", BrightBlue = "#0e0e0e", BrightMagenta = "#0f0f0f",
                BrightCyan = "#101010", BrightWhite = "#111110",
            }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var dst = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(src, opts), opts)!;

        Assert.Equal("Custom", dst.TerminalThemeName);
        Assert.NotNull(dst.CustomTerminalTheme);
        Assert.Equal("MiTema", dst.CustomTerminalTheme!.Name);
        Assert.Equal("#111111", dst.CustomTerminalTheme.Background);
        Assert.Equal("#eeeeee", dst.CustomTerminalTheme.Foreground);
        Assert.Equal("#ff00ff", dst.CustomTerminalTheme.Cursor);
        Assert.Equal("#010101", dst.CustomTerminalTheme.Black);
        Assert.Equal("#080808", dst.CustomTerminalTheme.White);
        Assert.Equal("#111110", dst.CustomTerminalTheme.BrightWhite);
    }

    [Fact]
    public void CustomKeybindings_DefaultsToEmpty()
    {
        var s = new AppSettings();
        Assert.NotNull(s.CustomKeybindings);
        Assert.Empty(s.CustomKeybindings);
    }

    [Fact]
    public void CustomKeybindings_PersistThroughJson()
    {
        var src = new AppSettings
        {
            CustomKeybindings = new Dictionary<string, string>
            {
                ["Hotkey"] = "Ctrl+Alt+Space",
                ["NewTab"] = "Ctrl+T",
            }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var dst = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(src, opts), opts)!;
        Assert.Equal(2, dst.CustomKeybindings.Count);
        Assert.Equal("Ctrl+Alt+Space", dst.CustomKeybindings["Hotkey"]);
        Assert.Equal("Ctrl+T", dst.CustomKeybindings["NewTab"]);
    }

    // -- DeepClone (regresión: el diálogo Settings perdía LastSessionTabs / Workspaces) ----

    /// <summary>
    /// AppSettings con TODOS los campos editables modificados (sin defaults).
    /// Útil para detectar regresiones donde un campo nuevo no se persiste.
    /// </summary>
    internal static AppSettings FullyPopulatedSettings() => new()
    {
        HotkeyModifiers     = new() { "Ctrl", "Alt", "Shift", "Win" },
        HotkeyKey           = "OemTilde",
        HeightRatio         = 0.77,
        WidthRatio          = 0.66,
        Opacity             = 0.83,
        DefaultProfile      = "Ubuntu",
        AutoHideOnFocusLost = true,
        StartWithWindows    = true,
        AnimationMs         = 333,
        ScrollbackLines     = 75000,
        TerminalThemeName   = "Custom",
        TerminalFontSize    = 17,
        MonitorIndex        = 2,
        ChromeBackgroundHex = "#0A0A0A",
        ChromeBorderHex     = "#1B1B1B",
        ChromeForegroundHex = "#FAFAFA",
        AccentHex           = "#FF8800",
        UserSnippets        = new()
        {
            new UserSnippet { Name = "build", Command = "make -j" },
            new UserSnippet { Name = "logs",  Command = "tail -F {cwd}/app.log" },
        },
        LastSessionTabs = new()
        {
            new PersistedTab { ProfileGuid = "{abc}", ProfileName = "Ubuntu", Cwd = @"C:\code", IsPinned = true, CustomLabel = "dev" },
            new PersistedTab
            {
                ProfileName = "split-root",
                Layout = new PersistedSplitNode
                {
                    Orientation = "Vertical",
                    First  = new PersistedSplitNode { ProfileName = "A", Cwd = @"C:\a" },
                    Second = new PersistedSplitNode { ProfileName = "B" }
                }
            }
        },
        Workspaces = new()
        {
            new Workspace
            {
                Name = "dev",
                Tabs = new() { new PersistedTab { ProfileName = "Ubuntu", Cwd = @"C:\code" } }
            }
        },
        CustomTerminalTheme = new TerminalThemeColors
        {
            Name = "MiTema",
            Background = "#101010", Foreground = "#fefefe", Cursor = "#ff0000",
            Black = "#010101", Red = "#020202", Green = "#030303", Yellow = "#040404",
            Blue = "#050505", Magenta = "#060606", Cyan = "#070707", White = "#080808",
            BrightBlack = "#0a0a0a", BrightRed = "#0b0b0b", BrightGreen = "#0c0c0c",
            BrightYellow = "#0d0d0d", BrightBlue = "#0e0e0e", BrightMagenta = "#0f0f0f",
            BrightCyan = "#101010", BrightWhite = "#111110",
        },
        CustomKeybindings = new()
        {
            ["Hotkey"] = "Ctrl+Alt+Space",
            ["NewTab"] = "Ctrl+T",
        },
    };

    [Fact]
    public void DeepClone_PreservesAllScalarFields()
    {
        var src = FullyPopulatedSettings();
        var copy = src.DeepClone();

        Assert.Equal(src.HotkeyModifiers, copy.HotkeyModifiers);
        Assert.Equal(src.HotkeyKey, copy.HotkeyKey);
        Assert.Equal(src.HeightRatio, copy.HeightRatio);
        Assert.Equal(src.WidthRatio, copy.WidthRatio);
        Assert.Equal(src.Opacity, copy.Opacity);
        Assert.Equal(src.DefaultProfile, copy.DefaultProfile);
        Assert.Equal(src.AutoHideOnFocusLost, copy.AutoHideOnFocusLost);
        Assert.Equal(src.StartWithWindows, copy.StartWithWindows);
        Assert.Equal(src.AnimationMs, copy.AnimationMs);
        Assert.Equal(src.ScrollbackLines, copy.ScrollbackLines);
        Assert.Equal(src.TerminalThemeName, copy.TerminalThemeName);
        Assert.Equal(src.TerminalFontSize, copy.TerminalFontSize);
        Assert.Equal(src.MonitorIndex, copy.MonitorIndex);
        Assert.Equal(src.ChromeBackgroundHex, copy.ChromeBackgroundHex);
        Assert.Equal(src.ChromeBorderHex, copy.ChromeBorderHex);
        Assert.Equal(src.ChromeForegroundHex, copy.ChromeForegroundHex);
        Assert.Equal(src.AccentHex, copy.AccentHex);
    }

    [Fact]
    public void DeepClone_PreservesLastSessionTabs()
    {
        // Regresión: SettingsWindow.Clone NO copiaba LastSessionTabs, así que abrir
        // y guardar Settings borraba las pestañas persistidas hasta el próximo cierre.
        var src = new AppSettings
        {
            LastSessionTabs = new()
            {
                new PersistedTab { ProfileGuid = "{abc}", ProfileName = "Ubuntu", Cwd = @"C:\code", IsPinned = true, CustomLabel = "dev" },
                new PersistedTab { ProfileName = "pwsh" },
            }
        };
        var copy = src.DeepClone();
        Assert.Equal(2, copy.LastSessionTabs.Count);
        Assert.Equal("Ubuntu", copy.LastSessionTabs[0].ProfileName);
        Assert.Equal(@"C:\code", copy.LastSessionTabs[0].Cwd);
        Assert.True(copy.LastSessionTabs[0].IsPinned);
        Assert.Equal("dev", copy.LastSessionTabs[0].CustomLabel);
        Assert.Equal("pwsh", copy.LastSessionTabs[1].ProfileName);
    }

    [Fact]
    public void DeepClone_PreservesWorkspaces()
    {
        // Regresión: el bug más visible — guardar Settings borraba todos los workspaces.
        var src = new AppSettings
        {
            Workspaces = new()
            {
                new Workspace
                {
                    Name = "dev",
                    Tabs = new() { new PersistedTab { ProfileName = "Ubuntu", Cwd = @"C:\code" } }
                },
                new Workspace
                {
                    Name = "ops",
                    Tabs = new() { new PersistedTab { ProfileName = "pwsh" } }
                },
            }
        };
        var copy = src.DeepClone();
        Assert.Equal(2, copy.Workspaces.Count);
        Assert.Equal("dev", copy.Workspaces[0].Name);
        Assert.Single(copy.Workspaces[0].Tabs);
        Assert.Equal("Ubuntu", copy.Workspaces[0].Tabs[0].ProfileName);
        Assert.Equal(@"C:\code", copy.Workspaces[0].Tabs[0].Cwd);
        Assert.Equal("ops", copy.Workspaces[1].Name);
    }

    [Fact]
    public void DeepClone_PreservesUserSnippets()
    {
        var src = new AppSettings
        {
            UserSnippets = new()
            {
                new UserSnippet { Name = "build", Command = "make -j" },
                new UserSnippet { Name = "logs",  Command = "tail -F log" },
            }
        };
        var copy = src.DeepClone();
        Assert.Equal(2, copy.UserSnippets.Count);
        Assert.Equal("build", copy.UserSnippets[0].Name);
        Assert.Equal("make -j", copy.UserSnippets[0].Command);
    }

    [Fact]
    public void DeepClone_PreservesCustomKeybindings()
    {
        var src = new AppSettings
        {
            CustomKeybindings = new Dictionary<string, string>
            {
                ["Hotkey"] = "Ctrl+Alt+Space",
                ["NewTab"] = "Ctrl+T",
            }
        };
        var copy = src.DeepClone();
        Assert.Equal(2, copy.CustomKeybindings.Count);
        Assert.Equal("Ctrl+Alt+Space", copy.CustomKeybindings["Hotkey"]);
        Assert.Equal("Ctrl+T", copy.CustomKeybindings["NewTab"]);
    }

    [Fact]
    public void DeepClone_PreservesCustomTerminalTheme()
    {
        var src = new AppSettings
        {
            TerminalThemeName = "Custom",
            CustomTerminalTheme = new TerminalThemeColors
            {
                Name = "MiTema",
                Background = "#101010", Foreground = "#fefefe", Cursor = "#ff0000",
                Black = "#010101", Red = "#020202", Green = "#030303", Yellow = "#040404",
                Blue = "#050505", Magenta = "#060606", Cyan = "#070707", White = "#080808",
                BrightBlack = "#0a0a0a", BrightRed = "#0b0b0b", BrightGreen = "#0c0c0c",
                BrightYellow = "#0d0d0d", BrightBlue = "#0e0e0e", BrightMagenta = "#0f0f0f",
                BrightCyan = "#101010", BrightWhite = "#111110",
            }
        };
        var copy = src.DeepClone();
        Assert.NotNull(copy.CustomTerminalTheme);
        Assert.Equal("MiTema", copy.CustomTerminalTheme!.Name);
        Assert.Equal("#101010", copy.CustomTerminalTheme.Background);
        Assert.Equal("#111110", copy.CustomTerminalTheme.BrightWhite);
    }

    [Fact]
    public void DeepClone_PreservesSplitTreeLayout()
    {
        var src = new AppSettings
        {
            LastSessionTabs = new()
            {
                new PersistedTab
                {
                    ProfileName = "root",
                    Layout = new PersistedSplitNode
                    {
                        Orientation = "Vertical",
                        First  = new PersistedSplitNode { ProfileName = "A", Cwd = @"C:\a" },
                        Second = new PersistedSplitNode
                        {
                            Orientation = "Horizontal",
                            First  = new PersistedSplitNode { ProfileName = "B" },
                            Second = new PersistedSplitNode { ProfileName = "C" }
                        }
                    }
                }
            }
        };
        var copy = src.DeepClone();
        var layout = copy.LastSessionTabs[0].Layout;
        Assert.NotNull(layout);
        Assert.Equal("Vertical", layout!.Orientation);
        Assert.Equal("A", layout.First!.ProfileName);
        Assert.Equal(@"C:\a", layout.First.Cwd);
        Assert.Equal("Horizontal", layout.Second!.Orientation);
        Assert.Equal("B", layout.Second.First!.ProfileName);
        Assert.Equal("C", layout.Second.Second!.ProfileName);
    }

    [Fact]
    public void DeepClone_ReturnsIndependentCopy()
    {
        // Mutar el clon NO debe afectar al original (verifica que las colecciones
        // sean nuevas, no referencias compartidas).
        var src = new AppSettings
        {
            HotkeyModifiers = new() { "Ctrl" },
            UserSnippets    = new() { new UserSnippet { Name = "a", Command = "b" } },
            Workspaces      = new() { new Workspace { Name = "ws1" } },
            LastSessionTabs = new() { new PersistedTab { ProfileName = "t1" } },
            CustomKeybindings = new() { ["x"] = "y" },
        };
        var copy = src.DeepClone();

        copy.HotkeyModifiers.Add("Alt");
        copy.UserSnippets[0].Name = "modified";
        copy.Workspaces[0].Name = "ws-modified";
        copy.LastSessionTabs[0].ProfileName = "t1-modified";
        copy.CustomKeybindings["x"] = "z";

        Assert.Single(src.HotkeyModifiers);
        Assert.Equal("a", src.UserSnippets[0].Name);
        Assert.Equal("ws1", src.Workspaces[0].Name);
        Assert.Equal("t1", src.LastSessionTabs[0].ProfileName);
        Assert.Equal("y", src.CustomKeybindings["x"]);
    }
}
