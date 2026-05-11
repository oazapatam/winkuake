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

## Fase 1 — Terminal profesional  ✅

### 1.A Addons xterm.js
- [x] `addon-web-links` — URLs clickeables.
- [x] `addon-search` — buscar en buffer con `Ctrl+Shift+F`.
- [x] `addon-unicode11` — soporte emojis modernos.
- [x] `addon-webgl` — rendering GPU acelerado.
- [x] `addon-clipboard` — copiar OSC 52.

### 1.B Tema y tipografía
- [x] Paleta tipo VSCode Dark+ (background `#0c0c0c`, ANSI colores ricos).
- [x] Font `Cascadia Code`, fallback `Consolas`.
- [x] Letter-spacing/line-height razonables.
- [x] Cursor block parpadeante.

### 1.C Quality of life
- [x] Scrollback 10 000 líneas → ahora ilimitado por default (Fase 4).
- [x] `Ctrl+Shift+C` copiar selección.
- [x] `Ctrl+Shift+V` pegar.
- [x] `Ctrl+Shift+F` abre search overlay.
- [x] `Ctrl+L` clear.
- [x] `Ctrl++` / `Ctrl+-` zoom font in/out.
- [x] Bracketed paste mode habilitado.
- [x] Selección con triple-click para línea completa.

---

## Fase 2 — Perfiles desde settings.json de wt  ✅

### 2.A Resolución de commandline por perfil
- [x] Extender `WtProfileSource` para leer también `commandline`, `source`, `startingDirectory`.
- [x] Mapear `source: "Windows.Terminal.Wsl"` → `wsl.exe -d <name>`.
- [x] Mapear perfiles incorporados sin commandline (PowerShell/cmd) → resolverlos por nombre.
- [x] Filtrar `Azure Cloud Shell` (requiere auth, no soportable).
- [x] Devolver `ShellCommandLine` listo para pasar a `ConPtyService.Start`.

### 2.B UI selector activo
- [x] El dropdown `⌄` lanza perfiles.
- [x] Click en perfil → cierra ConPTY actual + arranca uno nuevo con el commandline del perfil.
- [x] Tab única refleja el perfil activo (ícono + nombre).
- [x] Default = perfil marcado `defaultProfile` en wt; fallback pwsh.

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

## Fase 13 — Paralelizado con 3 agentes  ✅
- [x] **Agente A** — `UpdateService` chequea GitHub releases, compara versiones con `System.Version`, devuelve `UpdateInfo` con URL del .exe. 14 tests.
- [x] **Agente B** — Búsqueda global multi-buffer: `Ctrl+Shift+Alt+F` abre `FindGlobalWindow` (overlay 700×500 con debounce 200 ms), recolecta buffers de TODOS los panes en paralelo vía `GetBufferLinesAsync`, click en resultado salta a la línea con `scrollToLine`. 10 tests en `GlobalFindService`.
- [x] **Agente C** — Editor de paleta custom (19 colores hex con swatches, visible si tema == "Custom") + tab "Atajos" (DataGrid editable, 9 acciones, persistencia diff-vs-default). `TerminalTheme.ResolveCurrent` decide entre paleta predefinida o custom. 19 tests.

**Total tras merge**: 172 tests verdes, build limpio.

## Fase 14 — Persistencia del árbol de splits  ✅
- [x] `PersistedSplitNode` con Orientation null para leaves; First/Second para branches; campos ProfileGuid/Name/Cwd en leaves.
- [x] `PersistedTab.Layout` nullable; cuando hay splits se serializa el árbol.
- [x] `TerminalPane.OriginProfile` retiene perfil con el que arrancó el pane (asignado por `TerminalControl.StartShell(TerminalProfile)` y heredado en splits).
- [x] `TerminalControl.SerializeLayout` recorre el árbol de Border.Child y emite `PersistedSplitNode`.
- [x] `TerminalControl.RestoreLayout(node, profileResolver)` reconstruye el árbol creando panes con sus perfiles correctos.
- [x] `MainWindow.SnapshotCurrentSessions` incluye Layout; restore via `_pendingLayouts` aplicado en `OnSessionAdded`.
- [x] 2 tests nuevos: roundtrip JSON de leaf y branch. **174/174 tests verdes.**

