# Draws the SelfControl icon (a white brain on black) and writes src/SelfControl.App/Assets/SelfControl.ico
# with 16..256 px PNG frames, plus a 256 px preview PNG next to it.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path -Parent $PSScriptRoot
$assets = Join-Path $root 'src\SelfControl.App\Assets'
New-Item -ItemType Directory -Force $assets | Out-Null

function Draw-Brain([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.ScaleTransform($size / 256.0, $size / 256.0)
    # black rounded square
    $bg = New-Object System.Drawing.Drawing2D.GraphicsPath
    $r = 56
    $bg.AddArc(0, 0, $r, $r, 180, 90); $bg.AddArc(256 - $r, 0, $r, $r, 270, 90)
    $bg.AddArc(256 - $r, 256 - $r, $r, $r, 0, 90); $bg.AddArc(0, 256 - $r, $r, $r, 90, 90); $bg.CloseFigure()
    $g.FillPath([System.Drawing.Brushes]::Black, $bg)

    $white = [System.Drawing.Brushes]::White
    $black = [System.Drawing.Brushes]::Black
    # lobes of the left hemisphere (centre x, centre y, radius); the right one is mirrored
    $lobes = @(@(98, 78, 36), @(64, 110, 34), @(62, 152, 32), @(88, 184, 32), @(118, 70, 30), @(110, 160, 40), @(100, 120, 40))
    $t = 13  # outline thickness
    foreach ($pass in 0, 1) {
        foreach ($l in $lobes) {
            foreach ($x in $l[0], (256 - $l[0])) {
                $rad = $l[2] - ($pass * $t)
                $brush = if ($pass -eq 0) { $white } else { $black }
                $g.FillEllipse($brush, $x - $rad, $l[1] - $rad, 2 * $rad, 2 * $rad)
            }
        }
    }
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White), $t
    $pen.StartCap = 'Round'; $pen.EndCap = 'Round'
    # the groove between the hemispheres
    $g.DrawLine($pen, 128, 48, 128, 208)
    # folds
    foreach ($s in 1, -1) {
        $m = { param($x) if ($s -eq 1) { $x } else { 256 - $x } }
        $g.DrawBezier($pen, (& $m 66), 112, (& $m 84), 96, (& $m 102), 110, (& $m 98), 128)
        $g.DrawBezier($pen, (& $m 76), 162, (& $m 92), 146, (& $m 112), 150, (& $m 108), 172)
        $g.DrawBezier($pen, (& $m 96), 76, (& $m 104), 92, (& $m 114), 94, (& $m 116), 104)
    }
    $g.Dispose()
    return $bmp
}

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = foreach ($s in $sizes) {
    $b = Draw-Brain $s
    $ms = New-Object System.IO.MemoryStream
    $b.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    if ($s -eq 256) { $b.Save((Join-Path $assets 'SelfControl.png'), [System.Drawing.Imaging.ImageFormat]::Png) }
    $b.Dispose()
    , $ms.ToArray()
}

# ICO container with PNG frames
$out = New-Object System.IO.MemoryStream
$w = New-Object System.IO.BinaryWriter $out
$w.Write([uint16]0); $w.Write([uint16]1); $w.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $frames[$i]
    $w.Write([byte]($s % 256)); $w.Write([byte]($s % 256)); $w.Write([byte]0); $w.Write([byte]0)
    $w.Write([uint16]1); $w.Write([uint16]32); $w.Write([uint32]$d.Length); $w.Write([uint32]$offset)
    $offset += $d.Length
}
foreach ($d in $frames) { $w.Write($d) }
[System.IO.File]::WriteAllBytes((Join-Path $assets 'SelfControl.ico'), $out.ToArray())
"wrote $assets\SelfControl.ico"
