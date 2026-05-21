using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using WinKuake.Models;
using WinKuake.Services;
using WinKuake.Views;

namespace WinKuake;

public partial class MainWindow : Window
{
    private AppSettings _settings = SettingsService.Load();
    private readonly HotkeyService _hotkey = new();
    private readonly TerminalSessionsManager _sessions = new();
    private readonly Dictionary<int, TerminalControl> _controls = new();
    private WindowAnimator? _animator;
    private bool _firstShown;
    private TerminalProfile[] _profiles = System.Array.Empty<TerminalProfile>();

    /// <summary>Pestañas mostradas en la tab bar inferior. ViewModel del manager.</summary>
    public ObservableCollection<TabItem> Tabs { get; } = new();

    /// <summary>CWD inicial pasado vía argumento CLI <c>--cwd</c>. Se aplica a la primera tab.</summary>
    public string? InitialCwd { get; set; }

    public MainWindow()
    {
        InitializeComponent();

        TabsItems.ItemsSource = Tabs;

        SourceInitialized += OnSourceInitialized;
        Deactivated       += OnDeactivated;
        Closed            += OnClosed;
        SizeChanged       += OnSizeChanged;

        _sessions.SessionAdded   += OnSessionAdded;
        _sessions.SessionClosed  += OnSessionClosed;
        _sessions.ActiveChanged  += OnActiveChanged;
        _sessions.OrderChanged   += OnSessionsOrderChanged;
        _sessions.PinChanged     += OnSessionPinChanged;

        ReloadProfiles();
        BuildWorkspacesMenu();
    }

    private void LoadProfilesFromRegistry()
    {
        // Snapshot de UserProfiles antes de pedir LoadAll: si está vacío, los
        // detectores poblarán _settings.UserProfiles y persistimos el resultado.
        var hadProfiles = _settings.UserProfiles.Count > 0;
        var users = ProfileRegistry.LoadAll(_settings);
        if (!hadProfiles && _settings.UserProfiles.Count > 0)
        {
            SettingsService.Save(_settings);
        }
        _profiles = ProfileMapping.BuildTerminalProfiles(users, _settings.DefaultProfileId);
    }

    public void InitializeHidden()
    {
        ApplyGeometry();
        Top = -100000;
        ShowActivated = false;
        Show();
        Visibility = Visibility.Collapsed;
        Opacity = _settings.Opacity;
    }

    /// <summary>Dev-only: dispara el flujo de mostrar desde fuera (sin esperar F12).</summary>
    public void ForceShowForDev() => ToggleVisibility();

    private TrayIconService? _tray;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var ok = _hotkey.TryRegister(hwnd, _settings, out var info);
        if (!ok) CrashLogger.Info($"Hotkey no registrado ({HotkeyDisplay()}): {info}");
        else if (_hotkey.UsingLowLevelHook) CrashLogger.Info($"Hotkey {HotkeyDisplay()} via low-level hook. {info}");
        _hotkey.HotkeyPressed += ToggleVisibility;

