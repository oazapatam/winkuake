using System;
using System.Linq;
using WinKuake.Services;
using WinKuake.Services.Detectors;
using Xunit;

namespace WinKuake.Tests.Detectors;

public class WslDetectorTests
{
    [Fact]
    public void BuildProfiles_ListaVacia_DevuelveVacio()
    {
        Assert.Empty(WslDetector.BuildProfiles(Array.Empty<WslDistribution>()));
        Assert.Empty(WslDetector.BuildProfiles(null!));
    }

    [Fact]
    public void BuildProfiles_DesdeFixtureWslListVerbose_GeneraPerfiles()
    {
        // Reusa la fixture textual de WslServiceTests: parser + detector
        // forman el flujo completo desde "wsl.exe -l --verbose".
        var raw = "  NAME            STATE           VERSION\n" +
                  "* Ubuntu          Running         2\n" +
                  "  Debian          Stopped         2\n";
        var distros = WslService.ParseListVerbose(raw);
        var profiles = WslDetector.BuildProfiles(distros);

        Assert.Equal(2, profiles.Count);

        var ubuntu = profiles[0];
        Assert.Equal("Ubuntu", ubuntu.Name);
        Assert.Equal("🐧", ubuntu.IconGlyph);
        Assert.Equal("Detected", ubuntu.Source);
        Assert.Contains("wsl.exe -d Ubuntu", ubuntu.CommandLine);
        Assert.Contains("--shell-type login", ubuntu.CommandLine);
        Assert.Contains("--cd ~", ubuntu.CommandLine);
        Assert.NotEmpty(ubuntu.Id);
    }

    [Fact]
    public void BuildProfiles_DistroDocker_FiltradaPorWslService()
    {
        // WslService.ParseListVerbose ya filtra docker-desktop*. El detector
        // hereda ese filtro.
        var raw = "  NAME                  STATE           VERSION\n" +
                  "* Ubuntu                Running         2\n" +
                  "  docker-desktop        Stopped         2\n" +
                  "  docker-desktop-data   Stopped         2\n";
        var distros = WslService.ParseListVerbose(raw);
        var profiles = WslDetector.BuildProfiles(distros);
        Assert.Single(profiles);
        Assert.Equal("Ubuntu", profiles[0].Name);
    }

    [Fact]
    public void BuildProfiles_GuidEstablePorCommandLine()
    {
        var distros = new[] { new WslDistribution("Ubuntu", "Running", 2, true) };
        var a = WslDetector.BuildProfiles(distros);
        var b = WslDetector.BuildProfiles(distros);
        Assert.Equal(a[0].Id, b[0].Id);
    }

    [Fact]
    public void BuildProfiles_DistrosDistintas_GuidsDistintos()
    {
        var distros = new[]
        {
            new WslDistribution("Ubuntu", "Running", 2, true),
            new WslDistribution("Debian", "Stopped", 2, false),
        };
        var profiles = WslDetector.BuildProfiles(distros);
        Assert.NotEqual(profiles[0].Id, profiles[1].Id);
    }
}
