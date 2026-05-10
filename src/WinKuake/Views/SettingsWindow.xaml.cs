using System;
using System.Collections.Generic;
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

        // Terminal: tema, fuente, scrollback.
        CmbTheme.ItemsSource = TerminalTheme.All.Select(t => t.Name).ToArray();
        CmbTheme.SelectedItem = TerminalTheme.FindOrDefault(current.TerminalThemeName).Name;

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

        DialogResult = true;
        Close();
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
