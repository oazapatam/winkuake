using System.Linq;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 8 (paleta de comandos) y Fase 10 (snippets editables).
/// Verifica invariantes específicos del PLAN.md no cubiertos por los tests
/// existentes: cantidad exacta de defaults, orden defaults+user, behavior
/// de filter con tokens degenerados, etc.
/// </summary>
public class PaletteAuditTests
{
    [Fact]
    public void Defaults_TieneVeintiunoExacto()
    {
        // PLAN Fase 8: "21 defaults (git, docker, npm, dotnet, etc.)".
        Assert.Equal(21, CommandSnippetService.Defaults().Count);
    }

    [Fact]
    public void Defaults_IncluyeFamiliasClave()
    {
        var d = CommandSnippetService.Defaults();
        // Las familias mencionadas en el PLAN deben estar todas presentes.
        Assert.Contains(d, s => s.Command.StartsWith("git "));
        Assert.Contains(d, s => s.Command.StartsWith("docker "));
        Assert.Contains(d, s => s.Command.StartsWith("npm "));
        Assert.Contains(d, s => s.Command.StartsWith("dotnet "));
    }

    [Fact]
    public void Defaults_NombresUnicos()
    {
        // Si dos snippets compartieran nombre, el ListBox de la paleta los
        // mostraría ambiguos. Garantizamos unicidad.
        var d = CommandSnippetService.Defaults();
        var names = d.Select(s => s.Name).ToHashSet();
        Assert.Equal(d.Count, names.Count);
    }

    [Fact]
    public void LoadAll_OrdenEsDefaultsSeguidoPorUser()
    {
        var defaults = CommandSnippetService.Defaults();
        var user = new[] { new CommandSnippet("u1", "u1cmd"), new CommandSnippet("u2", "u2cmd") };
        var all = CommandSnippetService.LoadAll(user);

        // Los primeros N son exactamente los defaults en el mismo orden.
        for (int i = 0; i < defaults.Count; i++)
            Assert.Equal(defaults[i], all[i]);
        // Los últimos son los del user.
        Assert.Equal("u1", all[defaults.Count].Name);
        Assert.Equal("u2", all[defaults.Count + 1].Name);
    }

    [Fact]
    public void Filter_QuerySoloEspaciosTrataComoVacia()
    {
        // "    " debe comportarse como query vacía → devuelve todo.
        var all = CommandSnippetService.Defaults();
        var r = CommandSnippetService.Filter(all, "    ");
        Assert.Equal(all.Count, r.Count);
    }

    [Fact]
    public void Filter_TokensSeparadosPorVariosEspacios()
    {
        // PLAN: "todos los tokens deben matchear name o command".
        // El split debe descartar entradas vacías por espacios consecutivos.
        var snippets = new[]
        {
            new CommandSnippet("git log", "git log --oneline"),
            new CommandSnippet("git status", "git status"),
        };
        var r = CommandSnippetService.Filter(snippets, "git    log");
        Assert.Single(r);
        Assert.Equal("git log", r[0].Name);
    }

    [Fact]
    public void Filter_TokensMatcheanMezclandoNombreYComando()
    {
        // Un token puede matchear el nombre, otro el comando — basta con que
        // CADA token matchee al menos uno de los dos.
        var snippets = new[]
        {
            new CommandSnippet("Logs", "tail -f /var/log/syslog"),
            new CommandSnippet("Build", "make all"),
        };
        // "logs syslog": "logs" matchea Name, "syslog" matchea Command.
        var r = CommandSnippetService.Filter(snippets, "logs syslog");
        Assert.Single(r);
    }

    [Fact]
    public void LoadAll_UserSnippetsConMismoNombreNoDeduplican()
    {
        // No deduplicamos: si el usuario crea un snippet con el mismo nombre
        // que un default, ambos aparecen. Es responsabilidad del usuario.
        var user = new[] { new CommandSnippet("git status", "git status -s") };
        var all = CommandSnippetService.LoadAll(user);
        var matches = all.Where(s => s.Name == "git status").ToList();
        Assert.Equal(2, matches.Count);
    }

    [Fact]
    public void CommandSnippet_RecordEqualityPorValor()
    {
        // record equality es importante para el binding del ListBox.
        var a = new CommandSnippet("X", "x");
        var b = new CommandSnippet("X", "x");
        Assert.Equal(a, b);
        Assert.True(a == b);
    }

    [Fact]
    public void UserSnippet_PocoConDefaultsVacios()
    {
        var s = new UserSnippet();
        Assert.Equal("", s.Name);
        Assert.Equal("", s.Command);
    }
}
