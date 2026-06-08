using System.Collections.Generic;

namespace WinKuake.Services;

/// <summary>
/// Spec de un item del menú contextual del terminal. Pura data: la capa
/// WPF lo materializa a <c>MenuItem</c>. Separadores tienen
/// <see cref="IsSeparator"/>=true y el resto de campos vacíos.
/// </summary>
public sealed record ContextMenuItemSpec(
    string ActionId,
    string Label,
    string Shortcut,
    bool   Enabled,
    bool   IsSeparator = false)
{
    public static ContextMenuItemSpec Sep() => new("", "", "", false, IsSeparator: true);
}

/// <summary>
/// Construye la lista de items del menú contextual del terminal según el
/// estado del pane (selección presente, está dentro de un split). Función
/// pura para que sea trivial de testear; el handler WPF la mapea a
/// <c>MenuItem</c> y conecta clicks a los handlers existentes.
/// </summary>
public static class TerminalContextMenuBuilder
{
    public static IReadOnlyList<ContextMenuItemSpec> Build(bool hasSelection, bool isInSplit)
    {
        // Shortcuts default alineados con Yakuake. Los handlers JS también
        // aceptan los atajos legacy (Ctrl+Shift+D/E para splits, Ctrl+Shift+W
        // para close pane) por compatibilidad — el hint en el menú muestra
        // los oficiales de Yakuake.
        return new List<ContextMenuItemSpec>
        {
            new("copy",            "Copiar selección",   "Ctrl+C", Enabled: hasSelection),
            new("paste",           "Pegar",              "Ctrl+V",       Enabled: true),
            new("find",            "Buscar",             "Ctrl+Shift+F", Enabled: true),
            ContextMenuItemSpec.Sep(),
            new("splitVertical",   "Dividir vertical",   "Ctrl+(",       Enabled: true),
            new("splitHorizontal", "Dividir horizontal", "Ctrl+)",       Enabled: true),
            new("closePane",       "Cerrar pane",        "Ctrl+Shift+R", Enabled: isInSplit),
            ContextMenuItemSpec.Sep(),
            new("openPalette",     "Paleta de comandos", "Ctrl+Shift+P", Enabled: true),
            new("clearBuffer",     "Limpiar buffer",     "Ctrl+L",       Enabled: true),
            ContextMenuItemSpec.Sep(),
            new("devtools",        "Abrir DevTools",     "",             Enabled: true),
        };
    }
}
