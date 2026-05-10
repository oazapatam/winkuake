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
}
