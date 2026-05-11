using System.Diagnostics;
using System.IO;
using System.Linq;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 12 — GitService.GetBranch. No mockeamos: probamos contra
/// un repo real creado en %TEMP% y contra paths inexistentes/no-repo.
/// Marcado como integration porque depende de tener `git` en PATH.
/// </summary>
public class GitServiceAuditTests
{
    [Fact]
    public void GetBranch_PathNull_DevuelveNull()
    {
        Assert.Null(GitService.GetBranch(null));
    }

    [Fact]
    public void GetBranch_PathVacio_DevuelveNull()
    {
        Assert.Null(GitService.GetBranch(""));
    }

    [Fact]
    public void GetBranch_PathSoloEspacios_DevuelveNull()
    {
        Assert.Null(GitService.GetBranch("   "));
    }

    [Fact]
    public void GetBranch_PathNoExiste_DevuelveNull()
    {
        // Path inventado: git -C falla, devolvemos null sin lanzar.
        var fake = Path.Combine(Path.GetTempPath(), "winkuake-no-existe-" + System.Guid.NewGuid());
        Assert.Null(GitService.GetBranch(fake));
    }

    [Fact]
    [Trait("Category", "integration")]
    public void GetBranch_PathSinGitInit_DevuelveNull()
    {
        if (!HasGit()) return; // entorno sin git → skip silencioso
        var dir = Path.Combine(Path.GetTempPath(), "winkuake-empty-" + System.Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            Assert.Null(GitService.GetBranch(dir));
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    [Trait("Category", "integration")]
    public void GetBranch_RepoReal_DevuelveNombreDeRama()
    {
        if (!HasGit()) return;
        var dir = Path.Combine(Path.GetTempPath(), "winkuake-repo-" + System.Guid.NewGuid());
        Directory.CreateDirectory(dir);
        try
        {
            // Init + commit en una rama llamada explícitamente.
            Run("git", "-C", dir, "init", "-b", "auditbranch");
            // Configurar identidad local (algunos sistemas no tienen global).
            Run("git", "-C", dir, "config", "user.email", "ci@winkuake.test");
            Run("git", "-C", dir, "config", "user.name",  "ci");
            File.WriteAllText(Path.Combine(dir, "hello.txt"), "hi");
            Run("git", "-C", dir, "add", "hello.txt");
            Run("git", "-C", dir, "commit", "-m", "init");

            var branch = GitService.GetBranch(dir);
            Assert.Equal("auditbranch", branch);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    private static bool HasGit()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "--version")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            return p.WaitForExit(2000) && p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static void Run(string exe, params string[] args)
    {
        var psi = new ProcessStartInfo(exe)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute = false,
            CreateNoWindow  = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi);
        p!.WaitForExit(5000);
    }
}
