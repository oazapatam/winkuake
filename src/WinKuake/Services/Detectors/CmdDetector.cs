using System;
using System.Collections.Generic;
using System.IO;
using WinKuake.Models;

namespace WinKuake.Services.Detectors;

/// <summary>
/// Símbolo del sistema (cmd.exe). Siempre presente en %SystemRoot%\System32.
/// </summary>
public sealed class CmdDetector : IProfileDetector
{
    public IReadOnlyList<UserProfile> Detect()
    {
        var sysRoot = Environment.GetEnvironmentVariable("SystemRoot")
                      ?? Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (string.IsNullOrEmpty(sysRoot)) return Array.Empty<UserProfile>();

        var path = Path.Combine(sysRoot, "System32", "cmd.exe");
        return DetectAt(path);
    }

    public static IReadOnlyList<UserProfile> DetectAt(string cmdPath)
    {
        if (!File.Exists(cmdPath)) return Array.Empty<UserProfile>();
        var quoted = "\"" + cmdPath + "\"";
        return new[]
        {
            new UserProfile
            {
                Id = DetectorHelpers.StableGuidFromString(quoted),
                Name = "Símbolo del sistema",
                CommandLine = quoted,
                IconGlyph = "≫",
                Source = "Detected",
            }
        };
    }
}
