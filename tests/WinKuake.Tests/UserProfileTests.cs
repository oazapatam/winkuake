using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Contrato de Fase 20.0: modelo UserProfile + AppSettings.UserProfiles +
/// DefaultProfileId + stub ProfileRegistry. Los agentes A/B/C asumen estos
/// invariantes.
/// </summary>
public class UserProfileTests
{
    [Fact]
    public void UserProfile_DefaultsRazonables()
    {
        var p = new UserProfile();
        Assert.Equal("",       p.Id);
        Assert.Equal("",       p.Name);
        Assert.Equal("",       p.CommandLine);
        Assert.Null(p.StartingDirectory);
        Assert.Null(p.IconGlyph);
        Assert.Equal("Custom", p.Source);
        Assert.False(p.Hidden);
    }

    [Fact]
    public void UserProfile_DeepCloneIndependiente()
    {
        var src = new UserProfile
        {
            Id = "abc-123",
            Name = "PowerShell",
            CommandLine = "pwsh.exe",
            StartingDirectory = @"C:\dev",
            IconGlyph = "⚡",
            Source = "Detected",
            Hidden = true,
        };
        var dst = src.DeepClone();
        dst.Name = "otra";
        dst.Hidden = false;
        Assert.Equal("PowerShell", src.Name);
        Assert.True(src.Hidden);
        Assert.Equal("abc-123", dst.Id);
    }

    [Fact]
    public void UserProfile_RoundtripJson()
    {
        var src = new UserProfile
        {
            Id = "guid-x",
            Name = "Ubuntu",
            CommandLine = "wsl.exe -d Ubuntu --shell-type login",
            StartingDirectory = null,
            IconGlyph = "🐧",
            Source = "Detected",
            Hidden = false,
        };
        var json = JsonSerializer.Serialize(src);
        var dst  = JsonSerializer.Deserialize<UserProfile>(json)!;
        Assert.Equal(src.Id, dst.Id);
        Assert.Equal(src.Name, dst.Name);
        Assert.Equal(src.CommandLine, dst.CommandLine);
        Assert.Equal(src.IconGlyph, dst.IconGlyph);
        Assert.Equal(src.Source, dst.Source);
    }

    [Fact]
    public void AppSettings_UserProfilesEsListaVaciaPorDefecto()
    {
        var s = new AppSettings();
        Assert.NotNull(s.UserProfiles);
        Assert.Empty(s.UserProfiles);
        Assert.Null(s.DefaultProfileId);
    }

    [Fact]
    public void AppSettings_DeepCloneIncluyeUserProfilesYDefault()
    {
        var src = new AppSettings
        {
            DefaultProfileId = "id-default",
            UserProfiles = new()
            {
                new() { Id = "a", Name = "PS",    CommandLine = "pwsh.exe" },
                new() { Id = "b", Name = "Cmd",   CommandLine = "cmd.exe" },
            }
        };
        var dst = src.DeepClone();
        dst.UserProfiles[0].Name = "MUTADO";
        dst.DefaultProfileId = "otro";

        Assert.Equal("PS", src.UserProfiles[0].Name);
        Assert.Equal("id-default", src.DefaultProfileId);
        Assert.Equal(2, dst.UserProfiles.Count);
    }

    // ---- ProfileRegistry stub ---------------------------------------------

    [Fact]
    public void Registry_LoadAll_ListaVaciaCuandoSettingsVacio()
    {
        var s = new AppSettings();
        Assert.Empty(ProfileRegistry.LoadAll(s));
    }

    [Fact]
    public void Registry_LoadAll_FiltraOcultos()
    {
        var s = new AppSettings
        {
            UserProfiles = new()
            {
                new() { Id = "a", Name = "Visible", CommandLine = "pwsh.exe", Hidden = false },
                new() { Id = "b", Name = "Oculto",  CommandLine = "cmd.exe",  Hidden = true  },
            }
        };
        var result = ProfileRegistry.LoadAll(s);
        Assert.Single(result);
        Assert.Equal("Visible", result[0].Name);
    }

