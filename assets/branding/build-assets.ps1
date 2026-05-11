# build-assets.ps1
# Renderiza el logo de WinKuake (definido como geometría inline) a PNG en
# múltiples tamaños, los empaqueta en un .ico multi-resolución y produce
# los BMPs que Inno Setup requiere para el wizard del instalador.
#
# Uso: pwsh assets/branding/build-assets.ps1
# Salida: assets/branding/dist/{winkuake.ico, winkuake-256.png, ..., wizard-image.bmp, wizard-small.bmp}

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$here = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $here 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

# Paleta (debe coincidir con logo.svg).
$colorBgDark   = [System.Drawing.Color]::FromArgb(0xFF, 0x0E, 0x11, 0x16)
$colorBgLight  = [System.Drawing.Color]::FromArgb(0xFF, 0x1B, 0x20, 0x27)
$colorAccent   = [System.Drawing.Color]::FromArgb(0xFF, 0x00, 0xC8, 0xFF)
$colorAccent2  = [System.Drawing.Color]::FromArgb(0xFF, 0x00, 0x99, 0xCC)
$colorTextHi   = [System.Drawing.Color]::FromArgb(0xFF, 0xE8, 0xED, 0xF2)
$colorTextLo   = [System.Drawing.Color]::FromArgb(0xFF, 0x8A, 0x93, 0xA0)

# Renderiza el motif "caret + underscore" en un cuadrado redondeado.
function Render-Logo {
    param(
        [int] $size,
        [bool] $transparentBackground = $false
    )
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

    if ($transparentBackground) {
        $g.Clear([System.Drawing.Color]::Transparent)
    } else {
        $g.Clear($colorBgDark)
    }

    # Cuadrado redondeado de fondo (escalado al tamaño solicitado).
    $padding = [int]($size * 0.04)
    $rectSize = $size - 2 * $padding
    $radius   = [int]($size * 0.18)

    $rect = New-Object System.Drawing.Rectangle $padding, $padding, $rectSize, $rectSize
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($rect.X,                          $rect.Y,                           $radius * 2, $radius * 2, 180, 90)
    $path.AddArc($rect.Right - $radius * 2,        $rect.Y,                           $radius * 2, $radius * 2, 270, 90)
    $path.AddArc($rect.Right - $radius * 2,        $rect.Bottom - $radius * 2,        $radius * 2, $radius * 2,   0, 90)
    $path.AddArc($rect.X,                          $rect.Bottom - $radius * 2,        $radius * 2, $radius * 2,  90, 90)
    $path.CloseFigure()

    # Fondo con gradiente diagonal suave.
    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point $rect.X, $rect.Y),
        (New-Object System.Drawing.Point $rect.Right, $rect.Bottom),
        $colorBgLight, $colorBgDark)
    $g.FillPath($bgBrush, $path)
    $bgBrush.Dispose()

    # Borde tenue cyan.
    $borderPen = New-Object System.Drawing.Pen($colorAccent2, [single]([Math]::Max(1, $size * 0.008)))
    $borderPen.Color = [System.Drawing.Color]::FromArgb(40, 0x00, 0x99, 0xCC) # ~16% alpha
    $g.DrawPath($borderPen, $path)
    $borderPen.Dispose()

    # Caret ❯ (chevron).
    $caretPen = New-Object System.Drawing.Pen($colorAccent, [single]([Math]::Max(1, $size * 0.085)))
    $caretPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $caretPen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $caretPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round

    $cx1 = $size * 0.305
    $cx2 = $size * 0.555
    $cy1 = $size * 0.328
    $cy2 = $size * 0.500
    $cy3 = $size * 0.672
    $g.DrawLine($caretPen, [single]$cx1, [single]$cy1, [single]$cx2, [single]$cy2)
    $g.DrawLine($caretPen, [single]$cx2, [single]$cy2, [single]$cx1, [single]$cy3)
    $caretPen.Dispose()

    # Underscore _ del prompt.
    $underBrush = New-Object System.Drawing.SolidBrush($colorAccent)
    $ux = $size * 0.578
    $uy = $size * 0.648
    $uw = $size * 0.172
    $uh = $size * 0.055
    $g.FillRectangle($underBrush, [single]$ux, [single]$uy, [single]$uw, [single]$uh)
    $underBrush.Dispose()

    # Línea baseline tenue.
    $baseBrush = New-Object System.Drawing.SolidBrush(
        [System.Drawing.Color]::FromArgb(64, 0x00, 0xC8, 0xFF))
    $bx = $size * 0.219
    $by = $size * 0.781
    $bw = $size * 0.563
    $bh = [Math]::Max(1, $size * 0.008)
    $g.FillRectangle($baseBrush, [single]$bx, [single]$by, [single]$bw, [single]$bh)
    $baseBrush.Dispose()

    $g.Dispose()
    $path.Dispose()
    return $bmp
}

# Genera PNGs en los tamaños comunes.
$sizes = @(16, 24, 32, 48, 64, 128, 256, 512)
foreach ($s in $sizes) {
    $png = Render-Logo -size $s -transparentBackground $true
    $out = Join-Path $dist "winkuake-$s.png"
    $png.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
    $png.Dispose()
    Write-Host "  wrote $out"
}

