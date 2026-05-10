using System.Reflection;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class WtProfileSourceTests
{
    [Fact]
    public void StripJsonComments_RemovesLineComments()
    {
        var input = "{\n  // comment\n  \"a\": 1\n}";
        var result = StripViaReflection(input);
        Assert.DoesNotContain("//", result);
        Assert.Contains("\"a\": 1", result);
    }

    [Fact]
    public void StripJsonComments_RemovesBlockComments()
    {
        var input = "{ /* hola */ \"a\": 1 }";
        var result = StripViaReflection(input);
        Assert.DoesNotContain("/*", result);
        Assert.Contains("\"a\": 1", result);
    }

    [Fact]
    public void StripJsonComments_PreservesCommentSyntaxInsideStrings()
    {
        var input = "{ \"path\": \"C:/Users // not a comment\" }";
        var result = StripViaReflection(input);
        Assert.Contains("// not a comment", result);
    }

    [Fact]
    public void StripJsonComments_HandlesEscapedQuotesInStrings()
    {
        var input = "{ \"a\": \"he said \\\"hi //\\\"\" /* tail */ }";
        var result = StripViaReflection(input);
        Assert.DoesNotContain("/* tail */", result);
        Assert.Contains("he said", result);
    }

    [Fact]
    public void Load_ReturnsEmptyOrPopulatedList_WithoutThrowing()
    {
        // No verificamos contenido específico (depende del entorno del usuario),
        // sólo que no explote leyendo settings.json real.
        var profiles = WtProfileSource.Load();
        Assert.NotNull(profiles);
    }

    [Fact]
    public void ResolveCommandLine_WslSource_UsesWslServiceWithLoginShell()
    {
        var cmd = WtProfileSource.ResolveCommandLine("Ubuntu", null, "Windows.Terminal.Wsl");
        Assert.Contains("wsl.exe", cmd);
        Assert.Contains("-d Ubuntu", cmd);
        Assert.Contains("--shell-type login", cmd);
        Assert.Contains("--cd ~", cmd);
    }

    [Fact]
    public void ResolveCommandLine_WslSourceWithStartingDir_TranslatesPath()
    {
        var cmd = WtProfileSource.ResolveCommandLine(
            "Ubuntu", null, "Windows.Terminal.Wsl",
            startingDirectory: @"C:\Users\andre\proyectos");
        Assert.Contains("--cd /mnt/c/Users/andre/proyectos", cmd);
        Assert.DoesNotContain("--cd ~", cmd);
        Assert.DoesNotContain("C:\\", cmd);
    }

    [Fact]
    public void ResolveCommandLine_ExplicitCommandlineWinsOverDefaults()
    {
        var cmd = WtProfileSource.ResolveCommandLine(
            "Git Bash", "\"C:\\Program Files\\Git\\bin\\bash.exe\" -l", null);
        Assert.Contains("Git\\bin\\bash.exe", cmd);
        Assert.Contains("-l", cmd);
    }

    [Fact]
    public void ResolveCommandLine_PowerShellCoreSource_ReturnsPwsh()
    {
        var cmd = WtProfileSource.ResolveCommandLine("PowerShell", null, "Windows.Terminal.PowershellCore");
        Assert.Equal("pwsh.exe", cmd);
    }

    [Fact]
    public void ResolveCommandLine_WindowsPowerShell_ReturnsPowershellExe()
    {
        var cmd = WtProfileSource.ResolveCommandLine("Windows PowerShell", null, null);
        Assert.Equal("powershell.exe", cmd);
    }

    [Fact]
    public void ResolveCommandLine_CommandPromptByName_ReturnsCmd()
    {
        Assert.Equal("cmd.exe", WtProfileSource.ResolveCommandLine("Command Prompt", null, null));
        Assert.Equal("cmd.exe", WtProfileSource.ResolveCommandLine("Símbolo del sistema", null, null));
    }

    [Fact]
    public void ResolveCommandLine_AzureCloudShell_ReturnsNull()
    {
        Assert.Null(WtProfileSource.ResolveCommandLine("Azure Cloud Shell", null, null));
    }

    // -- Merge: combinar perfiles wt + distros WSL detectadas -----------------

    [Fact]
    public void Merge_WtEmpty_AddsAllWslAsSynthetic()
    {
        var wt = System.Array.Empty<TerminalProfile>();
        var wsl = new[] { new WslDistribution("Ubuntu", "Running", 2, true) };
        var merged = WtProfileSource.Merge(wt, wsl);
        Assert.Single(merged);
        Assert.Equal("Ubuntu", merged[0].DisplayName);
        Assert.Contains("wsl.exe", merged[0].CommandLine);
        Assert.Contains("--shell-type login", merged[0].CommandLine);
    }

    [Fact]
    public void Merge_WtAlreadyHasDistro_DoesNotDuplicate()
    {
        var wt = new[]
        {
            new TerminalProfile("Ubuntu", "Ubuntu")
            {
                CommandLine = "wsl.exe -d Ubuntu --shell-type login --cd ~"
            }
        };
        var wsl = new[] { new WslDistribution("Ubuntu", "Running", 2, true) };
        var merged = WtProfileSource.Merge(wt, wsl);
        Assert.Single(merged);
        Assert.Same(wt[0], merged[0]);
    }

    [Fact]
    public void Merge_LegacyQuotedDistroName_IsAlsoRecognized()
    {
        // Soportamos retrocompatibilidad: si por algún motivo el commandline
        // tiene comillas (formato viejo), también se detecta.
        var wt = new[]
        {
            new TerminalProfile("Ubuntu", "Ubuntu") { CommandLine = "wsl.exe -d \"Ubuntu\"" }
        };
        var wsl = new[] { new WslDistribution("Ubuntu", "Running", 2, true) };
        Assert.Single(WtProfileSource.Merge(wt, wsl));
    }

    [Fact]
    public void Merge_NewWslDistro_IsAddedAfterWtProfiles()
    {
        var wt = new[]
        {
            new TerminalProfile("Ubuntu", "Ubuntu") { CommandLine = "wsl.exe -d Ubuntu" }
        };
        var wsl = new[]
        {
            new WslDistribution("Ubuntu", "Running", 2, true),
            new WslDistribution("Debian", "Stopped", 2, false)
        };
        var merged = WtProfileSource.Merge(wt, wsl);
        Assert.Equal(2, merged.Count);
        Assert.Equal("Ubuntu", merged[0].DisplayName);
        Assert.Equal("Debian", merged[1].DisplayName);
    }

    [Fact]
    public void Merge_BothEmpty_ReturnsEmpty()
    {
        Assert.Empty(WtProfileSource.Merge(
            System.Array.Empty<TerminalProfile>(),
            System.Array.Empty<WslDistribution>()));
    }

    [Fact]
    public void Merge_SyntheticWslProfileUsesDistroNameAsDisplayName()
    {
        var merged = WtProfileSource.Merge(
            System.Array.Empty<TerminalProfile>(),
            new[] { new WslDistribution("kali-linux", "Running", 2, false) });
        Assert.Equal("kali-linux", merged[0].DisplayName);
        Assert.Equal("kali-linux", merged[0].WtArgs);
    }

    private static string StripViaReflection(string s)
    {
        var mi = typeof(WtProfileSource).GetMethod("StripJsonComments",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)mi.Invoke(null, new object[] { s })!;
    }
}
