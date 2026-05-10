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

        BuildProfileMenu();
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

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        var ok = _hotkey.TryRegister(hwnd, _settings, out var info);
        if (!ok) CrashLogger.Info($"Hotkey no registrado ({HotkeyDisplay()}): {info}");
        else if (_hotkey.UsingLowLevelHook) CrashLogger.Info($"Hotkey {HotkeyDisplay()} via low-level hook. {info}");
        _hotkey.HotkeyPressed += ToggleVisibility;

        _animator = new WindowAnimator(this);
        UpdateLockButtonGlyph();
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
                var initial = DefaultProfile();
                _sessions.Create(initial);
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

    // -- Manager → UI sync ------------------------------------------------

    private void OnSessionAdded(TerminalSession s)
    {
        var ctrl = new TerminalControl { Visibility = Visibility.Collapsed };
        _controls[s.Id] = ctrl;
        TerminalContainer.Children.Add(ctrl);
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

        // Status bar refleja el perfil activo.
        if (active?.Profile is { } p)
            StatusTitle.Text = $"{ProfileIconHelper.GlyphFor(p)}  {p.DisplayName}";
        else
            StatusTitle.Text = "WinKuake";
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
    private void SplitVertical_Click(object sender, RoutedEventArgs e) { /* pendiente Fase 4 */ }
    private void SplitHorizontal_Click(object sender, RoutedEventArgs e) { /* pendiente Fase 4 */ }

    private void CloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (_sessions.Active is { } a) _sessions.Close(a.Id);
    }

    private void CloseSpecificTab_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b && b.Tag is int id) _sessions.Close(id);
    }

    private void Tab_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe || fe.DataContext is not TabItem tab) return;
        if (e.ClickCount == 2) { PromptRename(tab); return; }
        _sessions.SetActive(tab.Index);
    }

    private void Tab_RightDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TabItem tab)
            _sessions.Close(tab.Index);
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

    private void OnClosed(object? sender, EventArgs e) => _hotkey.Dispose();

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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
