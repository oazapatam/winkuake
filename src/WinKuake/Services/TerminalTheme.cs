using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WinKuake.Models;

namespace WinKuake.Services;

/// <summary>
/// Paleta de colores ANSI para xterm.js. Cubre los 16 colores base
/// (8 normales + 8 brillantes) más background/foreground/cursor.
/// Los valores se serializan tal cual a las opciones <c>theme</c> de xterm.
/// </summary>
public sealed record TerminalTheme(
    string Name,
    string Background,
    string Foreground,
    string Cursor,
    string Black,       string Red,         string Green,       string Yellow,
    string Blue,        string Magenta,     string Cyan,        string White,
    string BrightBlack, string BrightRed,   string BrightGreen, string BrightYellow,
    string BrightBlue,  string BrightMagenta, string BrightCyan, string BrightWhite)
{
    /// <summary>Serializa como objeto JSON con las keys que xterm.js espera.</summary>
    public string ToXtermJson()
    {
        var dict = new Dictionary<string, string>
        {
            ["background"] = Background, ["foreground"] = Foreground, ["cursor"] = Cursor,
            ["black"]   = Black,   ["red"]     = Red,     ["green"]   = Green,  ["yellow"] = Yellow,
            ["blue"]    = Blue,    ["magenta"] = Magenta, ["cyan"]    = Cyan,   ["white"]  = White,
            ["brightBlack"]   = BrightBlack,   ["brightRed"]     = BrightRed,
            ["brightGreen"]   = BrightGreen,   ["brightYellow"]  = BrightYellow,
            ["brightBlue"]    = BrightBlue,    ["brightMagenta"] = BrightMagenta,
            ["brightCyan"]    = BrightCyan,    ["brightWhite"]   = BrightWhite,
        };
        return JsonSerializer.Serialize(dict);
    }

    public static TerminalTheme Default => All[0];

    public static TerminalTheme? Find(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return All.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    public static TerminalTheme FindOrDefault(string? name) => Find(name) ?? Default;

    /// <summary>Construye un <see cref="TerminalTheme"/> a partir de un POCO custom.</summary>
    public static TerminalTheme FromCustom(TerminalThemeColors c)
    {
        var name = string.IsNullOrWhiteSpace(c.Name) ? "Custom" : c.Name;
        return new TerminalTheme(
            name,
            Background: c.Background, Foreground: c.Foreground, Cursor: c.Cursor,
            Black: c.Black, Red: c.Red, Green: c.Green, Yellow: c.Yellow,
            Blue: c.Blue, Magenta: c.Magenta, Cyan: c.Cyan, White: c.White,
            BrightBlack: c.BrightBlack, BrightRed: c.BrightRed,
            BrightGreen: c.BrightGreen, BrightYellow: c.BrightYellow,
            BrightBlue: c.BrightBlue, BrightMagenta: c.BrightMagenta,
            BrightCyan: c.BrightCyan, BrightWhite: c.BrightWhite);
    }

    /// <summary>
    /// Resuelve el tema activo según settings: si el nombre es "Custom" y hay
    /// paleta custom guardada, la usa; en cualquier otro caso cae a
    /// <see cref="FindOrDefault"/>.
    /// </summary>
    public static TerminalTheme ResolveCurrent(AppSettings s) =>
        string.Equals(s.TerminalThemeName, "Custom", StringComparison.OrdinalIgnoreCase)
            && s.CustomTerminalTheme is not null
            ? FromCustom(s.CustomTerminalTheme)
            : FindOrDefault(s.TerminalThemeName);

    public static IReadOnlyList<TerminalTheme> All { get; } = new[]
    {
        // VSCode Dark+ — el que ya usábamos como default.
        new TerminalTheme("VSCode Dark+",
            Background: "#0c0c0c", Foreground: "#cccccc", Cursor: "#ffffff",
            Black: "#0c0c0c", Red: "#c50f1f", Green: "#13a10e", Yellow: "#c19c00",
            Blue: "#0037da", Magenta: "#881798", Cyan: "#3a96dd", White: "#cccccc",
            BrightBlack: "#767676",   BrightRed: "#e74856",     BrightGreen: "#16c60c",  BrightYellow: "#f9f1a5",
            BrightBlue: "#3b78ff",    BrightMagenta: "#b4009e", BrightCyan: "#61d6d6",   BrightWhite: "#f2f2f2"),

        // Dracula — popular tema oscuro.
        new TerminalTheme("Dracula",
            Background: "#282a36", Foreground: "#f8f8f2", Cursor: "#f8f8f2",
            Black: "#21222c", Red: "#ff5555", Green: "#50fa7b", Yellow: "#f1fa8c",
            Blue: "#bd93f9",  Magenta: "#ff79c6", Cyan: "#8be9fd", White: "#f8f8f2",
            BrightBlack: "#6272a4",   BrightRed: "#ff6e6e",     BrightGreen: "#69ff94",  BrightYellow: "#ffffa5",
            BrightBlue: "#d6acff",    BrightMagenta: "#ff92df", BrightCyan: "#a4ffff",   BrightWhite: "#ffffff"),

        // Nord — paleta fría minimalista.
        new TerminalTheme("Nord",
            Background: "#2e3440", Foreground: "#d8dee9", Cursor: "#d8dee9",
            Black: "#3b4252", Red: "#bf616a", Green: "#a3be8c", Yellow: "#ebcb8b",
            Blue: "#81a1c1",  Magenta: "#b48ead", Cyan: "#88c0d0", White: "#e5e9f0",
            BrightBlack: "#4c566a",   BrightRed: "#bf616a",     BrightGreen: "#a3be8c",  BrightYellow: "#ebcb8b",
            BrightBlue: "#81a1c1",    BrightMagenta: "#b48ead", BrightCyan: "#8fbcbb",   BrightWhite: "#eceff4"),

        // Gruvbox Dark — paleta cálida retro.
        new TerminalTheme("Gruvbox Dark",
            Background: "#282828", Foreground: "#ebdbb2", Cursor: "#ebdbb2",
            Black: "#282828", Red: "#cc241d", Green: "#98971a", Yellow: "#d79921",
            Blue: "#458588",  Magenta: "#b16286", Cyan: "#689d6a", White: "#a89984",
            BrightBlack: "#928374",   BrightRed: "#fb4934",     BrightGreen: "#b8bb26",  BrightYellow: "#fabd2f",
            BrightBlue: "#83a598",    BrightMagenta: "#d3869b", BrightCyan: "#8ec07c",   BrightWhite: "#ebdbb2"),

        // Monokai — clásico de TextMate / Sublime.
        new TerminalTheme("Monokai",
            Background: "#272822", Foreground: "#f8f8f2", Cursor: "#f8f8f0",
            Black: "#272822", Red: "#f92672", Green: "#a6e22e", Yellow: "#f4bf75",
            Blue: "#66d9ef",  Magenta: "#ae81ff", Cyan: "#a1efe4", White: "#f8f8f2",
            BrightBlack: "#75715e",   BrightRed: "#f92672",     BrightGreen: "#a6e22e",  BrightYellow: "#f4bf75",
            BrightBlue: "#66d9ef",    BrightMagenta: "#ae81ff", BrightCyan: "#a1efe4",   BrightWhite: "#f9f8f5"),
    };
}
