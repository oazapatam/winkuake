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

## Fase 8 — Paleta de comandos  ✅
- [x] `CommandSnippet` record + `CommandSnippetService` con 21 defaults (git, docker, npm, dotnet, etc.). Filter multi-token tipo VSCode (todos los tokens deben matchear name o command). 6 tests cubriendo.
- [x] `QuickCommandWindow` (overlay flotante, borderless, accent al item activo, doble-click commit). TextBox arriba para buscar, ListBox abajo, navegación con flechas.
- [x] Atajo `Ctrl+Shift+P` desde xterm → `OpenPaletteRequested` → `MainWindow.OpenCommandPalette`.
- [x] `TerminalPane.InjectInput(string)` / `TerminalControl.InjectInputToActive(string)` para escribir al PTY desde fuera.
- [x] **Enter** inyecta el texto al terminal (el usuario revisa y ejecuta). **Shift+Enter** inyecta + ejecuta directamente (`\n` final).
- [x] Si la query no matchea ningún snippet, se inyecta el texto crudo como comando ad-hoc.

## Fase 9 — Broadcast + variables en snippets  ✅
- [x] **Broadcast input**: `Ctrl+Shift+B` activa/desactiva. Cuando está activo, lo que escribes en cualquier pane se replica al resto de panes de la sesión. Status bar muestra `📡 BROADCAST` cuando está on.
- [x] **Variables en snippets**: `{cwd}`, `{home}`, `{user}` se expanden al inyectar. Case-insensitive. Si la variable no existe, se deja literal. 6 tests cubriendo (118 total).

## Fase 10 — Snippets editables + variable {date}  ✅
- [x] `AppSettings.UserSnippets`: `List<UserSnippet>` persistido en `settings.json`.
- [x] Nueva tab **"Snippets"** en SettingsWindow: `DataGrid` editable in-place con columnas Name / Command. `CanUserAddRows=True` (la fila vacía al final agrega una). Tip visible sobre las variables disponibles.
- [x] `CommandSnippetService.LoadAll(userSnippets)`: combina defaults + user (user al final). Null → solo defaults. 2 tests.
- [x] `MainWindow.OpenCommandPalette` ahora consume `LoadAll(_settings.UserSnippets)` → la paleta muestra defaults + tuyos.
- [x] Variable `{date}` se expande a `yyyy-MM-dd` (date local). 1 test cubriendo.
- [x] Snippets vacíos (name o command en blanco) se descartan al guardar.

## Fase 11 — Fixes de UX reportados  ✅
- [x] **Fix split horizontal**: `GridSplitter` ahora se construye con `ResizeDirection` explícito (`Columns` para vertical, `Rows` para horizontal) y alignment correcto (Stretch + Center según orientación). Antes ambos Stretch causaba que el splitter horizontal no respondiera al drag.
- [x] **Botón X cerrar pane**: cada pane muestra un ✕ rojo arriba a la derecha cuando es parte de un split (oculto cuando es el único). Opacidad 0.5, sube a 1.0 al hover. Click cierra ese pane.
- [x] **Item "Cerrar pane" en menú ≡** (acceso por menú además del atajo `Ctrl+Shift+W`).
- [x] **Item "Paleta de comandos" en menú ≡** (acceso a `Ctrl+Shift+P`).
- [x] **Persistencia reforzada**: `SettingsService.Load` ahora crea `settings.json` con defaults la primera vez que se ejecuta la app. El usuario ve que el archivo existe en `%AppData%\WinKuake\settings.json` desde el primer arranque y puede modificarlo a mano si quiere.
- [x] **Tests roundtrip completos**: `AllFields_PersistThroughJson` valida que TODOS los campos de AppSettings (incluyendo UserSnippets) sobreviven serialización; nuevos tests específicos para `Save_ThenLoad_PersistsTerminalSettings` y `Save_ThenLoad_PersistsUserSnippets` confirman el end-to-end con disco real.

## Auditoría del plan

**Implementado y validado:**
- Motor: ConPTY + xterm.js (WebView2) + 5 addons.
- Multi-tab con TerminalSessionsManager TDD.
- Perfiles wt (auto-detect + WSL login shell + cwd traducido).
- Tema xterm seleccionable (5 paletas) + fuente + scrollback configurable.
- Splits recursivos H/V con cerrar pane.
- Atajos: F12, Ctrl+Tab, Ctrl+Shift+1..9, PgUp/Dn, Alt+Shift+=/-, Ctrl+Shift+W/P/B/F/S/C/V/L, Ctrl++/-, Alt+arrows.
- Drag-and-drop reorder + pin de tabs.
- CWD en status bar (OSC 7).
- Links a archivos clickeables.
- Paleta de comandos con 21 defaults + user-snippets + variables `{cwd}/{home}/{user}/{date}`.
- Broadcast input.
- Hot-reload de wt settings.json.
- Persistencia completa de todos los settings.

## Fase 12 — Backlog atacado en una pasada  ✅
- [x] **Variable `{branch}`**: `GitService.GetBranch(cwd)` ejecuta `git rev-parse --abbrev-ref HEAD` con timeout 600 ms; se llama desde `OpenCommandPalette` con `Task.Run` para no bloquear UI. Cache implícito por invocación.
- [x] **Variable `{selection}`**: `TerminalPane.GetSelectionAsync()` llama `WebView2.ExecuteScriptAsync("term.getSelection()")` y deserializa el JSON. `TerminalControl.GetActivePaneSelectionAsync()` forward.
- [x] **Persistencia de tabs entre arranques**: `AppSettings.LastSessionTabs` (List<PersistedTab>) con ProfileGuid/Name, Cwd, CustomLabel, IsPinned. Snapshot al `OnClosed`; `RestoreSessionOrCreateDefault` recrea al primer toggle. Si el cwd es path Windows válido, se aplica como starting directory.
- [x] **Workspaces guardables**: `AppSettings.Workspaces`. Submenu en ≡ con "Guardar workspace…" (pide nombre con RenameDialog), "Cargar «name»", "Eliminar «name»". Reemplaza si el nombre coincide.
- [x] **Argumento CLI `--cwd <path>`** (también `--cwd=<path>`): `App.ParseArg` extrae, `MainWindow.InitialCwd` lo aplica como starting directory de la primera tab si el path existe.
- [x] **Tray icon**: `TrayIconService` usa `System.Windows.Forms.NotifyIcon` (con `UseWindowsForms=true` en csproj). Menú contextual: Mostrar/ocultar, Configuración, Salir. Doble-click toggle. `GlobalUsings.cs` resuelve los homónimos WPF↔Forms a favor de WPF.

## Fase 13 — Backlog
- Find global multi-buffer (overlay con resultados de TODOS los buffers, click → jump).
- Editor de paleta de tema custom (color pickers + preview en vivo).
- Persistencia de árbol de splits (serializar geometría dentro de cada PersistedTab).
- Auto-update vía GitHub releases.
- Configurar atajos custom (no solo hotkey global).
- Sincronizar settings vía GitHub Gist (opcional).
- Soporte de SSH/PuTTY integrado.
- Animación entre cambios de tab.

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
