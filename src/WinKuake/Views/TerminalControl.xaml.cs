using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using WinKuake.Services;

namespace WinKuake.Views;

/// <summary>
/// Vista de una sesión de terminal. Hospeda 1 o 2 <see cref="TerminalPane"/>
/// con un <see cref="GridSplitter"/> entre ellos cuando hay split.
/// Soporta una sola subdivisión por sesión (sin recursión). Para split adicional,
/// abre tab nueva.
/// </summary>
public partial class TerminalControl : UserControl
{
    private readonly List<TerminalPane> _panes = new();
    private TerminalPane? _activePane;
    private string? _lastCommandLine;
    private string? _lastStartingDir;

    /// <summary>Pane principal: el primero que se crea. Nunca es null tras inicializar.</summary>
    private TerminalPane MainPane => _panes[0];

    public string? CurrentCwd => _activePane?.CurrentCwd;

    public event Action<string>? CwdChanged;
    public event Action? NextTabRequested;
    public event Action? PrevTabRequested;
    public event Action<int>? ActivateAtRequested;
    public event Action<int>? MoveActiveByRequested;
    public event Action<string>? SaveBufferRequested;

    public TerminalControl()
    {
        InitializeComponent();
        AddPane(orientation: null, replace: true);
    }

    public void StartShell(string commandLine, string? startingDir = null)
    {
        _lastCommandLine = commandLine;
        _lastStartingDir = startingDir;
        MainPane.StartShell(commandLine, startingDir);
    }

    public void Restart(string commandLine, string? startingDir = null)
    {
        // Cerrar todos los splits y reiniciar el principal.
        while (_panes.Count > 1) RemovePaneAt(_panes.Count - 1);
        _lastCommandLine = commandLine;
        _lastStartingDir = startingDir;
        MainPane.Restart(commandLine, startingDir);
    }

    public void ApplyCurrentSettings()
    {
        foreach (var p in _panes) p.ApplyCurrentSettings();
    }

    // -- Split internals -----------------------------------------------------

    /// <summary>Vertical = pane al lado (split por columna). Solo si no hay split aún.</summary>
    public bool SplitVertical()  => Split(Orientation.Vertical);
    /// <summary>Horizontal = pane debajo (split por fila). Solo si no hay split aún.</summary>
    public bool SplitHorizontal() => Split(Orientation.Horizontal);

    private bool Split(Orientation orientation)
    {
        if (_panes.Count >= 2) return false; // v1: una sola subdivisión.
        AddPane(orientation, replace: false);
        return true;
    }

    public void CloseActivePane()
    {
        if (_panes.Count <= 1) return; // el pane principal nunca se cierra aquí
        var idx = _activePane is null ? -1 : _panes.IndexOf(_activePane);
        if (idx < 0) idx = _panes.Count - 1;
        RemovePaneAt(idx);
    }

    private void AddPane(Orientation? orientation, bool replace)
    {
        var pane = new TerminalPane();
        WireUpPane(pane);
        _panes.Add(pane);

        if (replace)
        {
            Root.Children.Clear();
            Root.ColumnDefinitions.Clear();
            Root.RowDefinitions.Clear();
            Root.Children.Add(pane);
        }
        else if (orientation == Orientation.Vertical)
        {
            // El pane existente pasa a Column 0; nuevo pane a Column 2; splitter en Column 1.
            Root.RowDefinitions.Clear();
            Root.ColumnDefinitions.Clear();
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            Root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            var existing = _panes[0];
            Root.Children.Clear();
            Grid.SetColumn(existing, 0);
            Root.Children.Add(existing);
            var splitter = new GridSplitter
            {
                Width = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
                Background          = (System.Windows.Media.Brush)FindResource("ChromeBorder")
            };
            Grid.SetColumn(splitter, 1);
            Root.Children.Add(splitter);
            Grid.SetColumn(pane, 2);
            Root.Children.Add(pane);
        }
        else // Horizontal
        {
            Root.ColumnDefinitions.Clear();
            Root.RowDefinitions.Clear();
            Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            var existing = _panes[0];
            Root.Children.Clear();
            Grid.SetRow(existing, 0);
            Root.Children.Add(existing);
            var splitter = new GridSplitter
            {
                Height = 4,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment   = VerticalAlignment.Stretch,
                Background          = (System.Windows.Media.Brush)FindResource("ChromeBorder")
            };
            Grid.SetRow(splitter, 1);
            Root.Children.Add(splitter);
            Grid.SetRow(pane, 2);
            Root.Children.Add(pane);
        }

        if (!replace && _lastCommandLine is not null)
            pane.StartShell(_lastCommandLine, _lastStartingDir);

        SetActivePane(pane);
    }

    private void RemovePaneAt(int idx)
    {
        if (idx < 0 || idx >= _panes.Count) return;
        var pane = _panes[idx];
        _panes.RemoveAt(idx);
        Root.Children.Clear();
        Root.RowDefinitions.Clear();
        Root.ColumnDefinitions.Clear();
        if (_panes.Count > 0)
        {
            Root.Children.Add(_panes[0]);
            SetActivePane(_panes[0]);
        }
    }

    private void SetActivePane(TerminalPane pane)
    {
        _activePane = pane;
        foreach (var p in _panes) p.SetActiveVisuals(p == pane);
    }

    private void WireUpPane(TerminalPane pane)
    {
        // El pane reemite eventos: solo forwardeamos los del pane activo
        // (Cwd/Next/Prev) para no duplicar reacciones.
        pane.CwdChanged += cwd =>
        {
            if (pane == _activePane) CwdChanged?.Invoke(cwd);
        };
        pane.NextTabRequested      += () => NextTabRequested?.Invoke();
        pane.PrevTabRequested      += () => PrevTabRequested?.Invoke();
        pane.ActivateAtRequested   += i => ActivateAtRequested?.Invoke(i);
        pane.MoveActiveByRequested += d => MoveActiveByRequested?.Invoke(d);
        pane.SaveBufferRequested   += t => SaveBufferRequested?.Invoke(t);
        pane.SplitHorizontalRequested += () => SplitHorizontal();
        pane.SplitVerticalRequested   += () => SplitVertical();
        pane.ClosePaneRequested       += CloseActivePane;
        pane.FocusReceived            += () => SetActivePane(pane);
    }
}
