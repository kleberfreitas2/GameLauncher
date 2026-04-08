Add-Type -AssemblyName System.Drawing

function New-RoundedPath($x, $y, $w, $h, $r) {
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($x, $y, $r*2, $r*2, 180, 90)
    $path.AddArc($x+$w-$r*2, $y, $r*2, $r*2, 270, 90)
    $path.AddArc($x+$w-$r*2, $y+$h-$r*2, $r*2, $r*2, 0, 90)
    $path.AddArc($x, $y+$h-$r*2, $r*2, $r*2, 90, 90)
    $path.CloseAllFigures()
    return $path
}

$s = 256
$bmp = New-Object System.Drawing.Bitmap $s, $s
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$g.Clear([System.Drawing.Color]::Transparent)

# --- Background rounded square ---
$bgPath  = New-RoundedPath 4 4 248 248 44
$bgBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,13,13,30))
$g.FillPath($bgBrush, $bgPath)

# Glow border (3 layers)
foreach ($w in @(8,4,2)) {
    $a = switch ($w) { 8 { 50 } 4 { 110 } 2 { 200 } }
    $p = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb($a,124,77,255)), $w
    $g.DrawPath($p, $bgPath)
    $p.Dispose()
}

# --- Controller body ---
$ctrlPath  = New-RoundedPath 42 82 172 90 20
$ctrlBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,28,28,62))
$g.FillPath($ctrlBrush, $ctrlPath)
$ctrlPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160,124,77,255)), 2
$g.DrawPath($ctrlPen, $ctrlPath)

# D-pad
$dp = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(220,160,140,255))
$g.FillRectangle($dp, 65, 122, 36, 10)
$g.FillRectangle($dp, 80, 107, 10, 36)
# D-pad arrows (tiny triangles)
$arrowBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,220,200,255))
$g.FillPolygon($arrowBrush, @([System.Drawing.PointF]::new(69,127),[System.Drawing.PointF]::new(75,122),[System.Drawing.PointF]::new(75,132)))
$g.FillPolygon($arrowBrush, @([System.Drawing.PointF]::new(97,127),[System.Drawing.PointF]::new(91,122),[System.Drawing.PointF]::new(91,132)))

# Buttons ABXY
$btnColors = @(
    [System.Drawing.Color]::FromArgb(220,255,70,70),
    [System.Drawing.Color]::FromArgb(220,70,210,70),
    [System.Drawing.Color]::FromArgb(220,70,140,255),
    [System.Drawing.Color]::FromArgb(220,255,210,50)
)
$bx = @(183,171,159,171); $by = @(121,109,121,133)
for ($i=0; $i -lt 4; $i++) {
    $b = New-Object System.Drawing.SolidBrush($btnColors[$i])
    $g.FillEllipse($b, $bx[$i]-8, $by[$i]-8, 16, 16)
    $b.Dispose()
}

# Center start button (glowing)
$cBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(200,124,77,255))
$g.FillEllipse($cBrush, 110, 110, 36, 36)
# Play triangle inside
$tri = @(
    [System.Drawing.PointF]::new(118,119),
    [System.Drawing.PointF]::new(118,141),
    [System.Drawing.PointF]::new(140,130)
)
$g.FillPolygon([System.Drawing.Brushes]::White, $tri)

# Analog sticks
$stBr  = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(255,44,44,88))
$stPen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(160,124,77,255)), 1.5
$g.FillEllipse($stBr,  72,144,24,24); $g.DrawEllipse($stPen,  72,144,24,24)
$g.FillEllipse($stBr, 160,144,24,24); $g.DrawEllipse($stPen, 160,144,24,24)
# Stick dots
$dotBr = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(180,124,77,255))
$g.FillEllipse($dotBr,  80,151,10,10)
$g.FillEllipse($dotBr, 168,151,10,10)

# --- "EXP." text with glow ---
$font = New-Object System.Drawing.Font("Segoe UI", 34, [System.Drawing.FontStyle]::Bold)
$sf   = New-Object System.Drawing.StringFormat
$sf.Alignment     = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$tr = New-Object System.Drawing.RectangleF 0, 182, 256, 60

# Glow layers
foreach ($gi in @(4,3,2,1)) {
    $ga = $gi * 35
    $gb = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb($ga,124,77,255))
    $gr = New-Object System.Drawing.RectangleF (-$gi),(182-$gi),(256+$gi*2),(60+$gi*2)
    $g.DrawString("EXP.", $font, $gb, $gr, $sf)
    $gb.Dispose()
}
$g.DrawString("EXP.", $font, [System.Drawing.Brushes]::White, $tr, $sf)

# Small "GAME LAUNCHER" subtitle
$subFont = New-Object System.Drawing.Font("Segoe UI", 9, [System.Drawing.FontStyle]::Regular)
$subBr   = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(160,180,160,255))
$subRect = New-Object System.Drawing.RectangleF 0, 218, 256, 22
$g.DrawString("GAME LAUNCHER", $subFont, $subBr, $subRect, $sf)

$g.Dispose()

# Save PNG
$assetsDir = "C:\Users\mcd_s\source\repos\DashGaming\GameLauncher\Assets"
New-Item -ItemType Directory -Force -Path $assetsDir | Out-Null
$pngPath = "$assetsDir\icon.png"
$bmp.Save($pngPath, [System.Drawing.Imaging.ImageFormat]::Png)

# Convert PNG -> ICO (PNG-in-ICO, Windows Vista+)
$icoPath = "$assetsDir\icon.ico"
$ms = New-Object System.IO.MemoryStream
$bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
$imgBytes = $ms.ToArray()
$ms.Dispose()

$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter($fs)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]1)
$bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0); $bw.Write([byte]0)
$bw.Write([uint16]1); $bw.Write([uint16]32)
$bw.Write([uint32]$imgBytes.Length)
$bw.Write([uint32]22)
$bw.Write($imgBytes)
$bw.Close(); $fs.Close()

$bmp.Dispose()
Write-Host "✅ Icone gerado: $icoPath"
