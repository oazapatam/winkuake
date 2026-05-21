using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using WinKuake.Native;

namespace WinKuake.Services;

/// <summary>
/// Hospeda una pseudo-consola de Windows (ConPTY) que ejecuta un shell.
/// Expone:
/// <list type="bullet">
/// <item><see cref="OutputReceived"/>: stream UTF-8 que produce el shell.</item>
/// <item><see cref="Write"/>: enviar input al shell.</item>
/// <item><see cref="Resize"/>: ajustar columnas/filas (lo dispara xterm.js cuando hace fit).</item>
/// </list>
/// </summary>
public sealed class ConPtyService : IDisposable
{
    private IntPtr _pseudoConsole = IntPtr.Zero;
    private SafeFileHandle? _inputWrite;
    private SafeFileHandle? _outputRead;
    private FileStream? _writeStream;
    private FileStream? _readStream;
    private IntPtr _attrList = IntPtr.Zero;
    private ConPtyNative.PROCESS_INFORMATION _procInfo;
    private CancellationTokenSource? _cts;
    private Task? _readPump;
    private bool _disposed;

    /// <summary>Datos crudos UTF-8 emitidos por el shell.</summary>
    public event Action<ReadOnlyMemory<byte>>? OutputReceived;

    /// <summary>Se dispara cuando el proceso del shell termina.</summary>
    public event Action? Exited;

    public bool IsRunning => _procInfo.hProcess != IntPtr.Zero;

    /// <summary>Arranca el shell con las dimensiones iniciales indicadas.</summary>
    public void Start(string commandLine, short cols, short rows, string? startingDirectory = null)
    {
        if (IsRunning) throw new InvalidOperationException("ConPty ya está corriendo.");
        if (cols <= 0) cols = 120;
        if (rows <= 0) rows = 30;

        CrashLogger.Info($"ConPty.Start: cmd='{commandLine}' cwd='{startingDirectory}' cols={cols} rows={rows}");

        // Dos pipes: PTY -> nuestro proceso (lectura del output), nuestro proceso -> PTY (input).
        if (!ConPtyNative.CreatePipe(out var ptyOutRead, out var ptyOutWrite, IntPtr.Zero, 0))
            ThrowLastError("CreatePipe (output)");
        if (!ConPtyNative.CreatePipe(out var ptyInRead, out var ptyInWrite, IntPtr.Zero, 0))
            ThrowLastError("CreatePipe (input)");

        _outputRead = ptyOutRead;
        _inputWrite = ptyInWrite;

        var hr = ConPtyNative.CreatePseudoConsole(new ConPtyNative.COORD(cols, rows), ptyInRead, ptyOutWrite, 0, out _pseudoConsole);
        if (hr != ConPtyNative.S_OK) throw new InvalidOperationException($"CreatePseudoConsole hr=0x{hr:X}");

        // ConPty duplica los handles internamente; liberar nuestras refs acá
        // hace que ClosePseudoConsole (en shutdown) cierre el único dueño y
        // PumpRead vea EOF en su Read síncrono. Sin esto, PumpRead se cuelga
        // tras el exit del shell.
        ptyInRead.Dispose();
        ptyOutWrite.Dispose();

        StartShell(commandLine, startingDirectory);
        CrashLogger.Info($"ConPty.Start: proceso lanzado PID={_procInfo.dwProcessId}");

        // Watcher en hilo aparte: cuando el shell muere, conhost no cierra el pipe
        // del lado lector, así que PumpRead se queda eternamente esperando bytes.
        // El watcher hace WaitForSingleObject sobre hProcess, loguea el exit code,
        // y cierra el pseudo-console para que PumpRead vea EOF y dispare Exited.
        var hProc = _procInfo.hProcess;
        var watcher = new Thread(() =>
        {
            try
            {
                ConPtyNative.WaitForSingleObject(hProc, 0xFFFFFFFF);
                uint exitCode = 0;
                ConPtyNative.GetExitCode(hProc, out exitCode);
                CrashLogger.Info($"ConPty watcher: shell PID={_procInfo.dwProcessId} EXITED con código 0x{exitCode:X} ({exitCode})");
                ShutdownPseudoConsoleFromWatcher();
            }
            catch (Exception ex) { CrashLogger.Log(ex); }
        }) { IsBackground = true, Name = "ConPty-Watcher" };
        watcher.Start();

        // CreatePipe produce handles sin FILE_FLAG_OVERLAPPED, así que NO se
        // pueden envolver con isAsync:true (FileStream lanzaría ArgumentException).
        // Usamos I/O síncrono en un hilo dedicado.
        _readStream = new FileStream(_outputRead, FileAccess.Read, 4096, isAsync: false);
        _writeStream = new FileStream(_inputWrite, FileAccess.Write, 4096, isAsync: false);

        _cts = new CancellationTokenSource();
        var t = new Thread(() => PumpRead(_cts.Token)) { IsBackground = true, Name = "ConPty-Read" };
        t.Start();
        _readPump = Task.CompletedTask; // ya no usamos Task aquí
    }

    private void StartShell(string commandLine, string? startingDirectory)
    {
        // Necesitamos pasarle el PseudoConsole al CreateProcess via attribute list.
        var sizeRef = IntPtr.Zero;
        ConPtyNative.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref sizeRef);
        _attrList = Marshal.AllocHGlobal(sizeRef);

        if (!ConPtyNative.InitializeProcThreadAttributeList(_attrList, 1, 0, ref sizeRef))
            ThrowLastError("InitializeProcThreadAttributeList");

        if (!ConPtyNative.UpdateProcThreadAttribute(
                _attrList, 0,
                new IntPtr(ConPtyNative.PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE),
                _pseudoConsole,
                new IntPtr(IntPtr.Size), IntPtr.Zero, IntPtr.Zero))
            ThrowLastError("UpdateProcThreadAttribute");

