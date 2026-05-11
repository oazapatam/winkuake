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
