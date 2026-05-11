using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using WinKuake.Native;
using WpfApp = System.Windows.Application;

namespace WinKuake.Services;

/// <summary>
/// Tray icon de Windows: ícono persistente en system tray con menú
/// contextual y balloon notifications. Acciones expuestas por eventos
/// para que MainWindow las cablée a sus handlers.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? _icon;
    private Icon?       _normalIcon;
    private Icon?       _broadcastIcon;
    private bool        _disposed;
    private bool        _broadcastActive;

    public event Action? ShowRequested;
    public event Action? HideRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public void Install()
    {
        if (_icon is not null) return;
        try
        {
            _normalIcon    = ExtractAppIcon();
            _broadcastIcon = BuildBroadcastIcon(_normalIcon);

            _icon = new NotifyIcon
            {
                Icon    = _normalIcon,
                Visible = true,
                Text    = "WinKuake — drop-down terminal"
            };

            BuildContextMenu();

            // Click izquierdo simple = toggle. RightButton se reserva al menú
            // contextual (NotifyIcon lo maneja automáticamente).
            _icon.MouseClick += (_, e) =>
            {
                if (e.Button == MouseButtons.Left) ShowRequested?.Invoke();
            };
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    /// <summary>Muestra una notificación tipo balloon junto al tray.</summary>
    public void ShowBalloon(string title, string text, int timeoutMs = 4000)
    {
        if (_icon is null) return;
        try
        {
            _icon.BalloonTipTitle = title;
            _icon.BalloonTipText  = text;
            _icon.BalloonTipIcon  = ToolTipIcon.None;
            _icon.ShowBalloonTip(timeoutMs);
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    /// <summary>
    /// Cambia el ícono entre normal y badge rojo (broadcast activo).
    /// Llamarlo desde el handler de BroadcastChanged del TerminalControl activo.
    /// </summary>
    public void SetBroadcastState(bool active)
    {
        if (_icon is null || _broadcastActive == active) return;
        try
        {
            _broadcastActive = active;
            _icon.Icon = active ? (_broadcastIcon ?? _normalIcon!) : _normalIcon!;
            _icon.Text = active ? "WinKuake — BROADCAST ON" : "WinKuake — drop-down terminal";
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    private void BuildContextMenu()
    {
        if (_icon is null) return;

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
    }

    private static Icon ExtractAppIcon()
    {
        var path = Environment.ProcessPath;
        if (string.IsNullOrEmpty(path)) return SystemIcons.Application;
        var icon = Icon.ExtractAssociatedIcon(path);
        return icon ?? SystemIcons.Application;
    }

    /// <summary>
    /// Genera una variante del ícono con un círculo rojo (radio overlay)
    /// en la esquina inferior derecha — usado como indicador visual cuando
    /// el broadcast input está activo.
    /// </summary>
    private static Icon BuildBroadcastIcon(Icon source)
    {
        // Trabajamos a 32x32 que es el tamaño que el shell usa para el tray.
        const int size = 32;
        using var bmp = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            // Pintamos el ícono original escalado.
            using var srcBmp = source.ToBitmap();
            g.DrawImage(srcBmp, 0, 0, size, size);

            // Badge rojo: ~38% del lado, en la esquina inf-derecha, con
            // borde blanco para que destaque sobre cualquier fondo.
            // Calificamos los tipos GDI completos porque los homónimos
            // System.Windows.Media.{Color,Pen,Brushes} están en GlobalUsings.
            var badge = size * 0.42f;
            var x = size - badge - 0.5f;
            var y = size - badge - 0.5f;
            using var fill = new SolidBrush(System.Drawing.Color.FromArgb(255, 0xFF, 0x33, 0x4D));
            g.FillEllipse(fill, x, y, badge, badge);
            using var stroke = new System.Drawing.Pen(System.Drawing.Color.White, 1.4f);
            g.DrawEllipse(stroke, x, y, badge, badge);
        }
        return BitmapToIcon(bmp);
    }

    /// <summary>
    /// Convierte un Bitmap a Icon administrando correctamente el HICON
    /// nativo (Bitmap.GetHicon devuelve un handle que NO se libera al
    /// disponer el Icon que lo envuelve — hay que destruirlo a mano).
    /// </summary>
    private static Icon BitmapToIcon(Bitmap bmp)
    {
        var hicon = bmp.GetHicon();
        try
        {
            // Clone() crea un Icon independiente del handle original.
            return (Icon)Icon.FromHandle(hicon).Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(hicon);
        }
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
            _normalIcon?.Dispose();
            _broadcastIcon?.Dispose();
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }
}
