namespace WinKuake.Services;

/// <summary>
/// Devuelve un glifo Unicode/emoji apropiado para mostrar como ícono del perfil
/// en la tab bar y el dropdown. Heurística por nombre porque los íconos reales
/// de wt suelen estar en ms-appx:// (paquete UWP) que no podemos resolver desde
/// una app Win32 sin elevation.
/// </summary>
public static class ProfileIconHelper
{
    public static string GlyphFor(TerminalProfile? profile)
    {
        if (profile is null) return "▶";
        var name = profile.DisplayName.ToLowerInvariant();

        // Linux / WSL
        if (name.Contains("ubuntu") || name.Contains("debian") ||
            name.Contains("fedora") || name.Contains("arch") ||
            name.Contains("kali")  || name.Contains("wsl") ||
            name.Contains("alpine")) return "🐧";

        // PowerShell variants
        if (name.Contains("powershell") || name.Contains("pwsh")) return "⚡";

        // Command Prompt
        if (name.Contains("símbolo") || name.Contains("simbolo") ||
            name.Contains("command prompt") || name.Contains("cmd")) return "≫";

        // Cloud / Azure
        if (name.Contains("azure") || name.Contains("cloud")) return "☁";

        // Git
        if (name.Contains("git")) return "❖";

        // Predeterminado synth
        if (name.Contains("predeterminado") || name.Contains("default")) return "▶";

        return "▶";
    }
}
