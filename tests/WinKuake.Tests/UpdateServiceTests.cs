using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Cubre la lógica pura de UpdateService: comparación de versiones y parser
/// del JSON de la GitHub Releases API. La llamada HTTP real no se testea aquí.
/// </summary>
public class UpdateServiceTests
{
    // ---------- IsNewer ----------

    [Fact]
    public void IsNewer_RemoteGreater_ReturnsTrue()
    {
        Assert.True(UpdateService.IsNewer("0.2.0", "0.1.0"));
    }

    [Fact]
    public void IsNewer_Equal_ReturnsFalse()
    {
        Assert.False(UpdateService.IsNewer("0.1.0", "0.1.0"));
    }

    [Fact]
    public void IsNewer_RemoteOlder_ReturnsFalse()
    {
        Assert.False(UpdateService.IsNewer("0.1.0", "0.2.0"));
    }

    [Fact]
    public void IsNewer_RemoteHasVPrefix_StripsAndCompares()
    {
        Assert.True(UpdateService.IsNewer("v0.2.0", "0.1.0"));
        Assert.False(UpdateService.IsNewer("v0.1.0", "0.1.0"));
    }

    [Fact]
    public void IsNewer_LocalHasVPrefix_StripsAndCompares()
    {
        Assert.True(UpdateService.IsNewer("0.2.0", "v0.1.0"));
    }

    [Fact]
    public void IsNewer_MalformedTag_ReturnsFalse()
    {
        Assert.False(UpdateService.IsNewer("not-a-version", "0.1.0"));
        Assert.False(UpdateService.IsNewer("0.2.0", "garbage"));
        Assert.False(UpdateService.IsNewer("", "0.1.0"));
    }

    [Fact]
    public void IsNewer_FourPartVersions_Work()
    {
        Assert.True(UpdateService.IsNewer("0.1.0.1", "0.1.0.0"));
        Assert.False(UpdateService.IsNewer("0.1.0.0", "0.1.0.0"));
    }

    // ---------- ParseRelease ----------

    [Fact]
    public void ParseRelease_ValidJsonWithExeAsset_ReturnsInfo()
    {
        const string json = """
        {
          "tag_name": "v0.2.0",
          "body": "Notas de release",
          "assets": [
            { "name": "WinKuake-Setup.exe", "browser_download_url": "https://example.com/WinKuake-Setup.exe" }
          ]
        }
        """;

        var info = UpdateService.ParseRelease(json);

        Assert.NotNull(info);
        Assert.Equal("v0.2.0", info!.Version);
        Assert.Equal("https://example.com/WinKuake-Setup.exe", info.DownloadUrl);
        Assert.Equal("Notas de release", info.ReleaseNotes);
    }

    [Fact]
    public void ParseRelease_NoExeAsset_ReturnsNull()
    {
        const string json = """
        {
          "tag_name": "v0.2.0",
          "assets": [
            { "name": "source.zip", "browser_download_url": "https://example.com/source.zip" }
          ]
        }
        """;

        Assert.Null(UpdateService.ParseRelease(json));
    }

    [Fact]
    public void ParseRelease_EmptyAssets_ReturnsNull()
    {
        const string json = """
        {
          "tag_name": "v0.2.0",
          "assets": []
        }
        """;

        Assert.Null(UpdateService.ParseRelease(json));
    }

    [Fact]
    public void ParseRelease_MissingTag_ReturnsNull()
    {
        const string json = """
        {
          "assets": [
            { "name": "WinKuake.exe", "browser_download_url": "https://example.com/WinKuake.exe" }
          ]
        }
        """;

        Assert.Null(UpdateService.ParseRelease(json));
    }

    [Fact]
    public void ParseRelease_MultipleAssets_PicksFirstExe()
    {
        const string json = """
        {
          "tag_name": "v0.3.0",
          "body": null,
          "assets": [
            { "name": "checksums.txt", "browser_download_url": "https://example.com/checksums.txt" },
            { "name": "WinKuake-0.3.0.exe", "browser_download_url": "https://example.com/WinKuake-0.3.0.exe" },
            { "name": "other.exe", "browser_download_url": "https://example.com/other.exe" }
          ]
        }
        """;

        var info = UpdateService.ParseRelease(json);

        Assert.NotNull(info);
        Assert.Equal("v0.3.0", info!.Version);
        Assert.Equal("https://example.com/WinKuake-0.3.0.exe", info.DownloadUrl);
        Assert.Null(info.ReleaseNotes);
    }

    [Fact]
    public void ParseRelease_GarbageJson_ReturnsNull()
    {
        Assert.Null(UpdateService.ParseRelease("{ not json"));
        Assert.Null(UpdateService.ParseRelease(""));
    }

    [Fact]
    public void ParseRelease_CaseInsensitiveExeMatch()
    {
        const string json = """
        {
          "tag_name": "v0.2.0",
          "assets": [
            { "name": "WinKuake.EXE", "browser_download_url": "https://example.com/WinKuake.EXE" }
          ]
        }
        """;

        var info = UpdateService.ParseRelease(json);
        Assert.NotNull(info);
        Assert.Equal("https://example.com/WinKuake.EXE", info!.DownloadUrl);
    }
}
