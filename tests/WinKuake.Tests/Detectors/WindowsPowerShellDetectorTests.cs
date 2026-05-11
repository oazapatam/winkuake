using System;
using System.IO;
using WinKuake.Services.Detectors;
using Xunit;

namespace WinKuake.Tests.Detectors;

public class WindowsPowerShellDetectorTests
{
    [Fact]
    public void DetectAt_PathInexistente_DevuelveVacio()
    {
        var path = Path.Combine(Path.GetTempPath(), "no-existe-" + Guid.NewGuid() + ".exe");
        Assert.Empty(WindowsPowerShellDetector.DetectAt(path));
    }

    [Fact]
    public void DetectAt_PathExistente_DevuelveUnPerfilConCamposCorrectos()
    {
        // Usamos un archivo real (cualquier .exe) — el método solo chequea
        // existencia, no que sea PowerShell.
        var tempExe = Path.Combine(Path.GetTempPath(), "winkuake-test-pwsh-" + Guid.NewGuid() + ".exe");
        File.WriteAllText(tempExe, "stub");
        try
        {
            var profiles = WindowsPowerShellDetector.DetectAt(tempExe);
            Assert.Single(profiles);
            var p = profiles[0];
            Assert.Equal("Windows PowerShell", p.Name);
            Assert.Equal("⚡", p.IconGlyph);
            Assert.Equal("Detected", p.Source);
            Assert.False(p.Hidden);
            Assert.Contains(tempExe, p.CommandLine);
            Assert.NotEmpty(p.Id);
        }
        finally { File.Delete(tempExe); }
    }

    [Fact]
    public void DetectAt_GuidEsDeterministicoPorCommandLine()
    {
        var tempExe = Path.Combine(Path.GetTempPath(), "winkuake-test-pwsh-stable-" + Guid.NewGuid() + ".exe");
        File.WriteAllText(tempExe, "stub");
        try
        {
            var first  = WindowsPowerShellDetector.DetectAt(tempExe);
            var second = WindowsPowerShellDetector.DetectAt(tempExe);
            Assert.Equal(first[0].Id, second[0].Id);
        }
        finally { File.Delete(tempExe); }
    }

    [Fact]
    public void Detect_EnWindows_EncuentraPowershellReal()
    {
        // Test integration: en cualquier Windows real, powershell.exe existe
        // siempre. Si se corre en otro SO se omite (no aplica).
        if (!OperatingSystem.IsWindows()) return;
        var detector = new WindowsPowerShellDetector();
        var profiles = detector.Detect();
        Assert.Single(profiles);
        Assert.Equal("Windows PowerShell", profiles[0].Name);
        Assert.Contains("powershell.exe", profiles[0].CommandLine, StringComparison.OrdinalIgnoreCase);
    }
}
