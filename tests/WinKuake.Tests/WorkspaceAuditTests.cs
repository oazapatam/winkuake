using System.Linq;
using System.Text.Json;
using WinKuake.Models;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 12 — Workspaces guardables. Verifica que el modelo de
/// persistencia soporta los casos cubiertos por el flujo de la UI:
/// múltiples tabs por workspace, splits anidados, IsPinned/CustomLabel.
/// </summary>
public class WorkspaceAuditTests
{
    private static readonly JsonSerializerOptions Opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    [Fact]
    public void Workspace_DefaultsRazonables()
    {
        var w = new Workspace();
        Assert.Equal("", w.Name);
        Assert.NotNull(w.Tabs);
        Assert.Empty(w.Tabs);
    }

    [Fact]
    public void Workspace_DeepClone_EsCopiaIndependiente()
    {
        // El clon profundo es la garantía de que SettingsWindow no muta el
        // workspace original al editar.
        var src = new Workspace
        {
            Name = "dev",
            Tabs = new()
            {
                new PersistedTab { ProfileName = "Ubuntu", Cwd = @"C:\code", IsPinned = true },
                new PersistedTab { ProfileName = "pwsh" },
            }
        };
        var copy = src.DeepClone();
        copy.Name = "modified";
        copy.Tabs[0].ProfileName = "tampered";
        copy.Tabs.Clear();

        Assert.Equal("dev", src.Name);
        Assert.Equal("Ubuntu", src.Tabs[0].ProfileName);
        Assert.Equal(2, src.Tabs.Count);
    }

    [Fact]
    public void Workspace_RoundtripJsonConSplitTree()
    {
        // Workspaces deben preservar el árbol de splits dentro de sus tabs.
        var src = new Workspace
        {
            Name = "split-ws",
            Tabs = new()
            {
                new PersistedTab
                {
                    ProfileName = "root",
                    Layout = new PersistedSplitNode
                    {
                        Orientation = "Horizontal",
                        First = new PersistedSplitNode { ProfileName = "top", Cwd = @"C:\a" },
                        Second = new PersistedSplitNode { ProfileName = "bot", Cwd = @"C:\b" },
                    }
                }
            }
        };
        var json = JsonSerializer.Serialize(src, Opts);
        var dst = JsonSerializer.Deserialize<Workspace>(json, Opts)!;
        Assert.Equal("split-ws", dst.Name);
        var layout = dst.Tabs[0].Layout!;
        Assert.Equal("Horizontal", layout.Orientation);
        Assert.Equal("top", layout.First!.ProfileName);
        Assert.Equal(@"C:\b", layout.Second!.Cwd);
    }

    [Fact]
    public void Workspaces_VariosConMismoTabSeMantienenIndependientes()
    {
        // Si dos workspaces comparten contenido similar, no debe haber aliasing.
        var src = new AppSettings
        {
            Workspaces = new()
            {
                new Workspace { Name = "a", Tabs = new() { new PersistedTab { ProfileName = "X" } } },
                new Workspace { Name = "b", Tabs = new() { new PersistedTab { ProfileName = "X" } } },
            }
        };
        var copy = src.DeepClone();
        copy.Workspaces[0].Tabs[0].ProfileName = "modified-a";

        Assert.Equal("X", src.Workspaces[0].Tabs[0].ProfileName);
        Assert.Equal("X", copy.Workspaces[1].Tabs[0].ProfileName);
        Assert.Equal("modified-a", copy.Workspaces[0].Tabs[0].ProfileName);
    }

    [Fact]
    public void PersistedTab_CamposOpcionalesNullables()
    {
        // ProfileGuid, ProfileName, Cwd, CustomLabel son nullable. PLAN Fase 12.
        var t = new PersistedTab();
        Assert.Null(t.ProfileGuid);
        Assert.Null(t.ProfileName);
        Assert.Null(t.Cwd);
        Assert.Null(t.CustomLabel);
        Assert.Null(t.Layout);
        Assert.False(t.IsPinned);
    }

    [Fact]
    public void Workspace_TabsConIsPinnedYCustomLabel_PersistenAJsonYClonan()
    {
        var src = new Workspace
        {
            Name = "pinned",
            Tabs = new()
            {
                new PersistedTab
                {
                    ProfileName = "Ubuntu",
                    CustomLabel = "etiqueta personalizada",
                    IsPinned = true,
                }
            }
        };
        var json = JsonSerializer.Serialize(src, Opts);
        var dst = JsonSerializer.Deserialize<Workspace>(json, Opts)!;
        Assert.True(dst.Tabs[0].IsPinned);
        Assert.Equal("etiqueta personalizada", dst.Tabs[0].CustomLabel);

        var clone = src.DeepClone();
        Assert.True(clone.Tabs[0].IsPinned);
        Assert.Equal("etiqueta personalizada", clone.Tabs[0].CustomLabel);
    }

    [Fact]
    public void AppSettings_ReplacePorNombreEsResponsabilidadDelLlamador()
    {
        // PLAN Fase 12: SaveCurrentWorkspace "Reemplaza si nombre coincide".
        // El modelo no fuerza unicidad; lo hace MainWindow vía RemoveAll antes
        // de Add. Verificamos que el modelo permite duplicados (señal de que
        // si se observara duplicación, sería bug del llamador, no del modelo).
        var s = new AppSettings();
        s.Workspaces.Add(new Workspace { Name = "x" });
        s.Workspaces.Add(new Workspace { Name = "x" });
        Assert.Equal(2, s.Workspaces.Count);
    }
}
