<#
.SYNOPSIS
    Arma el .zip del traductor listo para entregar: compila, limpia y empaqueta.

.DESCRIPTION
    Deja un .zip con tres archivos —Traductor.exe, config.json y LEEME.txt— que
    es todo lo que necesita una maquina Windows: el .exe es autocontenido, asi
    que en destino NO hay que instalar .NET.

    La URL del Guest se inyecta en config.json al empaquetar, no se compila
    dentro del .exe: para otro casino se vuelve a correr este script con otra
    -GuestUrl, sin tocar codigo.

.EXAMPLE
    .\empaquetar.ps1 -GuestUrl "https://guest.instalotto.com"
    Paquete de gabinete: al abrir el .exe, Chrome arranca en kiosko.

.EXAMPLE
    .\empaquetar.ps1 -GuestUrl "https://staging.instalotto.com" -SinKiosko
    Paquete para que un companero pruebe en su laptop: ventana normal de Chrome,
    se puede cerrar y usar DevTools.
#>
[CmdletBinding()]
param(
    # Direccion del Guest que abre el traductor al arrancar.
    [Parameter(Mandatory = $true)]
    [string]$GuestUrl,

    # Ventana normal en vez de pantalla completa sin salida. Para probar.
    [switch]$SinKiosko,

    # Donde dejar el .zip.
    [string]$Destino = "$HOME\Desktop"
)

$ErrorActionPreference = 'Stop'
$raiz = $PSScriptRoot
$proyecto = Join-Path $raiz 'Traductor\Traductor.csproj'

# dotnet no siempre esta en el PATH de la sesion; se busca donde vive.
$dotnet = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnet) { $dotnet = 'C:\Program Files\dotnet\dotnet.exe' }
if (-not (Test-Path $dotnet)) { throw "No se encontro dotnet. Instala el SDK de .NET 8." }

$staging = Join-Path ([System.IO.Path]::GetTempPath()) "traductor-paquete-$(Get-Random)"
$zip = Join-Path $Destino 'Traductor.zip'

Write-Host "1/4  Compilando..." -ForegroundColor Cyan
& $dotnet publish $proyecto -c Release -r win-x64 -o $staging | Out-Null
if ($LASTEXITCODE -ne 0) { throw "Fallo la compilacion." }

Write-Host "2/4  Quitando lo que no se entrega..." -ForegroundColor Cyan
# El .pdb son simbolos de depuracion (no sirven en destino) y el .log es de
# las corridas de esta maquina: no tiene por que viajar al usuario final.
Get-ChildItem $staging -Include *.pdb, *.log, *.log.old -Recurse | Remove-Item -Force

Write-Host "3/4  Escribiendo config.json con la URL..." -ForegroundColor Cyan
$config = Get-Content (Join-Path $raiz 'Traductor\config.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$config.guestUrl = $GuestUrl
$config.kiosk = -not $SinKiosko.IsPresent
$config | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $staging 'config.json') -Encoding UTF8

$modo = if ($SinKiosko) { 'ventana normal de Chrome' } else { 'Chrome en kiosko (pantalla completa, sin salida)' }
@"
TRADUCTOR DEL GABINETE
======================

COMO USARLO
  1. Extrae el .zip COMPLETO en una carpeta con permiso de escritura,
     por ejemplo C:\Traductor
     (NO lo abras desde dentro del .zip, y NO lo pongas en Archivos de programa).
  2. Dentro veras muchos archivos: es normal, el programa los necesita todos
     juntos. Busca Traductor.exe y dale doble clic.
  3. Se abre solo: $modo
     apuntando a:  $GuestUrl

  El traductor NO muestra ventana propia: es normal no ver nada aparte del navegador.
  No hace falta instalar nada mas (ni .NET ni ningun otro requisito).

COMO CERRARLO
  Cierra el navegador (Alt+F4 si esta en pantalla completa).
  El traductor se apaga solo junto con el.

SI WINDOWS AVISA
  "Windows protegio tu PC"  ->  Mas informacion  ->  Ejecutar de todas formas.
  El programa no esta firmado todavia; ese aviso es esperado y se puede pasar.

SI ALGO NO FUNCIONA
  Junto a Traductor.exe aparece traductor.log. Abrelo y mandalo: ahi esta que paso.
"@ | Set-Content (Join-Path $staging 'LEEME.txt') -Encoding UTF8

Write-Host "4/4  Comprimiendo..." -ForegroundColor Cyan
if (-not (Test-Path $Destino)) { New-Item -ItemType Directory -Path $Destino -Force | Out-Null }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -Force
Remove-Item $staging -Recurse -Force

$mb = [math]::Round((Get-Item $zip).Length / 1MB, 1)
Write-Host ""
Write-Host "Listo: $zip  ($mb MB)" -ForegroundColor Green
Write-Host "Contenido: Traductor.exe + config.json + LEEME.txt"
Write-Host "Modo: $modo"
