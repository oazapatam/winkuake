using System.Collections.Generic;
using System.Linq;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Tests de la lógica que MainWindow usa para mapear UserProfile → TerminalProfile
/// y resolver el default. La lógica vive en <see cref="ProfileMapping"/> para
/// poder testearla sin instanciar WPF.
/// </summary>
public class MainWindowProfilesIntegrationTests
{
    private static UserProfile MakePwsh(string id = "id-pwsh") => new()
    {
        Id = id, Name = "PowerShell", CommandLine = "pwsh.exe", Source = "Detected"
    };
    private static UserProfile MakeWinPs(string id = "id-winps") => new()
    {
        Id = id, Name = "Windows PowerShell", CommandLine = "powershell.exe", Source = "Detected"
    };
    private static UserProfile MakeCmd(string id = "id-cmd") => new()
    {
        Id = id, Name = "Símbolo del sistema", CommandLine = "cmd.exe", Source = "Detected"
    };
    private static UserProfile MakeUbuntu(string id = "id-ubuntu") => new()
    {
        Id = id, Name = "Ubuntu",
        CommandLine = "wsl.exe -d Ubuntu --shell-type login --cd ~",
        Source = "Detected"
    };

    [Fact]
    public void BuildTerminalProfiles_EmptyList_ReturnsEmpty()
    {
        var result = ProfileMapping.BuildTerminalProfiles(new List<UserProfile>(), null);
        Assert.Empty(result);
    }

    [Fact]
    public void BuildTerminalProfiles_OneProfile_BecomesDefaultByHeuristic()
    {
        var users = new[] { MakePwsh() };
        var result = ProfileMapping.BuildTerminalProfiles(users, null);
        Assert.Single(result);
        Assert.True(result[0].IsDefault);
        Assert.Equal("pwsh.exe", result[0].CommandLine);
        Assert.Equal("PowerShell", result[0].DisplayName);
    }

    [Fact]
    public void BuildTerminalProfiles_DefaultProfileIdSet_RespectsExplicitChoice()
    {
        var users = new[] { MakePwsh(), MakeWinPs(), MakeCmd() };
        var result = ProfileMapping.BuildTerminalProfiles(users, "id-cmd");
        Assert.Equal(3, result.Length);
        Assert.False(result[0].IsDefault); // pwsh
        Assert.False(result[1].IsDefault); // winps
        Assert.True(result[2].IsDefault);  // cmd ← explícito
    }

    [Fact]
    public void BuildTerminalProfiles_HeuristicPrefersPwshOverWindowsPowerShellOverCmd()
    {
        var users = new[] { MakeCmd(), MakeWinPs(), MakePwsh() };
        var result = ProfileMapping.BuildTerminalProfiles(users, null);
        var def = result.Single(p => p.IsDefault);
        Assert.Equal("PowerShell", def.DisplayName); // pwsh gana
    }

    [Fact]
    public void BuildTerminalProfiles_PreservesGuidIntoTerminalProfile()
    {
        var users = new[] { MakeUbuntu("ubuntu-guid-42") };
        var result = ProfileMapping.BuildTerminalProfiles(users, null);
        Assert.Equal("ubuntu-guid-42", result[0].Guid);
    }

    [Fact]
    public void BuildTerminalProfiles_StartingDirectoryEmpty_BecomesNull()
    {
        var users = new[] { new UserProfile
        {
            Id = "x", Name = "x", CommandLine = "x.exe", StartingDirectory = ""
        }};
        var result = ProfileMapping.BuildTerminalProfiles(users, null);
        Assert.Null(result[0].StartingDirectory);
    }

    [Fact]
    public void BuildTerminalProfiles_StartingDirectoryPopulated_PassesThrough()
    {
        var users = new[] { new UserProfile
        {
            Id = "x", Name = "x", CommandLine = "x.exe",
            StartingDirectory = @"C:\dev"
        }};
        var result = ProfileMapping.BuildTerminalProfiles(users, null);
        Assert.Equal(@"C:\dev", result[0].StartingDirectory);
    }

    [Fact]
    public void BuildTerminalProfiles_DefaultProfileIdNonexistent_FallsBackToHeuristic()
    {
        var users = new[] { MakePwsh(), MakeCmd() };
        var result = ProfileMapping.BuildTerminalProfiles(users, "id-nonexistent");
        Assert.True(result[0].IsDefault); // pwsh por heurística
        Assert.False(result[1].IsDefault);
    }

    // ---------- Ya cubrimos ResolvePersisted en ProfileMapping ----------

