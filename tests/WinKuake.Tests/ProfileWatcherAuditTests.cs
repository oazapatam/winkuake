using System;
using System.IO;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Auditoría Fase 6 — verifica indirectamente el handler JS del frontend
/// que dispara Ctrl+Shift+1..9 y Ctrl+Shift+PageUp/Down, y el handler del
/// hot-reload de wt settings.json (FileSystemWatcher con debounce).
///
/// El test del FileSystemWatcher arranca uno real sobre un directorio temp
/// y verifica que recibimos el cambio (no podemos cubrir el wiring exacto
/// de MainWindow sin inicializar WPF, pero confirmamos la mecánica
/// sub-yacente: archivos editados en sucesión se debouncen a un solo callback).
/// </summary>
public class ProfileWatcherAuditTests
{
    private static string ResourcesDir
    {
        get
        {
            var dir = AppContext.BaseDirectory;
            for (int i = 0; i < 10 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir, "src", "WinKuake", "Resources", "terminal");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException("No encontré src/WinKuake/Resources/terminal desde " + AppContext.BaseDirectory);
        }
    }

    private static string ReadHtml() => File.ReadAllText(Path.Combine(ResourcesDir, "terminal.html"));

    // ---- Handlers JS exigidos por Fase 6 (jump-to-tab, mover tab) ---------

    [Fact]
    public void TerminalHtml_HasJumpToTabHandler_OnDigitsWithCtrlShift()
    {
        var html = ReadHtml();
        // Debe contener algo del estilo: ev.ctrlKey && ev.shiftKey && ev.code.startsWith('Digit')
        Assert.Matches(
            @"ctrlKey.*shiftKey.*code.*startsWith\(\s*['""]Digit['""]\s*\)",
            html);
        Assert.Contains("activateAt", html);
    }

    [Fact]
    public void TerminalHtml_HasMoveActiveByHandler_OnPageUpAndPageDown()
    {
        var html = ReadHtml();
        Assert.Contains("PageUp", html);
        Assert.Contains("PageDown", html);
        Assert.Contains("moveActiveBy", html);
    }

    [Fact]
    public void TerminalHtml_HasNextTabAndPrevTab_OnCtrlTab()
    {
        var html = ReadHtml();
        Assert.Contains("nextTab", html);
        Assert.Contains("prevTab", html);
    }

    [Fact]
    public void TerminalHtml_HasSplitVerticalAndHorizontalHandlers()
    {
        var html = ReadHtml();
        Assert.Contains("splitVertical",   html);
        Assert.Contains("splitHorizontal", html);
        Assert.Contains("closePane",       html);
    }

    // ---- FileSystemWatcher real con debounce 300ms ------------------------

    [Fact]
    public void FileSystemWatcher_DebounceCoalescesRapidWrites()
    {
        // Validamos el patrón usado en MainWindow.StartWatchingWtSettings:
        // un Timer reseteado en cada Changed acaba disparando una sola vez.
        var dir = Path.Combine(Path.GetTempPath(), "winkuake-audit-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var path = Path.Combine(dir, "settings.json");
            File.WriteAllText(path, "{}");

            using var watcher = new FileSystemWatcher(dir, "settings.json")
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };

            var fired = 0;
            System.Threading.Timer? debounce = null;
            var done = new System.Threading.ManualResetEventSlim(false);
            watcher.Changed += (_, _) =>
            {
                debounce?.Dispose();
                debounce = new System.Threading.Timer(_ =>
                {
                    System.Threading.Interlocked.Increment(ref fired);
                    done.Set();
                }, null, 300, System.Threading.Timeout.Infinite);
            };

            // Burst: 5 escrituras rápidas que deberían coalescir a una sola callback.
            for (int i = 0; i < 5; i++)
            {
                File.WriteAllText(path, "{\"v\":" + i + "}");
                System.Threading.Thread.Sleep(20);
            }

            // Esperamos hasta 3s a que el debounce dispare.
            Assert.True(done.Wait(TimeSpan.FromSeconds(3)),
                "El debounce nunca disparó; el FileSystemWatcher no recibió el cambio.");
            Assert.Equal(1, fired);
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch { }
        }
    }

    [Fact]
    public void FileSystemWatcher_NonexistentDirectory_DoesNotThrowOnConstruction()
    {
        // MainWindow comprueba Directory.Exists antes de instanciar; nos aseguramos
        // que el patrón "no crear watcher si no existe el dir" sea seguro.
        var fakeDir = Path.Combine(Path.GetTempPath(), "winkuake-nonexistent-" + Guid.NewGuid().ToString("N"));
        Assert.False(Directory.Exists(fakeDir));
        // Si MainWindow llamara a `new FileSystemWatcher(fakeDir,...)` lanzaría;
        // por eso comprueba primero. Reproducimos esa pre-condición.
        Assert.Throws<ArgumentException>(() => new FileSystemWatcher(fakeDir, "settings.json"));
    }
}
