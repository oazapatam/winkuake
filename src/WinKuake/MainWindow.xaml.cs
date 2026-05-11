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
    private TerminalProfile[] _profiles = LoadProfiles();

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

        _sessions.SessionAdded   += OnSessionAdded;
        _sessions.SessionClosed  += OnSessionClosed;
        _sessions.ActiveChanged  += OnActiveChanged;
        _sessions.OrderChanged   += OnSessionsOrderChanged;
        _sessions.PinChanged     += OnSessionPinChanged;

        BuildProfileMenu();
        BuildWorkspacesMenu();
    }

    private static TerminalProfile[] LoadProfiles()
    {
        // LoadCombined = perfiles wt + distros WSL detectadas que falten.
        var combined = WtProfileSource.LoadCombined();
        if (combined.Count > 0)
        {
            return new[] { new TerminalProfile("(predeterminado)", "") }
                .Concat(combined).ToArray();
        }
        return new[]
        {
            new TerminalProfile("(predeterminado)", "") { CommandLine = "powershell.exe" },
            new TerminalProfile("PowerShell",       "pwsh") { CommandLine = "pwsh.exe" },
            new TerminalProfile("cmd",              "cmd")  { CommandLine = "cmd.exe" },
        };
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

    private System.IO.FileSystemWatcher? _wtWatcher;
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
        StartWatchingWtSettings();
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
    }

    private void StartWatchingWtSettings()
    {
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = System.IO.Path.Combine(local, "Packages",
                "Microsoft.WindowsTerminal_8wekyb3d8bbwe", "LocalState");
            if (!System.IO.Directory.Exists(dir)) return;

            _wtWatcher = new System.IO.FileSystemWatcher(dir, "settings.json")
            {
                NotifyFilter = System.IO.NotifyFilters.LastWrite | System.IO.NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            // Editores guardan en varios writes; debounce con un timer.
            System.Threading.Timer? debounce = null;
            _wtWatcher.Changed += (_, _) =>
            {
                debounce?.Dispose();
                debounce = new System.Threading.Timer(_ =>
                    Dispatcher.InvokeAsync(ReloadProfiles), null, 300, System.Threading.Timeout.Infinite);
            };
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    private void ReloadProfiles()
    {
        _profiles = LoadProfiles();
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
        return _profiles.FirstOrDefault(p => p.IsDefault && p.CommandLine is not null)
            ?? _profiles.FirstOrDefault(p => p.CommandLine is not null)
            ?? new TerminalProfile("PowerShell", "pwsh") { CommandLine = "powershell.exe" };
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
            var session = _sessions.Create(profile with { StartingDirectory = startDir });
            if (!string.IsNullOrEmpty(t.CustomLabel)) _sessions.Rename(session.Id, t.CustomLabel);
            if (t.IsPinned) _sessions.TogglePin(session.Id);
        }
    }

    private TerminalProfile ResolvePersistedProfile(PersistedTab t)
    {
        TerminalProfile? p = null;
        if (!string.IsNullOrEmpty(t.ProfileGuid))
            p = _profiles.FirstOrDefault(x => string.Equals(x.Guid, t.ProfileGuid, StringComparison.OrdinalIgnoreCase));
        if (p is null && !string.IsNullOrEmpty(t.ProfileName))
            p = _profiles.FirstOrDefault(x => string.Equals(x.DisplayName, t.ProfileName, StringComparison.OrdinalIgnoreCase));
        return p ?? DefaultProfile();
    }

    private List<PersistedTab> SnapshotCurrentSessions()
    {
        return _sessions.Sessions.Select(s => new PersistedTab
        {
            ProfileGuid = s.Profile?.Guid,
            ProfileName = s.Profile?.DisplayName,
            Cwd         = _controls.TryGetValue(s.Id, out var c) ? c.CurrentCwd : null,
            CustomLabel = s.CustomLabel,
            IsPinned    = s.IsPinned,
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
        ctrl.BroadcastChanged      += _ =>
        {
            if (_sessions.Active?.Id == s.Id) UpdateStatusForActive();
        };
        ctrl.CwdChanged += cwd =>
        {
            if (_sessions.Active?.Id == s.Id) UpdateStatusForActive();
        };

        if (s.Profile?.CommandLine is not null)
            ctrl.StartShell(s.Profile.CommandLine, s.Profile.StartingDirectory);

        Tabs.Add(new TabItem { Index = s.Id, Profile = s.Profile, IsActive = false });
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
        var screenW = SystemParameters.PrimaryScreenWidth;
        var screenH = SystemParameters.PrimaryScreenHeight;
        Width  = Math.Max(400, screenW * Math.Clamp(_settings.WidthRatio,  0.1, 1.0));
        Height = Math.Max(200, screenH * Math.Clamp(_settings.HeightRatio, 0.1, 1.0));
        Left   = (screenW - Width) / 2.0;
        Top    = 0;
        Opacity = Math.Clamp(_settings.Opacity, 0.5, 1.0);
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
            if (string.IsNullOrEmpty(p.WtArgs)) continue;
            var supported = p.CommandLine is not null;
            var item = new MenuItem
            {
                Header = p.DisplayName + (supported ? "" : "  (no soportado)"),
                Tag = p,
                Icon = new TextBlock
                {
                    Text = ProfileIconHelper.GlyphFor(p),
                    FontFamily = new System.Windows.Media.FontFamily("Segoe UI Emoji"),
                    FontSize = 14
                },
                InputGestureText = shortcutIndex <= 9 ? $"Ctrl+Shift+{shortcutIndex}" : "",
                FontWeight = p.IsDefault ? FontWeights.Bold : FontWeights.Normal,
                IsEnabled = supported
            };
            if (supported)
            {
                var profileCapture = p;
                item.Click += (_, _) => _sessions.Create(profileCapture);
            }
            ProfileMenu.Items.Add(item);
            shortcutIndex++;
        }

        ProfileMenu.Items.Add(new Separator());

        var about = new MenuItem
        {
            Header = "Acerca de",
            Icon = new TextBlock { Text = "?", FontWeight = FontWeights.Bold, FontSize = 14 }
        };
        about.Click += (_, _) => MessageBox.Show(
            "WinKuake — drop-down terminal estilo Yakuake.\nMotor: ConPTY + xterm.js (WebView2).",
            "Acerca de WinKuake", MessageBoxButton.OK, MessageBoxImage.Information);
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
            var s = _sessions.Create(profile with { StartingDirectory = startDir });
            if (!string.IsNullOrEmpty(t.CustomLabel)) _sessions.Rename(s.Id, t.CustomLabel);
            if (t.IsPinned) _sessions.TogglePin(s.Id);
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
            ? "Lock OFF — la ventana se ocultará al perder foco. Click para activar lock."
            : "Lock ON — la ventana NO se oculta al perder foco. Click para desactivar.";
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