# Empaqueta a .ico multi-resolución.
function Save-Ico {
    param([string] $path, [int[]] $iconSizes)

    $bitmaps = @()
    foreach ($s in $iconSizes) { $bitmaps += (Render-Logo -size $s -transparentBackground $true) }

    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter $ms

    # ICONDIR
    $bw.Write([uint16]0)                 # Reserved
    $bw.Write([uint16]1)                 # Type = 1 (icon)
    $bw.Write([uint16]$bitmaps.Count)    # Count

    # Cada entrada de directorio + datos PNG embebidos (Vista+).
    $entries = @()
    $offset  = 6 + 16 * $bitmaps.Count
    foreach ($bmp in $bitmaps) {
        $stream = New-Object System.IO.MemoryStream
        $bmp.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $bytes = $stream.ToArray()
        $stream.Dispose()
        $entries += @{ size = $bmp.Width; data = $bytes; offset = $offset }
        $offset += $bytes.Length
    }

    foreach ($e in $entries) {
        $w = if ($e.size -ge 256) { 0 } else { $e.size }
        $h = $w
        $bw.Write([byte]$w)              # Width  (0 = 256)
        $bw.Write([byte]$h)              # Height (0 = 256)
        $bw.Write([byte]0)               # Palette
        $bw.Write([byte]0)               # Reserved
        $bw.Write([uint16]1)             # Color planes
        $bw.Write([uint16]32)            # Bits per pixel
        $bw.Write([uint32]$e.data.Length)
        $bw.Write([uint32]$e.offset)
    }
    foreach ($e in $entries) { $bw.Write($e.data) }

    [System.IO.File]::WriteAllBytes($path, $ms.ToArray())
    $bw.Dispose()
    $ms.Dispose()
    foreach ($b in $bitmaps) { $b.Dispose() }
}

$icoPath = Join-Path $dist 'winkuake.ico'
Save-Ico -path $icoPath -iconSizes @(16, 24, 32, 48, 64, 128, 256)
Write-Host "  wrote $icoPath"

# BMPs del instalador Inno Setup (24-bit, exactamente 164x314 y 55x58).
function Render-WizardImage {
    param([int] $width, [int] $height, [bool] $small)
    $bmp = New-Object System.Drawing.Bitmap $width, $height
    $g   = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    $bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.Point 0, 0),
        (New-Object System.Drawing.Point $width, $height),
        $colorBgLight, $colorBgDark)
    $g.FillRectangle($bgBrush, 0, 0, $width, $height)
    $bgBrush.Dispose()

    if ($small) {
        # 55x58: solo el glyph, ocupando casi todo.
        $logo = Render-Logo -size ([Math]::Min($width, $height)) -transparentBackground $true
        $x = ($width  - $logo.Width)  / 2
        $y = ($height - $logo.Height) / 2
        $g.DrawImage($logo, [single]$x, [single]$y)
        $logo.Dispose()
    } else {
        # 164x314: glyph arriba centrado + wordmark abajo.
        $logoSize = [int]($width * 0.6)
        $logo = Render-Logo -size $logoSize -transparentBackground $true
        $lx = ($width - $logoSize) / 2
        $ly = $height * 0.15
        $g.DrawImage($logo, [single]$lx, [single]$ly)
        $logo.Dispose()

        # Wordmark.
        $font = New-Object System.Drawing.Font('Segoe UI', 14, [System.Drawing.FontStyle]::Bold)
        $brushHi = New-Object System.Drawing.SolidBrush($colorTextHi)
        $sf = New-Object System.Drawing.StringFormat
        $sf.Alignment     = [System.Drawing.StringAlignment]::Center
        $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
        $rectText = New-Object System.Drawing.RectangleF 0, ($height * 0.65), $width, 30
        $g.DrawString('WinKuake', $font, $brushHi, $rectText, $sf)
        $font.Dispose()
        $brushHi.Dispose()

        $fontSub = New-Object System.Drawing.Font('Segoe UI', 7)
        $brushLo = New-Object System.Drawing.SolidBrush($colorTextLo)
        $rectSub = New-Object System.Drawing.RectangleF 0, ($height * 0.72), $width, 30
        $g.DrawString('DROP-DOWN TERMINAL', $fontSub, $brushLo, $rectSub, $sf)
        $fontSub.Dispose()
        $brushLo.Dispose()
        $sf.Dispose()
    }

    $g.Dispose()
    return $bmp
}

# Inno Setup wizard images: BMP de 24bpp.
function Save-Bmp24 {
    param([System.Drawing.Bitmap] $bmp, [string] $path)
    # Convertir a 24bpp explícitamente. PowerShell exige el enum como literal.
    $pixfmt = [System.Drawing.Imaging.PixelFormat]'Format24bppRgb'
    $converted = New-Object System.Drawing.Bitmap $bmp.Width, $bmp.Height, $pixfmt
    $gc = [System.Drawing.Graphics]::FromImage($converted)
    $gc.Clear($colorBgDark)
    $gc.DrawImage($bmp, 0, 0)
    $gc.Dispose()
    $converted.Save($path, [System.Drawing.Imaging.ImageFormat]::Bmp)
    $converted.Dispose()
}

$wiz = Render-WizardImage -width 164 -height 314 -small $false
Save-Bmp24 -bmp $wiz -path (Join-Path $dist 'wizard-image.bmp')
$wiz.Dispose()
Write-Host "  wrote $(Join-Path $dist 'wizard-image.bmp')"

$wizSmall = Render-WizardImage -width 55 -height 58 -small $true
Save-Bmp24 -bmp $wizSmall -path (Join-Path $dist 'wizard-small.bmp')
$wizSmall.Dispose()
Write-Host "  wrote $(Join-Path $dist 'wizard-small.bmp')"

Write-Host ''
Write-Host 'Assets generated under:'
Write-Host "  $dist"
