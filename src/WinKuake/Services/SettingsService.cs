using System;
using System.IO;
using System.Text.Json;
using WinKuake.Models;

namespace WinKuake.Services;

public static class SettingsService
{
    private static readonly string AppDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WinKuake");

    private static readonly string SettingsPath = Path.Combine(AppDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
            {
                // Primera ejecución: persistir defaults para que el archivo
                // exista y el usuario vea que el sistema sí guarda.
                var defaults = new AppSettings();
                Save(defaults);
                return defaults;
            }
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
            return new AppSettings();
        }
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(AppDir);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
        }
    }

    public static string GetAppDataDir() => AppDir;
}
