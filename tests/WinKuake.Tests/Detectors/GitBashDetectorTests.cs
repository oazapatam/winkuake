using System;
using System.IO;
using System.Linq;
using WinKuake.Services.Detectors;
using Xunit;

namespace WinKuake.Tests.Detectors;

public class GitBashDetectorTests
{
    [Fact]
    public void CollectCandidates_TodasLasFuentesNullOrVacias_DevuelveVacio()
    {
        var c = GitBashDetector.CollectCandidates(null, null, null, null, "");
        Assert.Empty(c);
    }

    [Fact]
    public void CollectCandidates_ProgramFiles_AgregaPathCanonico()
    {
        var c = GitBashDetector.CollectCandidates(
            programFiles: @"C:\Program Files",
            programFilesX86: null, localAppData: null,
            registryInstallPath: null, whereOutput: "");
        Assert.Single(c);
        Assert.Equal(@"C:\Program Files\Git\bin\bash.exe", c[0]);
    }

    [Fact]
    public void CollectCandidates_TodasLasFuentes_DedupCaseInsensitive()
    {
        // ProgramFiles y registry apuntan al mismo install — debe quedar 1.
        var c = GitBashDetector.CollectCandidates(
            programFiles: @"C:\Program Files",
            programFilesX86: null,
            localAppData: null,
            registryInstallPath: @"C:\PROGRAM FILES\Git",
            whereOutput: "");
        Assert.Single(c);
    }

    [Fact]
    public void CollectCandidates_FiltraBashDeSystem32()
    {
        // bash.exe en System32 es el front-end de WSL — NO Git Bash. Se descarta.
        var c = GitBashDetector.CollectCandidates(
            programFiles: null, programFilesX86: null, localAppData: null,
            registryInstallPath: null,
            whereOutput: @"C:\Windows\System32\bash.exe");
        Assert.Empty(c);
    }

    [Fact]
    public void CollectCandidates_FiltraBashDeWindowsApps()
    {
        var c = GitBashDetector.CollectCandidates(
            programFiles: null, programFilesX86: null, localAppData: null,
            registryInstallPath: null,
            whereOutput: @"C:\Users\foo\AppData\Local\Microsoft\WindowsApps\bash.exe");
        Assert.Empty(c);
    }

    [Fact]
    public void CollectCandidates_AceptaBashDeProgramFilesAunqueVengaPorWhere()
    {
        var c = GitBashDetector.CollectCandidates(
            programFiles: null, programFilesX86: null, localAppData: null,
            registryInstallPath: null,
            whereOutput: @"C:\Program Files\Git\bin\bash.exe");
        Assert.Single(c);
        Assert.Contains("Git", c[0]);
    }

    [Fact]
    public void BuildProfilesAssumingExisting_ListaVacia_DevuelveVacio()
    {
        Assert.Empty(GitBashDetector.BuildProfilesAssumingExisting(Array.Empty<string>()));
    }

    [Fact]
    public void BuildProfilesAssumingExisting_UnPath_GeneraPerfilCorrecto()
    {
        var profiles = GitBashDetector.BuildProfilesAssumingExisting(
            new[] { @"C:\Program Files\Git\bin\bash.exe" });
        Assert.Single(profiles);
        var p = profiles[0];
        Assert.Equal("Git Bash", p.Name);
        Assert.Equal("🐚", p.IconGlyph);
        Assert.Equal("Detected", p.Source);
        Assert.Contains("bash.exe", p.CommandLine);
        Assert.Contains("-l -i", p.CommandLine);
        Assert.NotEmpty(p.Id);
    }

    [Fact]
    public void BuildProfilesAssumingExisting_GuidEstable()
    {
        var input = new[] { @"C:\Program Files\Git\bin\bash.exe" };
        var a = GitBashDetector.BuildProfilesAssumingExisting(input);
        var b = GitBashDetector.BuildProfilesAssumingExisting(input);
        Assert.Equal(a[0].Id, b[0].Id);
    }

    [Fact]
    public void BuildProfilesFromPaths_FiltraInexistentes()
    {
        // Los paths del candidato no existen → devuelve vacío.
        var p = GitBashDetector.BuildProfilesFromPaths(
            new[] { @"C:\noexiste\bash.exe" });
        Assert.Empty(p);
    }
}
