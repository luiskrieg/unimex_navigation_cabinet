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
        return _keyMap.Up.Contains(key) || _keyMap.Down.Contains(key)
            || _keyMap.Left.Contains(key) || _keyMap.Right.Contains(key)
            || _keyMap.Confirm.Contains(key);
    }

    public void OnKeyDown(Keys key, bool isRepeat)
    {
        if (!_state.IsOn) return;

        if (_keyMap.Up.Contains(key)) { _mouse.Move(Direction.Up, isRepeat); return; }
        if (_keyMap.Down.Contains(key)) { _mouse.Move(Direction.Down, isRepeat); return; }
        if (_keyMap.Left.Contains(key)) { _mouse.Move(Direction.Left, isRepeat); return; }
        if (_keyMap.Right.Contains(key)) { _mouse.Move(Direction.Right, isRepeat); return; }

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

    /// El Guest avisó "modo_juego: on" (#414 CA3). El estado ya lo puso en
    /// `on` quien recibió el mensaje (<see cref="WebSocketBridge"/>); aquí
    /// solo se ejecutan los efectos: vestir el cursor y centrarlo en el área
    /// de juego que reportó el Guest.
    public void EnterGameMode(RectDto? rect)
    {
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

    /// Se llama desde <see cref="ModoJuegoState.TurnedOff"/>, que es la
    /// única fuente de verdad para "salir de modo juego" — dispara igual si
    /// vino de un aviso explícito del Guest o del vigilante de latido
    /// (#414 CA7, CA9).
    public void ExitGameMode()
    {
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
