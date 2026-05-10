using System;
using System.Collections.Generic;
using System.Linq;

namespace WinKuake.Services;

/// <summary>
/// Sesión de terminal: representa un tab/PTY individual. La lógica de
/// renderizado (WebView2 + ConPty) la maneja MainWindow; este modelo
/// solo trackea identidad, perfil y label.
/// </summary>
public sealed class TerminalSession
{
    public int Id { get; }
    public TerminalProfile? Profile { get; internal set; }
    public string? CustomLabel { get; internal set; }

    public string Label => !string.IsNullOrEmpty(CustomLabel)
        ? CustomLabel!
        : (Profile?.DisplayName ?? "Shell");

    internal TerminalSession(int id, TerminalProfile? profile)
    {
        Id = id;
        Profile = profile;
    }
}

/// <summary>
/// Gestor de sesiones de terminal: añade, cierra, cambia activa.
/// Estado puro — sin dependencias de UI/WebView2/ConPty, lo que lo
/// hace unitariamente testeable.
/// </summary>
public sealed class TerminalSessionsManager
{
    private readonly List<TerminalSession> _sessions = new();
    private int _nextId = 1;
    private TerminalSession? _active;

    public IReadOnlyList<TerminalSession> Sessions => _sessions;
    public TerminalSession? Active => _active;

    public event Action<TerminalSession>? SessionAdded;
    public event Action<TerminalSession>? SessionClosed;
    public event Action<TerminalSession?>? ActiveChanged;

    public TerminalSession Create(TerminalProfile? profile)
    {
        var s = new TerminalSession(_nextId++, profile);
        _sessions.Add(s);
        SessionAdded?.Invoke(s);
        SetActiveInternal(s);
        return s;
    }

    public bool Close(int id)
    {
        var idx = _sessions.FindIndex(s => s.Id == id);
        if (idx < 0) return false;
        var s = _sessions[idx];
        _sessions.RemoveAt(idx);
        SessionClosed?.Invoke(s);

        if (_active?.Id == id)
        {
            // El más reciente que quede; si no queda, null.
            var next = _sessions.LastOrDefault();
            SetActiveInternal(next);
        }
        return true;
    }

    public bool SetActive(int id)
    {
        if (_active?.Id == id) return false;
        var s = _sessions.FirstOrDefault(x => x.Id == id);
        if (s is null) return false;
        SetActiveInternal(s);
        return true;
    }

    public TerminalSession Rename(int id, string label)
    {
        var s = _sessions.FirstOrDefault(x => x.Id == id)
            ?? throw new ArgumentException($"No existe la sesión {id}", nameof(id));
        s.CustomLabel = label;
        return s;
    }

    private void SetActiveInternal(TerminalSession? s)
    {
        _active = s;
        ActiveChanged?.Invoke(s);
    }
}
