using System;
using System.Diagnostics;
using System.Text;
using System.Threading;
using WinKuake.Services;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Smoke tests reales contra ConPty + un shell del SO. Lanzan procesos
/// auténticos, no son hermético. Categoría "integration" para poder filtrar
/// si en CI no hay un host adecuado. En local nos sirven para reproducir
/// "pantalla negra: shell muere sin emitir prompt" sin tener que pedirle
/// al humano F12 cada iteración.
/// </summary>
[Trait("Category", "integration")]
public class ConPtySmokeTests
{
    /// <summary>
    /// Captura todo lo que el PTY emite hasta que termina (o expira el timeout)
    /// y devuelve el blob. Útil para asserts del tipo "el output contiene XYZ".
    /// </summary>
    private static (string Output, bool TimedOut) RunAndCapture(string commandLine, TimeSpan timeout, short cols = 80, short rows = 24)
    {
        using var pty = new ConPtyService();
        var sb = new StringBuilder();
        var sync = new object();
        var doneEvt = new ManualResetEventSlim(false);

        pty.OutputReceived += data =>
        {
            lock (sync) sb.Append(Encoding.UTF8.GetString(data.Span));
        };
        pty.Exited += () => doneEvt.Set();

        pty.Start(commandLine, cols, rows);
        var timedOut = !doneEvt.Wait(timeout);

        // Dispose del pty cierra el pseudo-console y desbloquea PumpRead.
        // Sin esto, el hilo lector queda colgado y el siguiente test puede
        // ver state contaminado.
        pty.Dispose();
        lock (sync) return (sb.ToString(), timedOut);
    }

    [Fact]
    public void CmdExe_EchoEmitsExpectedString()
    {
        // cmd.exe /c "echo HELLO" es la prueba más simple posible: arranca,
        // imprime una línea, sale. Si esto no funciona, ConPty está fundido
        // de raíz y no hay shell que sobreviva.
        var (output, timedOut) = RunAndCapture(
            "cmd.exe /c \"echo HELLO_FROM_CONPTY\"",
            TimeSpan.FromSeconds(10));

        Assert.False(timedOut, "cmd.exe /c echo no terminó en 10s. Output capturado:\n" + output);
        Assert.Contains("HELLO_FROM_CONPTY", output);
    }

    [Fact]
    public void CmdExe_BatchedEcho_AlsoEmits()
    {
        // Doble echo + exit: si esto pasa el pipe sí transmite múltiples chunks.
        var (output, timedOut) = RunAndCapture(
            "cmd.exe /c \"echo LINE1 && echo LINE2 && exit\"",
            TimeSpan.FromSeconds(10));

        Assert.False(timedOut, "cmd.exe no terminó en 10s. Output:\n" + output);
        Assert.Contains("LINE1", output);
        Assert.Contains("LINE2", output);
    }

    [Fact]
    public void PowerShell_NoProfile_EmitsBanner()
    {
        // PowerShell con -NoProfile debe emitir banner + prompt aunque el
        // perfil del usuario tenga errores. Si esto falla con un perfil
        // estándar pero el cmd.exe smoke pasa, el bug está en cómo arrancamos
        // shells interactivos (no en ConPty).
        var (output, timedOut) = RunAndCapture(
            "powershell.exe -NoLogo -NoProfile -Command \"Write-Host 'PS_READY'; exit 0\"",
            TimeSpan.FromSeconds(15));

        Assert.False(timedOut, "powershell -NoProfile no terminó en 15s. Output:\n" + output);
        Assert.Contains("PS_READY", output);
    }
}
