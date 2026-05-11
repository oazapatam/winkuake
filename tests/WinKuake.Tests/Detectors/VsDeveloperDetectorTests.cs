using System;
using System.IO;
using System.Linq;
using WinKuake.Services.Detectors;
using Xunit;

namespace WinKuake.Tests.Detectors;

public class VsDeveloperDetectorTests
{
    [Fact]
    public void LocateVsWhere_ProgramFilesNull_DevuelveNull()
    {
        Assert.Null(VsDeveloperDetector.LocateVsWhere(null));
        Assert.Null(VsDeveloperDetector.LocateVsWhere(""));
    }

    [Fact]
    public void LocateVsWhere_PathInexistente_DevuelveNull()
    {
        var fake = Path.Combine(Path.GetTempPath(), "no-vs-" + Guid.NewGuid());
        Assert.Null(VsDeveloperDetector.LocateVsWhere(fake));
    }

    [Fact]
    public void LocateVsWhere_PathExistente_DevuelvePath()
    {
        // Creamos la estructura ...\Microsoft Visual Studio\Installer\vswhere.exe
        var tmp = Path.Combine(Path.GetTempPath(), "winkuake-vs-" + Guid.NewGuid());
        var dir = Path.Combine(tmp, "Microsoft Visual Studio", "Installer");
        Directory.CreateDirectory(dir);
        var vswhere = Path.Combine(dir, "vswhere.exe");
        File.WriteAllText(vswhere, "stub");
        try
        {
            var found = VsDeveloperDetector.LocateVsWhere(tmp);
            Assert.Equal(vswhere, found);
        }
        finally { Directory.Delete(tmp, recursive: true); }
    }

    [Fact]
    public void ParseInstallations_VacioOWhitespace_DevuelveVacio()
    {
        Assert.Empty(VsDeveloperDetector.ParseInstallations(""));
        Assert.Empty(VsDeveloperDetector.ParseInstallations("   \r\n\r\n"));
    }

    [Fact]
    public void ParseInstallations_VariasLineas_TrimmedYDeduped()
    {
        var raw = @"C:\Program Files\Microsoft Visual Studio\2022\Community" + "\r\n" +
                  @"C:\Program Files\Microsoft Visual Studio\2022\Community" + "\r\n" +
                  @"  C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools  ";
        var result = VsDeveloperDetector.ParseInstallations(raw);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void ExtractVersionLabel_PathTipico_DevuelveAnioYEdicion()
    {
        var label = VsDeveloperDetector.ExtractVersionLabel(
            @"C:\Program Files\Microsoft Visual Studio\2022\Community");
        Assert.Equal("2022 Community", label);
    }

    [Fact]
    public void ExtractVersionLabel_PathBuildTools()
    {
        var label = VsDeveloperDetector.ExtractVersionLabel(
            @"C:\Program Files (x86)\Microsoft Visual Studio\2019\BuildTools");
        Assert.Equal("2019 BuildTools", label);
    }

    [Fact]
    public void ExtractVersionLabel_SinAnio_DevuelveUltimoSegmento()
    {
        var label = VsDeveloperDetector.ExtractVersionLabel(@"C:\dev\custom-vs");
        Assert.Equal("custom-vs", label);
    }

    [Fact]
    public void ExtractVersionLabel_Vacio_DevuelveVacio()
    {
        Assert.Equal("", VsDeveloperDetector.ExtractVersionLabel(""));
    }

    [Fact]
    public void BuildProfiles_SinScripts_DevuelveVacio()
    {
        // Instalación apuntada pero sin VsDevCmd.bat → descartado.
        var fakeInstall = Path.Combine(Path.GetTempPath(), "fake-vs-" + Guid.NewGuid());
        Directory.CreateDirectory(fakeInstall);
        try
        {
            var profiles = VsDeveloperDetector.BuildProfiles(new[] { fakeInstall });
            Assert.Empty(profiles);
        }
        finally { Directory.Delete(fakeInstall, recursive: true); }
    }

    [Fact]
    public void BuildProfiles_SoloVsDevCmd_GeneraSoloCmdProfile()
    {
        var install = Path.Combine(Path.GetTempPath(), "vs-only-cmd-" + Guid.NewGuid(), "2022", "Community");
        var tools = Path.Combine(install, "Common7", "Tools");
        Directory.CreateDirectory(tools);
        File.WriteAllText(Path.Combine(tools, "VsDevCmd.bat"), "@echo off");
        try
        {
            var profiles = VsDeveloperDetector.BuildProfiles(new[] { install });
            Assert.Single(profiles);
            Assert.StartsWith("Developer Command Prompt for VS", profiles[0].Name);
            Assert.Contains("VsDevCmd.bat", profiles[0].CommandLine);
            Assert.StartsWith("cmd.exe /k", profiles[0].CommandLine);
            Assert.Equal("≫", profiles[0].IconGlyph);
            Assert.Equal("Detected", profiles[0].Source);
        }
        finally { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(install))!, recursive: true); }
    }

    [Fact]
    public void BuildProfiles_AmbosScripts_GeneraDosPerfiles()
    {
        var install = Path.Combine(Path.GetTempPath(), "vs-both-" + Guid.NewGuid(), "2022", "Enterprise");
        var tools = Path.Combine(install, "Common7", "Tools");
        Directory.CreateDirectory(tools);
        File.WriteAllText(Path.Combine(tools, "VsDevCmd.bat"), "@echo off");
        File.WriteAllText(Path.Combine(tools, "Launch-VsDevShell.ps1"), "# stub");
        try
        {
            var profiles = VsDeveloperDetector.BuildProfiles(new[] { install });
            Assert.Equal(2, profiles.Count);
            Assert.Contains(profiles, p => p.Name.StartsWith("Developer Command Prompt for VS"));
            Assert.Contains(profiles, p => p.Name.StartsWith("Developer PowerShell for VS"));

            var ps = profiles.First(p => p.Name.StartsWith("Developer PowerShell"));
            Assert.Contains("pwsh.exe -NoExit", ps.CommandLine);
            Assert.Contains("Launch-VsDevShell.ps1", ps.CommandLine);
            Assert.Equal("⚡", ps.IconGlyph);
        }
        finally { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(install))!, recursive: true); }
    }

    [Fact]
    public void BuildProfiles_GuidsEstables()
    {
        var install = Path.Combine(Path.GetTempPath(), "vs-stable-" + Guid.NewGuid(), "2022", "Community");
        var tools = Path.Combine(install, "Common7", "Tools");
        Directory.CreateDirectory(tools);
        File.WriteAllText(Path.Combine(tools, "VsDevCmd.bat"), "@echo off");
        try
        {
            var a = VsDeveloperDetector.BuildProfiles(new[] { install });
            var b = VsDeveloperDetector.BuildProfiles(new[] { install });
            Assert.Equal(a[0].Id, b[0].Id);
        }
        finally { Directory.Delete(Path.GetDirectoryName(Path.GetDirectoryName(install))!, recursive: true); }
    }

    [Fact]
    public void BuildProfiles_ListaVacia_DevuelveVacio()
    {
        Assert.Empty(VsDeveloperDetector.BuildProfiles(Array.Empty<string>()));
        Assert.Empty(VsDeveloperDetector.BuildProfiles(null!));
    }
}
