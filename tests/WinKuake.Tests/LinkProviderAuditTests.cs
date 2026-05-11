using System;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 7: link provider de paths a archivos en terminal.html
/// debe detectar Windows (C:\…), Linux absolutos (/usr/…) y relativos (./, ../),
/// y postear "openFile" al host. Replicamos la regex usada en el HTML para
/// validar que case-discriminamos correctamente sin falsos positivos en URLs.
/// </summary>
public class LinkProviderAuditTests
{
    private static string ResourcesDir
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir is not null; i++)
            {
                var c = Path.Combine(dir, "src", "WinKuake", "Resources", "terminal");
                if (Directory.Exists(c)) return c;
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("Resources/terminal no encontrado");
        }
    }

    private static string ReadHtml() => File.ReadAllText(Path.Combine(ResourcesDir, "terminal.html"));

    [Fact]
    public void Html_RegistersLinkProvider()
    {
        var html = ReadHtml();
        Assert.Contains("registerLinkProvider", html);
    }

    [Fact]
    public void Html_LinkProvider_PostsOpenFile()
    {
        var html = ReadHtml();
        Assert.Matches(@"type\s*:\s*['""]openFile['""]", html);
    }

    [Fact]
    public void Html_LinkProvider_SkipsHttpUrls()
    {
        // Los addons web-links manejan http(s); el file-link debe ignorarlas
        // para no duplicar handlers. La regex JS literal es /^https?:\/\//;
        // en el HTML aparece exactamente como esa secuencia de caracteres.
        var html = ReadHtml();
        Assert.Contains(@"^https?:\/\/", html);
    }

    // -- Replicamos la regex y validamos con casos representativos. --------
    // La regex literal en terminal.html (línea ~328):
    //   /(?:[A-Za-z]:[\\/][^\s"'<>|*?]+|\.{0,2}\/[^\s"'<>|*?:]+|\/[a-zA-Z][^\s"'<>|*?]+)/g
    private static readonly Regex FilePath = new(
        @"(?:[A-Za-z]:[\\/][^\s""'<>|*?]+|\.{0,2}\/[^\s""'<>|*?:]+|\/[a-zA-Z][^\s""'<>|*?]+)");

    [Theory]
    [InlineData(@"C:\Users\foo\bar.txt")]
    [InlineData(@"C:/Users/foo/bar.txt")]
    [InlineData(@"D:\code\proj\src\main.cs")]
    public void Regex_Matches_WindowsAbsolutePaths(string path)
    {
        Assert.Matches(FilePath, path);
    }

    [Theory]
    [InlineData("/usr/bin/python3")]
    [InlineData("/home/user/project/file.go")]
    [InlineData("/etc/hostname")]
    public void Regex_Matches_LinuxAbsolutePaths(string path)
    {
        Assert.Matches(FilePath, path);
    }

    [Theory]
    [InlineData("./script.sh")]
    [InlineData("../parent/file.txt")]
    [InlineData("./src/main.rs")]
    public void Regex_Matches_RelativePaths(string path)
    {
        Assert.Matches(FilePath, path);
    }

    [Fact]
    public void TerminalHtml_LinkProvider_IsGuardedByFeatureCheck()
    {
        // El handler debe estar dentro de "if (term.registerLinkProvider) {"
        // para no romper si la versión de xterm.js no expone la API.
        var html = ReadHtml();
        Assert.Matches(@"if\s*\(\s*term\.registerLinkProvider\s*\)", html);
    }
}
