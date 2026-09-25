# Navegación en el gabinete — del cursor libre a las posiciones mapeadas

> **Alcance:** repo `unimex/gabinete` (traductor nativo, C#/.NET) + repo `unimex/guest`
> (`universe_aic_guest_fe`, Angular). Los dos cambian a la vez y se entregan juntos.
> **Rama de trabajo (ambos repos):** `fix-borden`
> **Issues:** épica [`#411`](../../guest/issues/gabinete/411.md) y sus historias
> [`#412`](../../guest/issues/gabinete/412.md) · [`#413`](../../guest/issues/gabinete/413.md) ·
> [`#414`](../../guest/issues/gabinete/414.md) · historia nueva
> [`1-gabinete.md`](../../guest/issues/gabinete/1-gabinete.md)
> **Documentos hermanos:** [`GUIA-INSTALACION.md`](./GUIA-INSTALACION.md) (kiosko) ·
> [`../traductor/GUIA-DE-USO.md`](../traductor/GUIA-DE-USO.md) (empaquetado, config y diagnóstico) ·
> [`../README.md`](../README.md)
> **Fecha:** 2026-09-25

---

> 🧭 **Qué es este documento**
>
> El **porqué y el cómo** de la capa nativa del gabinete, y el contrato entre los dos repos que la
> forman. Está escrito para alguien que llega frío: explica primero qué problema físico existe, luego
> qué se decidió y por qué, y al final el detalle archivo por archivo.
>
> Lo que **no** está aquí: cómo se empaqueta y distribuye el ejecutable (eso es
> `../traductor/GUIA-DE-USO.md`) ni cómo se configura la máquina (eso es `GUIA-INSTALACION.md`).

---

## 1. El problema de fondo, y por qué no se resuelve con código de la página

El Guest abre cada juego dentro de un `<iframe>` servido por el **dominio del proveedor**. Eso
dispara la protección del navegador contra el secuestro de clics, con dos consecuencias que no
admiten rodeo:

1. **No se puede mirar dentro.** `document.elementFromPoint()` sobre el área de juego devuelve el
   propio `<iframe>`, nunca los botones del proveedor. `contentDocument` devuelve `null` o lanza.
2. **No se puede pulsar dentro.** Un evento despachado por código sobre el `<iframe>` se propaga por
   **nuestro** árbol y se queda ahí. La traducción de coordenadas de pantalla a un documento
   incrustado la hace el navegador **solo con input real del sistema**, y no la expone a la página.

Es la misma frontera que impide que cualquier página pulse el botón de confirmar de un banco
incrustado. No es una dificultad de implementación: no hay API que lo haga.

**La evidencia que abrió el camino:** activando "Mouse Keys" de accesibilidad en Windows —que mueve
el **cursor real del sistema** con el teclado numérico— el juego responde con total normalidad. O
sea: lo que el juego rechaza no es "el teclado", es que el input **no venga del sistema operativo**.

De ahí sale toda la arquitectura: hace falta un programa nativo, corriendo en la propia máquina del
gabinete, que convierta las pulsaciones de la botonera en **movimiento y clic reales** del cursor.
Ese programa es el **traductor**.

> **Corolario de despliegue, que suele malentenderse:** cada gabinete físico necesita su **propia
> instalación local** del traductor. No es un servicio centralizado y no puede serlo: solo un proceso
> corriendo en la sesión de Windows de esa pantalla puede mover ese cursor. El servidor del casino
> (Linux, nginx/apache) solo sirve el Guest, como siempre, y no se toca.

---

## 2. Cómo llegamos hasta aquí (el arco completo)

Cuatro etapas, en orden. Las tres primeras ya estaban entregadas y probadas en el gabinete antes de
este trabajo; la cuarta es lo que introduce esta rama.

| # | Etapa | Qué resolvió | Estado |
|---|---|---|---|
| 1 | **Puntero virtual del Guest** (épica del juego, #402–#405) | Un puntero dibujado en HTML que las flechas movían por la pantalla de juego. Alcanzaba y pulsaba **nuestros** controles (el chip de salida), pero al llegar a un botón del juego, Enter no hacía nada — por §1 | **Retirado** (ver §2.1) |
| 2 | **El traductor** (épica #411, historias #412–#414) | Hook global de teclado + `SendInput`: las flechas mueven el cursor **real** y Enter/espacio hacen un **clic real**. El juego responde porque para él es un mouse físico | Entregado y en uso |
| 3 | **Corrección de coordenadas** | El cursor aparecía "hasta abajo" en vez de centrado: el Guest reportaba coordenadas de **viewport** y `SetCursorPos` quiere coordenadas de **pantalla** | Entregado |
| 4 | **Posiciones mapeadas (Borden)** | Las flechas dejan de arrastrar el cursor y **saltan** entre los controles del juego, mapeados de antemano | **Esta rama** |

### 2.1 Por qué se retiró el puntero virtual

Cuando el traductor entró en escena, el puntero dibujado por el Guest quedó **redundante y dañino**:
se encimaba con el cursor real y su pulsación no entraba al juego. Se retiró por completo
(`GamePointerService`, el `<div>` de la punta y sus estilos), junto con los cuatro desvíos que el
motor de teclado hacía para cederle el mando.

Lo que **no** se perdió al retirarlo, y conviene tener claro porque fue la duda al hacerlo: el chip y
la píldora de salida **ya eran paradas normales del recorrido** (`knNav="game-exit-chip"`,
`knNav="game-exit-pill"` en `game-controls.component.html`). El puntero nunca fue necesario para
alcanzarlos: era un atajo alternativo. La salida garantizada —**Esc despliega la píldora, Esc otra
vez confirma**— nunca dependió de él.

### 2.2 Por qué el cursor aparecía abajo (etapa 3)

`getBoundingClientRect()` devuelve coordenadas relativas al **viewport del navegador**;
`SetCursorPos` las quiere **absolutas de pantalla**. En Chrome kiosko los dos sistemas coinciden por
casualidad —la ventana ocupa la pantalla entera desde (0,0), sin barras— y el error era invisible.
En una ventana normal, con su barra de direcciones y sin maximizar, la diferencia es de cientos de
píxeles.

La conversión vive ahora en `CabinetService.toScreenRect()`:

```ts
const chromeTop  = window.outerHeight - window.innerHeight;   // barra + pestañas
const chromeLeft = (window.outerWidth - window.innerWidth) / 2; // bordes laterales
x = rect.x + window.screenX + chromeLeft;
y = rect.y + window.screenY + chromeTop;
```

En kiosko esa suma da cero, así que no cambia nada donde ya funcionaba, y corrige donde se prueba en
ventana normal.

---

## 3. El reparto: el Guest decide, el traductor obedece

Es la decisión estructural de toda la capa, y de ella se derivan casi todas las demás.

**El traductor no sabe nada de juegos, proveedores ni pantallas.** Solo sabe cuatro cosas: si hay un
juego abierto, en qué modo, dónde poner el cursor, y qué teclas interceptar. Toda la lógica de
producto —qué proveedor es, dónde están sus botones, cómo se recorren— vive en el Guest.

**Por qué, y no al revés:**

| Razón | Consecuencia práctica |
|---|---|
| El `.zip` del traductor es **idéntico para cualquier cliente** (`README.md` del repo) | Si el mapa de posiciones viviera en `config.json`, habría un paquete distinto por casino y por proveedor. Con este reparto, uno solo sirve para todos |
| El Guest se despliega solo y llega al gabinete **con recargar** | Ajustar coordenadas, añadir un proveedor o cambiar el recorrido **no obliga a redistribuir el ejecutable** ni a pisar el gabinete |
| El motor de navegación por teclado ya existe en el Guest | Se reusa tal cual: navegación espacial, anillo de resaltado, contrato T1–T10, integración con el chip de salida. No se escribe un segundo motor paralelo |

### 3.1 El circuito, paso a paso

```
Jugador pulsa ← → ↑ ↓ en la botonera
   │
   ├─ juego SIN mapa, o sin traductor ──► el traductor se traga la flecha y
   │                                       mueve el cursor real paso a paso    (modo "cursor")
   │
   └─ juego de Borden, con traductor ───► el traductor NO se traga la flecha    (modo "mapeado")
                                           │
                                           ▼
                                    llega a Chrome como tecla normal
                                           │
                                           ▼
                                    KeyboardNavService mueve el resaltado al
                                    hotspot vecino (geometría) y pinta el anillo
                                           │
                                           ▼
                                    focus$ emite → el Guest manda
                                    {"type":"cursor_a", x, y}
                                           │
                                           ▼
                                    el traductor hace SetCursorPos: el cursor
                                    real salta encima de ese botón del juego
                                           │
                        Enter/espacio ─────► el traductor SÍ se los traga
                                             → clic real donde apunta el cursor
                                             → el juego responde
```

**La regla que lo mantiene simple:** *cada cambio de resaltado manda el cursor ahí*, sea un control
del juego o el chip de salida del Guest. No hay casos especiales — Enter siempre pulsa lo que está
resaltado.

---

## 4. El protocolo (contrato entre los dos repos)

Un WebSocket local, `ws://127.0.0.1:8765`. El traductor es el servidor; el Guest se conecta como
cliente. **El traductor nunca habla de vuelta**: la conexión abierta es toda la señal que hace falta.

| Mensaje (Guest → traductor) | Cuándo | Efecto |
|---|---|---|
| `{"type":"modo_juego","on":true,"navegacion":"cursor","rect":{x,y,w,h}}` | Al montar la pantalla de juego, **incluida la ventana de carga** | Entra en modo juego, viste el cursor y lo centra en `rect` |
| `{"type":"modo_juego","on":true,"navegacion":"mapeado","rect":{…}}` | Cuando se resuelve que el proveedor tiene mapa | Igual, **pero deja de tragarse las flechas** |
| `{"type":"cursor_a","x":…,"y":…}` | En cada cambio de resaltado, en modo mapeado | Salta el cursor real a ese punto |
| `{"type":"modo_juego","on":false}` | Al salir del juego | Sale de modo juego, restaura el cursor |
| `{"type":"ping"}` cada 500 ms | Mientras hay juego abierto | Reinicia el vigilante de latido |

**Compatibilidad hacia atrás:** un Guest anterior no manda `navegacion`; el traductor lo interpreta
como `"cursor"`, que es el comportamiento de siempre. Un traductor anterior ignora `cursor_a` y el
campo `navegacion` (su `switch` no los conoce), así que **nunca entra en modo mapeado**: el Guest
nuevo con un traductor viejo degrada al cursor libre en vez de romperse. Las dos direcciones son
seguras.

### 4.1 Coordenadas — las tres etapas de la conversión

Es el punto donde históricamente se han ido las tardes, así que conviene tenerlo escrito:

| Etapa | Unidad | Quién |
|---|---|---|
| `getBoundingClientRect()` | px CSS, relativos al **viewport** | Guest |
| `toScreenRect()` (§2.2) | px CSS, relativos a la **pantalla** | Guest — es lo que viaja por el WebSocket |
| `× dpiScale` | píxeles **físicos** para `SetCursorPos` | Traductor (`config.json`) |

`dpiScale` = el escalado de pantalla de Windows (100% → `1.0`, 125% → `1.25`, 150% → `1.5`). Es la
única pieza de la cadena que se ajusta en la máquina y no en el código.

### 4.2 Qué se traga y qué no

| Tecla | Modo cursor | Modo mapeado | Por qué |
|---|---|---|---|
| Flechas | Se tragan → mueven el cursor | **Pasan a Chrome** → mueven el resaltado | En mapeado la navegación es del Guest |
| Enter / espacio | Se tragan → clic real | Se tragan → clic real | Es lo **único** que puede pulsar dentro del iframe |
| **Esc y retroceso** | **Nunca se tragan** | **Nunca se tragan** | Sostienen la salida del juego, que no puede depender de nada más |

---

## 5. Cambios en el traductor (`unimex/gabinete`)

Commit `d4ab3b2`. Cinco archivos, ~149 líneas.

### 5.1 `GameModeController.cs` — el modo y el salto

- **`_modoMapeado`**, bandera `volatile`. Lo es porque **se escribe desde el hilo del WebSocket y se
  lee desde el hilo del hook de teclado**; el resto del proyecto resuelve esto con `lock`
  (`ModoJuegoState`), pero aquí una bandera booleana basta.
- **`ShouldSwallow`** deja de tragarse las flechas cuando el modo está activo. Sigue tragando
  `Confirm`; Esc y retroceso nunca entraron en esa lista.
- **`OnKeyDown`** deja de mover el cursor con las flechas en ese modo. **Las dos guardas son
  obligatorias**, y es la trampa principal de todo este cambio: `KeyboardHook` dispara sus eventos
  **antes** de mirar `ShouldSwallow`, así que tocar solo una de las dos deja la flecha navegando el
  Guest **y además** arrastrando el cursor.
- **`MoveCursorTo(x, y)`** (nuevo). Aplica `dpiScale`, valida contra `GetSystemMetrics` y llama a
  `SetCursorPos`. Si el punto cae fuera de la pantalla **ignora el salto y lo registra en el log** —
  a diferencia de `EnterGameMode`, que en ese caso centra el cursor. La diferencia es deliberada: al
  entrar, el centro es una aproximación razonable del área de juego; en un salto, dejaría el cursor
  en mitad del juego y el siguiente Enter pulsaría cualquier cosa.
- **`ExitGameMode`** limpia la bandera. Sin eso, la siguiente sesión arrancaría en modo mapeado sin
  que nadie lo pidiera y las flechas no moverían nada.

### 5.2 `WebSocketBridge.cs` — el mensaje nuevo

`InboundMessage` gana `Navegacion`, `X` e `Y`; `ModoJuegoOnRequested` pasa a llevar también el modo;
aparece el caso `"cursor_a"` con su evento `CursorRequested`.

**Se manda una coordenada por mensaje, no el mapa completo, y es a propósito:** el buffer de
recepción es de 4096 bytes y el bucle **ignora `EndOfMessage`**, así que un mensaje grande llegaría
partido y cada trozo se descartaría como JSON inválido. Mandar el mapa entero habría chocado justo
con eso.

### 5.3 `Program.cs` y `AppConfig.cs`

`Program.cs` cablea `bridge.CursorRequested += controller.MoveCursorTo` y propaga el modo.

`AppConfig.RawConfig` pasó de `private` a `internal`. **No es cosmético:** `JsonContext.cs` declara
`[JsonSerializable]` sobre esa clase y sobre `WebSocketBridge.InboundMessage`, las dos `private` — es
un error de compilación (CS0122). Ese archivo está preparado para cuando se active el recorte
(`PublishTrimmed`, que hoy el `.csproj` **no** tiene) y nadie lo usa todavía; se dejó en pie y se
hicieron `internal` las dos clases, que es el cambio mínimo que lo vuelve válido sin tirar trabajo de
nadie. **Queda una decisión pendiente:** terminar de cablearlo o borrarlo (§9).

### 5.4 `config.json` — sin cambios

El modo lo decide el Guest en cada sesión, así que no hay ninguna clave nueva. Se preserva la
invariante del `.zip` idéntico.

> Si algún día hiciera falta una clave: el patrón exige **tres** sitios (propiedad en `AppConfig`,
> espejo en `RawConfig`, y la línea de mapeo en `Load`). Olvidar la tercera no da error: el valor sale
> por defecto, en silencio.

---

## 6. Cambios en el Guest (`universe_aic_guest_fe`)

Commit `91885995`. Diez archivos, ~508 líneas.

### 6.1 Motor de teclado — dos añadidos mínimos

`core/keyboard-nav/keyboard-nav.service.ts`:

- **`focus$`** — observable nuevo, emitido en `focusEl()`. Es el **embudo único** por el que pasan
  todos los cambios de resaltado, venga de una flecha, del mouse, de un destino de entrada o de un
  `focusId` diferido. Copia el patrón que ya tenía `activate$`.
- **`setPointerDriven(on)`** — mientras está activo, `onMove` sale temprano y el motor **ignora el
  movimiento del mouse**.

  Esto último no es una optimización, es obligatorio: el salto de cursor que nosotros mismos
  provocamos genera un `mousemove` real. Sin la guarda, ese evento le da el mando al mouse y, al
  aterrizar sobre un hotspot —que es `pointer-events:none`, así que ahí `elementFromPoint` devuelve
  el iframe y no hay `[data-kn]` debajo—, entra en la rama que **apaga el resaltado**. El anillo
  desaparecería justo al saltar.

### 6.2 `CabinetService` — el protocolo

`notifyGameOpen(rect, navegacion)` gana el modo, y aparece `moveCursorToElement(el)`, que toma el
centro del elemento, lo pasa por `toScreenRect` y manda `cursor_a`. Es no-op si no hay traductor
conectado, así que llamarlo siempre es seguro.

### 6.3 `GameVendorService` (nuevo) — qué proveedor es

Resuelve el proveedor **por nombre**, no por id fijo, y consulta `lobby-open/suppliers`.

Dos razones, las dos importantes:

1. **El id es un cuid de base de datos** y no tiene por qué ser el mismo en stage que en producción.
   Clavarlo en el código fallaría en silencio justo al desplegar.
2. **Los endpoints de catálogo enmascaran el nombre.** Según el relevamiento del backend,
   `suppliers-games` y `suppliers-category` renombran Borden e IGS a **"AIC Games"**;
   `lobby-open/suppliers` es el que los devuelve sin enmascarar. Buscar "Borden" en el endpoint
   equivocado no encuentra nada, y tampoco da error.

**La petición es un observable cacheado** (`shareReplay`) y no un mapa ya resuelto. Esto arregla una
carrera real: el servicio se construye **en el mismo instante** en que se abre la pantalla de juego,
así que con un mapa síncrono la respuesta del juego casi siempre ganaría y el proveedor saldría
desconocido — el modo mapeado no se activaría casi nunca.

### 6.4 `GameHotspotsComponent` (nuevo) — la capa

Vive en `pages/home/open-game/game-hotspots/`. Se monta **solo si** el proveedor tiene mapa **y** el
traductor está conectado; en cualquier otro caso la pantalla de juego ni la incluye, así que un
jugador web no nota absolutamente nada.

| Decisión | Por qué |
|---|---|
| **`pointer-events: none`** en toda la capa | El clic real del traductor tiene que **atravesarla** hasta el juego. No estorba al recorrido: el motor filtra por visibilidad (`offsetParent`), nunca por `pointer-events` |
| **Envoltorio posicionado + botón dentro** | El resaltado **fotografía y restaura el atributo `style` completo** del elemento que marca. Unas coordenadas escritas en el botón se perderían al recalcularlas en un cambio de tamaño. Mismo patrón que `game-controls.component.html` |
| **`z-index: 9998`** | Por encima del iframe (para que el anillo se vea) y por debajo del control de salida (9999) |
| **`ResizeObserver`** sobre el propio host | Cubre de una vez el `resize` de ventana, la pantalla completa del proveedor, el giro y los cambios del contenedor |
| **`focusId(defaultId)` al montar, una sola vez** | Sin esto no habría nada resaltado hasta la primera flecha, y el cursor se quedaría donde lo dejó la pantalla anterior |

### 6.5 `open-game.component.ts` — el enganche

En el `next` de `game/get-game-by-id` —la **única** respuesta que trae el proveedor— se llama a
`applyVendor()`, que resuelve el nombre y, si procede, activa el modo mapeado:

1. Manda el **segundo** `modo_juego`, ahora con `navegacion: 'mapeado'`.
2. Llama `nav.setPointerDriven(true)`.
3. Se suscribe a `focus$` para mandar el cursor detrás del resaltado.

**Por qué hay dos avisos y no uno:** esa llamada está anidada dentro de `getGameURL`, así que el
proveedor se conoce **después** de que el iframe ya tiene URL. El primer aviso sale al montar la
pantalla (pide cursor libre, que es el respaldo seguro y ya cubre la ventana de carga) y el segundo
corrige el modo cuando se sabe quién sirve el juego.

`ngOnDestroy` devuelve `setPointerDriven(false)` siempre, tocara o no el modo mapeado.

---

## 7. El mapa de Borden

`pages/home/open-game/game-hotspots/hotspot-maps.ts`. Medido sobre una captura de **1920×943**.

| id | Control | x | y | w | h |
|---|---|---:|---:|---:|---:|
| `borden-denominacion-menos` | Disminuir denominación | 957 | 847 | 85 | 52 |
| `borden-denominacion-mas` | Aumentar denominación | 1212 | 847 | 86 | 52 |
| `borden-apuesta-menos` | Disminuir apuesta total | 1416 | 847 | 85 | 52 |
| `borden-apuesta-mas` | Aumentar apuesta total | 1755 | 847 | 86 | 52 |
| `borden-circular-superior` | Acción circular superior | 1750 | 405 | 169 | 195 |
| `borden-girar` | Girar (entrada por defecto) | 1690 | 600 | 229 | 210 |

**El chip de salida no está en el mapa** y no hace falta que esté: ya es una parada del recorrido por
sí mismo, así que las flechas lo alcanzan como a cualquier otro control.

### 7.1 Por qué píxeles de referencia y no fracciones

Las cajas se guardan en **píxeles de una resolución de referencia**, no en fracciones ya calculadas.
Así el número escrito en el archivo es **el mismo que se midió sobre la captura**, y recalibrar es
volver a medir y sustituir. Las fracciones las calcula el componente al posicionar.

### 7.2 La escala, y el letterboxing

No es un `x * ancho` directo. El iframe ocupa el 100% del área, **pero el juego puede poner sus
propias bandas negras dentro** si conserva su relación de aspecto. Por eso el componente calcula
primero la **caja del contenido** —la mayor caja con la relación de referencia (1920/943) que cabe
centrada, igual que `object-fit: contain`— y aplica las fracciones **dentro de esa caja**.

Si Borden no conserva la relación que suponemos, esto se ve como un desfase sistemático y se corrige
cambiando `referenceWidth`/`referenceHeight`. **Es la incógnita principal pendiente de la pantalla
real.**

### 7.3 El recorrido y los destinos explícitos

El motor exige que dos elementos **solapen en el eje transversal** para alcanzarse con una flecha.
Los tres controles de abajo a la izquierda no solapan con los circulares de la derecha, así que por
geometría pura no se llegaría de unos a otros. Por eso llevan `up` explícito hacia `borden-girar`, y
`borden-circular-superior` lleva `up` hacia el chip de salida.

**Estos destinos son la primera propuesta, no la palabra final:** el recorrido definitivo se decide
con el mapa ya calibrado sobre la pantalla del gabinete.

---

## 8. Calibración

Poner `kn_hotspots_debug` a `'1'` en `localStorage` y recargar. Las cajas se dibujan con borde
punteado sobre el juego, así que se ve de un vistazo cuál está corrida y cuánto.

```js
localStorage.setItem('kn_hotspots_debug', '1');   // activar
localStorage.removeItem('kn_hotspots_debug');     // desactivar
```

Ajustar en `hotspot-maps.ts`, desplegar el Guest y recargar el kiosko. **El ejecutable no se toca.**

---

## 9. Decisiones abiertas y riesgos

| # | Asunto | Estado |
|---|---|---|
| 1 | **`JsonContext.cs`** — quedó válido (§5.3) pero sigue sin usarse y sin `PublishTrimmed`. Hay que **terminarlo de cablear o borrarlo**, antes de que el próximo que toque JSON pierda una tarde | Decisión pendiente |
| 2 | **Letterboxing de Borden** (§7.2) — si no conserva la relación de aspecto de la referencia, las cajas se desalinean | Se descubre en la primera prueba en el gabinete |
| 3 | **Qué hace cada botón circular** — el mapa los nombra por posición, no por función, a propósito. Falta confirmar en el gabinete cuál es girar | Pendiente de confirmar |
| 4 | **Recorrido definitivo** (§7.3) | Se cierra con el mapa calibrado |
| 5 | **`dpiScale`** — si la pantalla del gabinete no está al 100%, hay que ajustarlo en `config.json` | Pendiente de medir en la máquina |
| 6 | **Nivel A del issue** (que el juego responda a espacio/+/− directamente) | Fuera de esta entrega. Espacio sigue siendo clic real |

---

## 10. Verificación

Los builds del Guest quedaron verdes en **dev y prod**. El traductor **no se pudo compilar desde
macOS** (depende de `user32.dll`): eso se verifica en Windows.

| # | Escenario | Qué debe pasar |
|---|---|---|
| 1 | Navegador normal, sin traductor, juego de Borden | **No** aparece ninguna capa. Todo se comporta como hoy |
| 2 | Gabinete, juego que **no** es de Borden | Flechas mueven el cursor libremente, Enter hace clic real, Esc y retroceso salen al Guest — intacto |
| 3 | Gabinete, juego de Borden | Las flechas saltan entre los seis controles y el chip; el resaltado cae sobre el botón correcto; Enter gira y cambia la apuesta de verdad |
| 4 | Gabinete, Borden, pulsar Esc | Sigue desplegando el control de salida, y a la segunda confirma |
| 5 | Dos resoluciones distintas + la del gabinete | Las cajas siguen alineadas (con el modo de calibración activo) |
| 6 | Cerrar Chrome de golpe con el juego abierto | El traductor vuelve a modo fuera-de-juego en menos de 2 s |

`traductor.log` registra cada salto de cursor — es la única observabilidad que hay en un gabinete
real. La tabla de diagnóstico está en `../traductor/GUIA-DE-USO.md` §5.

---

## 11. Archivos tocados

**`unimex/gabinete`** (commit `d4ab3b2`, rama `fix-borden`):

```
traductor/GUIA-DE-USO.md                    +33   protocolo y diagnóstico del salto
traductor/Traductor/GameModeController.cs   +93   modo mapeado y MoveCursorTo
traductor/Traductor/WebSocketBridge.cs      +30   mensaje cursor_a y campos nuevos
traductor/Traductor/Program.cs               +8   cableado
traductor/Traductor/AppConfig.cs             +3   RawConfig → internal
```

**`unimex/guest/universe_aic_guest_fe`** (commit `91885995`, rama `fix-borden`):

```
src/app/core/keyboard-nav/keyboard-nav.service.ts          +32   focus$ y setPointerDriven
src/app/core/keyboard-nav/cabinet.service.ts               +31   navegacion y moveCursorToElement
src/app/shared/services/game-vendor.service.ts             +59   proveedor por nombre (nuevo)
src/app/pages/home/open-game/game-hotspots/…               +318  la capa completa (nuevo)
src/app/pages/home/open-game/open-game.component.ts        +57   enganche del modo mapeado
src/app/pages/home/open-game/open-game.component.html      +10   montaje de la capa
src/app/pages/home/home.module.ts                           +2   declaración del componente
```

---

## 12. Referencias

- [`../traductor/GUIA-DE-USO.md`](../traductor/GUIA-DE-USO.md) — empaquetado, `config.json`,
  diagnóstico por línea de log y el protocolo del canal.
- [`GUIA-INSTALACION.md`](./GUIA-INSTALACION.md) — configuración de la máquina y el kiosko.
- [`../README.md`](../README.md) — qué es cada carpeta y por qué el repo está separado.
- Issues: [`1-gabinete.md`](../../guest/issues/gabinete/1-gabinete.md) (esta entrega),
  [`411`](../../guest/issues/gabinete/411.md) (épica de la capa nativa).
- En el repo del Guest: `CONTEXTO-JUEGO-KEYBOARD-NAV.md` §9 (la frontera del iframe, con la
  verificación en navegador) y `GUIA-KEYBOARD-NAV-NUEVAS-PAGINAS.md` (motor de teclado: contrato,
  vocabulario `data-kn-*` y trampas conocidas).