## Fase 15 — Fix definitivo de split + auditoría de plan  ✅
- [x] **Bug crítico de splits**: `CoreWebView2Environment.CreateAsync` solo permite UN environment por proceso. Cada `TerminalPane.InitializeWebViewAsync` creaba uno propio → al hacer split, el segundo pane lanzaba `ArgumentException "WebView2 was already initialized with a different CoreWebView2Environment"` y quedaba en blanco. Fix: singleton `_sharedEnv` con `SemaphoreSlim` que se crea una vez y se reutiliza por todos los panes. Tanto split horizontal como vertical funcionan correctamente.
- [x] **Atajos de split más robustos**: además de `Alt+Shift+=` / `Alt+Shift+-`, ahora también reconocen `Ctrl+Shift+D` / `Ctrl+Shift+E` (estilo wt) y detección por `ev.key` para layouts de teclado no-US.
- [x] **Plan auditado**: marcadas Fases 1 y 2 como completadas (estaban con `[ ]` aunque las features siempre estuvieron implementadas).

## Fase 16 — Bug fixes + auditoría paralela (2 agentes)  ✅

- [x] **String "Configurar Yakuake…" → "Configurar WinKuake…"** en el menú ≡. (Master, antes del lanzamiento de agentes).
- [x] **CLAUDE.md**: nueva sección "Desarrollo en paralelo con agentes" con reglas para usar `isolation=worktree`, agrupar features por archivos disjuntos, mergear ordenado y un commit final.
- [x] **Agente "Auditoría persistencia"** encontró el bug raíz: `SettingsWindow.Clone()` omitía `LastSessionTabs` y `Workspaces`. Cada vez que se abría Configuración y se guardaba, **los workspaces guardados se borraban del disco** (`LastSessionTabs` también se vaciaba pero se restablecía al cerrar la app via `OnClosed.SnapshotCurrentSessions()`). Fix: `AppSettings.DeepClone()` movido al modelo (con `DeepClone` simétrico en `PersistedTab`, `PersistedSplitNode`, `Workspace`, `TerminalThemeColors`); SettingsWindow delega a éste. 9 tests nuevos cubren cada colección + roundtrip end-to-end disco real.
- [x] **Agente "Auditoría Fase 1"** verificó cada item de la fase, encontró un único gap: `Ctrl+Shift+V` pegaba texto crudo al PTY (rompía multilínea con bracketed paste). Cambiado a `term.paste(t)` para envolver con `ESC[200~…ESC[201~`. 34 tests de regresión textual sobre `terminal.html` (existencia de cada `<script src>`, instanciación de addons, theme VSCode Dark+, atajos C/V/F/L/zoom, overlay search).

**Total tras merge**: 217 tests verdes, build limpio.

## Fase 17 — Auditoría completa Fases 2-16 (3 agentes paralelos)  ✅

Lanzados en worktrees aislados, archivos disjuntos. Cada agente verificó cada item `[x]` contra código real, escribió tests de regresión con sufijo `*AuditTests.cs`. **142 tests nuevos**, 0 bugs reales encontrados, sólo gaps cosméticos / inconsistencias suaves entre PLAN y código.

- [x] **Agente A (Fases 2/WSL/3/6)** — 36 tests en 4 archivos: `WtProfileSourceAuditTests`, `WslServiceAuditTests`, `TerminalSessionsManagerAuditTests`, `ProfileWatcherAuditTests`. Cubre case-insensitive del source WSL, env-var expansion, fallback `.exe`, merge wt+WSL, filtro rancher-desktop, debounce real 300 ms del FileSystemWatcher coalesciendo writes, pin individual, ActivateAt sobre la activa, MoveActiveBy(0).
  - Gaps detectados (no bugs): Azure Cloud Shell se marca como deshabilitado en lugar de filtrarse; FileSystemWatcher solo cubre wt-Store estable, no Preview/unpackaged; InputGestureText `Ctrl+Shift+1..9` en menú de perfiles puede confundir (activa tab N, no abre perfil N).
- [x] **Agente B (Fases 4/5/7/14/15)** — 48 tests en 5 archivos: `TerminalThemeAuditTests`, `OscHandlerAuditTests`, `SplitNavigationAuditTests`, `LinkProviderAuditTests`, `SplitTreeAuditTests`. Cubre las 5 paletas obligatorias (unicidad, hex válido, todas las claves xterm), regex del link provider replicada en C# para Windows/Linux/relativos, roundtrip JSON árbol 4 niveles de splits, singleton `_sharedEnv` + `_envLock` (SemaphoreSlim), atajos split duales (Alt+Shift y Ctrl+Shift+D/E + `ev.key` para layouts no-US), regresión textual sobre `terminal.html` (OSC 7, registerLinkProvider, atajos Ctrl+Tab/PageUp/Down).
  - Gaps detectados: slider de fuente capa 32 en UI aunque modelo soporta hasta 40; `AbbreviateCwd` no abrevia paths UNC `\\wsl$\…`; orientación de Grid en `SerializeSlot` se infiere por `ColumnDefinitions.Count > 0` (frágil si en el futuro alguien añade rama mixta).
