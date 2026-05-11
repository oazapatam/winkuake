using System;
using System.IO;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// SettingsService usa una ruta fija en %AppData%/WinKuake. Para no pisar el
/// settings real del usuario, los tests guardan/restauran la copia previa.
/// </summary>
public class SettingsServiceTests : IDisposable
{
    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinKuake");
    private static readonly string SettingsPath = Path.Combine(AppDir, "settings.json");

    private readonly string? _backup;

    public SettingsServiceTests()
    {
        if (File.Exists(SettingsPath))
        {
            _backup = SettingsPath + ".test-backup";
            File.Copy(SettingsPath, _backup, overwrite: true);
            File.Delete(SettingsPath);
        }
    }

    public void Dispose()
    {
        if (_backup is not null && File.Exists(_backup))
        {
            File.Copy(_backup, SettingsPath, overwrite: true);
            File.Delete(_backup);
        }
        else if (File.Exists(SettingsPath))
        {
            File.Delete(SettingsPath);
        }
    }

    [Fact]
    public void Load_NoFile_ReturnsDefaults()
    {
        var loaded = SettingsService.Load();
        Assert.Equal("F12", loaded.HotkeyKey);
        Assert.Equal(0.5, loaded.HeightRatio);
    }

    [Fact]
    public void Save_ThenLoad_Roundtrips()
    {
        var s = new AppSettings
        {
            HotkeyKey = "F11",
            HeightRatio = 0.92,
            HotkeyModifiers = new() { "Ctrl", "Shift" },
            AccentHex = "#FF00FF"
        };

        SettingsService.Save(s);
        var loaded = SettingsService.Load();

        Assert.Equal("F11", loaded.HotkeyKey);
        Assert.Equal(0.92, loaded.HeightRatio);
        Assert.Contains("Ctrl",  loaded.HotkeyModifiers);
        Assert.Contains("Shift", loaded.HotkeyModifiers);
        Assert.Equal("#FF00FF", loaded.AccentHex);
    }

    [Fact]
    public void Save_CreatesAppDataDirectoryIfMissing()
    {
        if (Directory.Exists(AppDir)) Directory.Delete(AppDir, recursive: true);
        SettingsService.Save(new AppSettings());
        Assert.True(File.Exists(SettingsPath));
    }

    [Fact]
    public void Load_CorruptJson_ReturnsDefaults()
    {
        Directory.CreateDirectory(AppDir);
        File.WriteAllText(SettingsPath, "{this is not valid json");
        var loaded = SettingsService.Load();
        Assert.Equal("F12", loaded.HotkeyKey); // fallback a defaults
    }

    [Fact]
    public void Save_ThenLoad_PersistsTerminalSettings()
    {
        var s = new AppSettings
        {
            TerminalThemeName = "Dracula",
            TerminalFontSize  = 18,
            ScrollbackLines   = 50000,
        };
        SettingsService.Save(s);
        var loaded = SettingsService.Load();

        Assert.Equal("Dracula", loaded.TerminalThemeName);
        Assert.Equal(18, loaded.TerminalFontSize);
        Assert.Equal(50000, loaded.ScrollbackLines);
    }

    [Fact]
    public void Save_ThenLoad_PersistsUserSnippets()
    {
        var s = new AppSettings
        {
            UserSnippets = new()
            {
                new() { Name = "Build", Command = "make -j" },
                new() { Name = "Logs",  Command = "tail -F {cwd}/log" },
            }
        };
        SettingsService.Save(s);
        var loaded = SettingsService.Load();

        Assert.Equal(2, loaded.UserSnippets.Count);
        Assert.Equal("Build", loaded.UserSnippets[0].Name);
        Assert.Equal("make -j", loaded.UserSnippets[0].Command);
        Assert.Equal("Logs", loaded.UserSnippets[1].Name);
        Assert.Equal("tail -F {cwd}/log", loaded.UserSnippets[1].Command);
    }

    [Fact]
    public void SettingsService_PersistsCompleteAppSettings_Roundtrip()
    {
        // Sanity check end-to-end: TODOS los campos persistibles deben sobrevivir
        // un ciclo Save → Load contra el disco real, no solo el JSON in-memory.
        var src = AppSettingsTests.FullyPopulatedSettings();

        SettingsService.Save(src);
        var dst = SettingsService.Load();

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

        Assert.Equal(src.UserSnippets.Count, dst.UserSnippets.Count);
        for (var i = 0; i < src.UserSnippets.Count; i++)
        {
            Assert.Equal(src.UserSnippets[i].Name,    dst.UserSnippets[i].Name);
            Assert.Equal(src.UserSnippets[i].Command, dst.UserSnippets[i].Command);
        }

        Assert.Equal(src.LastSessionTabs.Count, dst.LastSessionTabs.Count);
        for (var i = 0; i < src.LastSessionTabs.Count; i++)
        {
            Assert.Equal(src.LastSessionTabs[i].ProfileGuid, dst.LastSessionTabs[i].ProfileGuid);
            Assert.Equal(src.LastSessionTabs[i].ProfileName, dst.LastSessionTabs[i].ProfileName);
            Assert.Equal(src.LastSessionTabs[i].Cwd,         dst.LastSessionTabs[i].Cwd);
            Assert.Equal(src.LastSessionTabs[i].IsPinned,    dst.LastSessionTabs[i].IsPinned);
            Assert.Equal(src.LastSessionTabs[i].CustomLabel, dst.LastSessionTabs[i].CustomLabel);
        }
        // Verificar el árbol de splits del segundo tab.
        var layout = dst.LastSessionTabs[1].Layout;
        Assert.NotNull(layout);
        Assert.Equal("Vertical", layout!.Orientation);
        Assert.Equal("A", layout.First!.ProfileName);
        Assert.Equal(@"C:\a", layout.First.Cwd);
        Assert.Equal("B", layout.Second!.ProfileName);

        Assert.Equal(src.Workspaces.Count, dst.Workspaces.Count);
        Assert.Equal("dev", dst.Workspaces[0].Name);
        Assert.Equal(@"C:\code", dst.Workspaces[0].Tabs[0].Cwd);

        Assert.NotNull(dst.CustomTerminalTheme);
        Assert.Equal(src.CustomTerminalTheme!.Name, dst.CustomTerminalTheme!.Name);
        Assert.Equal(src.CustomTerminalTheme.Background, dst.CustomTerminalTheme.Background);
        Assert.Equal(src.CustomTerminalTheme.BrightWhite, dst.CustomTerminalTheme.BrightWhite);

        Assert.Equal(src.CustomKeybindings.Count, dst.CustomKeybindings.Count);
        Assert.Equal("Ctrl+Alt+Space", dst.CustomKeybindings["Hotkey"]);
        Assert.Equal("Ctrl+T",         dst.CustomKeybindings["NewTab"]);
    }
}
