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
producción del Guest de Instalotto **más el parámetro que activa el modo gabinete** que espera
`cabinet.service.ts` (issue #413) del lado del Guest:

```
chrome.exe --kiosk "https://<dominio-del-guest>/?cabinet=1" --edge-skip-first-run --noerrdialogs
```

Para un gabinete de otro cliente, este es el **único** lugar que cambia: la URL. Todo lo demás de esta
guía es igual (#412 CA6.1).

> El parámetro `?cabinet=1` es una decisión de este mismo proyecto (no depende de Kenosoft): el Guest
> lo lee una sola vez al arrancar y lo recuerda en `localStorage`, así que sigue vigente aunque la
> navegación interna de Angular borre la query string.

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
