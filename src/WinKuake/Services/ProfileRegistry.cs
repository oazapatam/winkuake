using System.Collections.Generic;
using System.Linq;
using WinKuake.Models;
using WinKuake.Services.Detectors;

namespace WinKuake.Services;

/// <summary>
/// Punto único de acceso a los perfiles que la app conoce. Combina los
/// detectores nativos (Fase 20.A) con la lista persistida en
/// <see cref="AppSettings.UserProfiles"/>. La app NO lee perfiles del
/// settings.json de Windows Terminal — esta clase es el único origen.
/// </summary>
public static class ProfileRegistry
{
    /// <summary>
    /// Conjunto de detectores activos. Lista pública para que tests y futuros
    /// flujos (botón "Detectar terminales") puedan correrlos individualmente.
    /// </summary>
    public static IReadOnlyList<IProfileDetector> AllDetectors { get; } = new IProfileDetector[]
    {
        new WindowsPowerShellDetector(),
        new PwshDetector(),
        new CmdDetector(),
        new WslDetector(),
        new GitBashDetector(),
        new VsDeveloperDetector(),
    };

    /// <summary>
    /// Devuelve los perfiles activos (no ocultos) que el usuario ve en la UI.
    /// Si <paramref name="settings"/>.<see cref="AppSettings.UserProfiles"/> está
    /// vacío, ejecuta los detectores y los persiste en la lista (mutación
    /// in-place). El caller (típicamente <c>MainWindow</c>) es responsable de
    /// invocar <c>SettingsService.Save</c> después del primer <c>LoadAll</c>.
    /// </summary>
    public static IReadOnlyList<UserProfile> LoadAll(AppSettings settings)
    {
        if (settings.UserProfiles.Count == 0)
        {
            var detected = RunAllDetectors();
            settings.UserProfiles.AddRange(detected);
        }
        return settings.UserProfiles.Where(p => !p.Hidden).ToList();
    }

    /// <summary>
    /// Corre todos los detectores en orden y concatena sus resultados.
    /// Cada detector ya descarta lo no resoluble en origen, así que el
    /// resultado es directamente persistible en <see cref="AppSettings.UserProfiles"/>.
    /// </summary>
    public static IReadOnlyList<UserProfile> RunAllDetectors() =>
        RunDetectors(AllDetectors);

    /// <summary>
    /// Variante testeable: corre la lista de detectores que se le pase
    /// (típicamente fakes en unit-tests).
    /// </summary>
    public static IReadOnlyList<UserProfile> RunDetectors(IEnumerable<IProfileDetector> detectors)
    {
        var result = new List<UserProfile>();
        foreach (var d in detectors)
        {
            try
            {
                var profiles = d.Detect();
                if (profiles is { Count: > 0 }) result.AddRange(profiles);
            }
            catch
            {
                // Un detector roto no debe tirar al resto. Best-effort.
            }
        }
        return result;
    }

    /// <summary>
    /// Mapea un <see cref="UserProfile"/> al <see cref="TerminalProfile"/> que
    /// usa el resto del runtime (TerminalControl.StartShell, etc.).
    /// </summary>
    public static TerminalProfile ToTerminalProfile(UserProfile p, bool isDefault = false) =>
        new(p.Name, WtArgs: "")
        {
            Guid = p.Id,
            CommandLine = p.CommandLine,
            StartingDirectory = string.IsNullOrEmpty(p.StartingDirectory) ? null : p.StartingDirectory,
            IsDefault = isDefault,
        };

    /// <summary>
    /// Resuelve el perfil "default" según <see cref="AppSettings.DefaultProfileId"/>;
    /// fallback heurístico: primer pwsh > primer powershell > primer cmd > primero.
    /// Devuelve null si la lista está vacía.
    /// </summary>
    public static UserProfile? ResolveDefault(IReadOnlyList<UserProfile> profiles, string? defaultId)
    {
        if (profiles.Count == 0) return null;
        if (!string.IsNullOrEmpty(defaultId))
        {
            var byId = profiles.FirstOrDefault(p => p.Id == defaultId);
            if (byId is not null) return byId;
        }
        return profiles.FirstOrDefault(p => p.CommandLine.Contains("pwsh", System.StringComparison.OrdinalIgnoreCase))
            ?? profiles.FirstOrDefault(p => p.CommandLine.Contains("powershell", System.StringComparison.OrdinalIgnoreCase))
            ?? profiles.FirstOrDefault(p => p.CommandLine.Contains("cmd", System.StringComparison.OrdinalIgnoreCase))
            ?? profiles[0];
    }
}