    [Fact]
    public void Registry_ToTerminalProfile_MapeaCampos()
    {
        var p = new UserProfile
        {
            Id = "guid",
            Name = "X",
            CommandLine = "pwsh.exe -NoExit",
            StartingDirectory = @"C:\dev",
        };
        var t = ProfileRegistry.ToTerminalProfile(p, isDefault: true);
        Assert.Equal("X",                   t.DisplayName);
        Assert.Equal("guid",                t.Guid);
        Assert.Equal("pwsh.exe -NoExit",    t.CommandLine);
        Assert.Equal(@"C:\dev",             t.StartingDirectory);
        Assert.True(t.IsDefault);
    }

    [Fact]
    public void Registry_ToTerminalProfile_StartingDirectoryVacioEsNull()
    {
        var p = new UserProfile { Id = "g", Name = "X", CommandLine = "cmd.exe", StartingDirectory = "" };
        Assert.Null(ProfileRegistry.ToTerminalProfile(p).StartingDirectory);
    }

    [Fact]
    public void Registry_ResolveDefault_ListaVaciaDevuelveNull()
    {
        Assert.Null(ProfileRegistry.ResolveDefault(new List<UserProfile>(), defaultId: null));
        Assert.Null(ProfileRegistry.ResolveDefault(new List<UserProfile>(), defaultId: "x"));
    }

    [Fact]
    public void Registry_ResolveDefault_RespetaIdPersistido()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "PS",   CommandLine = "pwsh.exe" },
            new() { Id = "b", Name = "Cmd",  CommandLine = "cmd.exe"  },
            new() { Id = "c", Name = "WSL",  CommandLine = "wsl.exe -d Ubuntu" },
        };
        var d = ProfileRegistry.ResolveDefault(profiles, "b");
        Assert.NotNull(d);
        Assert.Equal("Cmd", d!.Name);
    }

    [Fact]
    public void Registry_ResolveDefault_FallbackHeuristico_PreferePwsh()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "Cmd",          CommandLine = "cmd.exe" },
            new() { Id = "b", Name = "PowerShell",   CommandLine = "powershell.exe" },
            new() { Id = "c", Name = "PowerShell 7", CommandLine = "pwsh.exe" },
        };
        var d = ProfileRegistry.ResolveDefault(profiles, defaultId: null);
        Assert.Equal("PowerShell 7", d!.Name);
    }

    [Fact]
    public void Registry_ResolveDefault_FallbackHeuristico_LuegoPowershell()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "Cmd",         CommandLine = "cmd.exe" },
            new() { Id = "b", Name = "PowerShell",  CommandLine = "powershell.exe" },
        };
        var d = ProfileRegistry.ResolveDefault(profiles, defaultId: null);
        Assert.Equal("PowerShell", d!.Name);
    }

    [Fact]
    public void Registry_ResolveDefault_FallbackHeuristico_LuegoCmd()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "Bash", CommandLine = @"C:\Program Files\Git\bin\bash.exe" },
            new() { Id = "b", Name = "Cmd",  CommandLine = "cmd.exe" },
        };
        var d = ProfileRegistry.ResolveDefault(profiles, defaultId: null);
        Assert.Equal("Cmd", d!.Name);
    }

    [Fact]
    public void Registry_ResolveDefault_FallbackHeuristico_PrimeroSiNadaCalza()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "Bash", CommandLine = @"C:\Program Files\Git\bin\bash.exe" },
            new() { Id = "b", Name = "WSL",  CommandLine = "wsl.exe -d Ubuntu" },
        };
        var d = ProfileRegistry.ResolveDefault(profiles, defaultId: null);
        Assert.Equal("Bash", d!.Name);
    }

    [Fact]
    public void Registry_ResolveDefault_IdInexistenteCaeAlHeuristico()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "Cmd",         CommandLine = "cmd.exe" },
            new() { Id = "b", Name = "PowerShell",  CommandLine = "pwsh.exe" },
        };
        var d = ProfileRegistry.ResolveDefault(profiles, defaultId: "no-existe");
        Assert.Equal("PowerShell", d!.Name);
    }
}
