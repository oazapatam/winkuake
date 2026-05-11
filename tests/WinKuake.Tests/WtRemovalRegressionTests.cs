using System;
using System.IO;
using System.Linq;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Anti-regresión Fase 20.D — la app no debe depender de Windows Terminal en
/// ningún sentido (ni `WtProfileSource`, ni el package family name del wt
/// settings.json, ni cualquier otra invocación al import-from-wt). Estos tests
/// recorren todos los .cs bajo src/ con un grep programático.
/// </summary>
public class WtRemovalRegressionTests
{
    /// <summary>
    /// Sube en el árbol desde el binario de tests hasta encontrar el directorio
    /// con `src/WinKuake/` y devuelve esa ruta. Igual que el patrón usado en
    /// <c>OscHandlerAuditTests</c> y <c>ProfileWatcherAuditTests</c>.
    /// </summary>
    private static string FindSrcDir()
    {
        var dir = AppContext.BaseDirectory;
        for (int i = 0; i < 12 && dir is not null; i++)
        {
            var candidate = Path.Combine(dir, "src", "WinKuake");
            if (Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException(
            "No encontré src/WinKuake desde " + AppContext.BaseDirectory);
    }

    private static string[] AllSourceFiles() =>
        Directory.EnumerateFiles(FindSrcDir(), "*.cs", SearchOption.AllDirectories).ToArray();

    [Fact]
    public void NoCsFile_References_WtProfileSource()
    {
        var offenders = AllSourceFiles()
            .Where(f => File.ReadAllText(f).Contains("WtProfileSource"))
            .ToArray();
        Assert.True(offenders.Length == 0,
            "Aún hay referencias a WtProfileSource en: " + string.Join(", ", offenders));
    }

    [Fact]
    public void NoCsFile_References_WtProfileSource_LoadCombined()
    {
        var offenders = AllSourceFiles()
            .Where(f => File.ReadAllText(f).Contains("WtProfileSource.LoadCombined"))
            .ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void NoCsFile_References_WindowsTerminalPackageFamilyName()
    {
        var offenders = AllSourceFiles()
            .Where(f => File.ReadAllText(f).Contains("Microsoft.WindowsTerminal_8wekyb3d8bbwe"))
            .ToArray();
        Assert.True(offenders.Length == 0,
            "Aún hay referencias al package family name de wt en: " + string.Join(", ", offenders));
    }

    [Fact]
    public void NoCsFile_References_WindowsTerminalPreviewPackageFamilyName()
    {
        var offenders = AllSourceFiles()
            .Where(f => File.ReadAllText(f).Contains("Microsoft.WindowsTerminalPreview_8wekyb3d8bbwe"))
            .ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void WtProfileSource_FileDoesNotExist()
    {
        var path = Path.Combine(FindSrcDir(), "Services", "WtProfileSource.cs");
        Assert.False(File.Exists(path),
            "WtProfileSource.cs debe haberse eliminado en Fase 20.D");
    }

    [Fact]
    public void NoCsFile_References_StartWatchingWtSettings()
    {
        // El FileSystemWatcher sobre el settings.json de wt fue removido junto al import.
        var offenders = AllSourceFiles()
            .Where(f => File.ReadAllText(f).Contains("StartWatchingWtSettings"))
            .ToArray();
        Assert.Empty(offenders);
    }

    [Fact]
    public void NoCsFile_Reads_WtSettingsJson_Path()
    {
        // Doble check: ningún .cs construye un path al settings.json de wt
        // (algún detector futuro podría hacerlo por error). Buscamos la
        // combinación exacta "Windows Terminal\\..." o el subpath LocalState
        // que sólo tiene sentido para acceder al package de wt.
        var offenders = AllSourceFiles()
            .Where(f =>
            {
                var src = File.ReadAllText(f);
                // Path del unpackaged install de wt.
                if (src.Contains(@"Windows Terminal\settings.json")) return true;
                if (src.Contains("Windows Terminal/settings.json")) return true;
                // Subpath del package de Store que viviría en LocalState.
                if (src.Contains(@"WindowsTerminal", StringComparison.Ordinal)
                    && src.Contains("LocalState", StringComparison.Ordinal)) return true;
                return false;
            })
            .ToArray();
        Assert.True(offenders.Length == 0,
            "Algún .cs accede al settings.json de wt: " + string.Join(", ", offenders));
    }
}
