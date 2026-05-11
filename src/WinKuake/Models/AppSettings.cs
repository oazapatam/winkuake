using System.Collections.Generic;

namespace WinKuake.Models;

/// <summary>POCO persistido a JSON en %AppData%\WinKuake\settings.json.</summary>
public class AppSettings
{
    /// <summary>Modificadores del hotkey (Ctrl/Alt/Shift/Win) combinables.</summary>
    public List<string> HotkeyModifiers { get; set; } = new();

    /// <summary>Tecla del hotkey (nombre VK_*, p.ej. "F12").</summary>
    public string HotkeyKey { get; set; } = "F12";

    /// <summary>Porcentaje de la pantalla que ocupa la ventana en alto (0.1 a 1.0).</summary>
    public double HeightRatio { get; set; } = 0.5;

    /// <summary>Porcentaje de la pantalla que ocupa la ventana en ancho (0.1 a 1.0).</summary>
    public double WidthRatio { get; set; } = 1.0;

    /// <summary>Opacidad de la ventana 0.5 a 1.0.</summary>
    public double Opacity { get; set; } = 0.97;

    /// <summary>Perfil de Windows Terminal por defecto. "" = perfil predeterminado del usuario.</summary>
    public string DefaultProfile { get; set; } = "";

    /// <summary>Auto-ocultar al perder foco.</summary>
    public bool AutoHideOnFocusLost { get; set; } = false;

    /// <summary>Iniciar con Windows.</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>Duración de la animación slide en ms.</summary>
    public int AnimationMs { get; set; } = 180;

    /// <summary>
    /// Líneas de scrollback del terminal. <c>-1</c> = ilimitado (default).
    /// Cualquier valor &gt; 0 se respeta tal cual.
    /// </summary>
    public int ScrollbackLines { get; set; } = -1;

    /// <summary>Nombre del tema xterm (VSCode Dark+, Dracula, Nord, Gruvbox Dark, Monokai).</summary>
    public string TerminalThemeName { get; set; } = "VSCode Dark+";

    /// <summary>Tamaño de fuente del terminal en pt.</summary>
    public int TerminalFontSize { get; set; } = 14;

    /// <summary>Snippets definidos por el usuario, persistidos como pares Name/Command.</summary>
    public List<UserSnippet> UserSnippets { get; set; } = new();

    /// <summary>Tabs abiertas en la última sesión, restauradas al reabrir la app.</summary>
    public List<PersistedTab> LastSessionTabs { get; set; } = new();

    /// <summary>Workspaces guardados por el usuario (combos de tabs con nombre).</summary>
    public List<Workspace> Workspaces { get; set; } = new();

    /// <summary>Índice de monitor (0 = primario). -1 = monitor activo.</summary>
    public int MonitorIndex { get; set; } = 0;

    // -- Apariencia / skin (Fase 4) ---------------------------------------
    // Colores en formato "#RRGGBB". El motor de skin es trivial: estos
    // colores se aplican como recursos dinámicos al iniciar y al guardar.

    /// <summary>Color base del chrome (barra inferior, status). Default = #1E1E1E.</summary>
    public string ChromeBackgroundHex { get; set; } = "#1E1E1E";

    /// <summary>Color de borde / separadores. Default = #3C3C3C.</summary>
    public string ChromeBorderHex { get; set; } = "#3C3C3C";

    /// <summary>Color del texto del chrome. Default = #E6E6E6.</summary>
    public string ChromeForegroundHex { get; set; } = "#E6E6E6";

    /// <summary>Color de acento (pestaña activa, hover). Default = #0E7AB5.</summary>
    public string AccentHex { get; set; } = "#0E7AB5";

    /// <summary>
    /// Paleta custom del terminal. Solo se aplica cuando
    /// <see cref="TerminalThemeName"/> == "Custom".
    /// </summary>
    public TerminalThemeColors? CustomTerminalTheme { get; set; }

    /// <summary>
    /// Atajos definidos por el usuario, mapeados como acción → gesto
    /// (p.ej. "NewTab" → "Ctrl+Shift+T"). Aún no se aplican en runtime.
    /// </summary>
    public Dictionary<string, string> CustomKeybindings { get; set; } = new();
}

/// <summary>
/// POCO con los 19 colores de una paleta xterm.js custom. Las claves se
/// corresponden 1:1 con <see cref="WinKuake.Services.TerminalTheme"/>.
/// </summary>
public class TerminalThemeColors
{
    public string Name { get; set; } = "Custom";
    public string Background { get; set; } = "#000000";
    public string Foreground { get; set; } = "#000000";
    public string Cursor { get; set; } = "#000000";
    public string Black { get; set; } = "#000000";
    public string Red { get; set; } = "#000000";
    public string Green { get; set; } = "#000000";
    public string Yellow { get; set; } = "#000000";
    public string Blue { get; set; } = "#000000";
    public string Magenta { get; set; } = "#000000";
    public string Cyan { get; set; } = "#000000";
    public string White { get; set; } = "#000000";
    public string BrightBlack { get; set; } = "#000000";
    public string BrightRed { get; set; } = "#000000";
    public string BrightGreen { get; set; } = "#000000";
    public string BrightYellow { get; set; } = "#000000";
    public string BrightBlue { get; set; } = "#000000";
    public string BrightMagenta { get; set; } = "#000000";
    public string BrightCyan { get; set; } = "#000000";
    public string BrightWhite { get; set; } = "#000000";
}

/// <summary>POCO serializable para snippets del usuario.</summary>
public class UserSnippet
{
    public string Name { get; set; } = "";
    public string Command { get; set; } = "";
}

/// <summary>Tab persistida entre sesiones.</summary>
public class PersistedTab
{
    /// <summary>GUID del perfil en wt settings.json (preferido para matching).</summary>
    public string? ProfileGuid { get; set; }
    /// <summary>Nombre del perfil como fallback si el GUID no coincide.</summary>
    public string? ProfileName { get; set; }
    /// <summary>CWD del shell al cerrar la sesión (path Windows o vacío).</summary>
    public string? Cwd { get; set; }
    /// <summary>Label custom puesto por el usuario via doble-click en el tab.</summary>
    public string? CustomLabel { get; set; }
    /// <summary>Si la tab estaba fijada.</summary>
    public bool IsPinned { get; set; }
    /// <summary>Árbol de splits dentro de la tab. Null si la tab no tiene splits.</summary>
    public PersistedSplitNode? Layout { get; set; }
}

/// <summary>
/// Nodo del árbol de splits de una tab. Si <see cref="Orientation"/> es
/// "Vertical" u "Horizontal" → branch (con <see cref="First"/> y
/// <see cref="Second"/>). Si es null → leaf (con datos del pane).
/// </summary>
public class PersistedSplitNode
{
    /// <summary>"Vertical" (columnas) u "Horizontal" (filas). null = leaf.</summary>
    public string? Orientation { get; set; }
    public PersistedSplitNode? First { get; set; }
    public PersistedSplitNode? Second { get; set; }

    // Datos del leaf:
    public string? ProfileGuid { get; set; }
    public string? ProfileName { get; set; }
    public string? Cwd { get; set; }
}

/// <summary>Workspace: lista de tabs nombrada que el usuario puede guardar y cargar.</summary>
public class Workspace
{
    public string Name { get; set; } = "";
    public List<PersistedTab> Tabs { get; set; } = new();
}
