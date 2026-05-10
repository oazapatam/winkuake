using WinKuake;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class TabItemTests
{
    [Fact]
    public void DisplayLabel_ShowsProfileName()
    {
        var tab = new TabItem
        {
            Index = 2,
            Profile = new TerminalProfile("PowerShell", "PowerShell")
        };
        Assert.Equal("PowerShell", tab.DisplayLabel);
    }

    [Fact]
    public void DisplayLabel_FallsBackToShellWhenNoProfile()
    {
        var tab = new TabItem { Index = 1, Profile = null };
        Assert.Equal("Shell", tab.DisplayLabel);
    }

    [Fact]
    public void DisplayLabel_HonoursCustomLabel()
    {
        var tab = new TabItem
        {
            Index = 3,
            Profile = new TerminalProfile("PowerShell", "PowerShell"),
            CustomLabel = "build server"
        };
        Assert.Equal("build server", tab.DisplayLabel);
    }

    [Fact]
    public void IsActive_RaisesPropertyChanged()
    {
        var tab = new TabItem { Index = 1 };
        var fired = false;
        tab.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(TabItem.IsActive)) fired = true; };
        tab.IsActive = true;
        Assert.True(fired);
    }

    [Fact]
    public void Profile_ChangeUpdatesLabel()
    {
        var tab = new TabItem { Index = 1, Profile = null };
        var labelChanged = 0;
        tab.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(TabItem.DisplayLabel)) labelChanged++; };
        tab.Profile = new TerminalProfile("Bash", "Git Bash");
        Assert.True(labelChanged > 0);
        Assert.Equal("Bash", tab.DisplayLabel);
    }

    [Fact]
    public void CustomLabel_OverridesProfileBasedLabel()
    {
        var tab = new TabItem
        {
            Index = 1,
            Profile = new TerminalProfile("PowerShell", "PowerShell")
        };
        Assert.Equal("PowerShell", tab.DisplayLabel);
        tab.CustomLabel = "django dev";
        Assert.Equal("django dev", tab.DisplayLabel);
        tab.CustomLabel = null;
        Assert.Equal("PowerShell", tab.DisplayLabel);
    }

    [Fact]
    public void IconGlyph_ReflectsProfileType()
    {
        Assert.Equal("⚡", new TabItem { Profile = new TerminalProfile("Windows PowerShell", "") }.IconGlyph);
        Assert.Equal("🐧", new TabItem { Profile = new TerminalProfile("Ubuntu", "") }.IconGlyph);
        Assert.Equal("≫", new TabItem { Profile = new TerminalProfile("Símbolo del sistema", "") }.IconGlyph);
        Assert.Equal("☁", new TabItem { Profile = new TerminalProfile("Azure Cloud Shell", "") }.IconGlyph);
    }
}
