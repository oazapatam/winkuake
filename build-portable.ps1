<#
.SYNOPSIS
  Publica WinKuake como un solo .exe portable (self-contained, sin necesidad
  de .NET 10 instalado ni elevación). El .exe resultante puede copiarse
  a cualquier carpeta y ejecutarse al doble-click.

.DESCRIPTION
  Diferencias vs el publish que consume el instalador (CLAUDE.md):
    - SelfContained=true: empaqueta el runtime de .NET dentro del .exe.
    - PublishSingleFile=true: un solo .exe en vez de carpeta con DLLs.
    - IncludeAllContentForSelfExtract=true: terminal.html / xterm.js /
      addons se incrustan en el .exe y se extraen a una carpeta temporal
      al iniciar; AppContext.BaseDirectory apunta a esa carpeta, así
      ResolveResourcesDir() sigue funcionando sin cambios.
    - PublishTrimmed=false: WPF + reflection no son compatibles con trim
      por defecto; arriesgar trim suele romper temas/recursos.

  Settings, log y cache de WebView2 siguen en %AppData%\WinKuake\ y
  %LocalAppData%\WinKuake\WebView2\ — esto NO es "true portable" (los
  datos no viajan con el .exe), pero el binario sí lo es: cero install,
  cero permisos elevados, todo per-usuario.

  Auto-start sigue siendo decisión del usuario en Settings (escribe
  HKCU\Run, no requiere admin).

.PARAMETER OutDir
  Carpeta destino. Default: publish-portable\ en la raíz del repo.

.PARAMETER Configuration
  Debug | Release. Default: Release.
#>
[CmdletBinding()]
param(
    [string]$OutDir = "publish-portable",
    [ValidateSet("Debug","Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$csproj = Join-Path $repoRoot "src\WinKuake\WinKuake.csproj"
$absOut = if ([System.IO.Path]::IsPathRooted($OutDir)) { $OutDir } else { Join-Path $repoRoot $OutDir }

if (Test-Path $absOut) {
    Write-Host "Limpiando $absOut..."
    Remove-Item -Recurse -Force $absOut
}

Write-Host "Publicando portable ($Configuration) → $absOut"
dotnet publish $csproj `
    -c $Configuration `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeAllContentForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:PublishTrimmed=false `
    -p:PublishReadyToRun=true `
    -o $absOut

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish falló (exit $LASTEXITCODE)."
    exit $LASTEXITCODE
}

$exe = Join-Path $absOut "WinKuake.exe"
if (-not (Test-Path $exe)) {
    Write-Error "No se generó $exe."
    exit 1
}

$sizeMB = [Math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Listo: $exe ($sizeMB MB)" -ForegroundColor Green
Write-Host "Cópialo a cualquier carpeta y doble-click. Sin install, sin admin."
