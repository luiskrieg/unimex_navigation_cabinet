# Guía de instalación — kiosko del gabinete (#412)

> **Estado: esqueleto.** Los pasos exactos de Windows (edición, políticas de grupo, Tarea Programada)
> se completan y verifican con el primer gabinete físico — ver el checkpoint del plan de la épica
> §6.6. Lo que sí está fijo desde ahora es **qué** hay que lograr y **dónde** se toca cada cosa, para
> que quien instale el segundo gabinete no dependa de quien instaló el primero (#412 CA6).

## Qué deja listo esta guía

1. Chrome en modo kiosko abre, al terminar de arrancar Windows, la URL del Guest de Instalotto a
   pantalla completa — sin barra de direcciones, sin salida al escritorio (#412 CA1, CA3).
2. Si Chrome se cierra o deja de responder, se reabre solo en menos de 10 segundos (#412 CA4).
3. El traductor (`../traductor/`) ya está corriendo en segundo plano antes de que el jugador toque la
   botonera (#414 CA1).

## 1. Dirección del Guest que abre el kiosko

**Se cambia aquí:** el acceso directo / comando que lanza Chrome en modo kiosko lleva la URL de
producción del Guest de Instalotto, **sin ningún parámetro especial**:

```
chrome.exe --kiosk "https://<dominio-del-guest>" --edge-skip-first-run --noerrdialogs
```

Para un gabinete de otro cliente, este es el **único** lugar que cambia: la URL. Todo lo demás de esta
guía es igual (#412 CA6.1).

> El Guest **no necesita saber por adelantado** que está en un gabinete: al arrancar intenta conectarse
> a `127.0.0.1` (donde escucha el traductor, ver §4). Si el traductor ya está corriendo —que es
> justamente lo que deja listo la Tarea Programada del punto 4—, la conexión se establece sola y el
> Guest activa el modo gabinete sin URL, sin `localStorage` y sin que nadie tenga que configurar nada
> por casino. Si no hay traductor corriendo (o el kiosko aún no arrancó esa tarea), el intento de
> conexión simplemente falla y el Guest se comporta como en cualquier navegador normal.

## 2. Chrome en modo kiosko, sin salida posible

Pendiente de fijar con el primer gabinete real — puntos a cubrir (#412 CA2, CA2.1, CA3):

- Perfil de Chrome dedicado, sin extensiones ni gestos que abran otra pestaña/ventana.
- Bloquear combinaciones que lleven al escritorio de Windows (Alt+Tab, Win, Ctrl+Alt+Supr donde se
  pueda) vía política de grupo local o `chrome://policy`.
- Confirmar que las pulsaciones que emite el encoder de la botonera (flechas, Enter, espacio, Esc,
  retroceso) no disparan ningún atajo de Chrome o de Windows antes de llegar al Guest.

## 3. Reapertura automática

Pendiente de fijar con el primer gabinete real (#412 CA4, CA5) — candidatos: Tarea Programada de
Windows que vigile el proceso de Chrome, o un supervisor tipo `nssm`/script de PowerShell en bucle.

## 4. Arranque del traductor

El traductor (`../traductor/`) se instala como **Tarea Programada "al iniciar sesión"**, en la misma
sesión interactiva de Windows que Chrome (no como Windows Service — ver `README.md` de `traductor/`
sobre por qué). Pendiente de documentar el comando exacto de `schtasks` una vez validado en el primer
gabinete.

## 5. Verificación de la instalación

- [ ] Encender el gabinete sin tocar nada: Chrome abre a pantalla completa con el Guest.
- [ ] La botonera mueve el resaltado / el puntero como un teclado normal (#412 CA2).
- [ ] No hay forma de llegar al escritorio ni a otra ventana con la botonera (#412 CA3).
- [ ] Cerrar Chrome a la fuerza: se reabre solo en <10s (#412 CA4).
- [ ] Apagar la red, encender el gabinete, reconectar: el Guest carga solo sin exponer el escritorio
      (#412 CA5).
- [ ] El traductor ya está corriendo sin ventana ni ícono visible antes de tocar la botonera
      (#414 CA1).