        _animator = new WindowAnimator(this);
        UpdateLockButtonGlyph();
        InstallTrayIcon();
    }

    private void InstallTrayIcon()
    {
        _tray = new TrayIconService();
        _tray.ShowRequested     += () => Dispatcher.InvokeAsync(ToggleVisibility);
        _tray.HideRequested     += () => Dispatcher.InvokeAsync(() =>
        {
            if (_animator?.IsVisible == true) _animator.Hide(GetTargetTop(), _settings.AnimationMs);
        });
        _tray.SettingsRequested += () => Dispatcher.InvokeAsync(() => OpenSettings_Click(this, new RoutedEventArgs()));
        _tray.ExitRequested     += () => Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
        _tray.Install();

        // Saludo inicial: confirma al usuario que la app está viva y le
        // recuerda el hotkey actual (útil cuando arranca en background y
        // no aparece ninguna ventana).
        _tray.ShowBalloon(
            "WinKuake is running",
            $"Press {HotkeyDisplay()} to toggle the terminal. Right-click the tray icon for more options.");
    }

    private void ReloadProfiles()
    {
        LoadProfilesFromRegistry();
        BuildProfileMenu();
    }

    private void ToggleVisibility()
    {
        if (_animator is null) return;
        var top = GetTargetTop();

        if (_animator.IsVisible)
        {
            _animator.Hide(top, _settings.AnimationMs);
        }
        else
        {
            ApplyGeometry();
            _animator.Show(top, _settings.AnimationMs);

            if (!_firstShown)
            {
                _firstShown = true;
                RestoreSessionOrCreateDefault();
            }
            else Activate();
        }
    }

    private TerminalProfile DefaultProfile()
    {
        // El default ya viene marcado en _profiles (BuildTerminalProfiles usa
        // ResolveDefault con _settings.DefaultProfileId). Si por alguna razón
        // no hay perfiles, fallback duro a PowerShell para no crashear.
        return _profiles.FirstOrDefault(p => p.IsDefault && !string.IsNullOrEmpty(p.CommandLine))
            ?? _profiles.FirstOrDefault(p => !string.IsNullOrEmpty(p.CommandLine))
            ?? ProfileMapping.HardFallback();
    }

    private void RestoreSessionOrCreateDefault()
    {
        // --cwd CLI tiene prioridad: arranca tab fresca en ese dir, sin restaurar.
        if (!string.IsNullOrEmpty(InitialCwd) && System.IO.Directory.Exists(InitialCwd))
        {
            var p = DefaultProfile() with { StartingDirectory = InitialCwd };
            _sessions.Create(p);
            return;
        }
        // Si hay tabs persistidas en la última sesión, las recreamos.
        if (_settings.LastSessionTabs.Count == 0)
        {
            _sessions.Create(DefaultProfile());
            return;
        }
        foreach (var t in _settings.LastSessionTabs)
        {
            var profile = ResolvePersistedProfile(t);
            // Aplicar cwd si es un path Windows válido como starting directory.
            var startDir = (!string.IsNullOrEmpty(t.Cwd) && System.IO.Path.IsPathRooted(t.Cwd) && System.IO.Directory.Exists(t.Cwd))
                ? t.Cwd : profile.StartingDirectory;
            CreateRestoringTab(profile with { StartingDirectory = startDir }, t);
        }
    }

    private void CreateRestoringTab(TerminalProfile profile, PersistedTab t)
    {
        // Pre-registra el layout (si existe) ANTES de Create — OnSessionAdded
        // lo consume y aplica RestoreLayout.
        // Anticipamos el id que asignará el manager (siguiente id incremental).
        var nextId = (_sessions.Sessions.LastOrDefault()?.Id ?? 0) + 1;
        if (t.Layout is not null) _pendingLayouts[nextId] = t.Layout;
        var session = _sessions.Create(profile);
        if (!string.IsNullOrEmpty(t.CustomLabel)) _sessions.Rename(session.Id, t.CustomLabel);
        if (t.IsPinned) _sessions.TogglePin(session.Id);
    }

    private TerminalProfile ResolvePersistedProfile(PersistedTab t)
    {
        return ProfileMapping.ResolvePersisted(_profiles, t.ProfileGuid, t.ProfileName)
            ?? DefaultProfile();
    }

    private List<PersistedTab> SnapshotCurrentSessions()
    {
        return _sessions.Sessions.Select(s =>
        {
            _controls.TryGetValue(s.Id, out var c);
            return new PersistedTab
            {
                ProfileGuid = s.Profile?.Guid,
                ProfileName = s.Profile?.DisplayName,
                Cwd         = c?.CurrentCwd,
                CustomLabel = s.CustomLabel,
                IsPinned    = s.IsPinned,
                Layout      = c?.SerializeLayout(),
            };
        }).ToList();
    }

    // -- Manager → UI sync ------------------------------------------------

    private void OnSessionAdded(TerminalSession s)
    {
        var ctrl = new TerminalControl { Visibility = Visibility.Collapsed };
        _controls[s.Id] = ctrl;
        TerminalContainer.Children.Add(ctrl);

        ctrl.NextTabRequested      += () => _sessions.ActivateNext();
        ctrl.PrevTabRequested      += () => _sessions.ActivatePrevious();
        ctrl.ActivateAtRequested   += i => _sessions.ActivateAt(i);
        ctrl.MoveActiveByRequested += d => _sessions.MoveActiveBy(d);
        ctrl.SaveBufferRequested   += SaveBufferToFile;
        ctrl.OpenFileRequested     += path => OpenFileFromTerminal(ctrl, path);
        ctrl.OpenPaletteRequested  += () => OpenCommandPalette(ctrl);
        ctrl.OpenGlobalFindRequested += () => _ = OpenGlobalFindAsync();
        ctrl.ContextMenuRequested  += (pane, x, y, hasSel) =>
            ShowTerminalContextMenu(ctrl, pane, x, y, hasSel);
        ctrl.BroadcastChanged      += active =>
        {
            if (_sessions.Active?.Id == s.Id)
            {
                UpdateStatusForActive();
                _tray?.SetBroadcastState(active);
            }
        };
        ctrl.CwdChanged += cwd =>
        {
            if (_sessions.Active?.Id == s.Id) UpdateStatusForActive();
        };

        // Si hay layout pendiente (restauración de sesión), lo aplica antes de
        // arrancar shell; cada hoja del árbol creará su propio pane+shell.
        if (_pendingLayouts.TryGetValue(s.Id, out var layout) && layout is not null)
        {
            ctrl.RestoreLayout(layout, ResolvePersistedProfileFromGuidOrName);
            _pendingLayouts.Remove(s.Id);
        }
        else if (s.Profile is not null)
        {
            ctrl.StartShell(s.Profile);
        }

        Tabs.Add(new TabItem { Index = s.Id, Profile = s.Profile, IsActive = false });
    }

    /// <summary>Layouts pendientes a aplicar cuando OnSessionAdded reciba la sesión.</summary>
    private readonly Dictionary<int, PersistedSplitNode?> _pendingLayouts = new();

    private TerminalProfile ResolvePersistedProfileFromGuidOrName(string? guid, string? name)
    {
        return ProfileMapping.ResolvePersisted(_profiles, guid, name)
            ?? DefaultProfile();
    }

    private void OnSessionClosed(TerminalSession s)
    {
        if (_controls.TryGetValue(s.Id, out var ctrl))
        {
            TerminalContainer.Children.Remove(ctrl);
            _controls.Remove(s.Id);
        }
        var tab = Tabs.FirstOrDefault(t => t.Index == s.Id);
        if (tab is not null) Tabs.Remove(tab);

        // Política: si no queda ninguna sesión, abrir una default.
        if (_sessions.Sessions.Count == 0 && _firstShown)
        {
            _sessions.Create(DefaultProfile());
        }
    }

    private void OnActiveChanged(TerminalSession? active)
    {
        foreach (var kv in _controls)
            kv.Value.Visibility = (kv.Key == active?.Id) ? Visibility.Visible : Visibility.Collapsed;
        foreach (var tab in Tabs)
            tab.IsActive = (tab.Index == active?.Id);

        UpdateStatusForActive();

        // Sincroniza el badge del tray con el broadcast state de la nueva tab activa.
        if (active is not null && _controls.TryGetValue(active.Id, out var ctrl))
            _tray?.SetBroadcastState(ctrl.BroadcastEnabled);
        else
            _tray?.SetBroadcastState(false);
    }

    private void UpdateStatusForActive()
    {
        var active = _sessions.Active;
        if (active?.Profile is not { } p)
        {
            StatusTitle.Text = "WinKuake";
            return;
        }
        var glyph = ProfileIconHelper.GlyphFor(p);
        var ctrl = _controls.TryGetValue(active.Id, out var c) ? c : null;
        var cwd = ctrl?.CurrentCwd;
        var broadcast = ctrl?.BroadcastEnabled == true ? "  ·  📡 BROADCAST" : "";
        var cwdPart = string.IsNullOrEmpty(cwd) ? "" : $"  ·  {AbbreviateCwd(cwd!)}";
        StatusTitle.Text = $"{glyph}  {p.DisplayName}{cwdPart}{broadcast}";
    }

    private static string AbbreviateCwd(string cwd)
    {
        // Abreviar home → ~ tanto en path Windows como Linux.
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(home) && cwd.StartsWith(home, StringComparison.OrdinalIgnoreCase))
            return "~" + cwd.Substring(home.Length).Replace('\\', '/');
        // /home/user → ~ (heurística para WSL — el cwd es relativo al user de WSL).
        var m = System.Text.RegularExpressions.Regex.Match(cwd, @"^/home/[^/]+");
        if (m.Success && cwd.Length > m.Length && cwd[m.Length] == '/')
            return "~" + cwd.Substring(m.Length);
        if (m.Success && cwd.Length == m.Length)
            return "~";
        return cwd;
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (_settings.AutoHideOnFocusLost && _animator?.IsVisible == true)
            _animator.Hide(GetTargetTop(), _settings.AnimationMs);
    }

    private void ApplyGeometry()
    {
        // Suprimimos OnSizeChanged mientras aplicamos geometría desde settings:
        // si no, cada ApplyGeometry dispararía un Save inútil con el mismo valor.
        _suppressSizePersist = true;
        try
        {
            var screenW = SystemParameters.PrimaryScreenWidth;
            var screenH = SystemParameters.PrimaryScreenHeight;
            Width  = Math.Max(400, screenW * Math.Clamp(_settings.WidthRatio,  0.1, 1.0));
            Height = Math.Max(200, screenH * Math.Clamp(_settings.HeightRatio, 0.1, 1.0));
            Left   = (screenW - Width) / 2.0;
            Top    = 0;
            Opacity = Math.Clamp(_settings.Opacity, 0.5, 1.0);
        }
        finally { _suppressSizePersist = false; }
    }

    /// <summary>
    /// Capturamos el resize manual del usuario (drag de los bordes) y lo
    /// persistimos como ratios en settings.json. Sin esto, el siguiente F12
    /// volvía al tamaño antiguo aunque el usuario hubiera ajustado a mano.
    /// Debounce simple: en vez de Save por cada pixel, aplazamos con un
    /// timer que solo dispara tras 500ms sin nuevos cambios.
    /// </summary>
    private bool _suppressSizePersist;
    private System.Windows.Threading.DispatcherTimer? _sizePersistTimer;

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_suppressSizePersist) return;
        if (!IsVisible) return;                            // oculta → no persistir
        if (WindowState != WindowState.Normal) return;     // minimizada/maximizada
        if (Top < 0) return;                               // animación slide en curso
        _sizePersistTimer ??= new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _sizePersistTimer.Tick -= PersistSizeTick;
        _sizePersistTimer.Tick += PersistSizeTick;
        _sizePersistTimer.Stop();
        _sizePersistTimer.Start();
    }

    private void PersistSizeTick(object? sender, EventArgs e)
    {
        _sizePersistTimer?.Stop();
        // Re-chequeamos las precondiciones: el timer pudo haber sido armado y la
        // ventana entró en animación entre tanto. No persistir ratios degenerados.
        if (!IsVisible || WindowState != WindowState.Normal || Top < 0) return;
        var screenW = SystemParameters.PrimaryScreenWidth;
        var screenH = SystemParameters.PrimaryScreenHeight;
        // Sanidad mínima del tamaño: si la ventana está en menos de 200x200 píxeles,
        // casi seguro estamos en medio de un slide o estado raro — descartar.
        if (ActualWidth < 200 || ActualHeight < 200) return;
        var (w, h) = GeometryRatios.FromSize(ActualWidth, ActualHeight, screenW, screenH);
        var changed = GeometryRatios.RatiosDifferEnoughToSave(_settings.WidthRatio, w)
                   || GeometryRatios.RatiosDifferEnoughToSave(_settings.HeightRatio, h);
        if (!changed) return;
        _settings.WidthRatio  = w;
        _settings.HeightRatio = h;
        try { SettingsService.Save(_settings); }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    private double GetTargetTop() => 0;

    private void ChromeDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left) DragMove();
    }

    // -- Profile picker ----------------------------------------------------

    private void BuildProfileMenu()
    {
        ProfileMenu.Items.Clear();

        var shortcutIndex = 1;
        foreach (var p in _profiles)
        {
            // Los detectores garantizan que CommandLine está poblado; si por
            // alguna razón faltara, el perfil no se muestra.
            if (string.IsNullOrEmpty(p.CommandLine)) continue;
            var item = new MenuItem
            {
                Header = p.DisplayName,
                Tag = p,
                Icon = new TextBlock
                {
                    Text = ProfileIconHelper.GlyphFor(p),
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"),
                    FontSize = 14
                },
                InputGestureText = shortcutIndex <= 9 ? $"Ctrl+Shift+{shortcutIndex}" : "",
                FontWeight = p.IsDefault ? FontWeights.Bold : FontWeights.Normal,
            };
            var profileCapture = p;
            item.Click += (_, _) => _sessions.Create(profileCapture);
            ProfileMenu.Items.Add(item);
            shortcutIndex++;
        }

        ProfileMenu.Items.Add(new Separator());

        var about = new MenuItem
        {
            Header = "About",
            Icon = new TextBlock { Text = "?", FontWeight = FontWeights.Bold, FontSize = 14 }
        };
        about.Click += (_, _) => MessageBox.Show(
            "WinKuake — drop-down terminal for Windows.\nEngine: ConPTY + xterm.js (WebView2).",
            "About WinKuake", MessageBoxButton.OK, MessageBoxImage.Information);
        ProfileMenu.Items.Add(about);
    }

    private void NewTabButton_Click(object sender, RoutedEventArgs e)
    {
        // + : nueva tab con el perfil de la activa, o el default.
        var profile = _sessions.Active?.Profile ?? DefaultProfile();
        _sessions.Create(profile);
    }

    private void ProfilePickerButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is not null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Top;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void MenuNewTab_Click(object sender, RoutedEventArgs e) => NewTabButton_Click(sender, e);

    private TerminalControl? ActiveControl()
    {
        if (_sessions.Active is { } a && _controls.TryGetValue(a.Id, out var ctrl)) return ctrl;
        return null;
    }

    private void SplitVertical_Click(object sender, RoutedEventArgs e) => ActiveControl()?.SplitVertical();
    private void SplitHorizontal_Click(object sender, RoutedEventArgs e) => ActiveControl()?.SplitHorizontal();
    private void ClosePane_Click(object sender, RoutedEventArgs e) => ActiveControl()?.CloseActivePane();
    private void OpenPalette_Click(object sender, RoutedEventArgs e)
    {
        var ctrl = ActiveControl();
        if (ctrl is not null) OpenCommandPalette(ctrl);
    }

    /// <summary>
    /// Materializa el menú contextual del terminal. La lista de items la
    /// construye <see cref="WinKuake.Services.TerminalContextMenuBuilder"/>; aquí
    /// los conectamos a los handlers ya existentes (los mismos que usan los
    /// atajos de teclado) y posicionamos el menú en pantalla.
    /// </summary>
    private void ShowTerminalContextMenu(
        TerminalControl ctrl, TerminalPane pane, double clientX, double clientY, bool hasSelection)
    {
        // El click derecho debe activar el pane antes de operar (las acciones
        // posteriores miran el pane activo del control).
        ctrl.FocusPane(pane);

        var isInSplit = ctrl.AllPanes.Count > 1;
        var specs = WinKuake.Services.TerminalContextMenuBuilder.Build(hasSelection, isInSplit);

        var menu = new ContextMenu();
        foreach (var spec in specs)
        {
            if (spec.IsSeparator) { menu.Items.Add(new Separator()); continue; }
            var mi = new MenuItem
            {
                Header = spec.Label,
                InputGestureText = spec.Shortcut,
                IsEnabled = spec.Enabled,
            };
            var actionId = spec.ActionId;
            mi.Click += (_, _) => OnContextMenuAction(ctrl, pane, actionId);
            menu.Items.Add(mi);
        }

        // Posicionar en coords de pantalla traduciendo desde client del WebView2.
        try
        {
            var screen = pane.PointToScreen(new Point(clientX, clientY));
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.AbsolutePoint;
            menu.HorizontalOffset = screen.X;
            menu.VerticalOffset   = screen.Y;
            menu.PlacementTarget  = pane;
        }
        catch { /* fallback: aparece en posición default si PointToScreen falla */ }
        menu.IsOpen = true;
    }

    private void OnContextMenuAction(TerminalControl ctrl, TerminalPane pane, string actionId)
    {
        switch (actionId)
        {
            case "copy":            pane.CopySelectionToClipboard();   break;
            case "paste":           pane.PasteFromClipboard();         break;
            case "find":            pane.OpenSearch();                 break;
            case "splitVertical":   ctrl.SplitVertical();              break;
            case "splitHorizontal": ctrl.SplitHorizontal();            break;
            case "closePane":       ctrl.CloseActivePane();            break;
            case "openPalette":     OpenCommandPalette(ctrl);          break;
            case "clearBuffer":     pane.ClearBuffer();                break;
            case "devtools":        pane.OpenDevTools();               break;
        }
    }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (_sessions.Active is { } a) TryCloseSession(a.Id);
    }

    private void CloseSpecificTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is int id) TryCloseSession(id);
    }

    private void TryCloseSession(int id)
    {
        var s = _sessions.Sessions.FirstOrDefault(x => x.Id == id);
        if (s is null) return;
        if (s.IsPinned)
        {
            // Pestaña fijada: pedimos confirmación para evitar cierre accidental.
            var r = MessageBox.Show(this,
                $"La pestaña «{s.Label}» está fijada. ¿Cerrar de todos modos?",
                "WinKuake", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (r != MessageBoxResult.Yes) return;
        }
        _sessions.Close(id);
    }

    private Point _tabDragStart;

    private void Tab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not TabItem tab) return;
        if (e.ClickCount == 2) { PromptRename(tab); return; }
        _tabDragStart = e.GetPosition(this);
        _sessions.SetActive(tab.Index);
    }

    private void Tab_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not TabItem tab) return;
        var p = e.GetPosition(this);
        if (Math.Abs(p.X - _tabDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(p.Y - _tabDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        DragDrop.DoDragDrop(fe, new DataObject("WinKuakeTabId", tab.Index), DragDropEffects.Move);
    }

    private void Tab_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("WinKuakeTabId") ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private void Tab_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("WinKuakeTabId")) return;
        if (sender is not FrameworkElement fe || fe.DataContext is not TabItem target) return;
        var draggedId = (int)e.Data.GetData("WinKuakeTabId");
        if (draggedId == target.Index) return;
        var targetIdx = Tabs.IndexOf(target);
        _sessions.Move(draggedId, targetIdx);
        e.Handled = true;
    }

    private void OnSessionsOrderChanged()
    {
        // Sincroniza el orden visual con el orden del manager.
        var ids = _sessions.Sessions.Select(s => s.Id).ToArray();
        for (int target = 0; target < ids.Length; target++)
        {
            var current = Tabs.ToList().FindIndex(t => t.Index == ids[target]);
            if (current >= 0 && current != target) Tabs.Move(current, target);
        }
    }

    private void OnSessionPinChanged(TerminalSession s)
    {
        var tab = Tabs.FirstOrDefault(t => t.Index == s.Id);
        if (tab is not null) tab.IsPinned = s.IsPinned;
    }

    // -- Workspaces --------------------------------------------------------

    private void BuildWorkspacesMenu()
    {
        WorkspacesMenu.Items.Clear();

        var save = new MenuItem { Header = "Guardar workspace actual…" };
        save.Click += (_, _) => SaveCurrentWorkspace();
        WorkspacesMenu.Items.Add(save);

        if (_settings.Workspaces.Count > 0)
        {
            WorkspacesMenu.Items.Add(new Separator());
            foreach (var ws in _settings.Workspaces)
            {
                var capture = ws;
                var load = new MenuItem { Header = $"Cargar «{ws.Name}»" };
                load.Click += (_, _) => LoadWorkspace(capture);
                WorkspacesMenu.Items.Add(load);

                var del = new MenuItem { Header = $"Eliminar «{ws.Name}»" };
                del.Click += (_, _) => DeleteWorkspace(capture);
                WorkspacesMenu.Items.Add(del);
            }
        }
    }

    private void SaveCurrentWorkspace()
    {
        var dlg = new RenameDialog("nuevo-workspace") { Owner = this, Title = "Guardar workspace" };
        if (dlg.ShowDialog() != true || string.IsNullOrWhiteSpace(dlg.Result)) return;
        var name = dlg.Result.Trim();
        // Reemplaza si ya existe.
        _settings.Workspaces.RemoveAll(w => string.Equals(w.Name, name, StringComparison.OrdinalIgnoreCase));
        _settings.Workspaces.Add(new Workspace { Name = name, Tabs = SnapshotCurrentSessions() });
        SettingsService.Save(_settings);
        BuildWorkspacesMenu();
    }

    private void LoadWorkspace(Workspace ws)
    {
        // Cierra todas las sesiones actuales y crea las del workspace.
        var ids = _sessions.Sessions.Select(s => s.Id).ToList();
        foreach (var id in ids) _sessions.Close(id);
        foreach (var t in ws.Tabs)
        {
            var profile = ResolvePersistedProfile(t);
            var startDir = (!string.IsNullOrEmpty(t.Cwd) && System.IO.Path.IsPathRooted(t.Cwd) && System.IO.Directory.Exists(t.Cwd))
                ? t.Cwd : profile.StartingDirectory;
            CreateRestoringTab(profile with { StartingDirectory = startDir }, t);
        }
    }

    private void DeleteWorkspace(Workspace ws)
    {
        var r = MessageBox.Show(this, $"¿Eliminar workspace «{ws.Name}»?", "WinKuake",
            MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (r != MessageBoxResult.Yes) return;
        _settings.Workspaces.Remove(ws);
        SettingsService.Save(_settings);
        BuildWorkspacesMenu();
    }

    private async void OpenCommandPalette(TerminalControl ctrl)
    {
        // Pre-resolución de contexto async: selección de xterm + branch git.
        // Lo hacemos antes de mostrar la ventana para que {branch}/{selection}
        // ya estén disponibles al inyectar.
        var selection = await ctrl.GetActivePaneSelectionAsync();
        var branch = await System.Threading.Tasks.Task.Run(() => GitService.GetBranch(ctrl.CurrentCwd));

        var userSnippets = _settings.UserSnippets
            .Select(u => new CommandSnippet(u.Name, u.Command));
        var all = CommandSnippetService.LoadAll(userSnippets);
        var dlg = new QuickCommandWindow(all) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.SelectedSnippet is { } snip)
        {
            var ctx = new SnippetContext
            {
                Cwd       = ctrl.CurrentCwd,
                Home      = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                User      = Environment.UserName,
                Date      = DateTime.Now,
                Branch    = branch,
                Selection = selection,
            };
            var command = CommandSnippetService.Expand(snip.Command, ctx);
            var text = dlg.ExecuteAfterInject ? command + "\n" : command;
            ctrl.InjectInputToActive(text);
        }
    }

    private async System.Threading.Tasks.Task OpenGlobalFindAsync()
    {
        // Recolecta buffers de TODOS los panes de TODAS las sesiones. Hacemos
        // las llamadas en paralelo: cada GetBufferLinesAsync rebota al CoreWebView2
        // y nada gana serializar.
        var sources = new List<GlobalFindSource>();
        var fetches = new List<(TerminalSession s, TerminalPane p, int idx,
            System.Threading.Tasks.Task<IReadOnlyList<string>> task)>();
        foreach (var s in _sessions.Sessions)
        {
            if (!_controls.TryGetValue(s.Id, out var ctrl)) continue;
            var panes = ctrl.AllPanes;
            for (int i = 0; i < panes.Count; i++)
                fetches.Add((s, panes[i], i, panes[i].GetBufferLinesAsync()));
        }
        try
        {
            await System.Threading.Tasks.Task.WhenAll(fetches.ConvertAll(f => f.task));
        }
        catch (Exception ex) { CrashLogger.Log(ex); }

        foreach (var (s, _, idx, task) in fetches)
        {
            var lines = task.IsCompletedSuccessfully ? task.Result : (IReadOnlyList<string>)Array.Empty<string>();
            sources.Add(new GlobalFindSource(s.Id, s.Label, idx, lines));
        }

        var dlg = new FindGlobalWindow(sources) { Owner = this };
        if (dlg.ShowDialog() != true || dlg.SelectedResult is not { } pick) return;

        if (!_controls.TryGetValue(pick.SessionId, out var targetCtrl)) return;
        _sessions.SetActive(pick.SessionId);

        // El pane está identificado por índice en AllPanes (mismo orden que
        // se usó al recolectar).
        var allPanes = targetCtrl.AllPanes;
        if (pick.PaneIndex < 0 || pick.PaneIndex >= allPanes.Count) return;
        var pane = allPanes[pick.PaneIndex];
        targetCtrl.FocusPane(pane);
        pane.ScrollToLine(pick.LineNumber);
    }

    private void OpenFileFromTerminal(TerminalControl ctrl, string path)
    {
        try
        {
            // Si es path WSL (/mnt/c/...) lo traducimos a Windows; si es path
            // Linux puro (/home/foo) no podemos abrirlo desde explorer Windows.
            var resolved = path;
            if (resolved.StartsWith("/mnt/", StringComparison.Ordinal) && resolved.Length > 6 && resolved[6] == '/')
            {
                // /mnt/c/Users/foo → C:\Users\foo
                resolved = char.ToUpperInvariant(resolved[5]) + ":\\" + resolved.Substring(7).Replace('/', '\\');
            }
            else if (!System.IO.Path.IsPathRooted(resolved) && !string.IsNullOrEmpty(ctrl.CurrentCwd))
            {
                resolved = System.IO.Path.Combine(ctrl.CurrentCwd, resolved);
            }
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(resolved) { UseShellExecute = true });
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    private void SaveBufferToFile(string buffer)
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Guardar buffer del terminal",
            FileName = $"winkuake-{DateTime.Now:yyyyMMdd-HHmmss}.txt",
            DefaultExt = ".txt",
            Filter = "Texto (*.txt)|*.txt|Todos|*.*"
        };
        if (dlg.ShowDialog(this) == true)
        {
            try
            {
                System.IO.File.WriteAllText(dlg.FileName, buffer, System.Text.Encoding.UTF8);
            }
            catch (Exception ex) { CrashLogger.Log(ex); }
        }
    }

    private void Tab_RightDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not TabItem tab) return;
        var menu = new ContextMenu();

        var rename = new MenuItem { Header = "Renombrar…" };
        rename.Click += (_, _) => PromptRename(tab);
        menu.Items.Add(rename);

        var pin = new MenuItem { Header = tab.IsPinned ? "Desfijar pestaña" : "Fijar pestaña" };
        pin.Click += (_, _) => _sessions.TogglePin(tab.Index);
        menu.Items.Add(pin);

        menu.Items.Add(new Separator());

        var close = new MenuItem { Header = "Cerrar" };
        close.Click += (_, _) => _sessions.Close(tab.Index);
        menu.Items.Add(close);

        menu.PlacementTarget = fe;
        menu.IsOpen = true;
    }

    private void PromptRename(TabItem tab)
    {
        var dlg = new RenameDialog(tab.DisplayLabel) { Owner = this };
        if (dlg.ShowDialog() == true && !string.IsNullOrWhiteSpace(dlg.Result))
        {
            var label = dlg.Result.Trim();
            tab.CustomLabel = label;
            _sessions.Rename(tab.Index, label);
        }
    }

    // -- Status bar buttons -----------------------------------------------

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        _settings.AutoHideOnFocusLost = !_settings.AutoHideOnFocusLost;
        SettingsService.Save(_settings);
        UpdateLockButtonGlyph();
    }

    private void UpdateLockButtonGlyph()
    {
        LockButton.Content = _settings.AutoHideOnFocusLost ? "○" : "●";
        LockButton.ToolTip = _settings.AutoHideOnFocusLost
            ? "Lock OFF — the window will hide on focus loss. Click to enable lock."
            : "Lock ON — the window will NOT hide on focus loss. Click to disable.";
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.ContextMenu is not null)
        {
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            _settings = dlg.Result;
            SettingsService.Save(_settings);

            Opacity = _settings.Opacity;
            ApplyGeometry();
            SkinService.Apply(_settings);
            UpdateLockButtonGlyph();

            _hotkey.Dispose();
            var hwnd = new WindowInteropHelper(this).Handle;
            var ok = _hotkey.TryRegister(hwnd, _settings, out var err);
            if (!ok) CrashLogger.Info($"Hotkey no aplicado: {err}");
            _hotkey.HotkeyPressed += ToggleVisibility;

            AutoStartService.SetEnabled(_settings.StartWithWindows);

            // Aplicar tema/scrollback/fontSize en caliente a todas las sesiones.
            foreach (var ctrl in _controls.Values)
                ctrl.ApplyCurrentSettings();

            // Si el usuario editó perfiles en Configuración, refrescamos el menú.
            ReloadProfiles();
            BuildWorkspacesMenu();
        }
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        if (_animator?.IsVisible == true) _animator.Hide(GetTargetTop(), _settings.AnimationMs);
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void OnClosed(object? sender, EventArgs e)
    {
        try
        {
            _settings.LastSessionTabs = SnapshotCurrentSessions();
            SettingsService.Save(_settings);
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
        _hotkey.Dispose();
        _tray?.Dispose();
    }

    private string HotkeyDisplay()
    {
        var mods = _settings.HotkeyModifiers.Count == 0 ? "" : string.Join("+", _settings.HotkeyModifiers) + "+";
        return mods + _settings.HotkeyKey;
    }
}

