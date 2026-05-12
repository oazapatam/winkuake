using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Audita el script <c>build-portable.ps1</c>: garantiza que los flags
/// críticos para producir un .exe self-contained, single-file y sin
/// trim no se pierdan en futuros refactors. Cada flag tiene un
/// "por qué" concreto y romper alguno produce un binario distinto.
/// </summary>
public class BuildPortableScriptTests
{
    private static string ScriptPath
    {
        get
        {
            var dir = System.AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir, "build-portable.ps1");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new FileNotFoundException("build-portable.ps1 no encontrado");
        }
    }

    private static string ScriptText => File.ReadAllText(ScriptPath);

    [Fact]
    public void Script_Existe()
    {
        Assert.True(File.Exists(ScriptPath));
    }

    [Fact]
    public void Script_EmpaquetaElRuntimeDeNet()
    {
        // Sin --self-contained true el .exe requiere .NET 10 instalado en la
        // máquina del usuario, perdiendo el sentido de "portable".
        Assert.Matches(new Regex(@"--self-contained\s+true"), ScriptText);
    }

    [Fact]
    public void Script_GeneraUnSoloEjecutable()
    {
        // PublishSingleFile junta los DLLs gestionados en el .exe.
        Assert.Matches(new Regex(@"-p:PublishSingleFile=true"), ScriptText);
    }

    [Fact]
    public void Script_EmbedeAssetsDeTerminal()
    {
        // IncludeAllContentForSelfExtract incrusta terminal.html, xterm.js
        // y addons dentro del .exe (se extraen a temp al arrancar).
        // Sin esto, esos archivos viven como loose files al lado del .exe
        // y el "portable" deja de ser un único binario.
        Assert.Matches(new Regex(@"-p:IncludeAllContentForSelfExtract=true"), ScriptText);
    }

    [Fact]
    public void Script_NoActivaTrim()
    {
        // PublishTrimmed=true rompe WPF (reflection sobre tipos del runtime,
        // resources XAML, etc.). Mantenerlo explícitamente en false evita
        // que alguien lo prenda "para hacer el .exe más chico" y rompa
        // recursos en runtime.
        Assert.Matches(new Regex(@"-p:PublishTrimmed=false"), ScriptText);
    }

    [Fact]
    public void Script_TargetWindowsX64()
    {
        // Sólo soportamos Windows x64. ARM64 o x86 quedarían como futuras
        // tareas; ahora mismo el manifest y las P/Invoke asumen x64.
        Assert.Matches(new Regex(@"-r\s+win-x64"), ScriptText);
    }
}
