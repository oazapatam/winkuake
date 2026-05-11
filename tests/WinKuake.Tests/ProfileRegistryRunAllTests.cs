using System;
using System.Collections.Generic;
using System.Linq;
using WinKuake.Models;
using WinKuake.Services;
using WinKuake.Services.Detectors;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Tests para la nueva lógica de <see cref="ProfileRegistry.LoadAll(AppSettings)"/>:
/// detección + persistencia in-place la primera vez. La interfaz pública
/// <see cref="IProfileDetector"/> permite inyectar fakes para no depender del SO.
/// </summary>
public class ProfileRegistryRunAllTests
{
    private sealed class FakeDetector : IProfileDetector
    {
        private readonly UserProfile[] _profiles;
        public int CallCount { get; private set; }
        public FakeDetector(params UserProfile[] profiles) { _profiles = profiles; }
        public IReadOnlyList<UserProfile> Detect()
        {
            CallCount++;
            return _profiles;
        }
    }

    private sealed class ThrowingDetector : IProfileDetector
    {
        public IReadOnlyList<UserProfile> Detect() => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void RunDetectors_VariosFakes_ConcatenaResultados()
    {
        var a = new FakeDetector(
            new UserProfile { Id = "1", Name = "PS",  CommandLine = "pwsh.exe" });
        var b = new FakeDetector(
            new UserProfile { Id = "2", Name = "Cmd", CommandLine = "cmd.exe" });

        var result = ProfileRegistry.RunDetectors(new[] { a, b });
        Assert.Equal(2, result.Count);
        Assert.Equal("PS",  result[0].Name);
        Assert.Equal("Cmd", result[1].Name);
    }

    [Fact]
    public void RunDetectors_DetectorVacio_LoIgnora()
    {
        var a = new FakeDetector();
        var b = new FakeDetector(new UserProfile { Id = "x", Name = "Bash", CommandLine = "bash" });
        var result = ProfileRegistry.RunDetectors(new[] { a, b });
        Assert.Single(result);
        Assert.Equal("Bash", result[0].Name);
    }

    [Fact]
    public void RunDetectors_DetectorQueLanza_NoTumbaAlResto()
    {
        var ok = new FakeDetector(new UserProfile { Id = "1", Name = "X", CommandLine = "x" });
        var bad = new ThrowingDetector();
        var result = ProfileRegistry.RunDetectors(new[] { (IProfileDetector)bad, ok });
        Assert.Single(result);
        Assert.Equal("X", result[0].Name);
    }

    [Fact]
    public void LoadAll_SettingsVacio_EjecutaDetectoresYPersisteEnLista()
    {
        // Esta variante usa los detectores reales; en CI/Windows habrá al
        // menos cmd y powershell. En un entorno raro (tests en Linux) puede
        // venir vacío — chequeamos solo el contrato: la lista se mutó in-place
        // y futuras llamadas no re-corren.
        var s = new AppSettings();
        Assert.Empty(s.UserProfiles);

        var loaded = ProfileRegistry.LoadAll(s);
        Assert.Equal(s.UserProfiles.Where(p => !p.Hidden).Count(), loaded.Count);

        var snapshot = s.UserProfiles.Count;
        // Segunda llamada NO re-corre detección.
        ProfileRegistry.LoadAll(s);
        Assert.Equal(snapshot, s.UserProfiles.Count);
    }

    [Fact]
    public void LoadAll_SettingsConDatos_NoCorreDetectoresYRespetaHidden()
    {
        var s = new AppSettings
        {
            UserProfiles = new()
            {
                new() { Id = "1", Name = "Visible", CommandLine = "x", Hidden = false },
                new() { Id = "2", Name = "Oculto",  CommandLine = "y", Hidden = true  },
            }
        };
        var before = s.UserProfiles.Count;
        var result = ProfileRegistry.LoadAll(s);
        Assert.Equal(before, s.UserProfiles.Count); // no agregó nada
        Assert.Single(result);
        Assert.Equal("Visible", result[0].Name);
    }

    [Fact]
    public void AllDetectors_TieneLasSeisFamilias()
    {
        // Smoke: el array contiene un detector por familia (tipos exactos).
        var types = ProfileRegistry.AllDetectors.Select(d => d.GetType().Name).ToHashSet();
        Assert.Contains(nameof(WindowsPowerShellDetector), types);
        Assert.Contains(nameof(PwshDetector),              types);
        Assert.Contains(nameof(CmdDetector),               types);
        Assert.Contains(nameof(WslDetector),               types);
        Assert.Contains(nameof(GitBashDetector),           types);
        Assert.Contains(nameof(VsDeveloperDetector),       types);
    }

    [Fact]
    public void RunAllDetectors_NoLanza()
    {
        // Llama a los reales: aunque el SO no tenga pwsh/git/vs, ningún detector
        // debe propagar excepciones.
        var ex = Record.Exception(() => ProfileRegistry.RunAllDetectors());
        Assert.Null(ex);
    }
}
