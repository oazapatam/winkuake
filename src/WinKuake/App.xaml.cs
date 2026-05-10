using System;
using System.Threading;
using System.Windows;
using WinKuake.Services;

namespace WinKuake;

public partial class App : Application
{
    private static Mutex? _singleInstanceMutex;
    private const string MutexName = "WinKuake.SingleInstance.Mutex.v1";

    protected override void OnStartup(StartupEventArgs e)
    {
        // Single-instance: si ya está corriendo, salimos. Cualquier intento
        // adicional de abrir el ejecutable simplemente activa la ventana
        // existente vía hotkey global.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        if (!createdNew)
        {
            // No somos el dueño: liberamos la referencia para que OnExit no
            // intente hacer ReleaseMutex sobre un mutex que no poseemos.
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            Shutdown(0);
            return;
        }

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            CrashLogger.Log(args.ExceptionObject as Exception);
        DispatcherUnhandledException += (_, args) =>
        {
            CrashLogger.Log(args.Exception);
            args.Handled = true;
        };

        base.OnStartup(e);

        // Aplicamos el skin antes de crear la ventana para que las claves
        // ChromeBackground/AccentBrush/etc. existan ya en Application.Resources.
        SkinService.Apply(SettingsService.Load());

        var window = new MainWindow();
        MainWindow = window;
        // No la mostramos: arranca oculta y aparece con el hotkey.
        window.InitializeHidden();

        // Dev: --start-shown fuerza el toggle al iniciar (útil cuando F12
        // está bloqueado por utilidades OEM y queremos testear sin esperar).
        if (e.Args.Contains("--start-shown"))
        {
            window.Dispatcher.InvokeAsync(async () =>
            {
                await System.Threading.Tasks.Task.Delay(400);
                window.ForceShowForDev();
            });
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstanceMutex?.ReleaseMutex();
        _singleInstanceMutex?.Dispose();
        base.OnExit(e);
    }
}
