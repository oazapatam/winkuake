using System;
using System.Collections.Generic;
using System.IO;
using WinKuake.Models;

namespace WinKuake.Services.Detectors;

/// <summary>
/// Windows PowerShell 5.1 (incluido en Windows desde 7). Siempre presente
/// salvo en SKUs ultra-recortadas. Path único y conocido.
/// </summary>
public sealed class WindowsPowerShellDetector : IProfileDetector
{
    public IReadOnlyList<UserProfile> Detect()
    {
        var sysRoot = Environment.GetEnvironmentVariable("SystemRoot")
                      ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(sysRoot)) return Array.Empty<UserProfile>();

        var path = Path.Combine(sysRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        return DetectAt(path);
    }

    /// <summary>
    /// Variante testeable: dado un path arbitrario, devuelve el perfil si
    /// existe. Permite verificar el contrato sin depender del SO.
    /// </summary>
    public static IReadOnlyList<UserProfile> DetectAt(string powershellPath)
    {
        if (!File.Exists(powershellPath)) return Array.Empty<UserProfile>();
        var quoted = "\"" + powershellPath + "\"";
        return new[]
        {
            new UserProfile
            {
                Id = DetectorHelpers.StableGuidFromString(quoted),
                Name = "Windows PowerShell",
                CommandLine = quoted,
                IconGlyph = "⚡",
                Source = "Detected",
            }
        };
    }
}
