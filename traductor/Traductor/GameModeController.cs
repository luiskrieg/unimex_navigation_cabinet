namespace Traductor;

/// <summary>
/// El único lugar del traductor que "sabe" qué hacer con cada tecla lógica.
/// A propósito no sabe nada de juegos, proveedores ni pantallas (esa lógica
/// vive entera en el Guest, ver plan §"Todo lo que cambia vive en el
/// Guest") — solo traduce teclas de la botonera a movimiento/clic reales
/// mientras <see cref="ModoJuegoState.IsOn"/>, y no interviene cuando no.
/// </summary>
public sealed class GameModeController
{
    private readonly AppConfig.KeyMapConfig _keyMap;
    private readonly MouseSimulator _mouse;
    private readonly CursorAppearance _cursor;
    private readonly ModoJuegoState _state;
    private readonly double _dpiScale;
    private readonly HashSet<Keys> _confirmHeld = new();

    /// <summary>
    /// Modo de navegación de la sesión de juego en curso, que decide el Guest
    /// según el proveedor del juego abierto:
    ///
    /// - <b>false (cursor libre)</b>: lo de siempre — las flechas se tragan y
    ///   mueven el cursor real paso a paso.
    /// - <b>true (posiciones mapeadas)</b>: las flechas <b>no</b> se tragan y
    ///   llegan a Chrome, donde el Guest mueve su resaltado entre las
    ///   posiciones que tiene mapeadas y nos manda `cursor_a` con el destino.
    ///   Enter y espacio se siguen tragando: el clic real es lo único que
    ///   puede pulsar un juego en un iframe de otro dominio.
    ///
    /// `volatile` porque se escribe desde el hilo del WebSocket y se lee desde
    /// el hilo del hook de teclado.
    /// </summary>
    private volatile bool _modoMapeado;

    public GameModeController(
        AppConfig.KeyMapConfig keyMap,
        MouseSimulator mouse,
        CursorAppearance cursor,
        ModoJuegoState state,
        double dpiScale)
    {
        _keyMap = keyMap;
        _mouse = mouse;
        _cursor = cursor;
        _state = state;
        _dpiScale = dpiScale;
    }

    /// #414 CA2 y CA6.1 — solo se traga lo que el traductor va a convertir en
    /// movimiento o clic de mouse. Esc y retroceso **jamás** se tragan, ni en
    /// modo juego: siguen al Guest sin modificar.
    public bool ShouldSwallow(Keys key)
    {
        if (!_state.IsOn) return false;
        // En modo mapeado las flechas son del Guest: si se tragaran aquí, su
        // resaltado no se movería nunca.
        if (!_modoMapeado && EsFlecha(key)) return true;
        return _keyMap.Confirm.Contains(key);
    }

    public void OnKeyDown(Keys key, bool isRepeat)
    {
        if (!_state.IsOn) return;

        // Este método corre SIEMPRE, se haya tragado la tecla o no (el hook
        // dispara sus eventos antes de mirar `ShouldSwallow`). Sin esta guarda,
        // en modo mapeado la flecha llegaría al Guest **y además** movería el
        // cursor por su cuenta: los dos a la vez.
        if (!_modoMapeado)
        {
            if (_keyMap.Up.Contains(key)) { _mouse.Move(Direction.Up, isRepeat); return; }
            if (_keyMap.Down.Contains(key)) { _mouse.Move(Direction.Down, isRepeat); return; }
            if (_keyMap.Left.Contains(key)) { _mouse.Move(Direction.Left, isRepeat); return; }
            if (_keyMap.Right.Contains(key)) { _mouse.Move(Direction.Right, isRepeat); return; }
        }
        else if (EsFlecha(key))
        {
            return;
        }

        if (_keyMap.Confirm.Contains(key))
        {
            // #414 CA5.1/5.2 — Enter y Espacio pueden mapear ambos a
            // "confirmar"; el botón de mouse baja una sola vez con la
            // primera que se pulse y sube con la última que se suelte, para
            // que solapar las dos no produzca un clic de más.
            var wasEmpty = _confirmHeld.Count == 0;
            _confirmHeld.Add(key);
            if (wasEmpty) _mouse.ButtonDown();
        }
    }

    public void OnKeyUp(Keys key)
    {
        if (_keyMap.Confirm.Contains(key) && _confirmHeld.Remove(key) && _confirmHeld.Count == 0)
        {
            _mouse.ButtonUp();
        }
    }

    private bool EsFlecha(Keys key) =>
        _keyMap.Up.Contains(key) || _keyMap.Down.Contains(key)
        || _keyMap.Left.Contains(key) || _keyMap.Right.Contains(key);

