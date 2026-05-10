using System.Windows;
using System.Windows.Media;
using WinKuake.Models;

namespace WinKuake.Services;

/// <summary>
/// Skin trivial: traduce los colores de <see cref="AppSettings"/> en SolidColorBrushes
/// y los inyecta en los recursos de la aplicación con las mismas claves que usa
/// <c>App.xaml</c> (ChromeBackground, ChromeBorder, ChromeForeground, AccentBrush).
/// El XAML los referencia con <c>DynamicResource</c> para repintarse en caliente.
/// </summary>
public static class SkinService
{
    public static void Apply(AppSettings settings)
    {
        var res = Application.Current.Resources;
        res["ChromeBackground"] = Brush(settings.ChromeBackgroundHex, "#1E1E1E");
        res["ChromeBorder"]     = Brush(settings.ChromeBorderHex,     "#3C3C3C");
        res["ChromeForeground"] = Brush(settings.ChromeForegroundHex, "#E6E6E6");
        res["AccentBrush"]      = Brush(settings.AccentHex,           "#0E7AB5");
    }

    private static SolidColorBrush Brush(string hex, string fallback)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var b = new SolidColorBrush(color);
            b.Freeze();
            return b;
        }
        catch
        {
            var color = (Color)ColorConverter.ConvertFromString(fallback);
            var b = new SolidColorBrush(color);
            b.Freeze();
            return b;
        }
    }
}
