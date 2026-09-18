<#
.SYNOPSIS
    Genera el .ico multi-resolucion que usa el ejecutable, a partir del isotipo.

.DESCRIPTION
    El .ico que entrega diseno suele traer una sola imagen grande y no siempre
    cuadrada. Windows necesita varias resoluciones: usa la de 16x16 en el
    Administrador de tareas y la de 256 en vista de iconos grandes. Si solo hay
    una, la reduce el mismo y se ve borrosa; si no es cuadrada, la estira.

    Este script centra el arte en un lienzo cuadrado y exporta 16/24/32/48/64/
    128/256 en un solo .ico. No toca el archivo original.

.EXAMPLE
    .\generar-icono.ps1
    Regenera assets\AIC-Isotipo-app.ico desde assets\AIC-Isotipo.ico
#>
[CmdletBinding()]
param(
    [string]$Origen  = (Join-Path $PSScriptRoot '..\assets\AIC-Isotipo.ico'),
    [string]$Destino = (Join-Path $PSScriptRoot '..\assets\AIC-Isotipo-app.ico')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

# Se lee a memoria y no con FromFile para no dejar el archivo bloqueado.
$ms = New-Object IO.MemoryStream(,[IO.File]::ReadAllBytes($Origen))
$src = [System.Drawing.Image]::FromStream($ms)
Write-Host "Origen: $($src.Width)x$($src.Height)"

$lado = [Math]::Max($src.Width, $src.Height)
$cuadrado = New-Object System.Drawing.Bitmap($lado, $lado)
$g = [System.Drawing.Graphics]::FromImage($cuadrado)
$g.Clear([System.Drawing.Color]::Transparent)
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.DrawImage($src, [int](($lado - $src.Width)/2), [int](($lado - $src.Height)/2), $src.Width, $src.Height)
$g.Dispose()

$imgs = @()
foreach ($s in @(16,24,32,48,64,128,256)) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $gg = [System.Drawing.Graphics]::FromImage($bmp)
    $gg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $gg.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $gg.PixelOffsetMode   = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $gg.DrawImage($cuadrado, 0, 0, $s, $s)
    $gg.Dispose()
    $pms = New-Object IO.MemoryStream
    $bmp.Save($pms, [System.Drawing.Imaging.ImageFormat]::Png)
    $imgs += ,@{ size = $s; data = $pms.ToArray() }
    $bmp.Dispose(); $pms.Dispose()
}

# Formato ICO: cabecera de 6 bytes, luego 16 bytes por imagen, luego los datos.
$out = New-Object IO.MemoryStream
$bw  = New-Object IO.BinaryWriter($out)
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$imgs.Count)
$offset = 6 + (16 * $imgs.Count)
foreach ($i in $imgs) {
    $d = if ($i.size -ge 256) { [byte]0 } else { [byte]$i.size }   # 0 significa 256
    $bw.Write($d); $bw.Write($d); $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$i.data.Length); $bw.Write([uint32]$offset)
    $offset += $i.data.Length
}
foreach ($i in $imgs) { $bw.Write($i.data) }
$bw.Flush()
[IO.File]::WriteAllBytes($Destino, $out.ToArray())
$bw.Dispose(); $out.Dispose(); $src.Dispose(); $ms.Dispose(); $cuadrado.Dispose()

# Los parentesis son necesarios: suelto, PowerShell lee el -f de formato como
# abreviatura del parametro -ForegroundColor de Write-Host y falla.
Write-Host ("Listo: $Destino ({0:N0} KB, {1} resoluciones)" -f ((Get-Item $Destino).Length/1KB), $imgs.Count) -ForegroundColor Green
Write-Host "Recompila para que el .exe lo tome." 
