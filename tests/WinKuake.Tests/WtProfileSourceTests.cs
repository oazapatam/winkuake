using System.Reflection;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

public class WtProfileSourceTests
{
    [Fact]
    public void StripJsonComments_RemovesLineComments()
    {
        var input = "{\n  // comment\n  \"a\": 1\n}";
        var result = StripViaReflection(input);
        Assert.DoesNotContain("//", result);
        Assert.Contains("\"a\": 1", result);
    }

    [Fact]
    public void StripJsonComments_RemovesBlockComments()
    {
        var input = "{ /* hola */ \"a\": 1 }";
        var result = StripViaReflection(input);
        Assert.DoesNotContain("/*", result);
        Assert.Contains("\"a\": 1", result);
    }

    [Fact]
    public void StripJsonComments_PreservesCommentSyntaxInsideStrings()
    {
        var input = "{ \"path\": \"C:/Users // not a comment\" }";
        var result = StripViaReflection(input);
        Assert.Contains("// not a comment", result);
    }

    [Fact]
    public void StripJsonComments_HandlesEscapedQuotesInStrings()
    {
        var input = "{ \"a\": \"he said \\\"hi //\\\"\" /* tail */ }";
        var result = StripViaReflection(input);
        Assert.DoesNotContain("/* tail */", result);
        Assert.Contains("he said", result);
    }

    [Fact]
    public void Load_ReturnsEmptyOrPopulatedList_WithoutThrowing()
    {
        // No verificamos contenido específico (depende del entorno del usuario),
        // sólo que no explote leyendo settings.json real.
        var profiles = WtProfileSource.Load();
        Assert.NotNull(profiles);
    }

    private static string StripViaReflection(string s)
    {
        var mi = typeof(WtProfileSource).GetMethod("StripJsonComments",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        return (string)mi.Invoke(null, new object[] { s })!;
    }
}
