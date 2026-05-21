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
    // Guard contra reinicialización: WPF re-dispara Loaded en cada
    // reparenting del visual tree (split mueve el pane existente entre
    // slots). Sin este flag, cada Loaded re-suscribía el handler de
    // mensajes de la WebView y cada tecla se escribía al PTY N veces
    // (síntoma: "hhhh" al teclear "h").
    private bool _webInitStarted;
    private string? _pendingCommandLine;
    private string? _pendingStartingDir;
    private short _lastCols = 120;
    private short _lastRows = 30;

    /// <summary>CWD actual reportado por el shell vía OSC 7. Null si nunca llegó.</summary>
    public string? CurrentCwd { get; private set; }

    /// <summary>Perfil con el que arrancó este pane. Usado para persistir layout.</summary>
    public WinKuake.Services.TerminalProfile? OriginProfile { get; set; }

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

    /// <summary>
    /// Se dispara la primera vez que el JS embebido postea "ready" — es decir,
    /// xterm.js terminó de montar y el WebView ya acepta term.focus().
    /// El host usa esto para mover el foco al pane recién creado (Ctrl+Shift+T,
    /// primer F12) sin tener que clickear.
    /// </summary>
    public event Action? Ready;

    /// <summary>
    /// Click derecho sobre el terminal. Coordenadas en píxeles de cliente del
    /// WebView2 (las traduce el host a coords de pantalla para posicionar el menú).
    /// </summary>
    public event Action<double, double, bool>? ContextMenuRequested;

    /// <summary>Abre el overlay de búsqueda (equivalente a Ctrl+Shift+F).</summary>
    public void OpenSearch()
    {
        if (WebView.CoreWebView2 is null) return;
        try { WebView.CoreWebView2.ExecuteScriptAsync("openSearch()"); }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    /// <summary>Limpia el buffer (equivalente a Ctrl+L). Usado por el menú contextual.</summary>
    public void ClearBuffer()
    {
        if (WebView.CoreWebView2 is null) return;
        try { WebView.CoreWebView2.ExecuteScriptAsync("term.clear()"); }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    /// <summary>Abre las DevTools del WebView2 (Inspect Element / Console).</summary>
    public void OpenDevTools()
    {
        if (WebView.CoreWebView2 is null) return;
        try { WebView.CoreWebView2.OpenDevToolsWindow(); }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    /// <summary>Pega el contenido del clipboard (equivalente a Ctrl+Shift+V).</summary>
    public void PasteFromClipboard()
    {
        if (WebView.CoreWebView2 is null) return;
        try
        {
            // Reusamos navigator.clipboard.readText() y term.paste() del JS para
            // que aplique bracketed paste igual que el atajo de teclado.
            const string js =
                "navigator.clipboard.readText()" +
                ".then(t => { if (t) term.paste(t); }).catch(()=>{})";
            WebView.CoreWebView2.ExecuteScriptAsync(js);
        }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    /// <summary>Copia la selección actual al clipboard.</summary>
    public async void CopySelectionToClipboard()
    {
        var sel = await GetSelectionAsync();
        if (string.IsNullOrEmpty(sel)) return;
        try { System.Windows.Clipboard.SetText(sel); }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

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
        // No suscribimos Unloaded → Dispose: WPF dispara Unloaded en cualquier
        // reparenting (p.ej. al hacer split, el pane existente se mueve a un
        // sub-slot del nuevo Grid). Cleanup explícito vía Dispose() desde
        // TerminalControl.CloseInternal / RestoreLayout cubre los casos reales.
        _pty.OutputReceived += OnPtyOutput;
        // El click dentro del pane lo marca como activo dentro de su split.
        PreviewMouseDown += (_, _) => FocusReceived?.Invoke();
        WebView.GotFocus += (_, _) => FocusReceived?.Invoke();
    }

    public void StartShell(string commandLine, string? startingDir = null)
    {
        if (!_webReady)
        {
            _pendingCommandLine = commandLine;
            _pendingStartingDir = startingDir;
            CrashLogger.Info($"StartShell pending (webReady=false): cmd='{commandLine}' dir='{startingDir}'");
            return;
        }
        CrashLogger.Info($"StartShell immediate: cmd='{commandLine}' dir='{startingDir}' cols={_lastCols} rows={_lastRows}");
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
        if (_webInitStarted) return;
        _webInitStarted = true;
        try { await InitializeWebViewAsync(); }
        catch (Exception ex) { CrashLogger.Log(ex); _webInitStarted = false; }
    }

    /// <summary>
    /// Mueve el foco real al terminal embebido. La UserControl es Focusable=True;
    /// un <c>this.Focus()</c> robaba el foco al WebView2 que el click acababa de
    /// recibir, dejando el pane "activo" pero sin captura de teclado. Aquí
    /// enfocamos el WebView (delega al HWND nativo) y reforzamos con term.focus()
    /// para el textarea oculto de xterm.
    /// </summary>
    public void FocusTerminal()
    {
        WebView.Focus();
        if (WebView.CoreWebView2 is null) return;
        try { WebView.CoreWebView2.ExecuteScriptAsync("term.focus()"); }
        catch (Exception ex) { CrashLogger.Log(ex); }
    }

    /// <summary>
    /// Environment compartido entre TODOS los panes del proceso. CoreWebView2
    /// solo permite UN environment activo por proceso; intentar crear otro
    /// lanza ArgumentException "WebView2 was already initialized with a
    /// different CoreWebView2Environment", lo que dejaba el segundo pane de
    /// un split en blanco.
    /// </summary>
    private static CoreWebView2Environment? _sharedEnv;
    private static readonly System.Threading.SemaphoreSlim _envLock = new(1, 1);

    private static async Task<CoreWebView2Environment> GetSharedEnvAsync()
    {
        if (_sharedEnv is not null) return _sharedEnv;
        await _envLock.WaitAsync();
        try
        {
            if (_sharedEnv is not null) return _sharedEnv;
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WinKuake", "WebView2");
            Directory.CreateDirectory(userDataFolder);
            _sharedEnv = await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
            return _sharedEnv;
        }
        finally { _envLock.Release(); }
    }

    private async Task InitializeWebViewAsync()
    {
        // Diagnóstico: cada etapa queda en winkuake.log con timestamp. Sin estos
        // breadcrumbs un fallo silencioso (pane 100% negro sin cursor) era
        // indistinguible de varias causas (env, mapping, navegación, JS muerto).
        CrashLogger.Info("WebView init: solicitando shared environment");
        var env = await GetSharedEnvAsync();
        CrashLogger.Info("WebView init: environment OK, llamando EnsureCoreWebView2Async");
        await WebView.EnsureCoreWebView2Async(env);
        CrashLogger.Info("WebView init: EnsureCoreWebView2Async OK");

        var resourcesDir = ResolveResourcesDir();
        var htmlPath = Path.Combine(resourcesDir, "terminal.html");
        if (!File.Exists(htmlPath))
            CrashLogger.Info($"WebView init: WARN terminal.html NO existe en {htmlPath}");
        else
            CrashLogger.Info($"WebView init: terminal.html OK en {resourcesDir}");

        WebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "winkuake.local", resourcesDir, CoreWebView2HostResourceAccessKind.Allow);

        WebView.CoreWebView2.WebMessageReceived += OnWebMessage;
        WebView.CoreWebView2.NavigationStarting += (_, e) =>
            CrashLogger.Info($"WebView nav starting: {e.Uri}");
        WebView.CoreWebView2.NavigationCompleted += (_, e) =>
            CrashLogger.Info($"WebView nav completed: success={e.IsSuccess} status={e.WebErrorStatus} httpStatus={e.HttpStatusCode}");
        WebView.CoreWebView2.DOMContentLoaded += (_, _) =>
            CrashLogger.Info("WebView DOMContentLoaded");
        WebView.CoreWebView2.ProcessFailed += (_, e) =>
            CrashLogger.Info($"WebView process FAILED: kind={e.ProcessFailedKind} reason={e.Reason} exit={e.ExitCode}");

        WebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        WebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
        WebView.CoreWebView2.Settings.IsStatusBarEnabled = false;

        CrashLogger.Info("WebView init: Navigate → https://winkuake.local/terminal.html");
        WebView.CoreWebView2.Navigate("https://winkuake.local/terminal.html");
    }

    private static string ResolveResourcesDir()
        => Path.Combine(AppContext.BaseDirectory, "Resources", "terminal");

    private bool _firstPtyChunkLogged;
    private void OnPtyOutput(ReadOnlyMemory<byte> data)
    {
        if (!_firstPtyChunkLogged)
        {
            _firstPtyChunkLogged = true;
            var preview = Encoding.UTF8.GetString(data.Span).Replace("\r", "\\r").Replace("\n", "\\n");
            if (preview.Length > 80) preview = preview[..80] + "…";
            CrashLogger.Info($"OnPtyOutput primer chunk: {data.Length} bytes: \"{preview}\"");
        }
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
                        CrashLogger.Info($"ready: arrancando PTY pendiente cmd='{cmd}' dir='{dir}' cols={_lastCols} rows={_lastRows}");
                        _pty.Start(cmd, _lastCols, _lastRows, dir);
                    }
                    else
                    {
                        CrashLogger.Info($"ready: SIN _pendingCommandLine (nadie llamó StartShell antes de que el JS posteara ready). cols={_lastCols} rows={_lastRows}");
                    }
                    Ready?.Invoke();
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
                case "contextMenu":
                {
                    double x = ReadDouble(root, "x", 0);
                    double y = ReadDouble(root, "y", 0);
                    bool hasSel = root.TryGetProperty("hasSelection", out var hs)
                                  && hs.ValueKind == JsonValueKind.True;
                    ContextMenuRequested?.Invoke(x, y, hasSel);
                    break;
                }
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

    private static double ReadDouble(JsonElement root, string name, double fallback)
    {
        if (root.TryGetProperty(name, out var v) && v.TryGetDouble(out var d))
            return d;
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

    public void Dispose()
    {
        try { _pty.Dispose(); } catch { }
    }
}
