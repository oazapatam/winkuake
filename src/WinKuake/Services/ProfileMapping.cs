using System.Collections.Generic;
using System.Linq;
using WinKuake.Models;

namespace WinKuake.Services;

/// <summary>
/// Helper puro y testeable para mapear la lista persistida de
/// <see cref="UserProfile"/> al array de <see cref="TerminalProfile"/> que
/// consume la UI (dropdown, restauración de tabs, default profile).
///
/// Existe como capa fina entre <see cref="ProfileRegistry"/> y la MainWindow
/// para que la lógica se pueda testear sin instanciar WPF.
/// </summary>
public static class ProfileMapping
{
    /// <summary>
    /// Convierte los <see cref="UserProfile"/> visibles a
    /// <see cref="TerminalProfile"/>, marcando uno como default según
    /// <paramref name="defaultId"/> (heurística si es null).
    /// </summary>
    public static TerminalProfile[] BuildTerminalProfiles(
        IReadOnlyList<UserProfile> userProfiles,
        string? defaultId)
    {
        if (userProfiles.Count == 0) return System.Array.Empty<TerminalProfile>();
        var def = ProfileRegistry.ResolveDefault(userProfiles, defaultId);
        return userProfiles
            .Select(p => ProfileRegistry.ToTerminalProfile(p, isDefault: def is not null && def.Id == p.Id))
            .ToArray();
    }

    /// <summary>
    /// Resuelve el perfil con el que arrancar una <see cref="PersistedTab"/>:
    /// primero por GUID, luego por nombre, fallback al default. Devuelve
    /// null si la lista está vacía (caller debe usar un fallback duro).
    /// </summary>
    public static TerminalProfile? ResolvePersisted(
        IReadOnlyList<TerminalProfile> profiles,
        string? guid,
        string? name)
    {
        if (profiles.Count == 0) return null;
        TerminalProfile? p = null;
        if (!string.IsNullOrEmpty(guid))
            p = profiles.FirstOrDefault(x => string.Equals(x.Guid, guid, System.StringComparison.OrdinalIgnoreCase));
        if (p is null && !string.IsNullOrEmpty(name))
            p = profiles.FirstOrDefault(x => string.Equals(x.DisplayName, name, System.StringComparison.OrdinalIgnoreCase));
        return p ?? profiles.FirstOrDefault(x => x.IsDefault) ?? profiles[0];
    }

    /// <summary>
    /// Fallback último recurso cuando no hay ningún perfil detectado ni
    /// persistido. Devuelve un PowerShell genérico para que la app no
    /// crashee en máquinas sin nada.
    /// </summary>
    public static TerminalProfile HardFallback() =>
        new("PowerShell", WtArgs: "") { CommandLine = "powershell.exe" };
}
