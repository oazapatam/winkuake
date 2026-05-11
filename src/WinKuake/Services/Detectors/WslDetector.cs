using System;
using System.Collections.Generic;
using System.Linq;
using WinKuake.Models;

namespace WinKuake.Services.Detectors;

/// <summary>
/// Distros WSL detectadas vía <see cref="WslService"/>. Reutiliza el parser
/// y el constructor de commandline ya existentes (con <c>--shell-type login</c>
/// y <c>--cd ~</c> para arrancar en home con perfil del usuario cargado).
/// </summary>
public sealed class WslDetector : IProfileDetector
{
    public IReadOnlyList<UserProfile> Detect()
    {
        var distros = WslService.ListDistributions();
        return BuildProfiles(distros);
    }

    /// <summary>
    /// Variante testeable: dada una lista de distros (típicamente devueltas
    /// por <see cref="WslService.ParseListVerbose(string)"/>), construye un
    /// perfil por cada una. WslService ya filtra docker-desktop/rancher-desktop.
    /// </summary>
    public static IReadOnlyList<UserProfile> BuildProfiles(IReadOnlyList<WslDistribution> distros)
    {
        if (distros is null || distros.Count == 0) return Array.Empty<UserProfile>();

        return distros
            .Where(d => !string.IsNullOrWhiteSpace(d.Name))
            .Select(d =>
            {
                var cmd = WslService.BuildCommandLine(d.Name, loginShell: true, startAtHome: true);
                return new UserProfile
                {
                    Id = DetectorHelpers.StableGuidFromString(cmd),
                    Name = d.Name,
                    CommandLine = cmd,
                    IconGlyph = "🐧",
                    Source = "Detected",
                };
            })
            .ToList();
    }
}
