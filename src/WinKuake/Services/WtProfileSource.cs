using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace WinKuake.Services;

/// <summary>
/// Lee los perfiles configurados en el settings.json de Windows Terminal.
/// Busca primero la versión Store estable, luego Preview, luego unpackaged.
/// </summary>
public static class WtProfileSource
{
    private static IEnumerable<string> CandidatePaths()
    {
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        yield return Path.Combine(local, "Packages",
            "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState", "settings.json");
        yield return Path.Combine(local, "Packages",
            "Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe", "LocalState", "settings.json");
        yield return Path.Combine(local, "Microsoft", "Windows Terminal", "settings.json");
    }

    public static IReadOnlyList<TerminalProfile> Load()
    {
        var path = CandidatePaths().FirstOrDefault(File.Exists);
        if (path is null) return Array.Empty<TerminalProfile>();

        try
        {
            var raw = File.ReadAllText(path);
            var json = StripJsonComments(raw);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string? defaultGuid = null;
            if (root.TryGetProperty("defaultProfile", out var dp) && dp.ValueKind == JsonValueKind.String)
                defaultGuid = dp.GetString();

            if (!root.TryGetProperty("profiles", out var profilesNode))
                return Array.Empty<TerminalProfile>();

            JsonElement list;
            if (profilesNode.ValueKind == JsonValueKind.Array)
                list = profilesNode;
            else if (profilesNode.TryGetProperty("list", out var inner) && inner.ValueKind == JsonValueKind.Array)
                list = inner;
            else
                return Array.Empty<TerminalProfile>();

            var result = new List<TerminalProfile>();
            foreach (var p in list.EnumerateArray())
            {
                if (p.TryGetProperty("hidden", out var h) && h.ValueKind == JsonValueKind.True) continue;

                var name = p.TryGetProperty("name", out var nn) && nn.ValueKind == JsonValueKind.String
                    ? nn.GetString() : null;
                if (string.IsNullOrWhiteSpace(name)) continue;

                var guid = p.TryGetProperty("guid", out var gg) && gg.ValueKind == JsonValueKind.String
                    ? gg.GetString() : null;
                var icon = p.TryGetProperty("icon", out var ic) && ic.ValueKind == JsonValueKind.String
                    ? ic.GetString() : null;
                var commandline = p.TryGetProperty("commandline", out var cl) && cl.ValueKind == JsonValueKind.String
                    ? cl.GetString() : null;
                var source = p.TryGetProperty("source", out var sc) && sc.ValueKind == JsonValueKind.String
                    ? sc.GetString() : null;
                var startingDir = p.TryGetProperty("startingDirectory", out var sd) && sd.ValueKind == JsonValueKind.String
                    ? sd.GetString() : null;

                var resolvedCmd = ResolveCommandLine(name, commandline, source);

                result.Add(new TerminalProfile(name, name)
                {
                    Guid = guid,
                    IconPath = ResolveIcon(icon),
                    IsDefault = guid is not null && string.Equals(guid, defaultGuid, StringComparison.OrdinalIgnoreCase),
                    CommandLine = resolvedCmd,
                    StartingDirectory = ExpandPath(startingDir),
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
            return Array.Empty<TerminalProfile>();
        }
    }

    private static string? ResolveIcon(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon)) return null;
        // ms-appx:// son paths internos al package wt; los user-mode apps
        // no pueden listar el directorio del package, así que no se resuelven.
        if (icon.StartsWith("ms-appx://", StringComparison.OrdinalIgnoreCase)) return null;
        // Variables de entorno y paths relativos a perfil del usuario son comunes.
        var expanded = Environment.ExpandEnvironmentVariables(icon);
        if (File.Exists(expanded)) return expanded;
        return null;
    }

    private static string? ExpandPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        return Environment.ExpandEnvironmentVariables(path);
    }

    /// <summary>
    /// Convierte la definición de un perfil de wt a una línea de comandos
    /// ejecutable con CreateProcess. Devuelve null si el perfil no es
    /// soportable (ej. Azure Cloud Shell).
    /// </summary>
    private static string? ResolveCommandLine(string name, string? commandline, string? source)
    {
        // 1. Si el perfil define commandline explícito, úsalo.
        if (!string.IsNullOrWhiteSpace(commandline))
            return Environment.ExpandEnvironmentVariables(commandline);

        // 2. Source-based: WSL distributions.
        if (string.Equals(source, "Windows.Terminal.Wsl", StringComparison.OrdinalIgnoreCase))
            return $"wsl.exe -d \"{name}\"";

        // 3. Source-based: PowerShell Core.
        if (string.Equals(source, "Windows.Terminal.PowershellCore", StringComparison.OrdinalIgnoreCase))
            return "pwsh.exe";

        // 4. Builtin profiles (sin source, con name conocido).
        var n = name.Trim();
        if (n.Equals("Windows PowerShell", StringComparison.OrdinalIgnoreCase))
            return "powershell.exe";
        if (n.Equals("Command Prompt", StringComparison.OrdinalIgnoreCase) ||
            n.Equals("Símbolo del sistema", StringComparison.OrdinalIgnoreCase))
            return "cmd.exe";
        if (n.Equals("PowerShell", StringComparison.OrdinalIgnoreCase))
            return "pwsh.exe";

        // 5. Azure Cloud Shell y otros remotos: no soportables sin auth.
        if (n.Contains("Azure", StringComparison.OrdinalIgnoreCase))
            return null;

        // 6. Fallback: si el nombre parece un ejecutable, intentarlo.
        if (n.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return n;

        return null;
    }

    /// <summary>
    /// Elimina comentarios estilo // y /* */ de un string JSONC. wt usa JSONC
    /// y System.Text.Json no los acepta por defecto en .NET 10 sin opciones.
    /// Implementación simple que respeta strings JSON.
    /// </summary>
    internal static string StripJsonComments(string s)
    {
        var sb = new StringBuilder(s.Length);
        bool inString = false, inLine = false, inBlock = false;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            char n = i + 1 < s.Length ? s[i + 1] : '\0';

            if (inLine)
            {
                if (c == '\n') { inLine = false; sb.Append(c); }
            }
            else if (inBlock)
            {
                if (c == '*' && n == '/') { inBlock = false; i++; }
            }
            else if (inString)
            {
                sb.Append(c);
                if (c == '\\' && i + 1 < s.Length) sb.Append(s[++i]);
                else if (c == '"') inString = false;
            }
            else
            {
                if (c == '"') { inString = true; sb.Append(c); }
                else if (c == '/' && n == '/') { inLine = true; i++; }
                else if (c == '/' && n == '*') { inBlock = true; i++; }
                else sb.Append(c);
            }
        }
        return sb.ToString();
    }
}
