param(
    [string]$OutputDir = "$PSScriptRoot\..\src\iBackup.App\Assets"
)

Add-Type -AssemblyName System.Drawing

function New-MinimalistIcon([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    [float]$scale = [float]$size / 512.0

    # 1. Background Squircle
    [float]$pad = 24.0 * $scale
    [float]$sqSize = [float]$size - (2.0 * $pad)
    [float]$radius = 110.0 * $scale

    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $rect = New-Object System.Drawing.RectangleF($pad, $pad, $sqSize, $sqSize)
    [float]$dia = 2.0 * $radius

    $path.AddArc($rect.X, $rect.Y, $dia, $dia, 180, 90)
    $path.AddArc($rect.Right - $dia, $rect.Y, $dia, $dia, 270, 90)
    $path.AddArc($rect.Right - $dia, $rect.Bottom - $dia, $dia, $dia, 0, 90)
    $path.AddArc($rect.X, $rect.Bottom - $dia, $dia, $dia, 90, 90)
    $path.CloseFigure()

    # Gradient background: Deep slate obsidian (#0F172A to #1E293B)
    $cTop = [System.Drawing.Color]::FromArgb(255, 15, 23, 42)
    $cBottom = [System.Drawing.Color]::FromArgb(255, 30, 41, 59)
    $pt1 = New-Object System.Drawing.PointF(0.0, $pad)
    $pt2 = New-Object System.Drawing.PointF(0.0, $rect.Bottom)
    $brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush($pt1, $pt2, $cTop, $cBottom)
    $g.FillPath($brush, $path)

    # Squircle border: 1.5px subtle stroke
    [float]$borderWidth = [Math]::Max(1.0, 2.0 * $scale)
    $borderPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(120, 148, 163, 184), $borderWidth)
    $g.DrawPath($borderPen, $path)

    # 2. Storage Vault Disc / Cylinder (Right side)
    [float]$discX = 230.0 * $scale
    [float]$discY = 220.0 * $scale
    [float]$discW = 190.0 * $scale
    [float]$discH = 70.0 * $scale
    [float]$discDepth = 110.0 * $scale

    $discBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 30, 58, 88))
    $discLightBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 41, 75, 112))
    [float]$discPenW = [Math]::Max(1.0, 4.0 * $scale)
    $discPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 56, 189, 248), $discPenW)
    [float]$discSubtleW = [Math]::Max(1.0, 2.0 * $scale)
    $discPenSubtle = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(140, 56, 189, 248), $discSubtleW)

    # Lower cylinder curve
    $g.FillEllipse($discBrush, $discX, ($discY + $discDepth), $discW, $discH)
    $g.DrawEllipse($discPenSubtle, $discX, ($discY + $discDepth), $discW, $discH)
    $g.FillRectangle($discBrush, $discX, ($discY + ($discH / 2.0)), $discW, $discDepth)
    $g.DrawLine($discPenSubtle, $discX, ($discY + ($discH / 2.0)), $discX, ($discY + $discDepth + ($discH / 2.0)))
    $g.DrawLine($discPenSubtle, ($discX + $discW), ($discY + ($discH / 2.0)), ($discX + $discW), ($discY + $discDepth + ($discH / 2.0)))

    # Middle tier groove
    [float]$midY = $discY + ($discDepth * 0.5)
    $g.DrawArc($discPenSubtle, $discX, $midY, $discW, $discH, 0.0, 180.0)

    # Top disk surface
    $g.FillEllipse($discLightBrush, $discX, $discY, $discW, $discH)
    $g.DrawEllipse($discPen, $discX, $discY, $discW, $discH)

    # Platter spindle center
    [float]$spinW = 50.0 * $scale
    [float]$spinH = 20.0 * $scale
    [float]$spinX = $discX + (($discW - $spinW) / 2.0)
    [float]$spinY = $discY + (($discH - $spinH) / 2.0)
    $spindleBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 15, 23, 42))
    $g.FillEllipse($spindleBrush, $spinX, $spinY, $spinW, $spinH)
    $g.DrawEllipse($discPenSubtle, $spinX, $spinY, $spinW, $spinH)

    # Storage LED indicator dot
    [float]$ledX = $discX + $discW - (32.0 * $scale)
    [float]$ledY = $discY + $discDepth + (30.0 * $scale)
    [float]$ledSize = 12.0 * $scale
    $ledBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 52, 211, 153))
    $g.FillEllipse($ledBrush, $ledX, $ledY, $ledSize, $ledSize)

    # 3. iPhone Body (Left side, overlapping)
    [float]$phoneX = 96.0 * $scale
    [float]$phoneY = 120.0 * $scale
    [float]$phoneW = 150.0 * $scale
    [float]$phoneH = 270.0 * $scale
    [float]$phoneR = 24.0 * $scale

    $phonePath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $pRect = New-Object System.Drawing.RectangleF($phoneX, $phoneY, $phoneW, $phoneH)
    [float]$pDia = 2.0 * $phoneR
    $phonePath.AddArc($pRect.X, $pRect.Y, $pDia, $pDia, 180, 90)
    $phonePath.AddArc($pRect.Right - $pDia, $pRect.Y, $pDia, $pDia, 270, 90)
    $phonePath.AddArc($pRect.Right - $pDia, $pRect.Bottom - $pDia, $pDia, $pDia, 0, 90)
    $phonePath.AddArc($pRect.X, $pRect.Bottom - $pDia, $pDia, $pDia, 90, 90)
    $phonePath.CloseFigure()

    # Phone screen
    [float]$pRight = $phoneX + $phoneW
    [float]$pBottom = $phoneY + $phoneH
    $ptPhone1 = New-Object System.Drawing.PointF($phoneX, $phoneY)
    $ptPhone2 = New-Object System.Drawing.PointF($pRight, $pBottom)
    $phoneScreenBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $ptPhone1,
        $ptPhone2,
        [System.Drawing.Color]::FromArgb(240, 15, 23, 42),
        [System.Drawing.Color]::FromArgb(245, 14, 116, 144)
    )
    $g.FillPath($phoneScreenBrush, $phonePath)

    # Phone border stroke: Crisp electric cyan / white
    [float]$phonePenW = [Math]::Max(1.0, 5.0 * $scale)
    $phonePen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 241, 245, 249), $phonePenW)
    $g.DrawPath($phonePen, $phonePath)

    # Dynamic Island / Speaker notch
    [float]$notchW = 44.0 * $scale
    [float]$notchH = 10.0 * $scale
    [float]$notchX = $phoneX + (($phoneW - $notchW) / 2.0)
    [float]$notchY = $phoneY + (14.0 * $scale)
    $notchPath = New-Object System.Drawing.Drawing2D.GraphicsPath
    $notchPath.AddArc($notchX, $notchY, $notchH, $notchH, 90, 180)
    $notchPath.AddArc(($notchX + $notchW - $notchH), $notchY, $notchH, $notchH, 270, 180)
    $notchPath.CloseFigure()
    $notchBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 241, 245, 249))
    $g.FillPath($notchBrush, $notchPath)

    # Media icon in phone center
    [float]$photoCenterX = $phoneX + ($phoneW / 2.0)
    [float]$photoCenterY = $phoneY + ($phoneH / 2.0) - (15.0 * $scale)
    [float]$petalRadius = 22.0 * $scale
    $petalBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180, 56, 189, 248))
    $g.FillEllipse($petalBrush, ($photoCenterX - $petalRadius), ($photoCenterY - $petalRadius), ($petalRadius * 2.0), ($petalRadius * 2.0))

    # Home indicator bar
    [float]$barW = 54.0 * $scale
    [float]$barH = 4.0 * $scale
    [float]$barX = $phoneX + (($phoneW - $barW) / 2.0)
    [float]$barY = $phoneY + $phoneH - (18.0 * $scale)
    $barBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220, 241, 245, 249))
    $g.FillRectangle($barBrush, $barX, $barY, $barW, $barH)

    # 4. Sync Arcs
    [float]$arcW = [Math]::Max(1.0, 6.0 * $scale)
    $arcPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(255, 56, 189, 248), $arcW)
    $arcPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $arcPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($arcPen, ($phoneX + $phoneW - (25.0 * $scale)), (150.0 * $scale), (150.0 * $scale), (130.0 * $scale), -65.0, 85.0)

    [float]$arcW2 = [Math]::Max(1.0, 3.5 * $scale)
    $arcPen2 = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(180, 244, 244, 245), $arcW2)
    $arcPen2.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $arcPen2.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawArc($arcPen2, ($phoneX + $phoneW - (15.0 * $scale)), (165.0 * $scale), (125.0 * $scale), (105.0 * $scale), -65.0, 80.0)

    # Minimalist arrow pointing to storage
    [float]$arrowX = $discX + (45.0 * $scale)
    [float]$arrowY = $discY - (2.0 * $scale)
    $ptA = New-Object System.Drawing.PointF($arrowX, ($arrowY - (12.0 * $scale)))
    $ptB = New-Object System.Drawing.PointF(($arrowX + (18.0 * $scale)), ($arrowY + (2.0 * $scale)))
    $ptC = New-Object System.Drawing.PointF($arrowX, ($arrowY + (16.0 * $scale)))
    [System.Drawing.PointF[]]$arrowPoints = @($ptA, $ptB, $ptC)
    $arrowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255, 56, 189, 248))
    $g.FillPolygon($arrowBrush, $arrowPoints)

    $g.Dispose()
    return $bmp
}

