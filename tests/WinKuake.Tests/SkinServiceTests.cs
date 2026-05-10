using System.Windows;
using System.Windows.Media;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class SkinServiceTests
{
    private static Application EnsureApp() => Application.Current ?? new Application();

    [Fact]
    public void Apply_SetsAllFourBrushKeys()
    {
        StaTestRunner.OnStaThread(() =>
        {
            var app = EnsureApp();
            var settings = new AppSettings
            {
                ChromeBackgroundHex = "#101010",
                ChromeBorderHex     = "#202020",
                ChromeForegroundHex = "#FAFAFA",
                AccentHex           = "#FF0099"
            };

            SkinService.Apply(settings);

            Assert.IsType<SolidColorBrush>(app.Resources["ChromeBackground"]);
            Assert.IsType<SolidColorBrush>(app.Resources["ChromeBorder"]);
            Assert.IsType<SolidColorBrush>(app.Resources["ChromeForeground"]);
            Assert.IsType<SolidColorBrush>(app.Resources["AccentBrush"]);

            var bg = (SolidColorBrush)app.Resources["ChromeBackground"];
            Assert.Equal((Color)ColorConverter.ConvertFromString("#101010"), bg.Color);
        });
    }

    [Fact]
    public void Apply_InvalidHex_FallsBackToDefault()
    {
        StaTestRunner.OnStaThread(() =>
        {
            var app = EnsureApp();
            var settings = new AppSettings
            {
                ChromeBackgroundHex = "no-es-un-color",
                ChromeBorderHex     = "#3C3C3C",
                ChromeForegroundHex = "#E6E6E6",
                AccentHex           = "#0E7AB5"
            };

            SkinService.Apply(settings);

            var bg = (SolidColorBrush)app.Resources["ChromeBackground"];
            Assert.Equal((Color)ColorConverter.ConvertFromString("#1E1E1E"), bg.Color);
        });
    }

    [Fact]
    public void Apply_FreezesBrushes_ForCrossThreadSafety()
    {
        StaTestRunner.OnStaThread(() =>
        {
            var app = EnsureApp();
            SkinService.Apply(new AppSettings());
            var bg = (SolidColorBrush)app.Resources["ChromeBackground"];
            Assert.True(bg.IsFrozen);
        });
    }
}
