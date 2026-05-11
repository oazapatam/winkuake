using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinKuake.Models;

namespace WinKuake.Services.Detectors;

/// <summary>
/// Developer Command Prompt y Developer PowerShell para Visual Studio.
/// Usa <c>vswhere.exe</c> (siempre se instala con cualquier VS 2017+) para
/// enumerar instalaciones. Por cada una emite hasta 2 perfiles si los scripts
/// existen.
/// </summary>
public sealed class VsDeveloperDetector : IProfileDetector
{
    public IReadOnlyList<UserProfile> Detect()
    {
        var vswherePath = LocateVsWhere(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
        if (vswherePath is null) return Array.Empty<UserProfile>();

        // -all para incluir Build Tools y previews; -property installationPath
        // imprime una línea por instalación.
        var raw = DetectorHelpers.RunCapture(vswherePath, "-all -property installationPath");
        var installations = ParseInstallations(raw);
        return BuildProfiles(installations);
    }

    /// <summary>
    /// Devuelve el path completo a <c>vswhere.exe</c> si existe en
    /// <c>%ProgramFiles(x86)%\Microsoft Visual Studio\Installer</c>; null si no.
    /// </summary>
    public static string? LocateVsWhere(string? programFilesX86)
    {
        if (string.IsNullOrWhiteSpace(programFilesX86)) return null;
        var candidate = Path.Combine(programFilesX86, "Microsoft Visual Studio", "Installer", "vswhere.exe");
        return File.Exists(candidate) ? candidate : null;
    }

    /// <summary>
    /// Parsea el output de <c>vswhere -all -property installationPath</c>:
    /// una línea por instalación, paths absolutos. Filtra vacíos, dedupea.
    /// </summary>
    public static IReadOnlyList<string> ParseInstallations(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<string>();
        return raw
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Por cada instalación, intenta producir hasta 2 perfiles (cmd + ps).
    /// Descarta el cmd si falta <c>VsDevCmd.bat</c>; descarta el ps si falta
    /// <c>Launch-VsDevShell.ps1</c>. La detección NO requiere pwsh.exe — el
    /// script está pensado para correr tanto en Windows PowerShell como en
    /// PowerShell 7; si el usuario no tiene pwsh, el perfil de PS fallará al
    /// arrancar pero no se descarta aquí (es info que no tenemos garantizada).
    /// </summary>
    public static IReadOnlyList<UserProfile> BuildProfiles(IReadOnlyList<string> installationPaths)
    {
        if (installationPaths is null || installationPaths.Count == 0) return Array.Empty<UserProfile>();

        var profiles = new List<UserProfile>(installationPaths.Count * 2);
        foreach (var install in installationPaths)
        {
            if (string.IsNullOrWhiteSpace(install)) continue;

            var versionLabel = ExtractVersionLabel(install);
            var vsDevCmd = Path.Combine(install, "Common7", "Tools", "VsDevCmd.bat");
            var launchPs = Path.Combine(install, "Common7", "Tools", "Launch-VsDevShell.ps1");

            if (File.Exists(vsDevCmd))
            {
                var cmd = $"cmd.exe /k \"{vsDevCmd}\"";
                profiles.Add(new UserProfile
                {
                    Id = DetectorHelpers.StableGuidFromString(cmd),
                    Name = $"Developer Command Prompt for VS {versionLabel}",
                    CommandLine = cmd,
                    IconGlyph = "≫",
                    Source = "Detected",
                });

                if (File.Exists(launchPs))
                {
                    var psCmd = $"pwsh.exe -NoExit -Command \"& '{launchPs}'\"";
                    profiles.Add(new UserProfile
                    {
                        Id = DetectorHelpers.StableGuidFromString(psCmd),
                        Name = $"Developer PowerShell for VS {versionLabel}",
                        CommandLine = psCmd,
                        IconGlyph = "⚡",
                        Source = "Detected",
                    });
                }
            }
        }
        return profiles;
    }

    /// <summary>
    /// Heurística simple para sacar la "versión" desde el path de instalación.
    /// Paths típicos: <c>...\Microsoft Visual Studio\2022\Community</c>. Devuelve
    /// "2022" o "2022 Community" para diferenciar instalaciones múltiples.
    /// Si no calza, devuelve el último segmento del path.
    /// </summary>
    public static string ExtractVersionLabel(string installationPath)
    {
        if (string.IsNullOrWhiteSpace(installationPath)) return "";
        var parts = installationPath
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                   StringSplitOptions.RemoveEmptyEntries);

        // Buscar un segmento numérico de 4 dígitos (year) y combinar con el siguiente.
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length == 4 && int.TryParse(parts[i], out _))
            {
                var edition = i + 1 < parts.Length ? parts[i + 1] : "";
                return string.IsNullOrEmpty(edition) ? parts[i] : $"{parts[i]} {edition}";
            }
        }
        return parts.Length > 0 ? parts[^1] : installationPath;
    }
}
