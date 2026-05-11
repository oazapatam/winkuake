using System.Collections.Generic;
using System.Linq;
using WinKuake.Models;

namespace WinKuake.Services;

/// <summary>
/// Punto único de acceso a los perfiles que la app conoce. Combina los
/// detectores nativos (Fase 20.A) con la lista persistida en
/// <see cref="AppSettings.UserProfiles"/>. La app NO lee perfiles del
/// settings.json de Windows Terminal — esta clase es el único origen.
///
/// STUB de Fase 20.0: por ahora devuelve la lista persistida tal cual
/// (filtrando ocultos). El Agente A reemplaza esta implementación con la
/// detección real + persistencia primera vez.
/// </summary>
public static class ProfileRegistry
{
    /// <summary>
    /// Devuelve los perfiles activos (no ocultos) que el usuario ve en la UI.
    /// Si <paramref name="settings"/>.<see cref="AppSettings.UserProfiles"/> está
    /// vacío, el Agente A ejecutará los detectores y persistirá los resultados;
    /// por ahora devolvemos la lista tal cual (vacía si nunca se inicializó).
    /// </summary>
    public static IReadOnlyList<UserProfile> LoadAll(AppSettings settings)
    {
        return settings.UserProfiles.Where(p => !p.Hidden).ToList();
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