/// <summary>ViewModel de cada pestaña. Sincronizado con TerminalSessionsManager.</summary>
public class TabItem : INotifyPropertyChanged
{
    private TerminalProfile? _profile;
    private string? _customLabel;
    private bool _isActive;

    /// <summary>Identifica la sesión en el manager. Usamos el Id del manager.</summary>
    public int Index { get; set; }

    public TerminalProfile? Profile
    {
        get => _profile;
        set
        {
            _profile = value;
            OnPropertyChanged(nameof(Profile));
            OnPropertyChanged(nameof(Label));
            OnPropertyChanged(nameof(DisplayLabel));
            OnPropertyChanged(nameof(IconGlyph));
        }
    }

    public string? CustomLabel
    {
        get => _customLabel;
        set { _customLabel = value; OnPropertyChanged(nameof(CustomLabel)); OnPropertyChanged(nameof(Label)); OnPropertyChanged(nameof(DisplayLabel)); }
    }

    public string Label => DisplayLabel;

    public string DisplayLabel
    {
        get
        {
            if (!string.IsNullOrEmpty(_customLabel)) return _customLabel!;
            return _profile?.DisplayName ?? "Shell";
        }
    }

    public string IconGlyph => ProfileIconHelper.GlyphFor(_profile);

    public bool IsActive
    {
        get => _isActive;
        set { if (_isActive != value) { _isActive = value; OnPropertyChanged(nameof(IsActive)); } }
    }

    private bool _isPinned;
    public bool IsPinned
    {
        get => _isPinned;
        set { if (_isPinned != value) { _isPinned = value; OnPropertyChanged(nameof(IsPinned)); OnPropertyChanged(nameof(PinGlyph)); } }
    }

    public string PinGlyph => _isPinned ? "📌" : "";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
