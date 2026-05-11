using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WinKuake.Services;

public record UpdateInfo(string Version, string DownloadUrl, string? ReleaseNotes);

/// <summary>
/// Consulta la GitHub Releases API para detectar versiones nuevas. El owner/repo
/// es placeholder hasta que el usuario publique el repo definitivo.
/// </summary>
public static class UpdateService
{
    private const string ReleasesUrl =
        "https://api.github.com/repos/anthropics/winkuake/releases/latest";

    // GitHub API rechaza requests sin User-Agent identificable.
    private const string UserAgent = "WinKuake";

    public static async Task<UpdateInfo?> CheckLatestAsync(string currentVersion, CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");

            var json = await http.GetStringAsync(ReleasesUrl, ct).ConfigureAwait(false);
            var info = ParseRelease(json);
            if (info is null) return null;
            if (!IsNewer(info.Version, currentVersion)) return null;
            return info;
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
            return null;
        }
    }

    /// <summary>
    /// Compara dos strings de versión tolerando el prefijo "v". Devuelve true
    /// solo si <paramref name="remote"/> es estrictamente mayor que
    /// <paramref name="local"/>. Cualquier tag malformado se trata como "no es nuevo".
    /// </summary>
    public static bool IsNewer(string remote, string local)
    {
        if (!TryParseVersion(remote, out var r)) return false;
        if (!TryParseVersion(local,  out var l)) return false;
        return r > l;
    }

    /// <summary>
    /// Extrae version + url del primer asset .exe del JSON de la Releases API.
    /// Devuelve null si el JSON es inválido, falta tag_name, o no hay asset .exe.
    /// </summary>
    public static UpdateInfo? ParseRelease(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("tag_name", out var tagEl) || tagEl.ValueKind != JsonValueKind.String)
                return null;
            var tag = tagEl.GetString();
            if (string.IsNullOrWhiteSpace(tag)) return null;

            string? notes = null;
            if (root.TryGetProperty("body", out var bodyEl) && bodyEl.ValueKind == JsonValueKind.String)
                notes = bodyEl.GetString();

            if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array)
                return null;

            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.TryGetProperty("name", out var nEl) && nEl.ValueKind == JsonValueKind.String
                    ? nEl.GetString() : null;
                var url = asset.TryGetProperty("browser_download_url", out var uEl) && uEl.ValueKind == JsonValueKind.String
                    ? uEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(url)) continue;
                if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;

                return new UpdateInfo(tag, url, notes);
            }
            return null;
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
            return null;
        }
    }

    private static bool TryParseVersion(string? raw, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var trimmed = raw.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[1..];
        return Version.TryParse(trimmed, out version!);
    }
}
