# Traductor — guía de uso y distribución

Para quien arma el paquete, lo entrega y lo soporta. Quien solo lo recibe tiene
suficiente con el `LEEME.txt` que viaja dentro del `.zip`.

El traductor convierte flechas / Enter / espacio en movimiento y clic **reales**
del cursor de Windows mientras el Guest avisa que hay un juego abierto. No tiene
ventana ni ícono: es un proceso en segundo plano. Ver `../README.md` para el
porqué de la arquitectura.

---

## 1. Armar el `.zip`

Desde esta carpeta, en PowerShell:

```powershell
# Paquete de gabinete: al abrir, Chrome arranca en kiosko
.\empaquetar.ps1 -GuestUrl "https://<dominio-del-guest>"

# Paquete para que alguien pruebe en su laptop: ventana normal, se puede cerrar
.\empaquetar.ps1 -GuestUrl "https://<dominio-del-guest>" -SinKiosko
```

Deja `Traductor.zip` en el Escritorio (~67 MB). Dentro hay unos 244 archivos —el
programa trae su propio runtime de .NET— de los cuales importan tres:

| Archivo | Para qué |
|---|---|
| `Traductor.exe` | El programa. Autocontenido: **en destino no hay que instalar .NET**. |
| `config.json` | Ajustes en texto plano. Se edita sin recompilar. |
| `LEEME.txt` | Instrucciones para quien lo recibe. |

Los otros ~241 son las DLL del runtime y tienen que viajar junto al `.exe`: se
extrae y se reparte **la carpeta completa**, no el `.exe` suelto.

La URL **no se compila dentro del `.exe`**: se inyecta en `config.json` al
empaquetar. Para otro casino se vuelve a correr el script con otra `-GuestUrl`,
sin tocar código.

> Requiere el SDK de .NET 8 en la máquina que empaqueta. Si `dotnet` no aparece
> en el PATH, el script lo busca solo en `C:\Program Files\dotnet`.

## 2. Instalar en la máquina destino

1. Extraer el `.zip` **completo** en una carpeta con permiso de escritura, p. ej.
   `C:\Traductor`. No sirve dentro de `Archivos de programa` (el log se escribe
   junto al `.exe`) ni abrirlo desde dentro del `.zip`.
2. Si el `.zip` llegó por descarga o correo: clic derecho → Propiedades →
   **Desbloquear**, antes de extraer.
3. Doble clic en `Traductor.exe`.

No aparece ninguna ventana del traductor — solo el navegador. Es lo esperado.

**Para cerrarlo:** cerrar el navegador (`Alt+F4` si está en kiosko). El traductor
se apaga solo junto con él. Si se abrió sin `guestUrl`, se cierra desde el
Administrador de tareas → Detalles → `Traductor.exe`.

## 3. Cuando Windows lo bloquea

