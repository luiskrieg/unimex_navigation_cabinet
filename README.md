# Gabinete — capa nativa para operar el juego

Repositorio independiente del Guest FE. Contiene la parte de la épica
[#411](../guest/issues/gabinete/411.md) que no vive en el navegador:

- **`traductor/`** — el programa nativo de Windows de [#414](../guest/issues/gabinete/414.md): sin
  ventana, convierte flechas/Enter/espacio en movimiento y clic reales del cursor mientras el Guest
  avisa que hay un juego abierto.
- **`kiosko/`** — la guía reproducible de [#412](../guest/issues/gabinete/412.md) (configuración de
  Chrome en modo kiosko). No es código: es lo que sigue una persona del equipo al llegar un gabinete
  nuevo.

El plan completo de esta épica, con el reparto de riesgos y el orden de validación, está en
`acabamos-de-hacer-una-breezy-cake.md` (carpeta de planes de Claude Code) — lo relevante de ese plan
se repite aquí donde hace falta para no depender de él.

## Por qué es un repo aparte

Cada gabinete físico necesita su propia instalación local del traductor — no es un servicio
centralizado: mover el cursor real de una pantalla solo lo puede hacer un proceso corriendo en la
sesión de Windows de esa misma pantalla. El Guest FE (Angular, este mismo repo hermano `guest/`) se
sigue desplegando exactamente igual que hoy; el traductor se distribuye e instala aparte, máquina por
máquina.

## Compilar el traductor

Requiere el SDK de .NET 8 y, para probar de verdad (hook de teclado + `SendInput`), **Windows** — no
compila con sentido en otro sistema porque depende de `user32.dll`.

```powershell
cd traductor
dotnet publish Traductor/Traductor.csproj -c Release -r win-x64
# el .exe autocontenido queda en Traductor/bin/Release/net8.0-windows/win-x64/publish/
```

`config.json` viaja junto al `.exe` (se copia automáticamente al publicar) y se puede editar ahí
mismo sin recompilar — puerto del canal con el Guest, mapa de teclas y velocidad del cursor
(#414 CA10).

## Primera prueba en tu Windows (paso a paso)

El traductor **no muestra ventana ni consola** (así lo pide #414 CA1), así que para tu primera
corrida vas a depender del archivo `traductor.log` que escribe junto al `.exe` — sin eso estarías
probando a ciegas.

1. **Instalar el SDK de .NET 8** en la máquina Windows (el instalador oficial de Microsoft, "download
   .NET 8 SDK").
2. **Compilar y publicar:**
   ```powershell
   cd gabinete\traductor
   dotnet publish Traductor\Traductor.csproj -c Release
   ```
   El `.exe` autocontenido queda en
   `Traductor\Traductor\bin\Release\net8.0-windows\win-x64\publish\Traductor.exe`, junto con
   `config.json`.
3. **Correrlo suelto, sin kiosko ni Guest todavía** — doble clic en `Traductor.exe` (o desde una
   terminal). No vas a ver nada en pantalla: es la conducta esperada. Confirma que arrancó bien
   revisando **`traductor.log`** en esa misma carpeta — debe decir algo como:
   ```
   Traductor arrancando. Puerto WS: 8765. DpiScale: 1.
   Hook de teclado instalado correctamente.
   Servidor WebSocket escuchando en ws://127.0.0.1:8765/
   ```
   Si en vez de "instalado correctamente" ves un `ERROR instalando el hook de teclado`, es
   exactamente el riesgo **R-Gab4** del plan (antivirus/permisos bloqueando el hook global) —
   revisa Windows Defender / el antivirus de esa máquina antes de seguir.
4. **Confirmar que sigue corriendo:** Administrador de tareas → pestaña Detalles → buscar
   `Traductor.exe`. Para confirmar que escucha el puerto: `netstat -ano | findstr 8765` en una
   terminal (debe aparecer `LISTENING`).
5. **R-Gab1 — probar la conexión desde el Guest**, en un Chrome normal (sin kiosko todavía, para
   poder usar la consola de DevTools): abrir el Guest de staging, abrir la consola y ejecutar
   ```js
   const ws = new WebSocket('ws://127.0.0.1:8765');
   ws.onopen = () => console.log('conectó');
   ws.onerror = (e) => console.log('bloqueado / error', e);
   ```
   Si conecta, revisa `traductor.log`: debe aparecer `Guest conectado por WebSocket.`. Si Chrome lo
   bloquea (Private Network Access u otra política), aquí es donde se ve.
6. **Probar el circuito completo**, ya con `CabinetService` del Guest: con el traductor corriendo,
   abrir el Guest de staging normal (sin ningún parámetro en la URL), iniciar sesión y abrir cualquier
   juego. Al montarse la pantalla de juego, `traductor.log` debe registrar `modo_juego: ON
   (rect=...)`. Con eso activo, las flechas del teclado normal (simulando la botonera) deberían mover
   el **cursor real** de Windows en vez del puntero propio del Guest. No hace falta ningún parámetro ni
   configuración: el Guest se conecta solo apenas detecta el traductor en `127.0.0.1` (ver
   `cabinet.service.ts`).

**A propósito, no vayas directo al modo kiosko de `kiosko/GUIA-INSTALACION.md` para esta primera
prueba** — el kiosko bloquea DevTools, la barra de direcciones y la salida de pantalla completa, que
son justo las herramientas que necesitas para ver qué está pasando. Esa guía es el **último** paso,
una vez que los puntos 1–6 ya funcionan en una ventana normal de Chrome.

## Empaquetar y distribuir a un casino (una vez validado)

El traductor se distribuye como un `.zip` con el contenido de la carpeta `publish/` (el `.exe`
autocontenido — no necesita instalar el runtime de .NET en la máquina destino — más `config.json` y,
cuando exista, `cursor-gabinete.cur`). Ese `.zip` es **idéntico para cualquier cliente**: no sabe nada
de proveedores, juegos ni URLs.

Lo único que cambia por cliente/casino es el acceso directo de Chrome en modo kiosko
(`kiosko/GUIA-INSTALACION.md` §1): la URL del Guest de ese casino, sin ningún parámetro especial. El
traductor no redirige a nada ni conoce esa URL — solo escucha en `127.0.0.1` a la espera de que el
Guest (cualquier página del Guest que Chrome abra en esa misma máquina) se conecte solo.

**Nota importante:** esto significa que en un mismo casino conviven sin ningún flag ni configuración
los jugadores web normales (celular, laptop, sin traductor) y los gabinetes físicos (con traductor) —
cada sesión de navegador decide por sí sola, según si logra conectarse a su propio `127.0.0.1`, no
según ninguna configuración del casino en la base de datos.

## Orden de validación (sin gabinete físico todavía)

Ver el plan completo para el detalle de cada riesgo. En resumen, **en este orden**:

1. **R-Gab1 — bloqueante, primero.** ¿Chrome deja conectar un WebSocket a `ws://127.0.0.1:PUERTO`
   desde una página HTTPS (staging del Guest), o lo bloquea Private Network Access? Se prueba desde
   la consola del navegador antes de escribir nada más del traductor.
2. **R-Gab2.** Publicar el traductor, correrlo en Windows, y confirmar que el hook de teclado +
   `SendInput` sí mueven el cursor real y hacen clic real sobre un juego de producción — es
   literalmente automatizar lo que ya se probó a mano con "Mouse Keys" de accesibilidad.
3. Con los dos anteriores en verde: construir `cabinet.service.ts` en el Guest (issue #413) contra
   este mismo traductor, simulando la botonera con un teclado normal.
4. Cuando llegue el gabinete físico de Kenosoft: checkpoint de #412 (kiosko) y de la alineación de
   coordenadas (R-Gab3, el `dpiScale` de `config.json`) sobre la máquina real.

## Qué falta decidir con Kenosoft / el negocio

Ver §7 del plan de la épica — resumen: el mapa de teclas real que emite el encoder de la botonera, el
asset visual del cursor (`cursor-gabinete.cur`, hoy no existe en el repo), y si el `.exe` se firma
para distribuirlo a casinos de terceros.