    /// El Guest avisó "modo_juego: on" (#414 CA3). El estado ya lo puso en
    /// `on` quien recibió el mensaje (<see cref="WebSocketBridge"/>); aquí
    /// solo se ejecutan los efectos: fijar el modo de navegación que pide el
    /// Guest, vestir el cursor y centrarlo en el área de juego que reportó.
    public void EnterGameMode(RectDto? rect, string? navegacion)
    {
        _modoMapeado = string.Equals(navegacion, "mapeado", StringComparison.OrdinalIgnoreCase);
        Log.Write($"Navegación: {(_modoMapeado ? "posiciones mapeadas (las flechas son del Guest)" : "cursor libre")}.");

        _cursor.Apply();

        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

        int x = 0, y = 0;
        string? porQueAlCentro = null;

        if (rect is null || rect.Width <= 0 || rect.Height <= 0)
        {
            porQueAlCentro = "el Guest no mandó un rect utilizable";
        }
        else
        {
            x = (int)((rect.X + rect.Width / 2) * _dpiScale);
            y = (int)((rect.Y + rect.Height / 2) * _dpiScale);

            // El rect puede llegar en coordenadas del documento y no del
            // viewport: con la página desplazada, un juego que ocupa toda la
            // pantalla se reporta en y=1080 y su centro cae en 1620, fuera de
            // una pantalla de 1080 de alto. Windows entonces pega el cursor al
            // borde, y el jugador lo encuentra "hasta abajo" en vez de al
            // centro. Un centro fuera de la pantalla es siempre un rect en el
            // que no se puede confiar, venga de donde venga.
            if (x < 0 || x >= screenWidth || y < 0 || y >= screenHeight)
            {
                porQueAlCentro =
                    $"el centro del rect ({x},{y}) cae fuera de la pantalla de {screenWidth}x{screenHeight}";
            }
        }

        if (porQueAlCentro is not null)
        {
            // El centro de la pantalla es la mejor aproximación al centro del
            // juego: en kiosko a pantalla completa son el mismo punto. Sin
            // esto el cursor se quedaba donde lo dejó la sesión anterior y el
            // jugador empezaba sin saber dónde está el puntero (CA414.3).
            x = screenWidth / 2;
            y = screenHeight / 2;
        }

        NativeMethods.SetCursorPos(x, y);
        Log.Write($"Cursor colocado en {x},{y}"
            + (porQueAlCentro is null ? "." : $" (centro de la pantalla porque {porQueAlCentro})."));
    }

    /// <summary>
    /// El Guest movió su resaltado a una posición mapeada y pide que el cursor
    /// real salte ahí, para que el siguiente Enter pulse ese control del juego.
    ///
    /// Las coordenadas llegan en píxeles CSS **de pantalla** —el Guest ya le
    /// sumó la posición de la ventana y el alto de la barra del navegador—, así
    /// que aquí solo queda aplicar el escalado de Windows, igual que con el
    /// rect de entrada.
    /// </summary>
    public void MoveCursorTo(double x, double y)
    {
        if (!_state.IsOn) return;

        var px = (int)(x * _dpiScale);
        var py = (int)(y * _dpiScale);

        var screenWidth = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXSCREEN);
        var screenHeight = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYSCREEN);

        // A diferencia de la entrada en modo juego, un punto fuera de pantalla
        // NO cae al centro: ahí el centro es una aproximación razonable del
        // área de juego, pero para un salto dejaría el cursor en mitad del
        // juego y el siguiente Enter pulsaría cualquier cosa. Mejor no moverse.
        if (px < 0 || px >= screenWidth || py < 0 || py >= screenHeight)
        {
            Log.Write(
                $"Salto de cursor ignorado: ({px},{py}) cae fuera de la pantalla de {screenWidth}x{screenHeight}.");
            return;
        }

        NativeMethods.SetCursorPos(px, py);
        Log.Write($"Cursor saltó a {px},{py}.");
    }

    /// Se llama desde <see cref="ModoJuegoState.TurnedOff"/>, que es la
    /// única fuente de verdad para "salir de modo juego" — dispara igual si
    /// vino de un aviso explícito del Guest o del vigilante de latido
    /// (#414 CA7, CA9).
    public void ExitGameMode()
    {
        // Sin esto, la siguiente sesión de juego arrancaría en modo mapeado
        // aunque el Guest no lo haya pedido, y las flechas no moverían nada.
        _modoMapeado = false;
        // Nunca dejar un botón de mouse "atorado" abajo a medio clic
        // mantenido si el modo juego termina de golpe.
        if (_confirmHeld.Count > 0)
        {
            _confirmHeld.Clear();
            _mouse.ButtonUp();
        }
        _cursor.Restore();
    }
}
