using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinKuake.Models;
using WinKuake.Services;

namespace WinKuake.Views;

public partial class SettingsWindow : Window
{
    private static readonly string[] CommonKeys =
    {
        "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12",
        "OemTilde","OemPlus","OemMinus","Space","Tab","Escape"
    };

    public AppSettings Result { get; private set; }

    public SettingsWindow(AppSettings current)
    {
        InitializeComponent();
        Result = Clone(current);

        CmbKey.ItemsSource = CommonKeys;
        CmbKey.SelectedItem = CommonKeys.Contains(current.HotkeyKey) ? current.HotkeyKey : "F12";

        ChkCtrl.IsChecked  = HasMod(current, "ctrl");
        ChkAlt.IsChecked   = HasMod(current, "alt");
        ChkShift.IsChecked = HasMod(current, "shift");
        ChkWin.IsChecked   = HasMod(current, "win");

        SldHeight.Value  = current.HeightRatio;
        SldWidth.Value   = current.WidthRatio;
        SldOpacity.Value = current.Opacity;

        ChkAutoHide.IsChecked = current.AutoHideOnFocusLost;
        ChkStartup.IsChecked  = current.StartWithWindows;
        TxtAnim.Text = current.AnimationMs.ToString(CultureInfo.InvariantCulture);

        TxtChromeBg.Text     = current.ChromeBackgroundHex;
        TxtChromeFg.Text     = current.ChromeForegroundHex;
        TxtChromeBorder.Text = current.ChromeBorderHex;
        TxtAccent.Text       = current.AccentHex;

        // Terminal: tema, fuente, scrollback. "Custom" se añade al final como pseudo-tema.
        var themeNames = TerminalTheme.All.Select(t => t.Name).Append("Custom").ToArray();
        CmbTheme.ItemsSource = themeNames;
        CmbTheme.SelectedItem = string.Equals(current.TerminalThemeName, "Custom", StringComparison.OrdinalIgnoreCase)
            ? "Custom"
            : TerminalTheme.FindOrDefault(current.TerminalThemeName).Name;

        LoadCustomThemeColors(current.CustomTerminalTheme ?? new TerminalThemeColors());
        UpdateCustomSwatches();
        UpdateCustomThemeVisibility();
        CmbTheme.SelectionChanged += (_, _) => UpdateCustomThemeVisibility();
        HookCustomThemeSwatchSync();

        SldFontSize.Value = Math.Clamp(current.TerminalFontSize, 8, 32);
        TxtFontSize.Text  = ((int)SldFontSize.Value).ToString(CultureInfo.InvariantCulture);
        SldFontSize.ValueChanged += (_, _) =>
            TxtFontSize.Text = ((int)SldFontSize.Value).ToString(CultureInfo.InvariantCulture);
        TxtFontSize.TextChanged += (_, _) =>
        {
            if (int.TryParse(TxtFontSize.Text, out var v) && v is >= 8 and <= 32)
                SldFontSize.Value = v;
        };

        SelectScrollbackItem(current.ScrollbackLines);

        // Snippets: bind a ObservableCollection editable.
        _snippetsView = new ObservableCollection<UserSnippet>(
            current.UserSnippets.Select(s => new UserSnippet { Name = s.Name, Command = s.Command }));
        SnippetsGrid.ItemsSource = _snippetsView;

        // Atajos: una fila por acción del catálogo, con el gesto resuelto (custom o default).
        _keybindingsView = new ObservableCollection<KeybindingRow>(
            KeybindingService.All.Select(a => new KeybindingRow
            {
                Id = a.Id,
                DisplayName = a.DisplayName,
                Gesture = KeybindingService.GetGesture(current, a.Id),
                DefaultGesture = a.DefaultGesture,
            }));
        KeybindingsGrid.ItemsSource = _keybindingsView;

        SyncBoxesFromSliders();
        UpdateSwatches();

        // Sync bidireccional slider <-> textbox. _suppressSync evita el rebote
        // cuando el evento del slider dispara escritura en el textbox y viceversa.
        SldHeight.ValueChanged  += (_, _) => { if (!_suppressSync) { _suppressSync = true; TxtHeight.Text  = ToPercentText(SldHeight.Value);  _suppressSync = false; } };
        SldWidth.ValueChanged   += (_, _) => { if (!_suppressSync) { _suppressSync = true; TxtWidth.Text   = ToPercentText(SldWidth.Value);   _suppressSync = false; } };
        SldOpacity.ValueChanged += (_, _) => { if (!_suppressSync) { _suppressSync = true; TxtOpacity.Text = ToPercentText(SldOpacity.Value); _suppressSync = false; } };

        TxtHeight.TextChanged  += (_, _) => SyncSliderFromBox(TxtHeight,  SldHeight);
        TxtWidth.TextChanged   += (_, _) => SyncSliderFromBox(TxtWidth,   SldWidth);
        TxtOpacity.TextChanged += (_, _) => SyncSliderFromBox(TxtOpacity, SldOpacity);

        TxtChromeBg.TextChanged     += (_, _) => UpdateSwatches();
        TxtChromeFg.TextChanged     += (_, _) => UpdateSwatches();
        TxtChromeBorder.TextChanged += (_, _) => UpdateSwatches();
        TxtAccent.TextChanged       += (_, _) => UpdateSwatches();
    }

