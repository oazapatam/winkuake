using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace WinKuake.Views;

/// <summary>
/// Vista de una sesión de terminal. Hospeda un árbol de <see cref="TerminalPane"/>
/// con <see cref="GridSplitter"/> entre cada split. Soporta splits recursivos:
/// cada pane se puede subdividir vertical u horizontalmente las veces necesarias.
///
/// Estructura: el root contiene un <see cref="Border"/> "PaneSlot". El Child de
/// ese Border es bien un TerminalPane (caso leaf), bien un Grid de 3 cells
/// (2 sub-Borders + splitter, caso branch). Cuando se hace split, el Child del
/// Border que contiene al pane activo se reemplaza por un Grid de split.
/// Cuando se cierra un pane, su Border desaparece y el Grid padre se colapsa al
/// hermano.
/// </summary>
public partial class TerminalControl : UserControl
{
    private readonly List<TerminalPane> _panes = new();
    private TerminalPane? _activePane;
    private string? _lastCommandLine;
    private string? _lastStartingDir;
    private Border? _rootSlot;

    public string? CurrentCwd => _activePane?.CurrentCwd;

    public event Action<string>? CwdChanged;
    public event Action? NextTabRequested;
    public event Action? PrevTabRequested;
    public event Action<int>? ActivateAtRequested;
    public event Action<int>? MoveActiveByRequested;
    public event Action<string>? SaveBufferRequested;
    public event Action<string>? OpenFileRequested;

    public TerminalControl()
    {
        InitializeComponent();
        _rootSlot = NewSlot();
        var pane = CreatePane();
        _rootSlot.Child = pane;
        Root.Children.Clear();
        Root.Children.Add(_rootSlot);
        SetActivePane(pane);
    }

    public void StartShell(string commandLine, string? startingDir = null)
    {
        _lastCommandLine = commandLine;
        _lastStartingDir = startingDir;
        if (_panes.Count > 0) _panes[0].StartShell(commandLine, startingDir);
    }

    public void Restart(string commandLine, string? startingDir = null)
    {
        // Reset total: colapsamos todos los splits y reiniciamos el principal.
        while (_panes.Count > 1) CloseInternal(_panes[^1]);
        _lastCommandLine = commandLine;
        _lastStartingDir = startingDir;
        if (_panes.Count > 0) _panes[0].Restart(commandLine, startingDir);
    }

    public void ApplyCurrentSettings()
    {
        foreach (var p in _panes) p.ApplyCurrentSettings();
    }

    // -- Splits API públicos ------------------------------------------------

    public bool SplitVertical()    => SplitActive(Orientation.Vertical);
    public bool SplitHorizontal()  => SplitActive(Orientation.Horizontal);
    public void CloseActivePane()
    {
        if (_panes.Count <= 1) return;
        if (_activePane is null) return;
        CloseInternal(_activePane);
    }

    // -- Implementación ------------------------------------------------------

    private TerminalPane CreatePane()
    {
        var pane = new TerminalPane();
        _panes.Add(pane);
        pane.CwdChanged += cwd => { if (pane == _activePane) CwdChanged?.Invoke(cwd); };
        pane.NextTabRequested      += () => NextTabRequested?.Invoke();
        pane.PrevTabRequested      += () => PrevTabRequested?.Invoke();
        pane.ActivateAtRequested   += i => ActivateAtRequested?.Invoke(i);
        pane.MoveActiveByRequested += d => MoveActiveByRequested?.Invoke(d);
        pane.SaveBufferRequested   += t => SaveBufferRequested?.Invoke(t);
        pane.OpenFileRequested     += p => OpenFileRequested?.Invoke(p);
        pane.SplitHorizontalRequested += () => SplitActive(Orientation.Horizontal);
        pane.SplitVerticalRequested   += () => SplitActive(Orientation.Vertical);
        pane.ClosePaneRequested       += CloseActivePane;
        pane.FocusReceived            += () => SetActivePane(pane);
        pane.FocusPaneRequested       += FocusInDirection;
        return pane;
    }

    private static Border NewSlot() => new Border();

