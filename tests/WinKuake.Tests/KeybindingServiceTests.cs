using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class KeybindingServiceTests
{
    [Fact]
    public void All_ContainsAtLeastNineActions()
    {
        Assert.True(KeybindingService.All.Count >= 9,
            $"Esperaba >= 9 acciones, obtuve {KeybindingService.All.Count}");
    }

    [Fact]
    public void All_IncludesExpectedActionIds()
    {
        var ids = KeybindingService.All.Select(a => a.Id).ToHashSet();
        Assert.Contains("Hotkey", ids);
        Assert.Contains("NewTab", ids);
        Assert.Contains("ClosePane", ids);
        Assert.Contains("SplitVertical", ids);
        Assert.Contains("SplitHorizontal", ids);
        Assert.Contains("Palette", ids);
        Assert.Contains("Broadcast", ids);
        Assert.Contains("GlobalFind", ids);
        Assert.Contains("SaveBuffer", ids);
    }

    [Fact]
    public void All_ActionsHaveDisplayNameAndDefault()
    {
        foreach (var a in KeybindingService.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(a.DisplayName), $"{a.Id} sin DisplayName");
            Assert.False(string.IsNullOrWhiteSpace(a.DefaultGesture), $"{a.Id} sin DefaultGesture");
        }
    }

    [Fact]
    public void GetGesture_ReturnsCustomWhenSet()
    {
        var s = new AppSettings
        {
            CustomKeybindings = new Dictionary<string, string> { ["NewTab"] = "Ctrl+Alt+N" }
        };
        Assert.Equal("Ctrl+Alt+N", KeybindingService.GetGesture(s, "NewTab"));
    }

    [Fact]
    public void GetGesture_FallsBackToDefaultWhenNotSet()
    {
        var s = new AppSettings();
        var hotkey = KeybindingService.All.First(a => a.Id == "Hotkey");
        Assert.Equal(hotkey.DefaultGesture, KeybindingService.GetGesture(s, "Hotkey"));
    }

    [Fact]
    public void GetGesture_UnknownActionReturnsEmpty()
    {
        var s = new AppSettings();
        Assert.Equal(string.Empty, KeybindingService.GetGesture(s, "NoExiste"));
    }

    [Fact]
    public void CustomKeybindings_JsonRoundtrip()
    {
        var src = new AppSettings
        {
            CustomKeybindings = new Dictionary<string, string>
            {
                ["NewTab"] = "Ctrl+Alt+N",
                ["ClosePane"] = "Ctrl+Shift+Q",
                ["Hotkey"] = "F11",
            }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(src, opts);
        var dst = JsonSerializer.Deserialize<AppSettings>(json, opts)!;

        Assert.Equal(3, dst.CustomKeybindings.Count);
        Assert.Equal("Ctrl+Alt+N", dst.CustomKeybindings["NewTab"]);
        Assert.Equal("Ctrl+Shift+Q", dst.CustomKeybindings["ClosePane"]);
        Assert.Equal("F11", dst.CustomKeybindings["Hotkey"]);
    }

    [Fact]
    public void KeybindingAction_IsRecord()
    {
        var a = new KeybindingAction("Id", "Name", "Ctrl+X");
        var b = new KeybindingAction("Id", "Name", "Ctrl+X");
        Assert.Equal(a, b);
    }
}
