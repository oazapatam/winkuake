using System.Linq;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class WslServiceTests
{
    [Fact]
    public void ParseListVerbose_ExtractsDistros()
    {
        // Output típico de `wsl.exe -l --verbose` (UTF-16, con "* " para default).
        var raw = "  NAME            STATE           VERSION\n" +
                  "* Ubuntu          Running         2\n" +
                  "  Debian          Stopped         2\n" +
                  "  kali-linux      Stopped         2\n";
        var list = WslService.ParseListVerbose(raw);
        Assert.Equal(3, list.Count);
        Assert.Equal("Ubuntu",     list[0].Name);
        Assert.True(list[0].IsDefault);
        Assert.Equal("Running",    list[0].State);
        Assert.Equal(2,            list[0].Version);

        Assert.Equal("Debian",     list[1].Name);
        Assert.False(list[1].IsDefault);
        Assert.Equal("Stopped",    list[1].State);

        Assert.Equal("kali-linux", list[2].Name);
    }

    [Fact]
    public void ParseListVerbose_IgnoresEmptyAndDockerInternalDistros()
    {
        // docker-desktop y docker-desktop-data son internos y no se muestran como shell.
        var raw = "  NAME                  STATE           VERSION\n" +
                  "* Ubuntu                Running         2\n" +
                  "  docker-desktop        Stopped         2\n" +
                  "  docker-desktop-data   Stopped         2\n";
        var list = WslService.ParseListVerbose(raw);
        Assert.Single(list);
        Assert.Equal("Ubuntu", list[0].Name);
    }

    [Fact]
    public void ParseListVerbose_HandlesEmptyInput()
    {
        Assert.Empty(WslService.ParseListVerbose(""));
        Assert.Empty(WslService.ParseListVerbose("  NAME  STATE  VERSION\n"));
    }

    [Fact]
    public void ParseListVerbose_StripsNullBytes()
    {
        // wsl.exe a veces emite UTF-16 con \0 sueltos cuando se lee mal.
        var raw = "  NAME\0\0\n* Ubuntu\0  Running  2\n";
        var list = WslService.ParseListVerbose(raw);
        Assert.Single(list);
        Assert.Equal("Ubuntu", list[0].Name);
        Assert.True(list[0].IsDefault);
    }

    // IMPORTANTE: wsl.exe NO usa el parsing CRT estándar; las comillas se
    // pasan literales al binario. Por eso NO comillamos ni el nombre de distro
    // ni el path de --cd. Esto limita el soporte de nombres/paths con espacios.

    [Fact]
    public void BuildCommandLine_LoginShell_UsesShellTypeLogin()
    {
        var cmd = WslService.BuildCommandLine("Ubuntu", loginShell: true);
        Assert.Contains("-d Ubuntu", cmd);
        Assert.DoesNotContain("\"Ubuntu\"", cmd);
        Assert.Contains("--shell-type login", cmd);
    }

    [Fact]
    public void BuildCommandLine_NonLogin_OmitsShellType()
    {
        var cmd = WslService.BuildCommandLine("Ubuntu", loginShell: false);
        Assert.Contains("-d Ubuntu", cmd);
        Assert.DoesNotContain("--shell-type", cmd);
    }

    [Fact]
    public void BuildCommandLine_StartsAtHome_AddsCdHome()
    {
        var cmd = WslService.BuildCommandLine("Ubuntu", loginShell: true, startAtHome: true);
        Assert.Contains("--cd ~", cmd);
    }

    [Fact]
    public void BuildCommandLine_WithStartingDirectoryWindows_TranslatesPathUnquoted()
    {
        var cmd = WslService.BuildCommandLine("Ubuntu", loginShell: true,
            windowsStartingDirectory: @"C:\Users\andre\projects");
        Assert.Contains("--cd /mnt/c/Users/andre/projects", cmd);
        Assert.DoesNotContain("--cd \"", cmd);
    }

    [Fact]
    public void BuildCommandLine_WithStartingDirectoryUnix_KeepsAsIs()
    {
        var cmd = WslService.BuildCommandLine("Ubuntu", loginShell: true,
            windowsStartingDirectory: "/home/andre/code");
        Assert.Contains("--cd /home/andre/code", cmd);
    }

    [Fact]
    public void BuildCommandLine_NoQuotesEver()
    {
        var cmd = WslService.BuildCommandLine("kali-linux", loginShell: true,
            windowsStartingDirectory: @"D:\workspace");
        Assert.DoesNotContain("\"", cmd);
    }

    [Theory]
    [InlineData(@"C:\Users\andre",              "/mnt/c/Users/andre")]
    [InlineData(@"C:\",                         "/mnt/c/")]
    [InlineData(@"D:\workspace\repo",           "/mnt/d/workspace/repo")]
    [InlineData(@"C:\Program Files\Git",        "/mnt/c/Program Files/Git")]
    [InlineData("/home/user",                   "/home/user")]
    [InlineData("",                             "")]
    public void TranslateWindowsPathToWsl_ConvertsCorrectly(string input, string expected)
    {
        Assert.Equal(expected, WslService.TranslateWindowsPathToWsl(input));
    }
}
