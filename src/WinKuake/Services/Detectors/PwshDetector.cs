using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WinKuake.Models;

namespace WinKuake.Services.Detectors;

/// <summary>
/// PowerShell 7+ (pwsh.exe). Se distribuye por MSI (a Program Files) o por
/// la Store. Puede haber varias instalaciones simultáneas (estable + preview);
/// generamos un perfil por cada path único.
/// </summary>
public sealed class PwshDetector : IProfileDetector
{
    public IReadOnlyList<UserProfile> Detect()
    {
        var raw = DetectorHelpers.RunCapture("where.exe", "pwsh.exe");
        return BuildProfiles(raw);
    }

    /// <summary>
    /// Variante testeable: dado el output crudo de <c>where.exe pwsh.exe</c>,
    /// devuelve los perfiles correspondientes. Cada línea es un path; se
    /// dedupean (case-insensitive) y se descartan los inexistentes.
    /// </summary>
    public static IReadOnlyList<UserProfile> BuildProfiles(string whereOutput)
    {
        if (string.IsNullOrWhiteSpace(whereOutput)) return Array.Empty<UserProfile>();

        var paths = whereOutput
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .ToList();

        return BuildProfilesFromPaths(paths);
    }

    /// <summary>
    /// Construye perfiles desde una lista de paths ya validados. Útil para
    /// tests donde no queremos invocar <see cref="File.Exists"/>.
    /// </summary>
    public static IReadOnlyList<UserProfile> BuildProfilesFromPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0) return Array.Empty<UserProfile>();

        // Si hay más de un path, intentamos diferenciar por versión para que el
        // usuario pueda distinguir 7.4 de 7.5-preview.
        bool annotateVersion = paths.Count > 1;

        var profiles = new List<UserProfile>(paths.Count);
        foreach (var path in paths)
        {
            var quoted = "\"" + path + "\"";
            var name = "PowerShell";
            if (annotateVersion)
            {
                var version = DetectorHelpers.TryGetFileVersion(path);
                name = string.IsNullOrEmpty(version) ? $"PowerShell ({path})" : $"PowerShell {version}";
            }
            profiles.Add(new UserProfile
            {
                Id = DetectorHelpers.StableGuidFromString(quoted),
                Name = name,
                CommandLine = quoted,
                IconGlyph = "⚡",
                Source = "Detected",
            });
        }
        return profiles;
    }
}
