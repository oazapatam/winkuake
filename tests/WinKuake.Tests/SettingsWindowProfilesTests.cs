using System.Collections.Generic;
using System.Linq;
using WinKuake.Models;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Tests de la lógica pura que respalda la tab "Perfiles" de SettingsWindow.
/// Cubren los casos del Plan Fase 20.C — UI testeable sin abrir WPF.
/// </summary>
public class SettingsWindowProfilesTests
{
    // -------------------------------------------------------------------------
    // MakeDefault
    // -------------------------------------------------------------------------

    [Fact]
    public void MakeDefault_AsignaIdAlSettings()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "PS",  CommandLine = "pwsh.exe" },
            new() { Id = "b", Name = "Cmd", CommandLine = "cmd.exe"  },
        };
        string? defaultId = null;

        ProfileEditor.MakeDefault(profiles, ref defaultId, "b");

        Assert.Equal("b", defaultId);
    }

    [Fact]
    public void MakeDefault_SoloUnoMatcheaAlRender()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "PS",  CommandLine = "pwsh.exe" },
            new() { Id = "b", Name = "Cmd", CommandLine = "cmd.exe"  },
            new() { Id = "c", Name = "WSL", CommandLine = "wsl.exe -d Ubuntu" },
        };
        string? defaultId = null;
        ProfileEditor.MakeDefault(profiles, ref defaultId, "c");

        var checkedProfiles = profiles.Where(p => p.Id == defaultId).ToList();
        Assert.Single(checkedProfiles);
        Assert.Equal("WSL", checkedProfiles[0].Name);
    }

    [Fact]
    public void MakeDefault_IdInexistenteEsNoop()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "PS", CommandLine = "pwsh.exe" },
        };
        string? defaultId = "a";

        ProfileEditor.MakeDefault(profiles, ref defaultId, "no-existe");

        Assert.Equal("a", defaultId);
    }

    [Fact]
    public void MakeDefault_IdVacioEsNoop()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "PS", CommandLine = "pwsh.exe" },
        };
        string? defaultId = "a";

        ProfileEditor.MakeDefault(profiles, ref defaultId, "");

        Assert.Equal("a", defaultId);
    }

    // -------------------------------------------------------------------------
    // MergeDetected
    // -------------------------------------------------------------------------

    [Fact]
    public void Detect_AgregaSoloNuevos()
    {
        var existing = new List<UserProfile>();
        var detected = new List<UserProfile>
        {
            new() { Id = "id-pwsh", Name = "PowerShell 7", CommandLine = "pwsh.exe", Source = "Detected" },
            new() { Id = "id-cmd",  Name = "cmd",          CommandLine = "cmd.exe",  Source = "Detected" },
        };

        var added1 = ProfileEditor.MergeDetected(existing, detected);
        Assert.Equal(2, added1);
        Assert.Equal(2, existing.Count);

        // Segunda llamada con los mismos detectores → no duplica.
        var added2 = ProfileEditor.MergeDetected(existing, detected);
        Assert.Equal(0, added2);
        Assert.Equal(2, existing.Count);
    }

    [Fact]
    public void Detect_NoDuplicaPorMatchInsensitivoAMayusculas()
    {
        var existing = new List<UserProfile>
        {
            new() { Id = "ABC-DEF", Name = "PS", CommandLine = "pwsh.exe" },
        };
        var detected = new List<UserProfile>
        {
            new() { Id = "abc-def", Name = "PowerShell", CommandLine = "pwsh.exe" },
        };

        var added = ProfileEditor.MergeDetected(existing, detected);
        Assert.Equal(0, added);
        Assert.Single(existing);
        // No piso el name del usuario.
        Assert.Equal("PS", existing[0].Name);
    }

    [Fact]
    public void Detect_NoBorraCustomDelUsuario()
    {
        var existing = new List<UserProfile>
        {
            new() { Id = "custom-1", Name = "Mi shell raro", CommandLine = "ssh server", Source = "Custom" },
        };
        var detected = new List<UserProfile>
        {
            new() { Id = "id-cmd", Name = "cmd", CommandLine = "cmd.exe", Source = "Detected" },
        };

        var added = ProfileEditor.MergeDetected(existing, detected);

        Assert.Equal(1, added);
        Assert.Equal(2, existing.Count);
        Assert.Contains(existing, p => p.Source == "Custom" && p.Name == "Mi shell raro");
        Assert.Contains(existing, p => p.Source == "Detected" && p.Name == "cmd");
    }

    [Fact]
    public void Detect_DescartaPerfilesSinId()
    {
        var existing = new List<UserProfile>();
        var detected = new List<UserProfile>
        {
            new() { Id = "",  Name = "Sin id", CommandLine = "x.exe" },
            new() { Id = "x", Name = "Con id", CommandLine = "y.exe" },
        };

        var added = ProfileEditor.MergeDetected(existing, detected);

        Assert.Equal(1, added);
        Assert.Single(existing);
        Assert.Equal("x", existing[0].Id);
    }

    // -------------------------------------------------------------------------
    // CreateBlankCustom
    // -------------------------------------------------------------------------

    [Fact]
    public void AddManual_AsignaIdYCustomSource()
    {
        var p = ProfileEditor.CreateBlankCustom();
        Assert.False(string.IsNullOrEmpty(p.Id));
        Assert.True(System.Guid.TryParse(p.Id, out _));
        Assert.Equal("Custom", p.Source);
        Assert.Equal("", p.Name);
        Assert.Equal("", p.CommandLine);
        Assert.False(p.Hidden);
    }

    [Fact]
    public void AddManual_GeneraIdsUnicos()
    {
        var a = ProfileEditor.CreateBlankCustom();
        var b = ProfileEditor.CreateBlankCustom();
        Assert.NotEqual(a.Id, b.Id);
    }

    // -------------------------------------------------------------------------
    // Reset
    // -------------------------------------------------------------------------

    [Fact]
    public void Reset_LimpiaListaYDefault()
    {
        var profiles = new List<UserProfile>
        {
            new() { Id = "a", Name = "PS",  CommandLine = "pwsh.exe" },
            new() { Id = "b", Name = "Cmd", CommandLine = "cmd.exe"  },
        };
        string? defaultId = "a";

        ProfileEditor.Reset(profiles, ref defaultId);

        Assert.Empty(profiles);
        Assert.Null(defaultId);
    }

    // -------------------------------------------------------------------------
    // Cross-check con ProfileRegistry.LoadAll: Hidden no se devuelve.
    // -------------------------------------------------------------------------

    [Fact]
    public void Hidden_OcultoNoSeDevuelveDeLoadAll()
    {
        var settings = new AppSettings
        {
            UserProfiles = new()
            {
                new() { Id = "a", Name = "Visible", CommandLine = "pwsh.exe", Hidden = false },
                new() { Id = "b", Name = "Oculto",  CommandLine = "cmd.exe",  Hidden = true  },
                new() { Id = "c", Name = "Otro",    CommandLine = "wsl.exe",  Hidden = false },
            }
        };

        var visible = ProfileRegistry.LoadAll(settings);

        Assert.Equal(2, visible.Count);
        Assert.DoesNotContain(visible, p => p.Name == "Oculto");
    }

    // -------------------------------------------------------------------------
    // Flujo end-to-end: detect → merge → make default → toggle hidden → reset.
    // -------------------------------------------------------------------------

    [Fact]
    public void FlujoCompleto_DetectMakeDefaultHideReset()
    {
        var settings = new AppSettings();
        var detected = new List<UserProfile>
        {
            new() { Id = "id-pwsh", Name = "PowerShell 7", CommandLine = "pwsh.exe", Source = "Detected" },
            new() { Id = "id-cmd",  Name = "cmd",          CommandLine = "cmd.exe",  Source = "Detected" },
        };

        // Detect inicial.
        ProfileEditor.MergeDetected(settings.UserProfiles, detected);
        Assert.Equal(2, settings.UserProfiles.Count);

        // Default a cmd.
        var defaultId = settings.DefaultProfileId;
        ProfileEditor.MakeDefault(settings.UserProfiles, ref defaultId, "id-cmd");
        settings.DefaultProfileId = defaultId;
        Assert.Equal("id-cmd", settings.DefaultProfileId);

        // Ocultar pwsh.
        settings.UserProfiles.First(p => p.Id == "id-pwsh").Hidden = true;
        Assert.Single(ProfileRegistry.LoadAll(settings));

        // Reset borra todo.
        var d2 = settings.DefaultProfileId;
        ProfileEditor.Reset(settings.UserProfiles, ref d2);
        settings.DefaultProfileId = d2;
        Assert.Empty(settings.UserProfiles);
        Assert.Null(settings.DefaultProfileId);
    }
}
