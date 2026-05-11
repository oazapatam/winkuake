using System;
using System.Drawing;
using System.Windows.Forms;
using WpfApp = System.Windows.Application;

namespace WinKuake.Services;

/// <summary>
/// Tray icon de Windows: ícono persistente en system tray con menú
/// contextual para mostrar/ocultar la ventana y salir.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? _icon;
    private bool _disposed;

    public event Action? ShowRequested;
    public event Action? HideRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public void Install()
    {
        if (_icon is not null) return;
        try
        {
            var iconPath = Environment.ProcessPath;
            var icon = !string.IsNullOrEmpty(iconPath)
                ? Icon.ExtractAssociatedIcon(iconPath)
                : SystemIcons.Application;

            _icon = new NotifyIcon
            {
                Icon = icon ?? SystemIcons.Application,
                Visible = true,
                Text = "WinKuake — drop-down terminal"  // tray tooltip; already English
            };

            var menu = new ContextMenuStrip();
            var show = new ToolStripMenuItem("Show / hide");
            show.Click += (_, _) => ShowRequested?.Invoke();
            menu.Items.Add(show);

            var hide = new ToolStripMenuItem("Hide");
            hide.Click += (_, _) => HideRequested?.Invoke();
            menu.Items.Add(hide);

            menu.Items.Add(new ToolStripSeparator());

            var settings = new ToolStripMenuItem("Settings…");
            settings.Click += (_, _) => SettingsRequested?.Invoke();
            menu.Items.Add(settings);

            menu.Items.Add(new ToolStripSeparator());

            var exit = new ToolStripMenuItem("Quit");
            exit.Click += (_, _) => ExitRequested?.Invoke();
            menu.Items.Add(exit);

            _icon.ContextMenuStrip = menu;
            _icon.DoubleClick += (_, _) => ShowRequested?.Invoke();
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_icon is not null)
            {
                _icon.Visible = false;
                _icon.Dispose();
                _icon = null;
            }
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }
}
