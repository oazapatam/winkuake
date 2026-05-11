using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WinKuake.Services.Detectors;

/// <summary>
/// Utilidades compartidas por los detectores: generación de Guid estable a
/// partir de un commandline (para que re-correr la detección no duplique
/// perfiles), ejecución acotada de procesos auxiliares, y resolución de
/// version info de un .exe.
/// </summary>
internal static class DetectorHelpers
{
    /// <summary>
    /// Deriva un GUID estable de un string (típicamente el commandline del
    /// perfil). Determinístico: la misma entrada siempre devuelve el mismo
    /// GUID. Útil para que "Detectar terminales" no genere duplicados al
    /// re-correrse — el perfil mantiene su Id y así se hace match con el ya
    /// persistido.
    /// </summary>
    public static string StableGuidFromString(string input)
    {
        // Variant 5 de UUID (SHA-1) es overkill aquí; un MD5 truncado a 16
        // bytes y re-empaquetado como GUID es suficiente y trivial. No se usa
        // para nada criptográfico.
        var bytes = MD5.HashData(Encoding.UTF8.GetBytes(input ?? ""));
        return new Guid(bytes).ToString();
    }

    /// <summary>
    /// Corre un proceso auxiliar (where.exe, vswhere.exe, wsl.exe) leyendo
    /// stdout completo, con timeout. Devuelve string vacío si falla, no
    /// existe o excede el timeout. Nunca lanza.
    /// </summary>
    public static string RunCapture(string fileName, string arguments, int timeoutMs = 1500)
    {
        try
        {
            var psi = new ProcessStartInfo(fileName, arguments)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return "";
            var stdout = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(timeoutMs))
            {
                try { p.Kill(); } catch { /* idem */ }
                return "";
            }
            return stdout ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Devuelve la version "1.2.3.4" del .exe vía
    /// <see cref="FileVersionInfo"/>. Null si el archivo no existe o no
    /// tiene metadata. Best-effort, no lanza.
    /// </summary>
    public static string? TryGetFileVersion(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var v = FileVersionInfo.GetVersionInfo(path);
            return v.ProductVersion ?? v.FileVersion;
        }
        catch
        {
            return null;
        }
    }
}
