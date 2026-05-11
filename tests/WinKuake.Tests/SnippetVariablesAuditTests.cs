using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fases 9, 10, 12 — variables de snippet ({cwd}, {home}, {user},
/// {date}, {branch}, {selection}). Cubre invariantes que los tests
/// existentes no validan: case-insensitive estricto, anidamiento, valores
/// con caracteres especiales, etc.
/// </summary>
public class SnippetVariablesAuditTests
{
    [Fact]
    public void Expand_HomeYUser_SeReemplazanCorrectamente()
    {
        var ctx = new SnippetContext { Home = "/home/foo", User = "foo" };
        Assert.Equal("/home/foo/.config/foo", CommandSnippetService.Expand("{home}/.config/{user}", ctx));
    }

    [Fact]
    public void Expand_TodasLasVariablesEnUnComando()
    {
        var ctx = new SnippetContext
        {
            Cwd = "/proj", Home = "/h", User = "u",
            Branch = "main", Selection = "sel",
            Date = new System.DateTime(2026, 5, 10),
        };
        var result = CommandSnippetService.Expand(
            "U={user} H={home} D={cwd} B={branch} S={selection} T={date}", ctx);
        Assert.Equal("U=u H=/h D=/proj B=main S=sel T=2026-05-10", result);
    }

    [Fact]
    public void Expand_PreservaCaracteresEspecialesEnValores()
    {
        // El valor puede contener cualquier cosa (espacios, símbolos): se inserta tal cual.
        var ctx = new SnippetContext { Cwd = "/path with spaces/sub-dir & more" };
        var r = CommandSnippetService.Expand("cd {cwd}", ctx);
        Assert.Equal("cd /path with spaces/sub-dir & more", r);
    }

    [Fact]
    public void Expand_PlaceholdersConDigitos_NoSeReemplazan()
    {
        // El regex solo acepta [a-zA-Z_]+, así que {var1} no se interpreta.
        var ctx = new SnippetContext { Cwd = "/x" };
        Assert.Equal("hola {var1}", CommandSnippetService.Expand("hola {var1}", ctx));
    }

    [Fact]
    public void Expand_LlavesSinPalabra_SeDejanLiteral()
    {
        var ctx = new SnippetContext { Cwd = "/x" };
        Assert.Equal("hola {} {}", CommandSnippetService.Expand("hola {} {}", ctx));
    }

    [Fact]
    public void Expand_SinVariables_DevuelveStringIdentico()
    {
        var ctx = new SnippetContext { Cwd = "/x", Home = "/h" };
        Assert.Equal("ls -la", CommandSnippetService.Expand("ls -la", ctx));
    }

    [Fact]
    public void Expand_ContextoVacio_DejaPlaceholdersLiterales()
    {
        var ctx = new SnippetContext();
        // Sin valores, ninguna variable se reemplaza.
        Assert.Equal("{cwd}/{home}", CommandSnippetService.Expand("{cwd}/{home}", ctx));
    }

    [Fact]
    public void Expand_CommandVacio_DevuelveStringVacio()
    {
        Assert.Equal("", CommandSnippetService.Expand("", new SnippetContext { Cwd = "/x" }));
    }

    [Fact]
    public void Expand_CommandNull_NoLanza()
    {
        // Defensivo: si por algún motivo command es null/empty, devolvemos sin tocar.
        Assert.Equal("", CommandSnippetService.Expand("", null));
    }

    [Fact]
    public void Expand_VariableMixedCase_SeReemplaza()
    {
        // PLAN Fase 9: "Case-insensitive". Probamos camelCase, UPPER, mIxEd.
        var ctx = new SnippetContext { Branch = "develop" };
        Assert.Equal("develop", CommandSnippetService.Expand("{Branch}", ctx));
        Assert.Equal("develop", CommandSnippetService.Expand("{BRANCH}", ctx));
        Assert.Equal("develop", CommandSnippetService.Expand("{bRaNcH}", ctx));
    }

    [Fact]
    public void Expand_DateConFechaConocida_FormatoIsoEstricto()
    {
        // PLAN Fase 10: "{date} expande a yyyy-MM-dd".
        var ctx = new SnippetContext { Date = new System.DateTime(2026, 1, 5) };
        // Comprobamos zero-padding tanto en mes como en día.
        Assert.Equal("2026-01-05", CommandSnippetService.Expand("{date}", ctx));
    }

    [Fact]
    public void Expand_SelectionConMultilinea_PreservaSaltos()
    {
        var ctx = new SnippetContext { Selection = "line1\nline2\nline3" };
        Assert.Equal("echo \"line1\nline2\nline3\"",
            CommandSnippetService.Expand("echo \"{selection}\"", ctx));
    }

    [Fact]
    public void Expand_SelectionVacioStringVacio_TrataComoNoExpansion()
    {
        // Si la propiedad es exactamente "" (string vacía), Expand la inserta.
        // Esto es comportamiento documentado: null=literal, ""=expand a vacío.
        var ctx = new SnippetContext { Selection = "" };
        Assert.Equal("echo ''", CommandSnippetService.Expand("echo '{selection}'", ctx));
    }
}
