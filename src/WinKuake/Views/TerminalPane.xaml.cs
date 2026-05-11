using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using WinKuake.Services;

namespace WinKuake.Views;

/// <summary>
/// Un pane de terminal: una WebView2 con xterm.js conectada a su propio
/// ConPty. Es la unidad mínima que ejecuta y renderiza un shell.
/// Múltiples panes coexisten dentro de un <see cref="TerminalControl"/>
/// (un pane normal; dos cuando hay split).
/// </summary>
public partial class TerminalPane : UserControl
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
    public event Action<int>? ActivateAtRequested;
    public event Action<int>? MoveActiveByRequested;
    public event Action<string>? SaveBufferRequested;
    public event Action? SplitHorizontalRequested;
    public event Action? SplitVerticalRequested;
    public event Action? ClosePaneRequested;
    public event Action? FocusReceived;
    public event Action<string>? FocusPaneRequested;
    public event Action<string>? OpenFileRequested;
    public event Action? OpenPaletteRequested;
    public event Action? ToggleBroadcastRequested;
    public event Action? OpenGlobalFindRequested;
    public event Action<string>? InputReceived;

    /// <summary>Escribe texto directamente al PTY (uso: paleta de comandos).</summary>
    public void InjectInput(string text) => _pty.Write(text);

    /// <summary>Devuelve la selección actual del xterm, vacío si no hay nada.</summary>
    public async System.Threading.Tasks.Task<string?> GetSelectionAsync()
    {
        if (WebView.CoreWebView2 is null) return null;
        try
        {
            var raw = await WebView.CoreWebView2.ExecuteScriptAsync("term.getSelection()");
            if (string.IsNullOrEmpty(raw) || raw == "null") return null;
            // raw viene como JSON string ("..."), des-escapamos.
            return JsonSerializer.Deserialize<string>(raw);
        }
        catch (Exception ex) { CrashLogger.Log(ex); return null; }
    }

    /// <summary>
    /// Recolecta todas las líneas del buffer xterm (visible + scrollback) como
    /// strings, en orden absoluto. Pensado para alimentar la búsqueda global.
    /// </summary>
    public async Task<IReadOnlyList<string>> GetBufferLinesAsync()
    {
        if (WebView.CoreWebView2 is null) return Array.Empty<string>();
        try
        {
            const string js =
                "(()=>{const b=term.buffer.active;const r=[];" +
                "for(let i=0;i<b.length;i++){const l=b.getLine(i);" +
                "r.push(l?l.translateToString(true):'');}return JSON.stringify(r);})()";
            var raw = await WebView.CoreWebView2.ExecuteScriptAsync(js);
            if (string.IsNullOrEmpty(raw) || raw == "null") return Array.Empty<string>();
            // ExecuteScriptAsync envuelve cualquier string como JSON-string;
            // el inner es a su vez JSON con el array. Doble decode.
            var inner = JsonSerializer.Deserialize<string>(raw);
            if (string.IsNullOrEmpty(inner)) return Array.Empty<string>();
            var arr = JsonSerializer.Deserialize<string[]>(inner);
            return arr ?? Array.Empty<string>();
        }
        catch (Exception ex) { CrashLogger.Log(ex); return Array.Empty<string>(); }
    }

    /// <summary>Hace scroll y selecciona la línea indicada (absoluta en el buffer).</summary>
    public void ScrollToLine(int line)
    {
        if (WebView.CoreWebView2 is null) return;
        // term.scrollToLine espera la posición absoluta en el buffer; selectLines
        // pinta el highlight para que el usuario vea dónde quedó.
        var payload = $"{{\"type\":\"scrollToLine\",\"line\":{line}}}";
        WebView.CoreWebView2.PostWebMessageAsJson(payload);
    }

    public TerminalPane()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        _pty.OutputReceived += OnPtyOutput;
        // El click dentro del pane lo marca como activo dentro de su split.
        PreviewMouseDown += (_, _) => FocusReceived?.Invoke();
        WebView.GotFocus += (_, _) => FocusReceived?.Invoke();
    }

    public void StartShell(string commandLine, string? startingDir = null)
    {
        if (!_webReady) { _pendingCommandLine = commandLine; _pendingStartingDir = startingDir; return; }
        _pty.Start(commandLine, _lastCols, _lastRows, startingDir);
    }

    public void Restart(string commandLine, string? startingDir = null)
    {
        try { _pty.OutputReceived -= OnPtyOutput; } catch { }
        try { _pty.Dispose(); } catch { }
        _pty = new ConPtyService();
        _pty.OutputReceived += OnPtyOutput;
        Dispatcher.InvokeAsync(() =>
        {
            WebView.CoreWebView2?.PostWebMessageAsJson("{\"type\":\"reset\"}");
        });
        if (_webReady) _pty.Start(commandLine, _lastCols, _lastRows, startingDir);
        else { _pendingCommandLine = commandLine; _pendingStartingDir = startingDir; }
    }

    public void ApplyCurrentSettings()
    {
        if (_webReady) SendConfigToTerminal();
    }

    public void SetActiveVisuals(bool active)
    {
        ActiveOverlay.BorderBrush = active
            ? (Brush)Application.Current.FindResource("AccentBrush")
            : Brushes.Transparent;
    }

    /// <summary>Muestra/oculta el botón X de cerrar pane (solo cuando hay split).</summary>
    public void ShowCloseButton(bool show)
    {
        ClosePaneButton.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ClosePaneButton_Click(object sender, RoutedEventArgs e)
    {
        ClosePaneRequested?.Invoke();
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        try { await InitializeWebViewAsync(); }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    private async Task InitializeWebViewAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinKuake", "WebView2");
        Directory.CreateDirectory(userDataFolder);

        var env = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
        await WebView.EnsureCoreWebView2Async(env);

        var resourcesDir = ResolveResourcesDir();
        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "winkuake.local", resourcesDir, CoreWebView2HostResourceAccessKind.Allow);

        WebView.CoreWebView2.WebMessageReceived += OnWebMessage;
        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

        WebView.CoreWebView2.Navigate("https://winkuake.local/terminal.html");
    }

    private static string ResolveResourcesDir()
        => Path.Combine(AppContext.BaseDirectory, "Resources", "terminal");

    private void OnPtyOutput(ReadOnlyMemory<byte> data)
    {
        var text = Encoding.UTF8.GetString(data.Span);
        Dispatcher.InvokeAsync(() =>
            WebView.CoreWebView2?.PostWebMessageAsString(text));
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
                    {
                        var input = d.GetString() ?? "";
                        _pty.Write(input);
                        InputReceived?.Invoke(input);
                    }
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
                            System.Diagnostics.Process.Start(
                                new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
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

                case "nextTab":           NextTabRequested?.Invoke(); break;
                case "prevTab":           PrevTabRequested?.Invoke(); break;
                case "activateAt":
                    if (root.TryGetProperty("index", out var ix) && ix.TryGetInt32(out var ii))
                        ActivateAtRequested?.Invoke(ii);
                    break;
                case "moveActiveBy":
                    if (root.TryGetProperty("delta", out var dl) && dl.TryGetInt32(out var di))
                        MoveActiveByRequested?.Invoke(di);
                    break;
                case "saveBuffer":
                    if (root.TryGetProperty("text", out var tx))
                    {
                        var t = tx.GetString();
                        if (!string.IsNullOrEmpty(t)) SaveBufferRequested?.Invoke(t!);
                    }
                    break;
                case "splitHorizontal": SplitHorizontalRequested?.Invoke(); break;
                case "splitVertical":   SplitVerticalRequested?.Invoke();   break;
                case "closePane":       ClosePaneRequested?.Invoke();       break;
                case "focusPane":
                    if (root.TryGetProperty("direction", out var dr))
                        FocusPaneRequested?.Invoke(dr.GetString() ?? "");
                    break;
                case "openFile":
                    if (root.TryGetProperty("path", out var fp))
                    {
                        var path = fp.GetString();
                        if (!string.IsNullOrEmpty(path)) OpenFileRequested?.Invoke(path!);
                    }
                    break;
                case "openPalette":
                    OpenPaletteRequested?.Invoke();
                    break;
                case "toggleBroadcast":
                    ToggleBroadcastRequested?.Invoke();
                    break;
                case "openGlobalFind":
                    OpenGlobalFindRequested?.Invoke();
                    break;
            }
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    private static short ReadShort(JsonElement root, string name, short fallback)
    {
        if (root.TryGetProperty(name, out var v) && v.TryGetInt32(out var i))
            return (short)Math.Clamp(i, 1, short.MaxValue);
        return fallback;
    }

    private void SendConfigToTerminal()
    {
        var s = SettingsService.Load();
        long scrollback = s.ScrollbackLines == -1
            ? 9007199254740991L
            : Math.Max(100, s.ScrollbackLines);
        var theme = TerminalTheme.ResolveCurrent(s).ToXtermJson();
        var fontSize = Math.Clamp(s.TerminalFontSize, 8, 40);
        var payload = $"{{\"type\":\"config\",\"scrollback\":{scrollback},\"fontSize\":{fontSize},\"theme\":{theme}}}";
        WebView.CoreWebView2?.PostWebMessageAsJson(payload);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _pty.Dispose();

    public void Dispose()
    {
        try { _pty.Dispose(); } catch { }
    }
}