        var siex = new ConPtyNative.STARTUPINFOEX();
        siex.StartupInfo.cb = Marshal.SizeOf<ConPtyNative.STARTUPINFOEX>();
        siex.lpAttributeList = _attrList;
        // CRÍTICO: workaround del bug "ConPTY desde proceso GUI (WinExe) o con
        // stdout redirigido". A pesar de que MSDN dice "STARTF_USESTDHANDLES
        // must NOT be set" cuando se usa el atributo PSEUDOCONSOLE, en Win10
        // y varias versiones de Win11 hay que ponerlo CON los handles std en
        // NULL. Eso desactiva la duplicación automática del kernel que estaba
        // dándole al child los handles del padre (consola heredada o stdout
        // redirigido) en lugar del pseudo-console. Sin esto el child ignora
        // silenciosamente el atributo y escribe a la stdout del padre.
        // Ver: microsoft/terminal discussion #15814, issue #11276.
        siex.StartupInfo.dwFlags |= ConPtyNative.STARTF_USESTDHANDLES;

        // SECURITY_ATTRIBUTES con nLength sólo (resto de campos en 0/null) es
        // lo que usa el sample oficial de Microsoft (MiniTerm). Pasar IntPtr.Zero
        // a CreateProcess teóricamente debería ser equivalente, pero en algunos
        // builds de Windows 11 el atributo de pseudo-consola no se aplica al
        // child cuando lpProcessAttributes/lpThreadAttributes son NULL — el
        // proceso hereda silenciosamente la consola del padre.
        int secSize = Marshal.SizeOf<ConPtyNative.SECURITY_ATTRIBUTES>();
        var pSec = new ConPtyNative.SECURITY_ATTRIBUTES { nLength = secSize };
        var tSec = new ConPtyNative.SECURITY_ATTRIBUTES { nLength = secSize };

        // Si startingDirectory es "" lo normalizamos a null: CreateProcess
        // documenta null = "usar CWD del padre"; con "" en algunos casos falla.
        var cwd = string.IsNullOrWhiteSpace(startingDirectory) ? null : startingDirectory;

        if (!ConPtyNative.CreateProcess(
                null, commandLine,
                ref pSec, ref tSec,
                bInheritHandles: false,
                ConPtyNative.EXTENDED_STARTUPINFO_PRESENT,
                IntPtr.Zero, cwd,
                ref siex, out _procInfo))
            ThrowLastError("CreateProcess");
    }

    public void Write(ReadOnlySpan<byte> data)
    {
        if (_writeStream is null || data.Length == 0) return;
        _writeStream.Write(data);
        _writeStream.Flush();
    }

    public void Write(string text) => Write(Encoding.UTF8.GetBytes(text));

    public void Resize(short cols, short rows)
    {
        if (_pseudoConsole == IntPtr.Zero) return;
        if (cols <= 0 || rows <= 0) return;
        ConPtyNative.ResizePseudoConsole(_pseudoConsole, new ConPtyNative.COORD(cols, rows));
    }

    private readonly object _shutdownLock = new();
    private void ShutdownPseudoConsoleFromWatcher()
    {
        // Llamado desde el thread watcher cuando el child muere. Cierra el
        // pseudo-console para que PumpRead vea EOF y termine limpio.
        lock (_shutdownLock)
        {
            if (_pseudoConsole != IntPtr.Zero)
            {
                ConPtyNative.ClosePseudoConsole(_pseudoConsole);
                _pseudoConsole = IntPtr.Zero;
            }
        }
    }

    private void PumpRead(CancellationToken ct)
    {
        var buf = new byte[4096];
        bool firstRead = true;
        try
        {
            while (!ct.IsCancellationRequested && _readStream is not null)
            {
                var n = _readStream.Read(buf, 0, buf.Length);
                if (firstRead) { CrashLogger.Info($"ConPty PumpRead: primer Read devolvió n={n}"); firstRead = false; }
                if (n <= 0) { CrashLogger.Info($"ConPty PumpRead: EOF (n={n}). Pipe cerrado por el shell."); break; }
                // Copia: el handler puede ser async (Dispatcher.InvokeAsync) y
                // el buffer se reusa en la siguiente iteración.
                var copy = new byte[n];
                Buffer.BlockCopy(buf, 0, copy, 0, n);
                OutputReceived?.Invoke(copy);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
        }
        finally
        {
            CrashLogger.Info("ConPty PumpRead: terminado, disparando Exited");
            Exited?.Invoke();
        }
    }

    private static void ThrowLastError(string label)
    {
        var err = Marshal.GetLastWin32Error();
        throw new InvalidOperationException($"{label} failed: Win32 {err}");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try { _cts?.Cancel(); } catch { }
        try { _readStream?.Dispose(); } catch { }
        try { _writeStream?.Dispose(); } catch { }

        lock (_shutdownLock)
        {
            if (_pseudoConsole != IntPtr.Zero)
            {
                ConPtyNative.ClosePseudoConsole(_pseudoConsole);
                _pseudoConsole = IntPtr.Zero;
            }
        }
        if (_procInfo.hProcess != IntPtr.Zero) ConPtyNative.CloseHandle(_procInfo.hProcess);
        if (_procInfo.hThread != IntPtr.Zero)  ConPtyNative.CloseHandle(_procInfo.hThread);
        _procInfo = default;

        if (_attrList != IntPtr.Zero)
        {
            ConPtyNative.DeleteProcThreadAttributeList(_attrList);
            Marshal.FreeHGlobal(_attrList);
            _attrList = IntPtr.Zero;
        }

        try { _readPump?.Wait(500); } catch { }
    }
}
