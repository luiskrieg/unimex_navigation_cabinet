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