    private bool SplitActive(Orientation orientation)
    {
        if (_activePane is null) return false;
        var slot = FindSlotOf(_activePane);
        if (slot is null) return false;

        var existingPane = _activePane; // queda en el primer slot
        var newPane = CreatePane();

        var firstSlot  = NewSlot(); firstSlot.Child  = existingPane; // mover pane existente
        slot.Child = null; // desconecta antes de re-asignar
        firstSlot.Child = existingPane;

        var secondSlot = NewSlot(); secondSlot.Child = newPane;

        var grid = new Grid();
        if (orientation == Orientation.Vertical)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(firstSlot, 0);  grid.Children.Add(firstSlot);
            var sp = NewSplitter(Orientation.Vertical); Grid.SetColumn(sp, 1); grid.Children.Add(sp);
            Grid.SetColumn(secondSlot, 2); grid.Children.Add(secondSlot);
        }
        else
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(firstSlot, 0);  grid.Children.Add(firstSlot);
            var sp = NewSplitter(Orientation.Horizontal); Grid.SetRow(sp, 1); grid.Children.Add(sp);
            Grid.SetRow(secondSlot, 2); grid.Children.Add(secondSlot);
        }
        slot.Child = grid;

        if (_lastCommandLine is not null) newPane.StartShell(_lastCommandLine, _lastStartingDir);
        SetActivePane(newPane);
        return true;
    }

    private GridSplitter NewSplitter(Orientation orientation)
    {
        var brush = (Brush)Application.Current.FindResource("ChromeBorder");
        var s = new GridSplitter
        {
            Background = brush,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
            ShowsPreview = false
        };
        if (orientation == Orientation.Vertical) s.Width = 4;
        else                                     s.Height = 4;
        return s;
    }

    private void CloseInternal(TerminalPane pane)
    {
        var slot = FindSlotOf(pane);
        if (slot is null) return;

        // El slot que contiene el pane vive dentro de un Grid de split. Su
        // hermano es el otro slot del mismo Grid. Colapsamos: subimos al
        // Border-abuelo que apunta a ese Grid y le ponemos como Child el
        // contenido del slot hermano (que puede ser otro Pane u otro Grid).
        if (slot.Parent is not Grid splitGrid) return; // root sin split → nada.
        var siblings = splitGrid.Children.OfType<Border>().Where(b => b != slot).ToList();
        if (siblings.Count != 1) return; // estructura inesperada.
        var sibling = siblings[0];
        var siblingChild = sibling.Child;
        sibling.Child = null; // detach
        if (splitGrid.Parent is not Border grandparent) return;
        grandparent.Child = null; // detach splitGrid
        grandparent.Child = siblingChild;

        _panes.Remove(pane);
        pane.Dispose();

        // Foco al primer pane que quede dentro del grandparent.
        var firstPane = FindFirstPaneIn(grandparent);
        if (firstPane is not null) SetActivePane(firstPane);
    }

    private Border? FindSlotOf(TerminalPane pane)
    {
        // Recorre el árbol desde _rootSlot.
        return FindSlotOf(_rootSlot, pane);
    }

    private static Border? FindSlotOf(Border? slot, TerminalPane pane)
    {
        if (slot is null) return null;
        if (slot.Child == pane) return slot;
        if (slot.Child is Grid g)
        {
            foreach (var b in g.Children.OfType<Border>())
            {
                var found = FindSlotOf(b, pane);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static TerminalPane? FindFirstPaneIn(DependencyObject root)
    {
        if (root is TerminalPane tp) return tp;
        if (root is Border b) return FindFirstPaneIn(b.Child);
        if (root is Grid g)
        {
            foreach (var c in g.Children.OfType<DependencyObject>())
            {
                var found = FindFirstPaneIn(c);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private void SetActivePane(TerminalPane pane)
    {
        _activePane = pane;
        foreach (var p in _panes) p.SetActiveVisuals(p == pane);
        pane.Focus();
    }

    // -- Navegación Alt+arrows ----------------------------------------------

    private void FocusInDirection(string direction)
    {
        if (_activePane is null || _panes.Count < 2) return;
        Rect currentRect;
        try { currentRect = GetRectIn(_activePane, this); }
        catch { return; }
        var currentCenter = new Point(currentRect.Left + currentRect.Width / 2,
                                      currentRect.Top  + currentRect.Height / 2);

        TerminalPane? best = null;
        double bestDist = double.MaxValue;
        foreach (var p in _panes)
        {
            if (p == _activePane) continue;
            Rect r;
            try { r = GetRectIn(p, this); } catch { continue; }
            var center = new Point(r.Left + r.Width / 2, r.Top + r.Height / 2);
            bool ok = direction switch
            {
                "left"  => r.Right  <= currentRect.Left + 1,
                "right" => r.Left   >= currentRect.Right - 1,
                "up"    => r.Bottom <= currentRect.Top + 1,
                "down"  => r.Top    >= currentRect.Bottom - 1,
                _ => false
            };
            if (!ok) continue;
            var dx = center.X - currentCenter.X;
            var dy = center.Y - currentCenter.Y;
            var dist = dx * dx + dy * dy;
            if (dist < bestDist) { bestDist = dist; best = p; }
        }
        if (best is not null) SetActivePane(best);
    }

    private static Rect GetRectIn(FrameworkElement element, Visual reference)
    {
        if (!element.IsLoaded || element.ActualWidth <= 0) return Rect.Empty;
        var origin = element.TransformToVisual(reference).Transform(new Point(0, 0));
        return new Rect(origin, new Size(element.ActualWidth, element.ActualHeight));
    }
}
