using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class HotkeyServiceTests
{
    [Fact]
    public void DetectLikelyCulprit_ReturnsKnownProcessOrNull()
    {
        // No podemos garantizar que haya un sospechoso corriendo en el CI,
        // pero la función debe devolver string o null sin tirar excepción.
        var result = HotkeyService.DetectLikelyCulprit();
        Assert.True(result is null || !string.IsNullOrWhiteSpace(result));
    }

    [Fact]
    public void Service_StartsInactiveBeforeRegister()
    {
        using var hk = new HotkeyService();
        Assert.False(hk.IsActive);
        Assert.False(hk.UsingLowLevelHook);
    }

    [Fact]
    public void Dispose_DoesNotThrowWhenNeverRegistered()
    {
        var hk = new HotkeyService();
        var ex = Record.Exception(() => hk.Dispose());
        Assert.Null(ex);
    }

    [Fact]
    public void Dispose_IsIdempotent()
    {
        var hk = new HotkeyService();
        hk.Dispose();
        var ex = Record.Exception(() => hk.Dispose());
        Assert.Null(ex);
    }
}
