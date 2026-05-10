# Plan WinKuake — Motor ConPTY + UX profesional

## Estado actual (post-pivote)

Pivotamos de embeber `wt.exe` (chrome no se podía ocultar, clase de ventana cambió en 1.18+) a **ConPTY + xterm.js + WebView2**. Base MVP funcional: pwsh arranca, I/O real.

---

## Fase 0 — Base ConPTY  ✅
- [x] PackageReference `Microsoft.Web.WebView2`.
- [x] `ConPtyNative.cs` (P/Invoke a `CreatePseudoConsole`, `ResizePseudoConsole`, etc.).
- [x] `ConPtyService.cs` (gestión de pipes, lectura asíncrona, write, resize, dispose).
- [x] `TerminalControl.xaml` con WebView2 + bridge JSON bidireccional.
- [x] `Resources/terminal/terminal.html` base con xterm.js.
- [x] Borrado `TerminalHost.cs` y código wt-embed.

---

## Fase 1 — Terminal profesional (ESTA SESIÓN)

### 1.A Addons xterm.js
- [ ] `addon-web-links` — URLs clickeables.
- [ ] `addon-search` — buscar en buffer con `Ctrl+Shift+F`.
- [ ] `addon-unicode11` — soporte emojis modernos.
- [ ] `addon-webgl` — rendering GPU acelerado.
- [ ] `addon-clipboard` — copiar OSC 52.

### 1.B Tema y tipografía
- [ ] Paleta tipo VSCode Dark+ (background `#0c0c0c`, ANSI colores ricos).
- [ ] Font `Cascadia Code`, fallback `Consolas`.
- [ ] Letter-spacing/line-height razonables.
- [ ] Cursor block parpadeante.

### 1.C Quality of life
- [ ] Scrollback 10 000 líneas.
- [ ] `Ctrl+Shift+C` copiar selección (sin selección → SIGINT como bash).
- [ ] `Ctrl+Shift+V` pegar.
- [ ] `Ctrl+Shift+F` abre search overlay.
- [ ] `Ctrl+L` clear (envía `clear\n` al shell).
- [ ] `Ctrl++` / `Ctrl+-` zoom font in/out.
- [ ] Bracketed paste mode habilitado.
- [ ] Selección con triple-click para línea completa.

---

## Fase 2 — Perfiles desde settings.json de wt (ESTA SESIÓN)

### 2.A Resolución de commandline por perfil
- [ ] Extender `WtProfileSource` para leer también `commandline`, `source`, `startingDirectory`.
- [ ] Mapear `source: "Windows.Terminal.Wsl"` → `wsl.exe -d <name>`.
- [ ] Mapear perfiles incorporados sin commandline (PowerShell/cmd) → resolverlos por nombre.
- [ ] Filtrar `Azure Cloud Shell` (requiere auth, no soportable).
- [ ] Devolver `ShellCommandLine` listo para pasar a `ConPtyService.Start`.

### 2.B UI selector activo
- [ ] El dropdown `⌄` ahora SÍ lanza perfiles (no estará `IsEnabled = false`).
- [ ] Click en perfil → cierra ConPTY actual + arranca uno nuevo con el commandline del perfil.
- [ ] Tab única refleja el perfil activo (ícono + nombre).
- [ ] Default = perfil marcado `defaultProfile` en wt; fallback pwsh.

---

## Fase 3 — Multi-tab  ✅
- [x] `TerminalSessionsManager` con TDD (16 tests).
- [x] Cada tab = `TerminalControl` propio (preserva buffer al cambiar).
- [x] `+`, `⌄`, `✕` por tab, doble-click rename.
- [x] Cerrar última → arranca default, no cierra app.

## Fase WSL — Distros con login shell  ✅
- [x] `WslService.ParseListVerbose` parsea `wsl.exe -l --verbose`.
- [x] `WslService.BuildCommandLine` produce commandline con `--shell-type login` + `--cd ~` o `--cd /mnt/<letra>/...`.
- [x] `WslService.TranslateWindowsPathToWsl` mapea `C:\…` → `/mnt/c/…`.
- [x] Filtra `docker-desktop`/`docker-desktop-data` automáticamente.
- [x] `WtProfileSource.ResolveCommandLine` usa `WslService` para perfiles wt-WSL (login shell ahora).
- [x] `WtProfileSource.Merge` combina perfiles wt + distros WSL detectadas que falten.
- [x] `LoadCombined()` usado por MainWindow → dropdown muestra distros WSL aunque no estén en wt.
- [x] 12 tests nuevos cubriendo parser + builder + traducción de paths + merge.

## Fase 4 — UX adicional  ✅
- [x] AppSettings extendido: `ScrollbackLines` (-1=ilimitado, default), `TerminalThemeName`, `TerminalFontSize`.
- [x] `TerminalTheme` modelo + 5 paletas: VSCode Dark+, Dracula, Nord, Gruvbox Dark, Monokai. 10 tests cubriendo.
- [x] Settings window con nueva tab "Terminal": selector de tema, slider/textbox de fuente, combo de scrollback (Ilimitado / 1k / 10k / 50k / 100k).
- [x] Hot-reload: al guardar, todos los `TerminalControl` reciben `SendConfigToTerminal()` con los nuevos valores.
- [x] Status bar refleja shell activo con ícono (cuando cambias tab, muestra "🐧 Ubuntu" o "⚡ PowerShell").
- [x] Bridge JS `case 'config'` aplica scrollback, fontSize y theme en caliente.

