using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Win32;
using WinKuake.Models;

namespace WinKuake.Services.Detectors;

/// <summary>
/// Git Bash (bash.exe que viene con Git for Windows). Búsqueda en este orden:
/// %ProgramFiles%\Git\bin, %ProgramFiles(x86)%\Git\bin, %LOCALAPPDATA%\Programs\Git\bin,
/// luego registry HKLM\SOFTWARE\GitForWindows\InstallPath, y como último
/// recurso <c>where.exe bash</c> (filtrando WSL).
/// </summary>
public sealed class GitBashDetector : IProfileDetector
{
    public IReadOnlyList<UserProfile> Detect()
    {
        var candidates = CollectCandidates(
            programFiles: Environment.GetEnvironmentVariable("ProgramFiles"),
            programFilesX86: Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            localAppData: Environment.GetEnvironmentVariable("LOCALAPPDATA"),
            registryInstallPath: ReadRegistryInstallPath(),
            whereOutput: DetectorHelpers.RunCapture("where.exe", "bash"));

        return BuildProfilesFromPaths(candidates);
    }

    /// <summary>
    /// Variante testeable: combina las 4 fuentes (env-vars, registry, where)
    /// en una lista de candidatos, dedupea, filtra paths que contengan
    /// "WindowsApps" o "System32" (lugares donde aparece bash.exe del WSL
    /// front-end). No verifica existencia (lo hace el caller con File.Exists).
    /// </summary>
    public static IReadOnlyList<string> CollectCandidates(
        string? programFiles,
        string? programFilesX86,
        string? localAppData,
        string? registryInstallPath,
        string whereOutput)
    {
        var result = new List<string>();

        void TryAdd(string? dir, string subPath)
        {
            if (string.IsNullOrWhiteSpace(dir)) return;
            result.Add(Path.Combine(dir, subPath));
        }

        TryAdd(programFiles,    @"Git\bin\bash.exe");
        TryAdd(programFilesX86, @"Git\bin\bash.exe");
        TryAdd(localAppData,    @"Programs\Git\bin\bash.exe");
        TryAdd(registryInstallPath, @"bin\bash.exe");

        if (!string.IsNullOrWhiteSpace(whereOutput))
        {
            foreach (var line in whereOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var p = line.Trim();
                if (p.Length == 0) continue;
                // Filtrar bash.exe del front de WSL (System32) y de la Store
                // (WindowsApps). Esos NO son Git Bash.
                if (p.IndexOf(@"\System32\",  StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (p.IndexOf(@"\WindowsApps\", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                result.Add(p);
            }
        }

        return result
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<UserProfile> BuildProfilesFromPaths(IReadOnlyList<string> candidates)
    {
        if (candidates is null || candidates.Count == 0) return Array.Empty<UserProfile>();

        var found = candidates.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return BuildProfilesAssumingExisting(found);
    }

    /// <summary>
    /// Construye perfiles desde paths que el caller ya sabe que existen.
    /// Útil para tests que no quieren tocar el filesystem.
    /// </summary>
    public static IReadOnlyList<UserProfile> BuildProfilesAssumingExisting(IReadOnlyList<string> existingPaths)
    {
        if (existingPaths is null || existingPaths.Count == 0) return Array.Empty<UserProfile>();

        // Argumentos: -l (login shell, carga ~/.profile) -i (interactive).
        return existingPaths
            .Select(path =>
            {
                var cmd = "\"" + path + "\" -l -i";
                return new UserProfile
                {
                    Id = DetectorHelpers.StableGuidFromString(cmd),
                    Name = "Git Bash",
                    CommandLine = cmd,
                    IconGlyph = "🐚",
                    Source = "Detected",
                };
            })
            .ToList();
    }

    private static string? ReadRegistryInstallPath()
    {
        try
        {
            using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                .OpenSubKey(@"SOFTWARE\GitForWindows");
            return key?.GetValue("InstallPath") as string;
        }
        catch
        {
            return null;
        }
    }
}
