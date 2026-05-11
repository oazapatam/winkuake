using System.Linq;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 13 — Tab "Atajos" / KeybindingService. Verifica los gestos
/// por defecto que el plan promete y la lógica de resolución diff-vs-default.
/// </summary>
public class KeybindingAuditTests
{
    [Fact]
    public void DefaultGestures_CoincidenConLoQueDicePlanFases6_8_9_13()
    {
        // Cada acción del catálogo tiene un default. El plan documenta cada gesto.
        var byId = KeybindingService.All.ToDictionary(a => a.Id);

        Assert.Equal("F12",              byId["Hotkey"].DefaultGesture);
        Assert.Equal("Ctrl+Shift+W",     byId["ClosePane"].DefaultGesture);
        Assert.Equal("Alt+Shift+=",      byId["SplitVertical"].DefaultGesture);
        Assert.Equal("Alt+Shift+-",      byId["SplitHorizontal"].DefaultGesture);
        Assert.Equal("Ctrl+Shift+P",     byId["Palette"].DefaultGesture);
        Assert.Equal("Ctrl+Shift+B",     byId["Broadcast"].DefaultGesture);
        Assert.Equal("Ctrl+Shift+Alt+F", byId["GlobalFind"].DefaultGesture);
        Assert.Equal("Ctrl+Shift+S",     byId["SaveBuffer"].DefaultGesture);
    }

    [Fact]
    public void TodasLasAcciones_TienenIdNoVacio()
    {
        foreach (var a in KeybindingService.All)
            Assert.False(string.IsNullOrWhiteSpace(a.Id));
    }

    [Fact]
    public void GetGesture_DistingueCustomVacio()
    {
        // Si el usuario fija un valor vacío para un id, debe caer al default
        // (vacío significa "restaurar default").
        var s = new AppSettings
        {
            CustomKeybindings = new() { ["NewTab"] = "" }
        };
        Assert.Equal("Ctrl+Shift+T", KeybindingService.GetGesture(s, "NewTab"));
    }

    [Fact]
    public void GetGesture_WhitespaceCustom_FallbackADefault()
    {
        var s = new AppSettings
        {
            CustomKeybindings = new() { ["Hotkey"] = "   " }
        };
        Assert.Equal("F12", KeybindingService.GetGesture(s, "Hotkey"));
    }

    [Fact]
    public void GetGesture_CustomConValor_LoDevuelveTalCual()
    {
        var s = new AppSettings
        {
            CustomKeybindings = new() { ["Palette"] = "Ctrl+P" }
        };
        Assert.Equal("Ctrl+P", KeybindingService.GetGesture(s, "Palette"));
    }

    [Fact]
    public void All_NoDuplicaIds()
    {
        var ids = KeybindingService.All.Select(a => a.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void All_NoDuplicaDefaultGestures()
    {
        // Si dos acciones compartieran el mismo gesto por defecto, sería un
        // conflicto inmediato al activarse runtime application of customs.
        var gestures = KeybindingService.All.Select(a => a.DefaultGesture).ToList();
        Assert.Equal(gestures.Count, gestures.Distinct().Count());
    }
}
