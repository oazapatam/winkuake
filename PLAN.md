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

## Fase 3 — Multi-tab (sesión siguiente)
- Pool de `ConPtyService` + lista de `TerminalControl`.
- Cambio rápido de tab activa (Ctrl+Tab, click).
- `+` crea tab nueva con perfil activo, `⌄` con selector.
- `✕` por tab funciona; al cerrar última → abre default, no app exit.

## Fase 4 — UX adicional (sesión siguiente)
- Settings window con tabs (Apariencia/Atajos/Comportamiento/Perfiles).
- Tema seleccionable (Dracula/Nord/Gruvbox).
- Hotkey configurable.
- Status bar con info: cwd actual, shell activo, encoding.
- Detector de "actualizar perfiles wt" en caliente.

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
