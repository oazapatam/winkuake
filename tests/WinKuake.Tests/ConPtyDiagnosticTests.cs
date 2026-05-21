using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using WinKuake.Native;
using Xunit;

namespace WinKuake.Tests;

/// <summary>
/// Pruebas de diagnóstico de BAJO NIVEL para aislar dónde rompe la cadena
/// ConPty → child. Cada test reproduce el setup EN MINIATURA y verifica
/// una sola hipótesis. Categoría "integration" porque arrancan procesos reales.
/// </summary>
[Trait("Category", "integration")]
public class ConPtyDiagnosticTests
{
    private const string EscMarker9001 = "[?9001h";

    /// <summary>
    /// Captura todos los bytes que entran a una lectura de pipe (no-bloquea más
    /// del timeout). Devuelve los bytes recibidos como string.
    /// </summary>
    private static string ReadAvailable(FileStream stream, TimeSpan timeout)
    {
        var sb = new StringBuilder();
        var buf = new byte[4096];
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            int n = 0;
            var t = new Thread(() => { try { n = stream.Read(buf, 0, buf.Length); } catch { n = 0; } }) { IsBackground = true };
            t.Start();
            if (!t.Join(TimeSpan.FromMilliseconds(500))) continue;
            if (n <= 0) break;
            sb.Append(Encoding.UTF8.GetString(buf, 0, n));
        }
        return sb.ToString();
    }

    private static string Visible(string s)
        => s.Replace("", "\\e").Replace("\r", "\\r").Replace("\n", "\\n");

    /// <summary>
    /// Documenta el hallazgo: los 16 bytes "\e[?9001h\e[?1004h" que se observan
    /// al lanzar un shell NO los emite el conhost al abrirse — los emite cuando
    /// el child se conecta al pseudo-console. Si no hay child, no hay bytes.
    /// Este test bloquea regresiones a la hipótesis equivocada.
    /// </summary>
    [Fact]
    public void Handshake_16Bytes_DoNotAppear_WithoutAnyChild()
    {
        Assert.True(ConPtyNative.CreatePipe(out var outRead, out var outWrite, IntPtr.Zero, 0));
        Assert.True(ConPtyNative.CreatePipe(out var inRead, out var inWrite, IntPtr.Zero, 0));

        var hr = ConPtyNative.CreatePseudoConsole(new ConPtyNative.COORD(80, 24), inRead, outWrite, 0, out var hpc);
        Assert.Equal(0, hr);

        // NO lanzamos ningún proceso. Solo leemos.
        var reader = new FileStream(outRead, FileAccess.Read, 4096, isAsync: false);
        var output = ReadAvailable(reader, TimeSpan.FromSeconds(2));

        ConPtyNative.ClosePseudoConsole(hpc);

        Assert.False(output.Contains(EscMarker9001),
            $"Sin child el pseudo-console emitió bytes — esperábamos 0. Output {output.Length} bytes: {Visible(output)}");
    }

    /// <summary>
    /// Test del setup ConPty completo (raw, sin pasar por ConPtyService).
    /// Verifica si el output del child llega al pipe del pseudo-console.
    /// </summary>
    [Fact]
    public void RawConPty_Bypass_ConPtyService_Echo_GoesToPipe()
    {
        var saInherit = new ConPtyNative.SECURITY_ATTRIBUTES
        {
            nLength = Marshal.SizeOf<ConPtyNative.SECURITY_ATTRIBUTES>(),
            bInheritHandle = 1,
        };
        Assert.True(ConPtyNative.CreatePipeSA(out var outRead, out var outWrite, ref saInherit, 0), "CreatePipe out");
        Assert.True(ConPtyNative.CreatePipeSA(out var inRead, out var inWrite, ref saInherit, 0), "CreatePipe in");

        var hr = ConPtyNative.CreatePseudoConsole(new ConPtyNative.COORD(80, 24), inRead, outWrite, 0, out var hpc);
        Assert.Equal(0, hr);
        Assert.NotEqual(IntPtr.Zero, hpc);

        IntPtr sizeRef = IntPtr.Zero;
        ConPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref sizeRef);
        var attrList = Marshal.AllocHGlobal(sizeRef);
        Assert.True(ConPtyNative.InitializeProcThreadAttributeList(attrList, 1, 0, ref sizeRef), "InitAttrList second call");

        Assert.True(ConPtyNative.UpdateProcThreadAttribute(
            attrList, 0,
            new IntPtr(ConPtyNative.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE),
            hpc,
            new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero), "UpdateAttr");

        var siex = new ConPtyNative.STARTUPINFOEX();
        siex.StartupInfo.cb = Marshal.SizeOf<ConPtyNative.STARTUPINFOEX>();
        siex.lpAttributeList = attrList;
        // Workaround del bug "ConPTY desde proceso con stdout redirigido / WinExe":
        // STARTF_USESTDHANDLES con los 3 handles en NULL DESACTIVA la duplicación
        // automática del kernel que estaba inyectando los handles del padre en
        // el child, ignorando el pseudo-console. Ver microsoft/terminal #15814.
        siex.StartupInfo.dwFlags |= ConPtyNative.STARTF_USESTDHANDLES;

        int secSize = Marshal.SizeOf<ConPtyNative.SECURITY_ATTRIBUTES>();
        var pSec = new ConPtyNative.SECURITY_ATTRIBUTES { nLength = secSize };
        var tSec = new ConPtyNative.SECURITY_ATTRIBUTES { nLength = secSize };

        Assert.True(ConPtyNative.CreateProcess(
            null, "cmd.exe /c \"echo BYPASS_HELLO\"",
            ref pSec, ref tSec,
            bInheritHandles: false,
            ConPtyNative.EXTENDED_STARTUPINFO_PRESENT,
            IntPtr.Zero, null,
            ref siex, out var pi), "CreateProcess");

        ConPtyNative.WaitForSingleObject(pi.hProcess, 5000);
        ConPtyNative.GetExitCode(pi.hProcess, out var exitCode);

        var reader = new FileStream(outRead, FileAccess.Read, 4096, isAsync: false);
        var output = ReadAvailable(reader, TimeSpan.FromSeconds(2));

        ConPtyNative.ClosePseudoConsole(hpc);
        ConPtyNative.DeleteProcThreadAttributeList(attrList);
        Marshal.FreeHGlobal(attrList);
        ConPtyNative.CloseHandle(pi.hProcess);
        ConPtyNative.CloseHandle(pi.hThread);

        Assert.True(output.Contains("BYPASS_HELLO"),
            $"Child exit=0x{exitCode:X} pero 'BYPASS_HELLO' NO llegó al pipe. Output ({output.Length} bytes): {Visible(output)}");
    }
}
