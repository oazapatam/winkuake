using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using WinKuake.Models;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fases 14 y 15: persistencia del árbol de splits y fix del
/// CoreWebView2Environment singleton. Como el árbol vive en WPF no podemos
/// instanciarlo en test (xUnit corre STA solo con WpfFact), pero sí
/// validamos:
///  - Que PersistedSplitNode roundtripea ramas y hojas correctamente.
///  - Que TerminalPane expone la propiedad OriginProfile.
///  - Que TerminalPane define un campo estático compartido (_sharedEnv)
///    y un SemaphoreSlim para inicialización idempotente del environment.
///  - Que TerminalControl expone los métodos SerializeLayout/RestoreLayout.
/// </summary>
public class SplitTreeAuditTests
{
    // -- Fase 14: roundtrip JSON de árbol grande --------------------------

    [Fact]
    public void Audit_PersistedSplitNode_DeepNestedTree_Roundtrips()
    {
        // Árbol de 4 niveles para asegurar recursión correcta.
        var src = new PersistedSplitNode
        {
            Orientation = "Vertical",
            First = new PersistedSplitNode
            {
                Orientation = "Horizontal",
                First  = new PersistedSplitNode { ProfileGuid = "{g1}", ProfileName = "A", Cwd = @"C:\a" },
                Second = new PersistedSplitNode
                {
                    Orientation = "Vertical",
                    First  = new PersistedSplitNode { ProfileName = "B" },
                    Second = new PersistedSplitNode { ProfileName = "C" }
                }
            },
            Second = new PersistedSplitNode { ProfileGuid = "{g2}", ProfileName = "D" }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var dst = JsonSerializer.Deserialize<PersistedSplitNode>(JsonSerializer.Serialize(src, opts), opts)!;

        Assert.Equal("Vertical", dst.Orientation);
        Assert.Equal("Horizontal", dst.First!.Orientation);
        Assert.Equal("A", dst.First.First!.ProfileName);
        Assert.Equal(@"C:\a", dst.First.First.Cwd);
        Assert.Equal("Vertical", dst.First.Second!.Orientation);
        Assert.Equal("B", dst.First.Second.First!.ProfileName);
        Assert.Equal("C", dst.First.Second.Second!.ProfileName);
        Assert.Equal("D", dst.Second!.ProfileName);
    }

    [Fact]
    public void Audit_PersistedSplitNode_LeafHasNoOrientation()
    {
        var leaf = new PersistedSplitNode { ProfileName = "x" };
        // Convención del modelo: leaf ⇔ Orientation == null.
        Assert.Null(leaf.Orientation);
        Assert.Null(leaf.First);
        Assert.Null(leaf.Second);
    }

    [Fact]
    public void Audit_PersistedSplitNode_DeepClone_IsIndependent()
    {
        var src = new PersistedSplitNode
        {
            Orientation = "Vertical",
            First = new PersistedSplitNode { ProfileName = "A" },
            Second = new PersistedSplitNode { ProfileName = "B" }
        };
        var copy = src.DeepClone();
        copy.First!.ProfileName = "modified";
        Assert.Equal("A", src.First!.ProfileName);
    }

    // -- Fase 14: API de TerminalPane / TerminalControl -------------------

    [Fact]
    public void Audit_TerminalPane_Exposes_OriginProfile()
    {
        var t = typeof(WinKuake.Views.TerminalPane);
        var prop = t.GetProperty("OriginProfile",
            BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(prop);
        Assert.Equal(typeof(WinKuake.Services.TerminalProfile), prop!.PropertyType);
    }

    [Fact]
    public void Audit_TerminalControl_HasSerializeAndRestoreLayout()
    {
        var t = typeof(WinKuake.Views.TerminalControl);
        var ser = t.GetMethod("SerializeLayout", BindingFlags.Public | BindingFlags.Instance);
        var res = t.GetMethod("RestoreLayout",   BindingFlags.Public | BindingFlags.Instance);
        Assert.NotNull(ser);
        Assert.NotNull(res);
        // RestoreLayout(node, profileResolver) — 2 args.
        Assert.Equal(2, res!.GetParameters().Length);
    }

    // -- Fase 15: singleton CoreWebView2Environment ------------------------

    [Fact]
    public void Audit_TerminalPane_HasSharedWebView2Environment_Singleton()
    {
        // El field _sharedEnv debe existir, ser estático y privado.
        var t = typeof(WinKuake.Views.TerminalPane);
        var env = t.GetField("_sharedEnv",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(env);
        Assert.True(env!.IsStatic);
        Assert.Contains("CoreWebView2Environment", env.FieldType.Name);
    }

    [Fact]
    public void Audit_TerminalPane_HasSemaphoreSlim_GuardingEnvCreation()
    {
        // _envLock debe existir y ser SemaphoreSlim para serializar la
        // creación del env entre múltiples panes lanzados simultáneamente.
        var t = typeof(WinKuake.Views.TerminalPane);
        var lockField = t.GetField("_envLock",
            BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(lockField);
        Assert.True(lockField!.IsStatic);
        Assert.Equal(typeof(System.Threading.SemaphoreSlim), lockField.FieldType);
    }

    // -- Fase 14: PersistedTab.Layout es opcional --------------------------

    [Fact]
    public void Audit_PersistedTab_LayoutIsNullable()
    {
        var src = new PersistedTab { ProfileName = "x" };
        Assert.Null(src.Layout);
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var json = JsonSerializer.Serialize(src, opts);
        var dst  = JsonSerializer.Deserialize<PersistedTab>(json, opts)!;
        Assert.Null(dst.Layout);
    }

    [Fact]
    public void Audit_PersistedTab_WithLayout_RoundtripsThroughAppSettings()
    {
        // El uso real: Settings.LastSessionTabs[i].Layout. Verificamos el path
        // completo (settings → JSON → settings).
        var src = new AppSettings
        {
            LastSessionTabs = new()
            {
                new PersistedTab
                {
                    ProfileName = "root",
                    Layout = new PersistedSplitNode
                    {
                        Orientation = "Horizontal",
                        First  = new PersistedSplitNode { ProfileGuid = "{a}", ProfileName = "left" },
                        Second = new PersistedSplitNode { ProfileGuid = "{b}", ProfileName = "right", Cwd = @"D:\x" },
                    }
                }
            }
        };
        var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var dst  = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(src, opts), opts)!;
        var node = dst.LastSessionTabs[0].Layout!;
        Assert.Equal("Horizontal", node.Orientation);
        Assert.Equal("left",  node.First!.ProfileName);
        Assert.Equal("right", node.Second!.ProfileName);
        Assert.Equal(@"D:\x", node.Second.Cwd);
    }
}