    private bool _suppressSync;
    private ObservableCollection<UserSnippet> _snippetsView = new();
    private ObservableCollection<KeybindingRow> _keybindingsView = new();

    private void SyncBoxesFromSliders()
    {
        _suppressSync = true;
        TxtHeight.Text  = ToPercentText(SldHeight.Value);
        TxtWidth.Text   = ToPercentText(SldWidth.Value);
        TxtOpacity.Text = ToPercentText(SldOpacity.Value);
        _suppressSync = false;
    }

    private void SyncSliderFromBox(System.Windows.Controls.TextBox box, System.Windows.Controls.Slider slider)
    {
        if (_suppressSync) return;
        if (TryParsePercent(box.Text, out var ratio))
        {
            ratio = Math.Clamp(ratio, slider.Minimum, slider.Maximum);
            _suppressSync = true;
            slider.Value = ratio;
            _suppressSync = false;
        }
    }

    private static string ToPercentText(double ratio) =>
        Math.Round(ratio * 100).ToString("0", CultureInfo.InvariantCulture);

    private static bool TryParsePercent(string text, out double ratio)
    {
        ratio = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var clean = text.Trim().TrimEnd('%').Trim();
        if (!double.TryParse(clean, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) &&
            !double.TryParse(clean, NumberStyles.Float, CultureInfo.CurrentCulture,     out v))
            return false;
        ratio = v / 100.0;
        return true;
    }

    private void UpdateSwatches()
    {
        SwChromeBg.Background     = TryBrush(TxtChromeBg.Text);
        SwChromeFg.Background     = TryBrush(TxtChromeFg.Text);
        SwChromeBorder.Background = TryBrush(TxtChromeBorder.Text);
        SwAccent.Background       = TryBrush(TxtAccent.Text);
    }

