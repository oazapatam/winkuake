using System.Linq;
using System.Reflection;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 12 — argumento CLI <c>--cwd</c>. App.ParseArg es privado;
/// usamos reflection sobre el assembly de WinKuake para validar el contrato
/// que documenta el plan: soporta tanto <c>--cwd path</c> como <c>--cwd=path</c>.
/// </summary>
public class CliArgAuditTests
{
    private static string? Invoke(params string[] args)
    {
        // App está en el assembly de WinKuake. Cargamos vía typeof(SettingsService)
        // para forzar el assembly load sin instanciar Application (que requiere STA).
        var asm = typeof(WinKuake.Services.SettingsService).Assembly;
        var appType = asm.GetType("WinKuake.App")!;
        var method = appType.GetMethod("ParseArg",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string?)method.Invoke(null, new object[] { args, "--cwd" });
    }

    [Fact]
    public void ParseArg_FormatoEspaciado_DevuelveValor()
    {
        var r = Invoke("--cwd", @"C:\proj");
        Assert.Equal(@"C:\proj", r);
    }

    [Fact]
    public void ParseArg_FormatoIgual_DevuelveValor()
    {
        var r = Invoke(@"--cwd=C:\proj");
        Assert.Equal(@"C:\proj", r);
    }

    [Fact]
    public void ParseArg_Ausente_DevuelveNull()
    {
        Assert.Null(Invoke("--start-shown", "--otra"));
    }

    [Fact]
    public void ParseArg_VariosArgumentos_FuncionaEnCualquierPosicion()
    {
        var r = Invoke("--start-shown", "--cwd", @"D:\foo", "--otra");
        Assert.Equal(@"D:\foo", r);
    }

    [Fact]
    public void ParseArg_CaseInsensitive()
    {
        // ParseArg compara con OrdinalIgnoreCase (estilo Windows CLI).
        Assert.Equal(@"C:\x", Invoke("--CWD", @"C:\x"));
        Assert.Equal(@"C:\y", Invoke(@"--Cwd=C:\y"));
    }

    [Fact]
    public void ParseArg_FormatoIgualConValorVacio_DevuelveCadenaVacia()
    {
        // "--cwd=" : la app no debe lanzar; devuelve "" y MainWindow lo descarta
        // por Directory.Exists check.
        var r = Invoke("--cwd=");
        Assert.Equal("", r);
    }

    [Fact]
    public void ParseArg_SoloFlag_SinValor_DevuelveNull()
    {
        // "--cwd" sin valor a continuación: el bucle for itera hasta length-1,
        // así que ninguna iteración cumple la condición → null.
        Assert.Null(Invoke("--cwd"));
    }

    [Fact]
    public void ParseArg_BuscaArgumentoDistinto_NoConfundeNombres()
    {
        // Aseguramos que --cwd-extended no se confunda con --cwd.
        var asm = typeof(WinKuake.Services.SettingsService).Assembly;
        var appType = asm.GetType("WinKuake.App")!;
        var method = appType.GetMethod("ParseArg",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        // Pasamos --cwd-extended pero buscamos --cwd. Como hay match de prefijo
        // en el bucle "StartsWith(name + "=")", solo "--cwd-extended=" empieza
        // por "--cwd=" si el separador fuera "-". No hay falso positivo:
        // "--cwd-extended" no empieza por "--cwd=".
        var r = (string?)method.Invoke(null, new object[] { new[] { "--cwd-extended", "X" }, "--cwd" });
        Assert.Null(r);
    }
}
