using System;
using System.Collections.Generic;

namespace WinKuake.Services;

/// <summary>Fuente de búsqueda: las líneas de un pane junto con su identidad.</summary>
public sealed record GlobalFindSource(
    int SessionId,
    string SessionLabel,
    int PaneIndex,
    IReadOnlyList<string> Lines);

/// <summary>Match individual: ubicación + preview de la línea.</summary>
public sealed record FindResult(
    int SessionId,
    string SessionLabel,
    int PaneIndex,
    int LineNumber,
    string LinePreview);

/// <summary>
/// Lógica pura de búsqueda multi-buffer. Sin dependencias de WPF / WebView2:
/// recibe los buffers ya recolectados y devuelve los matches. Es la pieza
/// testeable de la feature de búsqueda global.
/// </summary>
public static class GlobalFindService
{
    public static IReadOnlyList<FindResult> Search(
        IEnumerable<GlobalFindSource> sources,
        string query)
    {
        var results = new List<FindResult>();
        if (string.IsNullOrWhiteSpace(query)) return results;

        foreach (var src in sources)
        {
            for (int i = 0; i < src.Lines.Count; i++)
            {
                var line = src.Lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                if (line.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0) continue;
                results.Add(new FindResult(
                    src.SessionId,
                    src.SessionLabel,
                    src.PaneIndex,
                    i,
                    line.TrimEnd()));
            }
        }
        return results;
    }
}