    [Fact]
    public void ResolvePersisted_EmptyList_ReturnsNull()
    {
        var result = ProfileMapping.ResolvePersisted(System.Array.Empty<TerminalProfile>(), "g", "n");
        Assert.Null(result);
    }

    [Fact]
    public void ResolvePersisted_MatchByGuid_ExactCase()
    {
        var profiles = ProfileMapping.BuildTerminalProfiles(new[]
        {
            MakePwsh("guid-A"), MakeCmd("guid-B")
        }, null);
        var match = ProfileMapping.ResolvePersisted(profiles, "guid-B", null);
        Assert.NotNull(match);
        Assert.Equal("Símbolo del sistema", match!.DisplayName);
    }

    [Fact]
    public void ResolvePersisted_MatchByGuid_CaseInsensitive()
    {
        var profiles = ProfileMapping.BuildTerminalProfiles(new[]
        {
            MakePwsh("GUID-AbC")
        }, null);
        var match = ProfileMapping.ResolvePersisted(profiles, "guid-abc", null);
        Assert.NotNull(match);
        Assert.Equal("PowerShell", match!.DisplayName);
    }

    [Fact]
    public void ResolvePersisted_GuidMissing_FallsBackToName()
    {
        var profiles = ProfileMapping.BuildTerminalProfiles(new[]
        {
            MakePwsh("guid-X"), MakeUbuntu("guid-Y")
        }, null);
        var match = ProfileMapping.ResolvePersisted(profiles, guid: null, name: "Ubuntu");
        Assert.NotNull(match);
        Assert.Equal("Ubuntu", match!.DisplayName);
    }

    [Fact]
    public void ResolvePersisted_NameMatchesCaseInsensitive()
    {
        var profiles = ProfileMapping.BuildTerminalProfiles(new[]
        {
            MakeUbuntu("g")
        }, null);
        var match = ProfileMapping.ResolvePersisted(profiles, null, "ubuntu");
        Assert.NotNull(match);
        Assert.Equal("Ubuntu", match!.DisplayName);
    }

    [Fact]
    public void ResolvePersisted_NoMatch_FallsBackToDefaultMarked()
    {
        var profiles = ProfileMapping.BuildTerminalProfiles(new[]
        {
            MakePwsh("guid-A"), MakeCmd("guid-B")
        }, defaultId: "guid-B");
        var match = ProfileMapping.ResolvePersisted(profiles, "ghost", "fantasma");
        Assert.NotNull(match);
        Assert.True(match!.IsDefault);
        Assert.Equal("Símbolo del sistema", match.DisplayName);
    }

    [Fact]
    public void ResolvePersisted_NoMatchAndNoDefault_ReturnsFirst()
    {
        var profiles = new[]
        {
            new TerminalProfile("Custom", "") { CommandLine = "x.exe" },
            new TerminalProfile("Otro", "")   { CommandLine = "y.exe" },
        };
        var match = ProfileMapping.ResolvePersisted(profiles, "g", "n");
        Assert.NotNull(match);
        Assert.Equal("Custom", match!.DisplayName);
    }

    // ---------- HardFallback ----------

    [Fact]
    public void HardFallback_AlwaysReturnsRunnableShell()
    {
        var fb = ProfileMapping.HardFallback();
        Assert.False(string.IsNullOrEmpty(fb.CommandLine));
        Assert.Contains("powershell", fb.CommandLine!.ToLowerInvariant());
    }

    // ---------- Hidden filter (cubre el contrato de ProfileRegistry.LoadAll) ----------

    [Fact]
    public void ProfileRegistry_LoadAll_FiltersHiddenProfiles()
    {
        var settings = new AppSettings
        {
            UserProfiles = new List<UserProfile>
            {
                MakePwsh("a"),
                new() { Id = "b", Name = "Hidden", CommandLine = "x.exe", Hidden = true },
                MakeCmd("c"),
            }
        };
        var loaded = ProfileRegistry.LoadAll(settings);
        Assert.Equal(2, loaded.Count);
        Assert.DoesNotContain(loaded, p => p.Name == "Hidden");
    }

    [Fact]
    public void ProfileRegistry_ResolveDefault_RespectsExplicitId()
    {
        var profiles = new[]
        {
            MakePwsh("a"),
            MakeCmd("b"),
        };
        var def = ProfileRegistry.ResolveDefault(profiles, "b");
        Assert.NotNull(def);
        Assert.Equal("Símbolo del sistema", def!.Name);
    }

    [Fact]
    public void ProfileRegistry_ResolveDefault_NullList_ReturnsNull()
    {
        var def = ProfileRegistry.ResolveDefault(System.Array.Empty<UserProfile>(), null);
        Assert.Null(def);
    }
}
