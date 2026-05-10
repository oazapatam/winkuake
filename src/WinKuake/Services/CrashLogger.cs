using System;
using System.IO;

namespace WinKuake.Services;

internal static class CrashLogger
{
    private static readonly string LogPath = Path.Combine(SettingsService.GetAppDataDir(), "winkuake.log");

    public static void Log(Exception? ex)
    {
        if (ex is null) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath,
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex.GetType().FullName}: {ex.Message}\n{ex.StackTrace}\n\n");
        }
        catch
        {
            // No hay nada útil que hacer si el log mismo falla.
        }
    }

    public static void Info(string message)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] INFO: {message}\n");
        }
        catch { /* idem */ }
    }
}
