using System.Collections.Generic;
using WinKuake.Models;

namespace WinKuake.Services;

/// <summary>Acción configurable en la UI de atajos.</summary>
public sealed record KeybindingAction(string Id, string DisplayName, string DefaultGesture);

/// <summary>
/// Catálogo de acciones que se pueden re-bindear. La persistencia vive en
/// <see cref="AppSettings.CustomKeybindings"/>. El runtime aún no consume
/// estos overrides — eso queda como trabajo futuro.
/// </summary>
public static class KeybindingService
{
    public static IReadOnlyList<KeybindingAction> All { get; } = new[]
    {
        new KeybindingAction("Hotkey",          "Hotkey global de drop-down",   "F12"),
        new KeybindingAction("NewTab",          "Nueva pestaña",                "Ctrl+Shift+T"),
        new KeybindingAction("ClosePane",       "Cerrar pane",                  "Ctrl+Shift+R"),
        new KeybindingAction("SplitVertical",   "Dividir vertical",             "Ctrl+("),
        new KeybindingAction("SplitHorizontal", "Dividir horizontal",           "Ctrl+)"),
        new KeybindingAction("Palette",         "Paleta de comandos",           "Ctrl+Shift+P"),
        new KeybindingAction("Broadcast",       "Toggle broadcast input",       "Ctrl+Shift+B"),
        new KeybindingAction("GlobalFind",      "Buscar en todos los buffers",  "Ctrl+Shift+Alt+F"),
        new KeybindingAction("SaveBuffer",      "Guardar buffer",               "Ctrl+Shift+S"),
    };

    public static string GetGesture(AppSettings s, string actionId)
    {
        if (s.CustomKeybindings.TryGetValue(actionId, out var custom)
            && !string.IsNullOrWhiteSpace(custom))
            return custom;
        foreach (var a in All)
            if (a.Id == actionId) return a.DefaultGesture;
        return string.Empty;
    }
}
