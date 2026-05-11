using System;
using System.IO;
using WinKuake.Services.Detectors;
using Xunit;

namespace WinKuake.Tests.Detectors;

public class CmdDetectorTests
{
    [Fact]
    public void DetectAt_PathInexistente_DevuelveVacio()
    {
        var path = Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid() + ".exe");
        Assert.Empty(CmdDetector.DetectAt(path));
    }

    [Fact]
    public void DetectAt_PathExistente_DevuelveUnPerfilConCamposCorrectos()
    {
        var tempExe = Path.Combine(Path.GetTempPath(), "winkuake-test-cmd-" + Guid.NewGuid() + ".exe");
        File.WriteAllText(tempExe, "stub");
        try
        {
            var profiles = CmdDetector.DetectAt(tempExe);
            Assert.Single(profiles);
            var p = profiles[0];
            Assert.Equal("Símbolo del sistema", p.Name);
            Assert.Equal("≫", p.IconGlyph);
            Assert.Equal("Detected", p.Source);
            Assert.False(p.Hidden);
            Assert.Contains(tempExe, p.CommandLine);
            Assert.NotEmpty(p.Id);
        }
        finally { File.Delete(tempExe); }
    }

    [Fact]
    public void DetectAt_GuidEstable()
    {
        var tempExe = Path.Combine(Path.GetTempPath(), "winkuake-test-cmd-stable-" + Guid.NewGuid() + ".exe");
        File.WriteAllText(tempExe, "stub");
        try
        {
            var a = CmdDetector.DetectAt(tempExe);
            var b = CmdDetector.DetectAt(tempExe);
            Assert.Equal(a[0].Id, b[0].Id);
        }
        finally { File.Delete(tempExe); }
    }

    [Fact]
    public void Detect_EnWindows_EncuentraCmdReal()
    {
        if (!OperatingSystem.IsWindows()) return;
        var detector = new CmdDetector();
        var profiles = detector.Detect();
        Assert.Single(profiles);
        Assert.Equal("Símbolo del sistema", profiles[0].Name);
        Assert.Contains("cmd.exe", profiles[0].CommandLine, StringComparison.OrdinalIgnoreCase);
    }
}
