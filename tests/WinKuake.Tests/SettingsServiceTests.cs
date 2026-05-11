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
}