# Ensure output directory exists
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Write-Host "Generating Modern Minimalist 512x512 PNG..."
$bmp512 = New-MinimalistIcon 512
$pngPath = Join-Path $OutputDir "iBackup-icon.png"
$bmp512.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved: $pngPath"

Write-Host "Generating Multi-Resolution Windows ICO (16, 24, 32, 48, 64, 128, 256)..."
$sizes = @(16, 24, 32, 48, 64, 128, 256)
$images = @()

foreach ($s in $sizes) {
    $images += New-MinimalistIcon $s
}

$icoPath = Join-Path $OutputDir "iBackup.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$images.Count)

$headerSize = 6
$dirEntrySize = 16
$offset = $headerSize + ($dirEntrySize * $images.Count)

$pngStreams = @()
foreach ($img in $images) {
    $ms = New-Object System.IO.MemoryStream
    $img.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $pngStreams += $ms
}

for ($i = 0; $i -lt $images.Count; $i++) {
    $img = $images[$i]
    $ms = $pngStreams[$i]
    $w = if ($img.Width -ge 256) { 0 } else { [byte]$img.Width }
    $h = if ($img.Height -ge 256) { 0 } else { [byte]$img.Height }
    $bw.Write([byte]$w)
    $bw.Write([byte]$h)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$ms.Length)
    $bw.Write([uint32]$offset)

    $offset += $ms.Length
}

for ($i = 0; $i -lt $pngStreams.Count; $i++) {
    $bytes = $pngStreams[$i].ToArray()
    $bw.Write($bytes)
}

$bw.Flush()
$bw.Close()
$fs.Close()

Write-Host "Saved: $icoPath"

$bmp512.Dispose()
foreach ($img in $images) { $img.Dispose() }
foreach ($ms in $pngStreams) { $ms.Dispose() }