El `.exe` **no está firmado** (decisión pendiente, ver `../README.md` §"Qué falta
decidir"), así que Windows va a decir algo. Hay dos avisos distintos:

| Mensaje | Qué es | Qué hacer |
|---|---|---|
| "Windows protegió tu PC" | SmartScreen: no reconoce el programa | **Más información** → **Ejecutar de todas formas**. Se pasa con un clic. |
| "Una directiva de Control de aplicaciones bloqueó este archivo" | **Smart App Control**: bloqueo duro, sin clic que lo salte | No debería pasar ya. Ver abajo. |

### Por qué este paquete no lo publicamos como un solo `.exe`

Medido en una máquina con Smart App Control activo y forzado:

| Cómo se publica | Tamaño | Resultado |
|---|---|---|
| Autocontenido **en un solo archivo** (`PublishSingleFile`) | 154 MB, 1 archivo | **Bloqueado**, 3 de 3 intentos |
| Autocontenido **en carpeta** (lo que usamos) | 144 MB, 244 archivos | Corre, 3 de 3 |
| Dependiente del runtime | 0.2 MB + .NET instalado aparte | Corre, 3 de 3 |

Los tres son el mismo código y **ninguno** está firmado, así que lo que Smart App
Control rechaza no es la falta de firma: es el patrón. Un binario grande y sin
firma que se autodescomprime en memoria al arrancar es exactamente lo que hace un
packer de malware, y el `.exe` de un solo archivo se ve así desde afuera.

Por eso `Traductor.csproj` **no** usa `PublishSingleFile`. Si alguien lo vuelve a
activar para tener "un solo archivo bonito", el paquete deja de correr en parte
del parque de máquinas Windows 11 — y el síntoma no dice nada de single-file.

### Dónde aplica Smart App Control

- **Windows 10: no existe.** Ahí solo aparece el aviso de SmartScreen, que se pasa
  con un clic.
- **Windows 11 22H2 en adelante:** solo viene encendido en **instalaciones
  limpias**. Una máquina que se actualizó desde Windows 10, o desde un Windows 11
  anterior, lo tiene apagado de forma permanente.

Para saber cómo está una máquina:

```powershell
(Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\CI\Policy").VerifiedAndReputablePolicyState
# 0 = apagado    1 = activo y forzado    2 = evaluación
```

Si alguna vez vuelve a aparecer el bloqueo duro pese a todo lo anterior, la única
salida buena es **firmar el ejecutable**. Apagar Smart App Control también
funciona, pero es irreversible —volver a encenderlo exige reinstalar Windows—, así
que no forma parte del procedimiento: es algo que cada quien decide para su propia
máquina.

### Probar sin publicar, durante el desarrollo

```powershell
dotnet build Traductor\Traductor.csproj -c Release --self-contained false -p:PublishSingleFile=false -o C:\temp\traductor
dotnet C:\temp\traductor\Traductor.dll
```

Corre a través de `dotnet.exe`, que sí está firmado por Microsoft. Útil para
iterar rápido sin esperar el publish completo de 144 MB.

## 4. Ajustes (`config.json`)

Se edita con el Bloc de notas, junto al `.exe`, y toma efecto al reiniciar el
programa. Si el archivo no existe, el traductor arranca con estos mismos valores
salvo `guestUrl`, que queda vacío.

| Clave | Por defecto | Qué hace |
|---|---|---|
| `guestUrl` | `""` | Dirección que abre al arrancar. Vacío = no abre nada, solo escucha. |
| `kiosk` | `true` | Pantalla completa sin salida. `false` = ventana normal. |
| `webSocketPort` | `8765` | Puerto del canal con el Guest, en `127.0.0.1`. |
| `keyMap` | flechas / Enter / Espacio | Teclas que emite la botonera. Admite alias por acción. |
| `cursor` | 16 / 7 / 9 | Velocidad inicial, aceleración y tope del cursor. |
| `dpiScale` | `1.0` | Ajuste si el gabinete tiene escalado de Windows distinto de 100 % (riesgo R-Gab3). |
| `logEnabled` | `true` | Apaga el log por completo. |
| `logMaxBytes` | `1048576` | Tope por archivo (1 MB). |

Cuando hay `guestUrl`, Chrome se abre con un **perfil dedicado** en
`%LOCALAPPDATA%\Traductor\chrome-perfil`, no con el perfil personal. Es a
propósito: sin eso, si la persona ya tiene Chrome abierto, Windows le entrega la
URL a esa ventana, `--kiosk` se ignora en silencio y el traductor cree que el
navegador se cerró. Implica que ese perfil arranca sin sesión iniciada la primera
vez.

## 5. Diagnóstico

Junto al `.exe` aparece `traductor.log`. **Está acotado a propósito:** al llegar a
`logMaxBytes` se recicla a `traductor.log.old` y empieza uno nuevo, así que el
traductor nunca ocupa en disco más del doble de ese tope, corra los meses que
corra. Un arranque sano se ve así:

```
Traductor arrancando. Puerto WS: 8765. DpiScale: 1.
Hook de teclado instalado correctamente.
Servidor WebSocket escuchando en ws://127.0.0.1:8765/
Guest abierto en https://... (kiosko: True).
Guest conectado por WebSocket.
```

| Lo que dice el log | Qué pasa |
|---|---|
| `ERROR instalando el hook de teclado` | Antivirus o permisos bloqueando el hook global (riesgo R-Gab4). Revisar Windows Defender en esa máquina. |
| `ERROR arrancando el servidor WebSocket` | Otra cosa ya ocupa el puerto. Cambiar `webSocketPort`. |
| Nunca aparece `Guest conectado` | El Guest no logró conectarse: Chrome lo bloqueó (Private Network Access) o el traductor arrancó después de la página. |
| `No se encontró Chrome` | Se abrió el navegador por defecto y sin kiosko. Instalar Chrome en esa máquina. |
| El archivo no existe | O `logEnabled` está en `false`, o la carpeta no tiene permiso de escritura (típico en `Archivos de programa`). |

## 6. Arranque automático con Windows

Para un gabinete, lo mínimo que funciona es un acceso directo a `Traductor.exe`
dentro de la carpeta que abre `shell:startup`. El comando exacto de `schtasks`
—Tarea Programada "al iniciar sesión", que es lo que corresponde— queda pendiente
de fijar con el primer gabinete físico: ver `../kiosko/GUIA-INSTALACION.md` §4.

Con `guestUrl` configurado, el traductor abre Chrome él mismo, así que esa única
tarea deja el gabinete completo arriba. Si en cambio se prefiere que Chrome lo
lance su propia tarea (el reparto que describe la guía de kiosko), hay que dejar
`guestUrl` vacío para que no se abran dos navegadores.