- [x] **Agente C (Fases 8/9/10/12/13)** — 58 tests en 7 archivos: `PaletteAuditTests`, `SnippetVariablesAuditTests`, `WorkspaceAuditTests`, `KeybindingAuditTests`, `GitServiceAuditTests`, `CliArgAuditTests`, `BroadcastAuditTests`. Cubre los 21 defaults exactos (familias git/docker/npm/dotnet, nombres únicos), variables case-mixed `{Branch}/{BRANCH}/{bRaNcH}`, fecha con zero-padding, selección multilínea, integration tests con `git init -b auditbranch` real, ParseArg vía reflection (`--cwd path`, `--cwd=path`, `--cwd-extended` no confunde), workspaces con árbol de splits roundtrip, KeybindingService gestos default vs plan.
  - Gaps detectados: TrayIcon tiene ítem extra "Ocultar" no mencionado en PLAN; LoadWorkspace usa `Close` en vez de `TryCloseSession` (no respeta confirmación de pinned, probablemente intencional); `BuildWorkspacesMenu` no se reconstruye al guardar Settings (no es regresión actual porque SettingsWindow no edita workspaces).

**Total post-merge: 359 tests verdes** (217 previos + 142 nuevos). Build limpio. Ningún `.cs` de `src/` modificado por los agentes — el plan estaba completo y correcto, sólo faltaba cobertura de tests defensivos.

## Fase 19 — Menú contextual del terminal (click derecho)  ✅
- [x] Listener `contextmenu` en `terminal.html` con `preventDefault()` que postea `{type:'contextMenu', x, y, hasSelection}` al host.
- [x] `TerminalPane.ContextMenuRequested` event + case `"contextMenu"` en `OnWebMessage`. `ReadDouble` helper para coords.
- [x] `TerminalPane.OpenSearch()`, `ClearBuffer()`, `PasteFromClipboard()`, `CopySelectionToClipboard()` para que cada item del menú reuse la misma ruta que el atajo de teclado.
- [x] `TerminalControl.ContextMenuRequested` propaga el evento con el pane que recibió el click.
- [x] `TerminalContextMenuBuilder.Build(hasSelection, isInSplit)` — función pura que retorna `IReadOnlyList<ContextMenuItemSpec>` con label, shortcut, enabled. Items: Copiar (gris si !hasSelection), Pegar, Buscar, ─, Dividir vert/horiz, Cerrar pane (gris si !isInSplit), ─, Paleta, Limpiar.
- [x] `MainWindow.ShowTerminalContextMenu` materializa el spec a `ContextMenu` WPF, traduce client-coords del WebView2 a screen via `pane.PointToScreen()`, conecta cada `MenuItem.Click` al handler ya existente del atajo equivalente. Antes de abrir, hace `FocusPane(pane)` para que las acciones operen sobre el pane que recibió el click.
- [x] 22 tests nuevos en `ContextMenuTests.cs` (builder puro 9 tests + regex sobre `terminal.html` 5 + regex sobre `TerminalPane.xaml.cs` 2 + theory de labels/shortcuts 8). **381/381 verdes**.

## Fase 20 — Detección de terminales propia (independencia TOTAL de wt)  ✅

**Motivación:** hoy WinKuake depende del `settings.json` de Windows Terminal para enumerar perfiles. Eso causa: duplicados (Ubuntu por wt + Ubuntu por detección WSL nativa), entradas "(no soportado)" no actionables (Azure Cloud Shell, perfiles wt sin commandline resoluble), y dependencia obligatoria de que el usuario tenga wt instalado y configurado.

**Decisión del usuario (explícita):** la app **NO debe depender de wt para nada**. Sin importer, sin fallback, sin lectura del `settings.json`. Cero rastro. WinKuake descubre terminales por sí misma y el usuario gestiona la lista desde su propia UI.

### 20.A — Detectores nativos (un servicio por familia)

Cada detector implementa `IProfileDetector { IReadOnlyList<TerminalProfile> Detect(); }`. Funciones puras que solo miran filesystem / registry / `wsl.exe -l`. Resultado se mergea en un único `ProfileRegistry`. Los detectores **descartan en origen** todo perfil cuyo commandline no se pueda resolver — nunca generan entradas "(no soportado)".

