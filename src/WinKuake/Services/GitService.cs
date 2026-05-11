using System;
using System.Diagnostics;

namespace WinKuake.Services;

/// <summary>
/// Operaciones puntuales de git ejecutadas como child process. No mantiene
/// estado; cada llamada lanza git con timeout corto.
/// </summary>
public static class GitService
{
    /// <summary>
    /// Rama actual del repo en <paramref name="cwd"/>. Null si no es repo,
    /// git no está instalado, o el proceso no respondió a tiempo.
    /// </summary>
    public static string? GetBranch(string? cwd, int timeoutMs = 600)
    {
        if (string.IsNullOrWhiteSpace(cwd)) return null;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "git",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };
            psi.ArgumentList.Add("-C");
            psi.ArgumentList.Add(cwd);
            psi.ArgumentList.Add("rev-parse");
            psi.ArgumentList.Add("--abbrev-ref");
            psi.ArgumentList.Add("HEAD");

            using var p = Process.Start(psi);
            if (p is null) return null;
            var output = p.StandardOutput.ReadToEnd().Trim();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { }
                return null;
            }
            if (p.ExitCode != 0 || string.IsNullOrEmpty(output)) return null;
            return output;
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
            return null;
        }
    }
}
