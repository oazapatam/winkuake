using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class TerminalProfileTests
{
    [Fact]
    public void TerminalProfile_ToStringReturnsDisplayName()
    {
        var p = new TerminalProfile("Mi shell", "MyShell");
        Assert.Equal("Mi shell", p.ToString());
    }

    [Fact]
    public void TerminalProfile_RecordEquality()
    {
        var a = new TerminalProfile("X", "x-args");
        var b = new TerminalProfile("X", "x-args");
        var c = new TerminalProfile("Y", "x-args");
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void TerminalProfile_InitProperties()
    {
        var p = new TerminalProfile("Ubuntu", "Ubuntu")
        {
            Guid = "{aaa}", IconPath = null, IsDefault = true
        };
        Assert.Equal("{aaa}", p.Guid);
        Assert.True(p.IsDefault);
    }
}
