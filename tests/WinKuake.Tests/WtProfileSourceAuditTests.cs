using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 2.A — fija invariantes que no estaban cubiertos por tests
/// existentes para evitar regresiones (Azure stays unsupported, .exe fallback,
/// merge respeta orden de wt antes que distros nuevas, etc.).
/// </summary>
public class WtProfileSourceAuditTests
{
    [Fact]
    public void ResolveCommandLine_WslSourceCaseInsensitive_StillRoutedToWslService()
    {
        // El source de wt es siempre "Windows.Terminal.Wsl" exacto, pero el contrato
        // dice "case-insensitive" — así nos protegemos contra cambios futuros de wt.
        var cmd = WtProfileSource.ResolveCommandLine("Ubuntu", null, "windows.terminal.wsl");
        Assert.Contains("wsl.exe", cmd);
        Assert.Contains("--shell-type login", cmd);
    }

    [Fact]
    public void ResolveCommandLine_WslSourceWithExplicitCommandline_StillUsesWslBuilder()
    {
        // El builtin de wt ignora el commandline cuando source=Wsl: WinKuake debe
        // hacer lo mismo para mantener consistencia (login shell siempre).
        var cmd = WtProfileSource.ResolveCommandLine("Ubuntu", "ignored.exe", "Windows.Terminal.Wsl");
        Assert.Contains("wsl.exe", cmd);
        Assert.DoesNotContain("ignored.exe", cmd);
    }

    [Fact]
    public void ResolveCommandLine_AzurePartialName_AlsoFiltered()
    {
        // Variantes localizadas o renombradas que contengan "Azure" no deben
        // intentarse — siempre requieren auth.
        Assert.Null(WtProfileSource.ResolveCommandLine("Azure",                 null, null));
        Assert.Null(WtProfileSource.ResolveCommandLine("Mi Azure favorito",     null, null));
    }

    [Fact]
    public void ResolveCommandLine_NameEndingInExe_Fallback()
    {
        // Fallback final: si el nombre parece un binario, lo intentamos lanzar.
        Assert.Equal("nu.exe", WtProfileSource.ResolveCommandLine("nu.exe", null, null));
    }

    [Fact]
    public void ResolveCommandLine_UnknownProfileWithoutCommandline_ReturnsNull()
    {
        // Caso "Git Bash" sin source ni commandline: no podemos adivinar el path.
        Assert.Null(WtProfileSource.ResolveCommandLine("Git Bash", null, null));
    }

    [Fact]
    public void ResolveCommandLine_PowerShellByExactName_UsesPwsh()
    {
        // El builtin de wt llama "PowerShell" al moderno (pwsh).
        Assert.Equal("pwsh.exe", WtProfileSource.ResolveCommandLine("PowerShell", null, null));
    }

    [Fact]
    public void ResolveCommandLine_ExpandsEnvVarsInCommandline()
    {
        // El commandline puede contener %USERPROFILE% u otras vars.
        System.Environment.SetEnvironmentVariable("WK_AUDIT_TEST_VAR", "hola");
        var cmd = WtProfileSource.ResolveCommandLine("test", "%WK_AUDIT_TEST_VAR%.exe", null);
        Assert.Equal("hola.exe", cmd);
    }

    [Fact]
    public void ResolveCommandLine_WslWithUnixStartingDir_DoesNotDoubleSlash()
    {
        var cmd = WtProfileSource.ResolveCommandLine(
            "Ubuntu", null, "Windows.Terminal.Wsl",
            startingDirectory: "/home/andre");
        Assert.Contains("--cd /home/andre", cmd);
        Assert.DoesNotContain("--cd ~", cmd);
    }

    [Fact]
    public void Merge_NewWslDistroIsDefaultOnly_IfNoWtProfileIsDefault()
    {
        // Cuando wt ya tiene un default (ej. Windows PowerShell), la distro WSL
        // sintética NO debe pisar ese default.
        var wt = new[]
        {
            new TerminalProfile("Windows PowerShell", "ps") { CommandLine = "powershell.exe", IsDefault = true }
        };
        var wsl = new[] { new WslDistribution("Ubuntu", "Running", 2, true) };
        var merged = WtProfileSource.Merge(wt, wsl);
        Assert.Equal(2, merged.Count);
        Assert.True(merged[0].IsDefault);
        Assert.False(merged[1].IsDefault); // Ubuntu sintética no se marca default
    }

    [Fact]
    public void Merge_WslDefaultDistro_MarkedDefault_WhenNoWtDefault()
    {
        // Si no hay default en wt y la distro es default en WSL, merge la marca default.
        var wt = new[]
        {
            new TerminalProfile("cmd", "cmd") { CommandLine = "cmd.exe" }
        };
        var wsl = new[] { new WslDistribution("Ubuntu", "Running", 2, true) };
        var merged = WtProfileSource.Merge(wt, wsl);
        Assert.False(merged[0].IsDefault);
        Assert.True(merged[1].IsDefault);
    }

    [Fact]
    public void StripJsonComments_HandlesUrlsInsideStrings()
    {
        // El "//" en URLs no debe arrancar comentario.
        var input = "{ \"u\": \"https://example.com\" }";
        var result = StripPublicProxy(input);
        Assert.Contains("https://example.com", result);
    }

    private static string StripPublicProxy(string s)
    {
        // Reflection helper, mismo que tests existentes.
        var mi = typeof(WtProfileSource).GetMethod(
            "StripJsonComments",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        return (string)mi.Invoke(null, new object[] { s })!;
    }
}