    private static Brush TryBrush(string hex)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        }
        catch
        {
            return Brushes.Transparent;
        }
    }

    private static bool HasMod(AppSettings s, string mod) =>
        s.HotkeyModifiers.Any(m => string.Equals(m, mod, StringComparison.OrdinalIgnoreCase));

    private static AppSettings Clone(AppSettings s) => new()
    {
        HotkeyModifiers     = new List<string>(s.HotkeyModifiers),
        HotkeyKey           = s.HotkeyKey,
        HeightRatio         = s.HeightRatio,
        WidthRatio          = s.WidthRatio,
        Opacity             = s.Opacity,
        DefaultProfile      = s.DefaultProfile,
        AutoHideOnFocusLost = s.AutoHideOnFocusLost,
        StartWithWindows    = s.StartWithWindows,
        AnimationMs         = s.AnimationMs,
        MonitorIndex        = s.MonitorIndex,
        ChromeBackgroundHex = s.ChromeBackgroundHex,
        ChromeBorderHex     = s.ChromeBorderHex,
        ChromeForegroundHex = s.ChromeForegroundHex,
        AccentHex           = s.AccentHex,
        ScrollbackLines     = s.ScrollbackLines,
        TerminalThemeName   = s.TerminalThemeName,
        TerminalFontSize    = s.TerminalFontSize,
        UserSnippets        = s.UserSnippets.Select(x => new UserSnippet { Name = x.Name, Command = x.Command }).ToList(),
        CustomTerminalTheme = s.CustomTerminalTheme is null ? null : CloneCustomColors(s.CustomTerminalTheme),
        CustomKeybindings   = new Dictionary<string, string>(s.CustomKeybindings),
    };

    private static TerminalThemeColors CloneCustomColors(TerminalThemeColors c) => new()
    {
        Name = c.Name,
        Background = c.Background, Foreground = c.Foreground, Cursor = c.Cursor,
        Black = c.Black, Red = c.Red, Green = c.Green, Yellow = c.Yellow,
        Blue = c.Blue, Magenta = c.Magenta, Cyan = c.Cyan, White = c.White,
        BrightBlack = c.BrightBlack, BrightRed = c.BrightRed,
        BrightGreen = c.BrightGreen, BrightYellow = c.BrightYellow,
        BrightBlue = c.BrightBlue, BrightMagenta = c.BrightMagenta,
        BrightCyan = c.BrightCyan, BrightWhite = c.BrightWhite,
    };

    private void SelectScrollbackItem(int value)
    {
        foreach (var obj in CmbScrollback.Items)
        {
            if (obj is ComboBoxItem item && item.Tag is string tag
                && int.TryParse(tag, out var v) && v == value)
            {
                CmbScrollback.SelectedItem = item;
                return;
            }
        }
        // Si el valor guardado no está en la lista, dejamos "Ilimitado" (-1).
        CmbScrollback.SelectedIndex = 0;
    }

    private int ReadScrollback()
    {
        if (CmbScrollback.SelectedItem is ComboBoxItem item && item.Tag is string tag
            && int.TryParse(tag, out var v))
            return v;
        return -1;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        Result.HotkeyKey = CmbKey.SelectedItem?.ToString() ?? "F12";

        var mods = new List<string>();
        if (ChkCtrl.IsChecked  == true) mods.Add("Ctrl");
        if (ChkAlt.IsChecked   == true) mods.Add("Alt");
        if (ChkShift.IsChecked == true) mods.Add("Shift");
        if (ChkWin.IsChecked   == true) mods.Add("Win");
        Result.HotkeyModifiers = mods;

        Result.HeightRatio  = SldHeight.Value;
        Result.WidthRatio   = SldWidth.Value;
        Result.Opacity      = SldOpacity.Value;
        Result.AutoHideOnFocusLost = ChkAutoHide.IsChecked == true;
        Result.StartWithWindows    = ChkStartup.IsChecked  == true;

        if (int.TryParse(TxtAnim.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ms)
            && ms is >= 0 and <= 2000)
            Result.AnimationMs = ms;

        Result.ChromeBackgroundHex = NormalizeHex(TxtChromeBg.Text,     Result.ChromeBackgroundHex);
        Result.ChromeForegroundHex = NormalizeHex(TxtChromeFg.Text,     Result.ChromeForegroundHex);
        Result.ChromeBorderHex     = NormalizeHex(TxtChromeBorder.Text, Result.ChromeBorderHex);
        Result.AccentHex           = NormalizeHex(TxtAccent.Text,       Result.AccentHex);

        Result.TerminalThemeName = CmbTheme.SelectedItem?.ToString() ?? "VSCode Dark+";
        Result.TerminalFontSize  = (int)SldFontSize.Value;
        Result.ScrollbackLines   = ReadScrollback();

        // Snippets: persistimos solo los que tienen al menos nombre y comando.
        Result.UserSnippets = _snippetsView
            .Where(s => !string.IsNullOrWhiteSpace(s.Name) && !string.IsNullOrWhiteSpace(s.Command))
            .Select(s => new UserSnippet { Name = s.Name.Trim(), Command = s.Command.Trim() })
            .ToList();

        // Paleta custom: solo persistimos si hay algo que vale la pena guardar.
        Result.CustomTerminalTheme = ReadCustomColorsFromUi();

        // Atajos: persistimos solo los que difieren del default. Vacío = restaurar default.
        var customs = new Dictionary<string, string>();
        foreach (var row in _keybindingsView)
        {
            var g = (row.Gesture ?? "").Trim();
            if (!string.IsNullOrWhiteSpace(g) && !string.Equals(g, row.DefaultGesture, StringComparison.OrdinalIgnoreCase))
                customs[row.Id] = g;
        }
        Result.CustomKeybindings = customs;

        DialogResult = true;
        Close();
    }

    private void UpdateCustomThemeVisibility()
    {
        var isCustom = string.Equals(CmbTheme.SelectedItem?.ToString(), "Custom", StringComparison.OrdinalIgnoreCase);
        CustomThemePanel.Visibility = isCustom ? Visibility.Visible : Visibility.Collapsed;
    }

    private void LoadCustomThemeColors(TerminalThemeColors c)
    {
        TxtCtName.Text     = c.Name;
        TxtCtBg.Text       = c.Background;
        TxtCtFg.Text       = c.Foreground;
        TxtCtCursor.Text   = c.Cursor;
        TxtCtBlack.Text    = c.Black;
        TxtCtRed.Text      = c.Red;
        TxtCtGreen.Text    = c.Green;
        TxtCtYellow.Text   = c.Yellow;
        TxtCtBlue.Text     = c.Blue;
        TxtCtMagenta.Text  = c.Magenta;
        TxtCtCyan.Text     = c.Cyan;
        TxtCtWhite.Text    = c.White;
        TxtCtBBlack.Text   = c.BrightBlack;
        TxtCtBRed.Text     = c.BrightRed;
        TxtCtBGreen.Text   = c.BrightGreen;
        TxtCtBYellow.Text  = c.BrightYellow;
        TxtCtBBlue.Text    = c.BrightBlue;
        TxtCtBMagenta.Text = c.BrightMagenta;
        TxtCtBCyan.Text    = c.BrightCyan;
        TxtCtBWhite.Text   = c.BrightWhite;
    }

    private TerminalThemeColors ReadCustomColorsFromUi() => new()
    {
        Name          = string.IsNullOrWhiteSpace(TxtCtName.Text) ? "Custom" : TxtCtName.Text.Trim(),
        Background    = TxtCtBg.Text,
        Foreground    = TxtCtFg.Text,
        Cursor        = TxtCtCursor.Text,
        Black         = TxtCtBlack.Text,
        Red           = TxtCtRed.Text,
        Green         = TxtCtGreen.Text,
        Yellow        = TxtCtYellow.Text,
        Blue          = TxtCtBlue.Text,
        Magenta       = TxtCtMagenta.Text,
        Cyan          = TxtCtCyan.Text,
        White         = TxtCtWhite.Text,
        BrightBlack   = TxtCtBBlack.Text,
        BrightRed     = TxtCtBRed.Text,
        BrightGreen   = TxtCtBGreen.Text,
        BrightYellow  = TxtCtBYellow.Text,
        BrightBlue    = TxtCtBBlue.Text,
        BrightMagenta = TxtCtBMagenta.Text,
        BrightCyan    = TxtCtBCyan.Text,
        BrightWhite   = TxtCtBWhite.Text,
    };

    private void HookCustomThemeSwatchSync()
    {
        // Cada TextBox del editor refresca su swatch al cambiar.
        TxtCtBg.TextChanged       += (_, _) => SwCtBg.Background       = TryBrush(TxtCtBg.Text);
        TxtCtFg.TextChanged       += (_, _) => SwCtFg.Background       = TryBrush(TxtCtFg.Text);
        TxtCtCursor.TextChanged   += (_, _) => SwCtCursor.Background   = TryBrush(TxtCtCursor.Text);
        TxtCtBlack.TextChanged    += (_, _) => SwCtBlack.Background    = TryBrush(TxtCtBlack.Text);
        TxtCtRed.TextChanged      += (_, _) => SwCtRed.Background      = TryBrush(TxtCtRed.Text);
        TxtCtGreen.TextChanged    += (_, _) => SwCtGreen.Background    = TryBrush(TxtCtGreen.Text);
        TxtCtYellow.TextChanged   += (_, _) => SwCtYellow.Background   = TryBrush(TxtCtYellow.Text);
        TxtCtBlue.TextChanged     += (_, _) => SwCtBlue.Background     = TryBrush(TxtCtBlue.Text);
        TxtCtMagenta.TextChanged  += (_, _) => SwCtMagenta.Background  = TryBrush(TxtCtMagenta.Text);
        TxtCtCyan.TextChanged     += (_, _) => SwCtCyan.Background     = TryBrush(TxtCtCyan.Text);
        TxtCtWhite.TextChanged    += (_, _) => SwCtWhite.Background    = TryBrush(TxtCtWhite.Text);
        TxtCtBBlack.TextChanged   += (_, _) => SwCtBBlack.Background   = TryBrush(TxtCtBBlack.Text);
        TxtCtBRed.TextChanged     += (_, _) => SwCtBRed.Background     = TryBrush(TxtCtBRed.Text);
        TxtCtBGreen.TextChanged   += (_, _) => SwCtBGreen.Background   = TryBrush(TxtCtBGreen.Text);
        TxtCtBYellow.TextChanged  += (_, _) => SwCtBYellow.Background  = TryBrush(TxtCtBYellow.Text);
        TxtCtBBlue.TextChanged    += (_, _) => SwCtBBlue.Background    = TryBrush(TxtCtBBlue.Text);
        TxtCtBMagenta.TextChanged += (_, _) => SwCtBMagenta.Background = TryBrush(TxtCtBMagenta.Text);
        TxtCtBCyan.TextChanged    += (_, _) => SwCtBCyan.Background    = TryBrush(TxtCtBCyan.Text);
        TxtCtBWhite.TextChanged   += (_, _) => SwCtBWhite.Background   = TryBrush(TxtCtBWhite.Text);
    }

    private void UpdateCustomSwatches()
    {
        SwCtBg.Background       = TryBrush(TxtCtBg.Text);
        SwCtFg.Background       = TryBrush(TxtCtFg.Text);
        SwCtCursor.Background   = TryBrush(TxtCtCursor.Text);
        SwCtBlack.Background    = TryBrush(TxtCtBlack.Text);
        SwCtRed.Background      = TryBrush(TxtCtRed.Text);
        SwCtGreen.Background    = TryBrush(TxtCtGreen.Text);
        SwCtYellow.Background   = TryBrush(TxtCtYellow.Text);
        SwCtBlue.Background     = TryBrush(TxtCtBlue.Text);
        SwCtMagenta.Background  = TryBrush(TxtCtMagenta.Text);
        SwCtCyan.Background     = TryBrush(TxtCtCyan.Text);
        SwCtWhite.Background    = TryBrush(TxtCtWhite.Text);
        SwCtBBlack.Background   = TryBrush(TxtCtBBlack.Text);
        SwCtBRed.Background     = TryBrush(TxtCtBRed.Text);
        SwCtBGreen.Background   = TryBrush(TxtCtBGreen.Text);
        SwCtBYellow.Background  = TryBrush(TxtCtBYellow.Text);
        SwCtBBlue.Background    = TryBrush(TxtCtBBlue.Text);
        SwCtBMagenta.Background = TryBrush(TxtCtBMagenta.Text);
        SwCtBCyan.Background    = TryBrush(TxtCtBCyan.Text);
        SwCtBWhite.Background   = TryBrush(TxtCtBWhite.Text);
    }

    private static string NormalizeHex(string input, string fallback)
    {
        try
        {
            _ = (Color)ColorConverter.ConvertFromString(input);
            return input.Trim();
        }
        catch
        {
            return fallback;
        }
    }

    private void ResetSkin_Click(object sender, RoutedEventArgs e)
    {
        TxtChromeBg.Text     = "#1E1E1E";
        TxtChromeFg.Text     = "#E6E6E6";
        TxtChromeBorder.Text = "#3C3C3C";
        TxtAccent.Text       = "#0E7AB5";
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}

/// <summary>Fila del DataGrid de atajos. Mantiene Id (no visible) y el gesto editable.</summary>
public class KeybindingRow
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Gesture { get; set; } = "";
    public string DefaultGesture { get; set; } = "";
}