## Fase 5 — Navegación y CWD  ✅
- [x] `TerminalSessionsManager.Move(id, newIndex)` con clamp y evento `OrderChanged` (7 tests).
- [x] `ActivateNext()` / `ActivatePrevious()` con wrap-around (3 tests).
- [x] OSC 7 handler en xterm → `term.parser.registerOscHandler(7, …)`. Parsea `file://hostname/path`.
- [x] `TerminalControl.CwdChanged` event + `CurrentCwd` property.
- [x] Status bar muestra `<glyph> Perfil · ~/relative/path` con abreviación de home.
- [x] `Ctrl+Tab` / `Ctrl+Shift+Tab` rotan entre tabs (interceptados en JS → `nextTab`/`prevTab` → manager).
- [x] Drag-and-drop tabs: `Tab_MouseMove` arranca `DoDragDrop`, drop reordena vía `_sessions.Move()`, `OrderChanged` sincroniza la `ObservableCollection` con `Tabs.Move()`.

## Fase 6 — Productividad avanzada  ✅
- [x] `Ctrl+Shift+1..9` → `_sessions.ActivateAt(n)` (jump-to-tab).
- [x] `Ctrl+Shift+PageUp/PageDown` → `_sessions.MoveActiveBy(±1)` (mover tab con teclado).
- [x] Tab pinning (📌): `TerminalSessionsManager.TogglePin`, evento `PinChanged`, glyph en cada tab, prompt al cerrar pinned.
- [x] Context menu del tab (click derecho): Renombrar / Fijar / Cerrar.
- [x] `FileSystemWatcher` sobre `settings.json` de wt → recarga perfiles en caliente con debounce 300 ms.
- [x] `Ctrl+Shift+S` → exporta buffer completo (visible + scrollback) a archivo `.txt` con SaveFileDialog.
- [x] **Splits**: extraído `TerminalPane` (WebView2+ConPty), `TerminalControl` ahora hospeda 1-2 panes con `GridSplitter`.
  - `Alt+Shift+=` split vertical (pane al lado).
  - `Alt+Shift+-` split horizontal (pane debajo).
  - `Ctrl+Shift+W` cierra el pane activo (cuando hay split).
  - Click en un pane lo marca activo (borde superior accent).
  - Menú ≡ → Dividir vertical / Dividir horizontal enganchados.
  - **Limitación v1**: una sola subdivisión por sesión (no recursivo). Para más, usar nuevas tabs.

## Fase 7 — Splits avanzados y navegación  ✅
- [x] **Splits recursivos**: TerminalControl rehecho con árbol `Border.Child` (leaf = pane, branch = Grid con 3 cells). Cada pane puede subdividirse las veces que quieras vertical u horizontalmente.
- [x] Cerrar pane colapsa el split: el slot padre adopta el hermano (que puede ser pane u otro Grid).
- [x] **Alt+ArrowUp/Down/Left/Right** → enfoca el pane vecino en esa dirección (heurística por geometría: rect más cercano que cumple constraint).
- [x] **Links a archivos clickeables**: xterm `registerLinkProvider` detecta paths Windows (`C:\…`), Linux absolutos (`/…`) y relativos (`./`, `../`). Click → host abre con `ShellExecute`.
  - Resolución de paths relativos contra `CurrentCwd` del pane.
  - Traducción `/mnt/c/…` → `C:\…` para abrir archivos WSL desde Windows.

## Fase 8 — Backlog
- Snippets / quick command palette (overlay con search + lista, hotkey `Ctrl+Shift+P`).
- Find global multi-buffer (`Ctrl+Shift+F` busca en TODOS los buffers a la vez con UI dedicada).
- Editor de paleta custom (tab adicional en Settings).
- Persistencia de splits (recordar layout al reabrir tab).
- Broadcast input (escribir en varios panes a la vez).

---

## Limitaciones aceptadas
- **Autocompletación**: la provee el SHELL, no WinKuake. pwsh trae PSReadLine; WSL+zsh trae oh-my-zsh; fish nativo.
- **Ligaduras de fuente**: `xterm-addon-ligatures` requiere parser de fonts en JS (~150 KB). Diferido a fase futura.
- **Azure Cloud Shell**: requiere auth Azure y túnel WebSocket. Fuera de scope.
- **Imagenes sixel**: posible con `addon-image` pero requiere WebGL + perfil. Fase futura.

---

## Comandos clave durante la implementación

```powershell
# Compilar
dotnet build src\WinKuake\WinKuake.csproj

# Tests
dotnet test tests\WinKuake.Tests\WinKuake.Tests.csproj

# Ejecutar
Start-Process src\WinKuake\bin\Debug\net10.0-windows\win-x64\WinKuake.exe
```
