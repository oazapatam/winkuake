using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace WinKuake.Services;

/// <summary>Distribución WSL detectada en el sistema.</summary>
public sealed record WslDistribution(string Name, string State, int Version, bool IsDefault);

/// <summary>
/// Operaciones de WSL: listar distros, construir líneas de comando que
/// arrancan login shell con el shell del usuario, traducir paths Windows
/// → /mnt/&lt;letra&gt;/ para usar como <c>--cd</c>.
/// </summary>
public static class WslService
{
    // Distribuciones internas que NO son shells de usuario: las usa Docker
    // Desktop como backend pero no las queremos exponer como perfil.
    private static readonly HashSet<string> InternalDistros = new(StringComparer.OrdinalIgnoreCase)
    {
        "docker-desktop", "docker-desktop-data", "rancher-desktop", "rancher-desktop-data"
    };

    /// <summary>
    /// Lista distros llamando a <c>wsl.exe -l --verbose</c>. Devuelve lista vacía
    /// si WSL no está instalado o no responde dentro del timeout.
    /// </summary>
    public static IReadOnlyList<WslDistribution> ListDistributions(int timeoutMs = 3000)
    {
        try
        {
            var psi = new ProcessStartInfo("wsl.exe", "-l --verbose")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.Unicode
            };
            using var p = Process.Start(psi);
            if (p is null) return Array.Empty<WslDistribution>();
            var output = p.StandardOutput.ReadToEnd();
            if (!p.WaitForExit(timeoutMs)) try { p.Kill(); } catch { }
            return ParseListVerbose(output);
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
            return Array.Empty<WslDistribution>();
        }
    }

    /// <summary>
    /// Parsea el output de <c>wsl.exe -l --verbose</c>. Formato típico:
    /// <code>
    ///   NAME            STATE           VERSION
    /// * Ubuntu          Running         2
    ///   Debian          Stopped         2
    /// </code>
    /// El asterisco al inicio marca la distro default.
    /// </summary>
    public static IReadOnlyList<WslDistribution> ParseListVerbose(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<WslDistribution>();

        var result = new List<WslDistribution>();
        var lines = raw.Replace("\0", "").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line)) continue;
            // Skip header.
            if (line.TrimStart().StartsWith("NAME", StringComparison.OrdinalIgnoreCase)) continue;

            // El prefijo "* " marca default; si no, espacios.
            bool isDefault = line.StartsWith("*");
            var rest = isDefault ? line.Substring(1).Trim() : line.Trim();

            // Las columnas están separadas por 2+ espacios.
            var cols = Regex.Split(rest, @"\s{2,}").Where(c => !string.IsNullOrWhiteSpace(c)).ToArray();
            if (cols.Length < 1) continue;
            var name = cols[0].Trim();
            if (string.IsNullOrEmpty(name)) continue;
            if (InternalDistros.Contains(name)) continue;

            var state   = cols.Length >= 2 ? cols[1].Trim() : "";
            var version = cols.Length >= 3 && int.TryParse(cols[2].Trim(), out var v) ? v : 0;

            result.Add(new WslDistribution(name, state, version, isDefault));
        }
        return result;
    }

    /// <summary>
    /// Construye la línea de comandos para arrancar una distro WSL.
    /// <paramref name="loginShell"/> = true añade <c>--shell-type login</c> que
    /// hace que el shell del usuario cargue su perfil (.bashrc/.zshrc/.profile).
    /// <paramref name="windowsStartingDirectory"/> se traduce a path WSL si parece Windows.
    /// </summary>
    public static string BuildCommandLine(
        string distro,
        bool loginShell = true,
        bool startAtHome = false,
        string? windowsStartingDirectory = null)
    {
        // wsl.exe NO usa el parsing CRT estándar de Windows: las comillas se
        // tratan como parte del valor literal. Por eso NO comillamos NI el
        // nombre de la distro NI el path de --cd. Esto significa que distros
        // con espacios en el nombre no son soportadas (caso muy raro).
        var sb = new StringBuilder();
        sb.Append("wsl.exe -d ").Append(distro);

        if (loginShell) sb.Append(" --shell-type login");

        if (!string.IsNullOrWhiteSpace(windowsStartingDirectory))
        {
            var p = TranslateWindowsPathToWsl(windowsStartingDirectory);
            sb.Append(" --cd ").Append(p);
        }
        else if (startAtHome)
        {
            sb.Append(" --cd ~");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Traduce <c>C:\Users\foo</c> a <c>/mnt/c/Users/foo</c>. Si el path ya
    /// parece Unix (empieza con /), lo deja como está. Vacío devuelve vacío.
    /// </summary>
    public static string TranslateWindowsPathToWsl(string path)
    {
        if (string.IsNullOrEmpty(path)) return path;
        if (path.StartsWith("/")) return path;

        // Letra de unidad: "C:\..." → "/mnt/c/..."
        if (path.Length >= 2 && path[1] == ':')
        {
            var drive = char.ToLowerInvariant(path[0]);
            var rest = path.Substring(2).Replace('\\', '/');
            // Eliminar slash duplicado tras "/mnt/c/" si el path original era solo "C:\"
            return "/mnt/" + drive + rest;
        }
        // Path relativo o desconocido — devolver tal cual con separadores Unix.
        return path.Replace('\\', '/');
    }
}
