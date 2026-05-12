# Instrucciones para Claude Code — proyecto WinKuake

> Este archivo contiene **reglas operativas** para los agentes Claude que trabajan
> en este repo. Para arquitectura técnica ver [`ARCHITECTURE.md`](ARCHITECTURE.md);
> para uso final ver [`README.md`](README.md).

---

## Idioma y comunicación

- **Toda comunicación con el usuario es en español.**
- **Comentarios en código y XMLDoc:** en español, breves, solo cuando el "por qué" no es obvio. No comentar el "qué" — los nombres ya lo explican.
- **Mensajes de UI:** español (audiencia principal).
- **Identificadores en código:** inglés (PascalCase / camelCase estándar de C#).

## Convenciones de código

- **Lenguaje:** C# 12 (file-scoped namespaces, primary constructors, records, `nullable enable`).
- **Framework:** .NET 10 LTS, WPF. **No** introducir WinForms, WinUI 3, MAUI, ni Electron.
- **Indentación:** 4 espacios. Llaves en línea nueva (estilo Allman / por defecto de VS).
- **Async:** sufijo `Async` y aceptar `CancellationToken` cuando aplique.
- **Logging:** `WinKuake.Services.CrashLogger.Log(ex)` para errores no fatales. **No** usar `Console.WriteLine` (la app es `WinExe`, no hay consola).
- **P/Invoke:** todo Win32 vive en [src/WinKuake/Native/NativeMethods.cs](src/WinKuake/Native/NativeMethods.cs). No esparcir `[DllImport]` por el código de servicios excepto cuando el método sea exclusivo de un único host (caso `TerminalHost.CreateWindowEx`).
- **Settings:** cualquier opción nueva persistible va en [src/WinKuake/Models/AppSettings.cs](src/WinKuake/Models/AppSettings.cs) con un valor por defecto sensato. Migrar settings.json es responsabilidad del autor del cambio.

## Restricciones del proyecto

- **No reimplementar el motor de terminal.** WinKuake embebe `wt.exe` por diseño. Si una feature necesita ConPTY directo, discútelo con el usuario antes — probablemente exista una alternativa vía `wt --window winkuake-host <action>`.
- **No tocar ramas / releases sin pedir.** Este es un proyecto personal del usuario; commits, tags y publicaciones requieren confirmación explícita.
- **No instalar dependencias NuGet adicionales** sin pedir aprobación. La meta es que el ejecutable sea pequeño y self-contained.
- **No añadir telemetría, analytics, ni llamadas de red.** WinKuake es 100% local.

## Comandos clave

```powershell
# Restaurar y compilar (Debug)
dotnet build src\WinKuake\WinKuake.csproj

# Ejecutar
dotnet run --project src\WinKuake\WinKuake.csproj

# Publicar self-contained (lo que consume el instalador)
dotnet publish src\WinKuake\WinKuake.csproj -c Release -r win-x64 `
    --self-contained true -o publish

# Construir el instalador (requiere Inno Setup 6 instalado)
iscc installer\WinKuake.iss

# Construir el .exe portable (un solo binario, sin install ni elevación;
# settings/log/WebView2 cache siguen en %AppData%\WinKuake\ y
# %LocalAppData%\WinKuake\WebView2\ — el .exe es portable, los datos no).
.\build-portable.ps1
```

## Test-Driven Development (regla del proyecto)

**Todo cambio sigue TDD cuando es testeable:**

1. **Escribe el test primero** (`tests/WinKuake.Tests/*.cs`, xUnit). Debe fallar.
2. **Implementa lo mínimo** para hacerlo pasar.
3. **Refactoriza** si hace falta, con los tests cubriendo.
4. **Corre `dotnet test`** antes de dar un cambio por terminado.

**Qué se testea unitariamente (obligatorio TDD):**
- Servicios (`WtProfileSource`, `SettingsService`, `ProfileIconHelper`, parsing de cualquier tipo).
- Modelos / records (`TerminalProfile`, `TabItem`, `AppSettings`).
- Lógica de negocio pura (resolución de commandlines, mapeos de perfiles, etc.).

**Qué se valida manualmente (no se puede unit-testar):**
- WebView2 / xterm.js render.
- ConPTY P/Invoke real (lanza procesos del SO).
- Animaciones WPF, hotkey global, COM activation.
- Interacciones de UI (clicks, foco, atajos).

Para esos casos: documentar el resultado esperado en el PR/respuesta y, si la regresión es probable, dejar un test de smoke (lanzar proceso real, esperar output, etc.) marcado como `[Trait("Category","integration")]`.

**No se acepta** "luego pongo el test" — el test va con el cambio, no después.

---

## Desarrollo en paralelo con agentes (regla del proyecto)

**Cuando hay features independientes pendientes, paralelízalas con sub-agentes en worktrees aislados** (`isolation: "worktree"` en el Agent tool). Reglas:

1. **Agrupa features con archivos disjuntos** en cada agente. Si dos features tocan el mismo archivo, secuencia o asigna ambas al mismo agente.
2. **Prompt explícito** por agente: qué crear, qué modificar, **qué NO tocar**, restricciones (TDD, build limpio, comentarios en español).
3. **Lanza todos en un mismo turno** (`Agent` tool con `run_in_background: true`) para que corran a la vez.
4. **Mientras corren**, trabaja en master en algo NO conflictivo (típicamente la feature más invasiva o documentación).
5. **Al terminar**, los archivos del worktree quedan sin commitear; cópialos a master con `cp` y resuelve manualmente los conflictos de archivos que más de un agente toca. Build + `dotnet test` antes del commit final.
6. **Un solo commit** describiendo qué hizo cada agente, no uno por agente — más fácil de leer en `git log`.

Cuando un cambio NO se beneficia de paralelismo (rápido / muy acoplado a una sola zona), hazlo secuencial. El paralelismo es una herramienta, no un fin.

---

La verificación visual / integración manual va abajo en "Validación manual".

## Validación manual antes de marcar tarea completada

Cuando hagas un cambio que afecte la UX, valida en ejecución (F5 desde VS):

- [ ] **F12** muestra la ventana con animación slide desde el tope.
- [ ] **F12 de nuevo** la oculta con animación inversa.
- [ ] Primer F12 lanza Windows Terminal y queda embebido sin chrome propio.
- [ ] Botón **＋ Pestaña** abre nueva pestaña dentro de la misma ventana embebida.
- [ ] Botones **⬌ Vertical** / **⬍ Horizontal** dividen el panel actual.
- [ ] Cambiar **opacidad / alto / ancho** desde Configuración aplica al instante.
- [ ] Cambiar el **hotkey** en Configuración funciona sin reiniciar la app.

Si no puedes ejecutar la app (no tienes el SDK, etc.), **dilo explícitamente** en la respuesta — no afirmes que funciona sin haberlo probado.

## Cosas que NO hacer

- **No** ejecutar `git push`, `git commit --amend`, `git reset --hard`, o cualquier operación destructiva sin que el usuario lo pida en esa misma conversación.
- **No** crear archivos `.md` adicionales (planning, notes, decisions, summary…) salvo que el usuario lo pida.
- **No** añadir comentarios narrativos del estilo "// Added by Claude" o "// TODO: refactor". Si algo es un TODO real, abrirlo como issue o consultar al usuario.
- **No** introducir abstracciones / interfaces / DI containers para "preparar el futuro". Tres líneas similares es mejor que una abstracción prematura.
- **No** añadir `try/catch` defensivo en código interno. Solo en límites del sistema (P/Invoke, IO, parsing externo).
- **No** mockear nada en pruebas (cuando existan): ejecutar contra el sistema real cuando se pueda.

## Cuándo preguntar al usuario

- Antes de cambiar la arquitectura central (cambiar el motor de terminal, cambiar de WPF a otra UI, etc.).
- Antes de añadir dependencias NuGet.
- Antes de cualquier acción de git / GitHub que vaya más allá de leer estado.
- Cuando la solicitud sea ambigua y haya 2+ caminos razonables con tradeoffs distintos.

## Recursos

- Yakuake (referencia de comportamiento): https://apps.kde.org/es/yakuake/
- Windows Terminal command-line args: https://learn.microsoft.com/en-us/windows/terminal/command-line-arguments
- Inno Setup docs: https://jrsoftware.org/ishelp/