- [ ] `WindowsPowerShellDetector` — `%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe`. Siempre presente desde Win 7.
- [ ] `PwshDetector` — `where.exe pwsh.exe`. Detecta PS 7+ instalado por MSI o Store. Múltiples instalaciones → múltiples perfiles con sufijo de versión.
- [ ] `CmdDetector` — `%SystemRoot%\System32\cmd.exe`. Siempre presente.
- [ ] `WslDetector` — reusa `WslService.ParseListVerbose` ya existente. Filtra `docker-desktop*`/`rancher-desktop*`.
- [ ] `GitBashDetector` — busca `bash.exe` en `%ProgramFiles%\Git\bin`, `%LOCALAPPDATA%\Programs\Git\bin`, registry `HKLM\SOFTWARE\GitForWindows\InstallPath`.
- [ ] `VsDeveloperDetector` — para cada VS detectado vía `vswhere.exe` (en `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\`), produce 2 perfiles: Developer Command Prompt (`cmd.exe /k "...\VsDevCmd.bat"`) y Developer PowerShell (`pwsh.exe -NoExit -Command "...\Launch-VsDevShell.ps1"`).
- [ ] `MsysCygwinDetector` (opcional, fase futura) — detecta MSYS2/Cygwin si `bash.exe` está en `C:\msys64\usr\bin\` o `C:\cygwin64\bin\`.

### 20.B — Modelo de datos persistido

Extender `AppSettings`:
- [ ] `List<UserProfile> UserProfiles` — perfiles editables por el usuario (auto-detectados + custom). Cada uno con: `Id` (Guid string), `Name`, `CommandLine`, `StartingDirectory?`, `IconGlyph?`, `Source` (enum: `Detected` / `Custom`), `Hidden` (bool — usuario lo ocultó del menú sin borrarlo).
- [ ] `string? DefaultProfileId` — Guid del perfil default elegido por el usuario. Null = heurística (pwsh > powershell > cmd).

**No** hay campo de migración desde wt. Si `UserProfiles` está vacío, se llena ejecutando los detectores (no leyendo wt).

### 20.C — UI nueva en SettingsWindow → tab "Perfiles"

- [ ] DataGrid editable: columnas Default (radio), Visible (checkbox), Icono, Nombre, Commandline, Cwd, Origen (Detected/Custom). Eliminar fila con tecla Delete.
- [ ] Botón **Detectar terminales** → corre todos los detectores, hace diff contra `UserProfiles`, agrega los nuevos. Toast "N nuevos perfiles detectados" o "Sin cambios".
- [ ] Botón **Añadir manual** → diálogo con Name + CommandLine + Cwd opcional + IconGlyph opcional. Source = Custom.
- [ ] Botón **Restablecer defaults** → borra `UserProfiles` y `DefaultProfileId`, fuerza re-detección al cerrar.
- [ ] El radio "Default" actualiza `DefaultProfileId` y desmarca el resto.

**No** hay botón "Importar de Windows Terminal".

### 20.D — Refactor del código actual

- [ ] **Eliminar** `WtProfileSource.cs` por completo y todos sus tests (`WtProfileSourceTests.cs`, `WtProfileSourceAuditTests.cs`).
- [ ] **Eliminar** `MainWindow.StartWatchingWtSettings` y el `FileSystemWatcher` sobre el `settings.json` de wt.
- [ ] Nueva clase `ProfileRegistry` con: `LoadAll(AppSettings) → IReadOnlyList<TerminalProfile>` — devuelve `UserProfiles` filtrados por `Hidden=false`. Si `UserProfiles` vacío: ejecuta detectores, persiste y devuelve.
- [ ] `MainWindow.LoadProfiles` usa `ProfileRegistry.LoadAll` en lugar de `WtProfileSource.LoadCombined`.
- [ ] `MainWindow.DefaultProfile()` respeta `_settings.DefaultProfileId`; fallback a heurística (primer pwsh > primer powershell > primer cmd > primer perfil de la lista).
- [ ] El menú dropdown ⌄ deja de mostrar entradas "(no soportado)" — `Hidden=true` se filtra antes de renderizar; los detectores nunca generan perfiles no resolubles.

### 20.E — Migración para usuarios actuales

Al primer arranque post-upgrade, si el usuario ya tiene `settings.json` con perfiles wt heredados pero NO tiene `UserProfiles` (porque es una versión nueva sin ese campo), simplemente se ejecutan los detectores y se le presenta la lista nueva. **No hay backfill desde wt** — si el usuario tenía perfiles custom configurados en wt, deberá agregarlos manualmente en la nueva UI. Esto es aceptable porque los perfiles wt típicos (PowerShell, cmd, WSL distros, Git Bash) los redescubre la app sin intervención.

### 20.F — TDD

- [ ] Cada detector tiene su test (`PwshDetectorTests`, etc.) con un filesystem fake o `where.exe` mockeado por path lookup.
- [ ] `ProfileRegistry` tests: load vacío → detecta + persiste; load con datos → respeta el orden y los `Hidden`; default fallback heurístico.
- [ ] UI tab "Perfiles" — tests del builder de items (similar a `TerminalContextMenuBuilder`): orden, default radio mutuamente excluyente, hide checkbox.
- [ ] Test de regresión: `grep` sobre la solución entera no encuentra `WtProfileSource`, `Microsoft.WindowsTerminal_8wekyb3d8bbwe`, ni lectura del `settings.json` de wt en runtime.

### Limitaciones aceptadas
- No respetamos colorScheme/font/icon custom de wt — WinKuake tiene los suyos vía `ProfileIconHelper`.
- No detectamos shells exóticos (nushell, xonsh, fish nativo) — el usuario los agrega como Custom.
- Usuarios que dependían de perfiles wt custom (ej. SSH a server X con flags raros) deben recrearlos en la UI nueva. La fase 21 backlog "SSH integrado" cubre el caso SSH específicamente.

## Fase 21 — Backlog
- Sincronizar settings vía GitHub Gist.
- Soporte de SSH/PuTTY integrado (perfil con `ssh user@host`).
- Animación entre cambios de tab.
- Tamaño relativo de splitters persistido (no solo estructura).
- Aplicar atajos custom en runtime (ahora solo se guardan).
- UpdateService: notificación in-app cuando hay versión nueva + descargador.
- **Tray icon — mejoras de UX**:
  - **Notificaciones balloon** vía `NotifyIcon.ShowBalloonTip`: "WinKuake started", "Update available (v0.2.0)", "Profile detected: Ubuntu". Throttle para no spam.
  - **Indicador de estado broadcast** en el ícono mismo (overlay rojo/cyan o cambio de glyph cuando `Ctrl+Shift+B` activa broadcast en algún `TerminalControl`). Implementación: generar un segundo .ico con badge y swappear `_icon.Icon` cuando `BroadcastChanged` se dispare.
  - **Click izquierdo simple = toggle** (hoy solo doble-click). El comportamiento Windows estándar de NotifyIcon no expone single-click directo; hacer override con `MouseClick` filtrando por `MouseButtons.Left` y debounce para distinguir de doble-click.
- Fixes de gaps detectados en Fase 17 (ver arriba): Azure filter, watcher Preview/unpackaged, slider fuente >32, AbbreviateCwd para `\\wsl$\…`, BuildWorkspacesMenu hot-reload.

### Diseño de persistencia de árbol de splits

Cada `PersistedTab` necesita un campo `Layout` opcional que describa el árbol de splits dentro de esa tab:

```csharp
public class PersistedSplitNode
{
    // Si Orientation != null → branch (First/Second populated, ProfileGuid/Cwd ignorados).
    // Si Orientation == null → leaf (ProfileGuid/Cwd populated, First/Second ignorados).
    public string? Orientation { get; set; } // "Vertical" o "Horizontal"
    public PersistedSplitNode? First { get; set; }
    public PersistedSplitNode? Second { get; set; }

    // Datos del leaf:
    public string? ProfileGuid { get; set; }
    public string? ProfileName { get; set; }
    public string? Cwd { get; set; }
}
```

`PersistedTab.Layout` reemplaza la noción "una sola sesión, un solo perfil": ahora el `Layout` puede ser un árbol con perfiles distintos en cada hoja. El campo `ProfileGuid/Cwd` del nivel `PersistedTab` queda como fallback para sesiones sin splits o para mostrar el ícono/label de la tab.

Plan de integración:
1. `TerminalPane` recuerda `TerminalProfile? OriginProfile` (asignada al crear).
2. `TerminalControl.SerializeLayout()` recorre el árbol de `Border.Child` y produce `PersistedSplitNode`.
3. `TerminalControl.RestoreLayout(PersistedSplitNode)` reconstruye el árbol creando panes con el profile/cwd correcto.
4. `MainWindow.SnapshotCurrentSessions` incluye `Layout`.
5. `MainWindow.RestoreSessionOrCreateDefault` usa `Layout` si existe; si no, comportamiento actual.

Limitaciones aceptables v1:
- El tamaño relativo del splitter no se persiste (siempre vuelve al 50/50).
- El cwd por pane se captura del OSC 7 más reciente.

## Fase 14 — Próximo backlog
- Sincronizar settings vía GitHub Gist.
- Soporte de SSH/PuTTY integrado.
- Animación entre cambios de tab.
- Tamaño relativo de splitters persistido.

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
