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
        // Igual que Windows Terminal: combinamos varias fuentes para no perder
        // instalaciones cuando el PATH no está refrescado (caso clásico post-MSI
        // sin reboot) o cuando pwsh se instaló por dotnet tool.
        var pf    = EnumerateInstallExes(Environment.GetEnvironmentVariable("ProgramFiles"));
        var pfx86 = EnumerateInstallExes(Environment.GetEnvironmentVariable("ProgramFiles(x86)"));
        var dotnetTools = TryDotnetToolsPwsh(Environment.GetEnvironmentVariable("USERPROFILE"));
        var raw = DetectorHelpers.RunCapture("where.exe", "pwsh.exe");

        var candidates = CollectCandidates(pf, pfx86, dotnetTools, raw);
        return BuildProfilesFromPaths(candidates);
    }

    /// <summary>
    /// Combina paths candidatos de las 4 fuentes (Program Files, x86, .dotnet/tools,
    /// where.exe), dedupea case-insensitive y descarta cualquier path que pase
    /// por <c>\WindowsApps\</c> (MSIX o App Execution Alias — no son ejecutables
    /// usables desde un pseudo-console). Función pura: no toca filesystem.
    /// </summary>
    public static IReadOnlyList<string> CollectCandidates(
        IEnumerable<string> programFilesPwshExes,
        IEnumerable<string> programFilesX86PwshExes,
        string? dotnetToolsPwshExe,
        string whereOutput)
    {
        var result = new List<string>();
        result.AddRange(programFilesPwshExes);
        result.AddRange(programFilesX86PwshExes);
        if (!string.IsNullOrWhiteSpace(dotnetToolsPwshExe))
            result.Add(dotnetToolsPwshExe!);

        if (!string.IsNullOrWhiteSpace(whereOutput))
        {
            foreach (var line in whereOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Trim();
                if (p.Length == 0) continue;
                result.Add(p);
            }
        }

        return result
            .Where(p => p.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) < 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Enumera <c>{root}\PowerShell\&lt;version&gt;\pwsh.exe</c> para cada subdir
    /// existente. Devuelve vacío si <paramref name="root"/> es null o si la
    /// carpeta PowerShell no existe.
    /// </summary>
    private static IEnumerable<string> EnumerateInstallExes(string? root)
    {
        if (string.IsNullOrWhiteSpace(root)) yield break;
        var pwshDir = Path.Combine(root, "PowerShell");
        if (!Directory.Exists(pwshDir)) yield break;
        IEnumerable<string> subs;
        try { subs = Directory.EnumerateDirectories(pwshDir); }
        catch { yield break; }
        foreach (var sub in subs)
        {
            var exe = Path.Combine(sub, "pwsh.exe");
            if (File.Exists(exe)) yield return exe;
        }
    }

    private static string? TryDotnetToolsPwsh(string? userProfile)
    {
        if (string.IsNullOrWhiteSpace(userProfile)) return null;
        var p = Path.Combine(userProfile, ".dotnet", "tools", "pwsh.exe");
        return File.Exists(p) ? p : null;
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

        // Filtrar TODO lo que pase por una carpeta WindowsApps:
        //   - C:\Program Files\WindowsApps\... (instalación MSIX real, ACLs
        //     restrictivos: CreateProcess "tiene éxito" pero el proceso no
        //     accede al stdout del ConPty → terminal silenciosa, pantalla negra).
        //   - %LocalAppData%\Microsoft\WindowsApps\pwsh.exe (App Execution
        //     Alias que delega al binario MSIX, hereda el mismo problema).
        // El detector debe emitir solo paths "clásicos" como
        //   C:\Program Files\PowerShell\7\pwsh.exe.
        var filtered = paths
            .Where(p => p.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) < 0)
            .ToList();
        if (filtered.Count == 0) return Array.Empty<UserProfile>();
        paths = filtered;

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
