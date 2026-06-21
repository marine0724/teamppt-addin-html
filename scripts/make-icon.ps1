# Generate TEAMPPT ribbon icon (64x64 PNG, transparent rounded square). ASCII only.
param([string]$Out = 'c:\Projects\teamppt-addin\src\TeampptAddin\Assets\teamppt-icon.png')

Add-Type -AssemblyName System.Drawing

$size = 64
$bmp = New-Object System.Drawing.Bitmap $size, $size
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
$g.Clear([System.Drawing.Color]::Transparent)

# rounded rectangle path
$inset = 3
$rect = New-Object System.Drawing.Rectangle $inset, $inset, ($size - 2*$inset), ($size - 2*$inset)
$radius = 15
$d = $radius * 2
$path = New-Object System.Drawing.Drawing2D.GraphicsPath
$path.AddArc($rect.X, $rect.Y, $d, $d, 180, 90)
$path.AddArc($rect.Right - $d, $rect.Y, $d, $d, 270, 90)
$path.AddArc($rect.Right - $d, $rect.Bottom - $d, $d, $d, 0, 90)
$path.AddArc($rect.X, $rect.Bottom - $d, $d, $d, 90, 90)
$path.CloseFigure()

# indigo vertical gradient fill
$c1 = [System.Drawing.Color]::FromArgb(255, 129, 140, 248)  # 818CF8
$c2 = [System.Drawing.Color]::FromArgb(255, 67, 56, 202)    # 4338CA
$brush = New-Object System.Drawing.Drawing2D.LinearGradientBrush $rect, $c1, $c2, 90
$g.FillPath($brush, $path)

# subtle top highlight stroke
$penHi = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(60, 255, 255, 255)), 1
$g.DrawPath($penHi, $path)

# stacked text TEAM / PPT
$white = [System.Drawing.Brushes]::White
$fontTeam = New-Object System.Drawing.Font 'Segoe UI', 13, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$fontPpt  = New-Object System.Drawing.Font 'Segoe UI', 17, ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
$fmt = New-Object System.Drawing.StringFormat
$fmt.Alignment = [System.Drawing.StringAlignment]::Center
$fmt.LineAlignment = [System.Drawing.StringAlignment]::Center

$rTeam = New-Object System.Drawing.RectangleF 0, 13, $size, 20
$rPpt  = New-Object System.Drawing.RectangleF 0, 34, $size, 22
$g.DrawString('TEAM', $fontTeam, $white, $rTeam, $fmt)
$g.DrawString('PPT',  $fontPpt,  $white, $rPpt,  $fmt)

$g.Dispose()
$dir = Split-Path $Out -Parent
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force $dir | Out-Null }
$bmp.Save($Out, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()
Write-Output ("Saved: " + $Out)
