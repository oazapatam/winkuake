using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class GeometryRatiosTests
{
    [Fact]
    public void FromSize_RatiosCorrectos_FullScreen()
    {
        var (w, h) = GeometryRatios.FromSize(1920, 1080, 1920, 1080);
        Assert.Equal(1.0, w);
        Assert.Equal(1.0, h);
    }

    [Fact]
    public void FromSize_RatiosCorrectos_Mitad()
    {
        var (w, h) = GeometryRatios.FromSize(960, 540, 1920, 1080);
        Assert.Equal(0.5, w);
        Assert.Equal(0.5, h);
    }

    [Fact]
    public void FromSize_ClampInferior()
    {
        // Ventana minimizada o degenerada: clamp a 0.1, no a 0 (evita
        // persistir un settings que dejaría la ventana invisible).
        var (w, h) = GeometryRatios.FromSize(0, 0, 1920, 1080);
        Assert.Equal(0.1, w);
        Assert.Equal(0.1, h);
    }

    [Fact]
    public void FromSize_ClampSuperior()
    {
        var (w, h) = GeometryRatios.FromSize(3000, 2000, 1920, 1080);
        Assert.Equal(1.0, w);
        Assert.Equal(1.0, h);
    }

    [Fact]
    public void FromSize_ScreenZeroNoExplota()
    {
        var (w, h) = GeometryRatios.FromSize(960, 540, 0, 0);
        // Con screen=1, 960/1 = 960 → clamp a 1.0.
        Assert.Equal(1.0, w);
        Assert.Equal(1.0, h);
    }

    [Theory]
    [InlineData(0.5, 0.5,  false)]   // Mismo valor: no save.
    [InlineData(0.5, 0.502, false)]  // Diff < epsilon: no save.
    [InlineData(0.5, 0.51, true)]    // Diff > epsilon: save.
    [InlineData(0.5, 0.8,  true)]    // Diff grande: save.
    public void RatiosDifferEnoughToSave_RespetaEpsilon(double oldR, double newR, bool expected)
    {
        Assert.Equal(expected, GeometryRatios.RatiosDifferEnoughToSave(oldR, newR));
    }

    [Fact]
    public void FromSize_Asimetrico()
    {
        // Caso real: ventana 80% alto, 100% ancho.
        var (w, h) = GeometryRatios.FromSize(1920, 864, 1920, 1080);
        Assert.Equal(1.0, w);
        Assert.Equal(0.8, h);
    }
}
