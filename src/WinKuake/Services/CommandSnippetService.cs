using System;
using System.Collections.Generic;
using System.Linq;

namespace WinKuake.Services;

/// <summary>Comando rápido (snippet) para inyectar en el terminal.</summary>
public sealed record CommandSnippet(string Name, string Command)
{
    public override string ToString() => Name;
}

/// <summary>Contexto de expansión de placeholders en snippets.</summary>
public sealed class SnippetContext
{
    public string? Cwd { get; init; }
    public string? Home { get; init; }
    public string? User { get; init; }
    public System.DateTime? Date { get; init; }
}

/// <summary>
/// Provee la lista de snippets y filtrado por substring. La lista es
/// estática por ahora; en una iteración futura se persistirá en settings.
/// </summary>
public static class CommandSnippetService
{
    public static IReadOnlyList<CommandSnippet> Defaults() => new[]
    {
        new CommandSnippet("git status",                 "git status"),
        new CommandSnippet("git log oneline",            "git log --oneline --graph --decorate -30"),
        new CommandSnippet("git diff staged",            "git diff --staged"),
        new CommandSnippet("git pull --rebase",          "git pull --rebase"),
        new CommandSnippet("git checkout main",          "git checkout main"),
        new CommandSnippet("git stash",                  "git stash"),
        new CommandSnippet("ls -la",                     "ls -la"),
        new CommandSnippet("du -sh *",                   "du -sh * | sort -h"),
        new CommandSnippet("docker ps",                  "docker ps"),
        new CommandSnippet("docker logs (last 100)",     "docker logs --tail 100 -f "),
        new CommandSnippet("npm install",                "npm install"),
        new CommandSnippet("npm test",                   "npm test"),
        new CommandSnippet("dotnet build",               "dotnet build"),
        new CommandSnippet("dotnet test",                "dotnet test"),
        new CommandSnippet("dotnet run",                 "dotnet run"),
        new CommandSnippet("clear",                      "clear"),
        new CommandSnippet("cls (Windows)",              "cls"),
        new CommandSnippet("pwd",                        "pwd"),
        new CommandSnippet("ssh (template)",             "ssh user@host"),
        new CommandSnippet("Python REPL",                "python"),
        new CommandSnippet("Node REPL",                  "node"),
    };

    /// <summary>
    /// Filtra por substring case-insensitive sobre nombre y comando.
    /// Si la query tiene varios tokens (separados por espacios), TODOS deben
    /// matchear (estilo VSCode command palette).
    /// </summary>
    /// <summary>Concatena defaults con los snippets del usuario; user al final.</summary>
    public static IReadOnlyList<CommandSnippet> LoadAll(IEnumerable<CommandSnippet>? userSnippets)
    {
        if (userSnippets is null) return Defaults();
        return Defaults().Concat(userSnippets).ToList();
    }

    public static IReadOnlyList<CommandSnippet> Filter(IEnumerable<CommandSnippet> snippets, string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return snippets.ToList();
        var tokens = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return snippets
            .Where(s => tokens.All(t =>
                s.Name.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                s.Command.Contains(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Reemplaza <c>{cwd}</c>, <c>{home}</c>, <c>{user}</c> por los valores del
    /// contexto. Case-insensitive. Si una variable no existe, se deja literal.
    /// </summary>
    public static string Expand(string command, SnippetContext? ctx)
    {
        if (string.IsNullOrEmpty(command) || ctx is null) return command;
        return System.Text.RegularExpressions.Regex.Replace(
            command,
            @"\{(?<name>[a-zA-Z_]+)\}",
            m =>
            {
                var name = m.Groups["name"].Value.ToLowerInvariant();
                return name switch
                {
                    "cwd"  => ctx.Cwd  ?? m.Value,
                    "home" => ctx.Home ?? m.Value,
                    "user" => ctx.User ?? m.Value,
                    "date" => ctx.Date?.ToString("yyyy-MM-dd") ?? m.Value,
                    _      => m.Value
                };
            });
    }
}
