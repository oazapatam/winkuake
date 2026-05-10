using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using WinKuake.Services;

namespace WinKuake.Views;

/// <summary>
/// Vista de un terminal: WebView2 que carga xterm.js como renderer y se conecta
/// a un <see cref="ConPtyService"/> que ejecuta el shell real. Bridge full-duplex
/// vía postMessage (JS↔C#).
/// </summary>
public partial class TerminalControl : UserControl
{
    private ConPtyService _pty = new();
    private bool _webReady;
    private string? _pendingCommandLine;
    private string? _pendingStartingDir;
    private short _lastCols = 120;
    private short _lastRows = 30;

    /// <summary>CWD actual reportado por el shell vía OSC 7. Null si nunca llegó.</summary>
    public string? CurrentCwd { get; private set; }

    public event Action<string>? CwdChanged;
    public event Action? NextTabRequested;
    public event Action? PrevTabRequested;

    public TerminalControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _pty.OutputReceived += OnPtyOutput;
    }

    /// <summary>Arranca el shell indicado. Si la WebView aún no terminó de inicializar, se difiere.</summary>
    public void StartShell(string commandLine, string? startingDir = null)
    {
        if (!_webReady) { _pendingCommandLine = commandLine; _pendingStartingDir = startingDir; return; }
        _pty.Start(commandLine, _lastCols, _lastRows, startingDir);
    }

    /// <summary>
    /// Cambia el shell del terminal: cierra el actual, limpia la pantalla, arranca uno nuevo.
    /// </summary>
    public void Restart(string commandLine, string? startingDir = null)
    {
        try { _pty.OutputReceived -= OnPtyOutput; } catch { }
        try { _pty.Dispose(); } catch { }
        _pty = new ConPtyService();
        _pty.OutputReceived += OnPtyOutput;
        // Limpiar buffer del terminal y arrancar nuevo proceso.
        Dispatcher.InvokeAsync(() =>
        {
            WebView.CoreWebView2?.PostWebMessageAsJson("{\"type\":\"reset\"}");
        });
        if (_webReady) _pty.Start(commandLine, _lastCols, _lastRows, startingDir);
        else { _pendingCommandLine = commandLine; _pendingStartingDir = startingDir; }
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try
        {
            await InitializeWebViewAsync();
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
        }
    }

    private async Task InitializeWebViewAsync()
    {
        // UserDataFolder en %LocalAppData%\WinKuake\WebView2 para no chocar con otros usos de WebView2.
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinKuake", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        await WebView.EnsureCoreWebView2Async(env);

        // Mapear nuestra carpeta de recursos como un host virtual https://winkuake.local/
        // así xterm.js, css, etc. se cargan con una URL coherente.
        var resourcesDir = ResolveResourcesDir();
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "winkuake.local", resourcesDir, CoreWebView2HostResourceAccessKind.Allow);

        WebView.CoreWebView2.WebMessageReceived += OnWebMessage;

        // Deshabilitar el menú contextual default (es ruidoso); a futuro armamos uno propio.
        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = true; // útil para debug
        WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

        WebView.CoreWebView2.Navigate("https://winkuake.local/terminal.html");
    }

    private static string ResolveResourcesDir()
    {
        // En dev y en single-file publish, los recursos están junto al exe
        // bajo Resources/terminal/.
        var baseDir = AppContext.BaseDirectory;
        return Path.Combine(baseDir, "Resources", "terminal");
    }

    private void OnPtyOutput(ReadOnlyMemory<byte> data)
    {
        // El shell genera UTF-8; lo despachamos al UI thread y reenviamos a xterm.
        var text = Encoding.UTF8.GetString(data.Span);
        Dispatcher.InvokeAsync(() =>
        {
            // PostWebMessageAsString manda un string crudo; xterm.js lo escribirá.
            WebView.CoreWebView2?.PostWebMessageAsString(text);
        });
    }

    private void OnWebMessage(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var doc = JsonDocument.Parse(e.WebMessageAsJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("type", out var typeProp)) return;
            var type = typeProp.GetString();

            switch (type)
            {
                case "ready":
                    _webReady = true;
                    _lastCols = ReadShort(root, "cols", 120);
                    _lastRows = ReadShort(root, "rows", 30);
                    SendConfigToTerminal();
                    if (_pendingCommandLine is not null)
                    {
                        var cmd = _pendingCommandLine;
                        var dir = _pendingStartingDir;
                        _pendingCommandLine = null;
                        _pendingStartingDir = null;
                        _pty.Start(cmd, _lastCols, _lastRows, dir);
                    }
                    break;

                case "in":
                    if (root.TryGetProperty("data", out var d))
                        _pty.Write(d.GetString() ?? "");
                    break;

                case "resize":
                    _lastCols = ReadShort(root, "cols", 120);
                    _lastRows = ReadShort(root, "rows", 30);
                    _pty.Resize(_lastCols, _lastRows);
                    break;

                case "openUrl":
                    if (root.TryGetProperty("url", out var u))
                    {
                        var url = u.GetString();
                        if (!string.IsNullOrEmpty(url))
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
                    }
                    break;

                case "debug":
                    if (root.TryGetProperty("msg", out var dm))
                        CrashLogger.Info("[js] " + dm.GetString());
                    break;

                case "cwd":
                    if (root.TryGetProperty("path", out var pp))
                    {
                        var path = pp.GetString();
                        if (!string.IsNullOrEmpty(path))
                        {
                            CurrentCwd = path;
                            CwdChanged?.Invoke(path);
                        }
                    }
                    break;

                case "nextTab":
                    NextTabRequested?.Invoke();
                    break;
                case "prevTab":
                    PrevTabRequested?.Invoke();
                    break;
            }
        }
        catch (Exception ex)
        {
            CrashLogger.Log(ex);
        }
    }

    private static short ReadShort(JsonElement root, string name, short fallback)
    {
        if (root.TryGetProperty(name, out var v) && v.TryGetInt32(out var i))
            return (short)Math.Clamp(i, 1, short.MaxValue);
        return fallback;
    }

    /// <summary>Envía settings reconfigurables (scrollback, fuente, tema) al terminal JS.</summary>
    private void SendConfigToTerminal()
    {
        var s = SettingsService.Load();
        long scrollback = s.ScrollbackLines == -1
            ? 9007199254740991L
            : Math.Max(100, s.ScrollbackLines);
        var theme = TerminalTheme.FindOrDefault(s.TerminalThemeName).ToXtermJson();
        var fontSize = Math.Clamp(s.TerminalFontSize, 8, 40);
        var payload = $"{{\"type\":\"config\",\"scrollback\":{scrollback},\"fontSize\":{fontSize},\"theme\":{theme}}}";
        WebView.CoreWebView2?.PostWebMessageAsJson(payload);
    }

    /// <summary>Re-aplica los settings actuales al terminal sin reiniciar.</summary>
    public void ApplyCurrentSettings()
    {
        if (_webReady) SendConfigToTerminal();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _pty.Dispose();
    }
}
