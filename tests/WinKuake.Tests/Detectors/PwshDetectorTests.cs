using System;
using System.Collections.Generic;
using System.IO;
using WinKuake.Services.Detectors;
using Xunit;

namespace WinKuake.Tests.Detectors;

public class PwshDetectorTests
{
    [Fact]
    public void BuildProfiles_OutputVacio_DevuelveVacio()
    {
        Assert.Empty(PwshDetector.BuildProfiles(""));
        Assert.Empty(PwshDetector.BuildProfiles("   "));
        Assert.Empty(PwshDetector.BuildProfiles(null!));
    }

    [Fact]
    public void BuildProfiles_PathsInexistentes_FiltraTodos()
    {
        var raw = @"C:\noexiste\pwsh.exe" + "\r\n" + @"D:\tampoco\pwsh.exe";
        Assert.Empty(PwshDetector.BuildProfiles(raw));
    }

    [Fact]
    public void BuildProfilesFromPaths_ListaVacia_DevuelveVacio()
    {
        Assert.Empty(PwshDetector.BuildProfilesFromPaths(Array.Empty<string>()));
    }

    [Fact]
    public void BuildProfilesFromPaths_UnPath_NombreSinVersion()
    {
        var profiles = PwshDetector.BuildProfilesFromPaths(new[] { @"C:\Program Files\PowerShell\7\pwsh.exe" });
        Assert.Single(profiles);
        Assert.Equal("PowerShell", profiles[0].Name);
        Assert.Equal("⚡", profiles[0].IconGlyph);
        Assert.Equal("Detected", profiles[0].Source);
        Assert.Contains("pwsh.exe", profiles[0].CommandLine);
    }

    [Fact]
    public void BuildProfilesFromPaths_VariosPaths_AnotaParaDistinguir()
    {
        // 2+ paths → cada perfil debe tener un sufijo (versión o el path) que
        // los diferencie. No necesitamos que sea la versión exacta — solo que
        // los names no colisionen.
        var profiles = PwshDetector.BuildProfilesFromPaths(new[]
        {
            @"C:\Program Files\PowerShell\7\pwsh.exe",
            @"C:\Program Files\PowerShell\7-preview\pwsh.exe",
        });
        Assert.Equal(2, profiles.Count);
        Assert.NotEqual(profiles[0].Name, profiles[1].Name);
        Assert.NotEqual(profiles[0].Id,   profiles[1].Id);
    }

    [Fact]
    public void BuildProfilesFromPaths_GuidEstableEntreLlamadas()
    {
        var input = new[] { @"C:\Program Files\PowerShell\7\pwsh.exe" };
        var a = PwshDetector.BuildProfilesFromPaths(input);
        var b = PwshDetector.BuildProfilesFromPaths(input);
        Assert.Equal(a[0].Id, b[0].Id);
    }

    [Fact]
    public void BuildProfilesFromPaths_FiltraWindowsAppsMsixPath()
    {
        // Regresión: where.exe pwsh.exe puede devolver
        //   C:\Program Files\WindowsApps\Microsoft.PowerShell_X.Y.Z_x64__hash\pwsh.exe
        // Esa instalación MSIX vive en una carpeta con ACLs restrictivos. Aunque
        // CreateProcess "tiene éxito", el proceso resultante no puede acceder a
        // stdin/stdout del ConPty y queda silencioso → la terminal se ve en
        // negro porque nunca llega el prompt. Hay que filtrar esos paths para
        // que el detector use solo las instalaciones ejecutables (Program
        // Files\PowerShell\X, o el shim en %LocalAppData%\Microsoft\WindowsApps).
        var paths = new[]
        {
            @"C:\Program Files\WindowsApps\Microsoft.PowerShell_7.6.1.0_x64__8wekyb3d8bbwe\pwsh.exe",
            @"C:\Program Files\PowerShell\7\pwsh.exe",
        };
        var profiles = PwshDetector.BuildProfilesFromPaths(paths);
        Assert.Single(profiles);
        Assert.DoesNotContain("WindowsApps", profiles[0].CommandLine);
    }

    [Fact]
    public void BuildProfilesFromPaths_FiltroWindowsApps_NoCuentaParaAnotarVersion()
    {
        // Si después de filtrar queda un solo path, el name NO debe anotar versión.
        var paths = new[]
        {
            @"C:\Program Files\WindowsApps\Microsoft.PowerShell_7.6.1.0_x64__abc\pwsh.exe",
            @"C:\Program Files\PowerShell\7\pwsh.exe",
        };
        var profiles = PwshDetector.BuildProfilesFromPaths(paths);
        Assert.Single(profiles);
        Assert.Equal("PowerShell", profiles[0].Name);
    }

    [Fact]
    public void BuildProfilesFromPaths_FiltraShimEnLocalAppDataWindowsApps()
    {
        // El alias %LocalAppData%\Microsoft\WindowsApps\pwsh.exe es un App
        // Execution Alias que delega a la instalación MSIX. Igual que el path
        // bajo Program Files\WindowsApps, no funciona con ConPty: el proceso
        // arranca pero queda silencioso. Filtramos TODO lo que pase por
        // `\WindowsApps\`. El detector aceptable es Program Files\PowerShell\X.
        var paths = new[]
        {
            @"C:\Users\andre\AppData\Local\Microsoft\WindowsApps\pwsh.exe",
            @"C:\Program Files\PowerShell\7\pwsh.exe",
        };
        var profiles = PwshDetector.BuildProfilesFromPaths(paths);
        Assert.Single(profiles);
        Assert.DoesNotContain("WindowsApps", profiles[0].CommandLine);
    }

    [Fact]
    public void BuildProfiles_DedupeaCaseInsensitive()
    {
        // Si where.exe devuelve el mismo path con distinta capitalización,
        // BuildProfiles dedupe antes de chequear existencia.
        var raw = @"C:\PWSH.EXE" + "\r\n" + @"c:\pwsh.exe";
        // Ambos son inexistentes → vacío. La intención del test es que el
        // contrato de dedupe no reviente en flow real (no que devuelva 1).
        Assert.Empty(PwshDetector.BuildProfiles(raw));
    }
}
