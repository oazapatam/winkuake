# WinKuake

Terminal drop-down estilo **[Yakuake](https://apps.kde.org/es/yakuake/)** (KDE) para Windows.
Pulsa **F12**, baja el terminal desde la parte superior; pulsa F12 otra vez y se oculta.

Aplicación nativa **WPF / .NET 10** que **embebe Windows Terminal** como motor — análogo a cómo
Yakuake embebe Konsole. Hereda gratis: tabs, splits horizontal/vertical, perfiles
PowerShell · WSL · CMD · Git Bash, render DirectX, copy/paste, búsqueda, hyperlinks.

---

## Características

- 🎯 **Drop-down animado** desde el tope, con easing.
- ⌨️ **Hotkey global F12** (configurable: Ctrl/Alt/Shift/Win + cualquier tecla).
- 📑 **Múltiples pestañas** (Ctrl+Shift+T o botón "+ Pestaña").
- ➗ **Split vertical y horizontal** (Alt+Shift++ / Alt+Shift+− o botones de la barra).
- 🐧 **Perfiles**: detecta automáticamente PowerShell 7, Windows PowerShell, CMD, Git Bash,
   Azure Cloud Shell y todas las distros **WSL** instaladas.
- 🎚️ **Configuración** del % de pantalla (alto, ancho), opacidad, animación, auto-ocultar al perder foco.
- 🚀 **Iniciar con Windows** (opcional, sin admin).
- 📦 **Instalador profesional** generado con Inno Setup.

## Atajos

| Atajo                 | Acción                                       |
|-----------------------|----------------------------------------------|
| **F12**               | Mostrar / ocultar WinKuake                   |
| Ctrl+Shift+T          | Nueva pestaña                                |
| Ctrl+Tab / Ctrl+Shift+Tab | Pestaña siguiente / anterior            |
| Alt+Shift+`+`         | Dividir panel verticalmente                  |
| Alt+Shift+`-`         | Dividir panel horizontalmente                |
| Alt+Flechas           | Mover foco entre paneles                     |
| Ctrl+Shift+W          | Cerrar panel / pestaña                       |
| Ctrl+`+` / Ctrl+`-`   | Zoom in / out                                |

## Requisitos

- Windows 10 1809 o superior (recomendado **Windows 11**).
- **Windows Terminal** instalado (preinstalado en Win 11; si no, gratis en
   [Microsoft Store](https://aka.ms/terminal)).
- **.NET 10 Desktop Runtime** — solo si compilas sin `--self-contained`.
   El instalador empaqueta la versión self-contained y no requiere instalación previa.

## Compilar desde código

### Lo que necesitas instalar

1. **Visual Studio 2022 Community** (gratis): https://visualstudio.microsoft.com/es/vs/community/
   - En el instalador marca el workload **"Desarrollo de escritorio de .NET"** (esto incluye
      el SDK de .NET 10 y todo lo necesario para WPF).
2. **Inno Setup 6** (para generar el instalador): https://jrsoftware.org/isinfo.php

Eso es todo.

### Pasos

```powershell
# 1. Clonar / abrir el proyecto
git clone <url> winkuake
cd winkuake

# 2. Restaurar y compilar
dotnet build src\WinKuake\WinKuake.csproj -c Release

# 3. Ejecutar en debug (o abrir WinKuake.sln en VS y F5)
dotnet run --project src\WinKuake\WinKuake.csproj
```

### Generar el instalador

```powershell
# 1. Publicar binario self-contained (no requiere .NET en el equipo destino)
dotnet publish src\WinKuake\WinKuake.csproj -c Release -r win-x64 `
    --self-contained true -o publish

# 2. Compilar el instalador con Inno Setup
iscc installer\WinKuake.iss

# Salida: installer\Output\WinKuake-Setup-0.1.0.exe
```

## Configuración

`%AppData%\WinKuake\settings.json`:

```json
{
  "hotkeyModifiers": [],
  "hotkeyKey": "F12",
  "heightRatio": 0.5,
  "widthRatio": 1.0,
  "opacity": 0.97,
  "defaultProfile": "",
  "autoHideOnFocusLost": false,
  "startWithWindows": false,
  "animationMs": 180,
  "monitorIndex": 0
}
```

Editable también desde el icono ⚙ en la barra superior de la ventana.

## Arquitectura técnica

Ver [`ARCHITECTURE.md`](ARCHITECTURE.md). Resumen:

- WPF borderless window + animación slide en `Window.Top`.
- `RegisterHotKey` para hotkey global (vía `HwndSource` hook a `WM_HOTKEY`).
- `HwndHost` con un Win32 "static" como contenedor, dentro del cual hacemos
   `SetParent` al HWND de **Windows Terminal**, localizado por
   `EnumWindows` filtrando `CASCADIA_HOSTING_WINDOW_CLASS` + título único.
- Pestañas y splits delegados a `wt.exe --window winkuake-host <action>`.

## Roadmap

- [ ] Soporte multi-monitor con selección de monitor activo.
- [ ] Skins / temas personalizables.
- [ ] Modo "always on top" toggle desde la chrome.
- [ ] Notificaciones de bell.
- [ ] Atajo "mostrar en monitor del cursor".

## Licencia

MIT — ver [LICENSE](LICENSE) (pendiente de añadir).
