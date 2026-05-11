using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase WSL — fija invariantes de parsing y construcción de
/// commandlines no cubiertos antes (rancher-desktop, formato compacto sin
/// espacios dobles, traducción de paths con espacios, etc.).
/// </summary>
public class WslServiceAuditTests
{
    [Fact]
    public void ParseListVerbose_FiltersRancherDesktopDistros()
    {
        // Rancher Desktop (alternativa a Docker Desktop) también pone distros
        // internas que no queremos exponer.
        var raw = "  NAME                    STATE           VERSION\n" +
                  "* Ubuntu                  Running         2\n" +
                  "  rancher-desktop         Stopped         2\n" +
                  "  rancher-desktop-data    Stopped         2\n";
        var list = WslService.ParseListVerbose(raw);
        Assert.Single(list);
        Assert.Equal("Ubuntu", list[0].Name);
    }

    [Fact]
    public void ParseListVerbose_NoDefaultMarker_AllNonDefault()
    {
        var raw = "  NAME      STATE     VERSION\n" +
                  "  Ubuntu    Running   2\n" +
                  "  Debian    Stopped   2\n";
        var list = WslService.ParseListVerbose(raw);
        Assert.Equal(2, list.Count);
        Assert.False(list[0].IsDefault);
        Assert.False(list[1].IsDefault);
    }

    [Fact]
    public void ParseListVerbose_OnlyHeader_ReturnsEmpty()
    {
        var raw = "  NAME            STATE           VERSION\n";
        Assert.Empty(WslService.ParseListVerbose(raw));
    }

    [Fact]
    public void BuildCommandLine_StartingDirOverridesStartAtHome()
    {
        // Si pasamos windowsStartingDirectory != null, gana sobre startAtHome.
        var cmd = WslService.BuildCommandLine(
            "Ubuntu",
            loginShell: true,
            startAtHome: true,
            windowsStartingDirectory: @"C:\repos");
        Assert.Contains("--cd /mnt/c/repos", cmd);
        Assert.DoesNotContain("--cd ~", cmd);
    }

    [Fact]
    public void BuildCommandLine_NoStartingDir_NoStartAtHome_NoCdFlag()
    {
        var cmd = WslService.BuildCommandLine("Ubuntu", loginShell: false, startAtHome: false);
        Assert.DoesNotContain("--cd", cmd);
    }

    [Fact]
    public void TranslateWindowsPathToWsl_PathsWithSpaces_PreservesSpaces()
    {
        // wsl.exe acepta espacios literales en --cd (sin comillado).
        Assert.Equal("/mnt/c/Program Files/Git",
            WslService.TranslateWindowsPathToWsl(@"C:\Program Files\Git"));
    }

    [Fact]
    public void TranslateWindowsPathToWsl_LowercaseLetterUsed()
    {
        // /mnt/c/, no /mnt/C/.
        Assert.StartsWith("/mnt/c/", WslService.TranslateWindowsPathToWsl(@"C:\Foo"));
        Assert.StartsWith("/mnt/d/", WslService.TranslateWindowsPathToWsl(@"D:\bar"));
    }

    [Fact]
    public void TranslateWindowsPathToWsl_RelativePath_ReplacesSeparators()
    {
        // Path relativo: solo cambia los separadores.
        Assert.Equal("foo/bar/baz", WslService.TranslateWindowsPathToWsl(@"foo\bar\baz"));
    }

    [Fact]
    public void BuildCommandLine_DistroWithDashInName_NoQuoting()
    {
        var cmd = WslService.BuildCommandLine("kali-linux", loginShell: true);
        Assert.Contains("-d kali-linux", cmd);
        Assert.DoesNotContain("\"kali-linux\"", cmd);
    }
}
