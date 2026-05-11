using System;
using System.Collections.Generic;
using System.Linq;
using WinKuake.Models;

namespace WinKuake.Services;

/// <summary>
/// Lógica pura para la tab "Perfiles" de SettingsWindow. La separamos del
/// code-behind WPF para poder testearla sin abrir ventanas: la UI conecta
/// botones a estos métodos y el resto fluye solo.
/// </summary>
public static class ProfileEditor
{
    /// <summary>
    /// Mergea los perfiles detectados (Agente A) en la lista existente. Si un
    /// detectado tiene el mismo Id que uno ya presente, NO se duplica ni se
    /// pisa — la idea es respetar lo que el usuario haya tocado a mano.
    /// Devuelve cuántos NUEVOS se agregaron (0 = no hay nada nuevo).
    /// </summary>
    public static int MergeDetected(List<UserProfile> existing, IReadOnlyList<UserProfile> detected)
    {
        var knownIds = new HashSet<string>(
            existing.Select(p => p.Id ?? "").Where(id => !string.IsNullOrEmpty(id)),
            StringComparer.OrdinalIgnoreCase);

        var added = 0;
        foreach (var d in detected)
        {
            if (string.IsNullOrEmpty(d.Id)) continue;
            if (knownIds.Contains(d.Id)) continue;
            existing.Add(d.DeepClone());
            knownIds.Add(d.Id);
            added++;
        }
        return added;
    }

    /// <summary>
    /// Devuelve un perfil nuevo en blanco con Id Guid + Source="Custom".
    /// Para usar como fila vacía editable cuando el usuario hace "Añadir manual".
    /// </summary>
    public static UserProfile CreateBlankCustom() => new()
    {
        Id          = Guid.NewGuid().ToString(),
        Name        = "",
        CommandLine = "",
        Source      = "Custom",
    };

    /// <summary>
    /// Marca <paramref name="id"/> como default. Si el id no existe en la
    /// lista, no toca <paramref name="defaultId"/> (no-op silencioso para que
    /// la UI no tenga que prevalidar).
    /// </summary>
    public static void MakeDefault(List<UserProfile> profiles, ref string? defaultId, string id)
    {
        if (string.IsNullOrEmpty(id)) return;
        if (profiles.Any(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)))
            defaultId = id;
    }

    /// <summary>Borra todos los perfiles y limpia el default. La próxima
    /// detección los recreará desde cero.</summary>
    public static void Reset(List<UserProfile> profiles, ref string? defaultId)
    {
        profiles.Clear();
        defaultId = null;
    }
}
