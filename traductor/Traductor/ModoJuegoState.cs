namespace Traductor;

/// <summary>
/// Estado de "modo juego" con vigilante de latido (#414 CA9): si el Guest
/// avisó que entró en modo juego pero deja de mandar `ping` —Chrome se
/// cerró, se recargó, o el Guest perdió la sesión—, el traductor vuelve
/// solo a modo fuera-de-juego en menos de 2 segundos. Es la red de
/// seguridad real: no depende de que llegue un aviso explícito de salida,
/// que es best-effort por diseño del navegador (`beforeunload`).
/// </summary>
public sealed class ModoJuegoState : IDisposable
{
    private readonly int _timeoutMs;
    private readonly Timer _watchdog;
    private readonly object _lock = new();
    private DateTime _lastPing = DateTime.MinValue;
    private bool _on;

    /// Se dispara solo al pasar de apagado a encendido.
    public event Action? TurnedOn;

    /// Fuente única de verdad para "salir de modo juego": se dispara tanto
    /// por un aviso explícito del Guest como por el vencimiento del latido.
    public event Action? TurnedOff;

    public ModoJuegoState(int timeoutMs)
    {
        _timeoutMs = timeoutMs;
        _watchdog = new Timer(_ => CheckTimeout(), null, 250, 250);
    }

    public bool IsOn { get { lock (_lock) return _on; } }

    public void SetOn()
    {
        bool justTurnedOn;
        lock (_lock)
        {
            _lastPing = DateTime.UtcNow;
            justTurnedOn = !_on;
            _on = true;
        }
        if (justTurnedOn) TurnedOn?.Invoke();
    }

    public void SetOff()
    {
        bool justTurnedOff;
        lock (_lock)
        {
            justTurnedOff = _on;
            _on = false;
        }
        if (justTurnedOff) TurnedOff?.Invoke();
    }

    public void Ping()
    {
        lock (_lock) _lastPing = DateTime.UtcNow;
    }

    private void CheckTimeout()
    {
        bool expired;
        lock (_lock)
        {
            expired = _on && (DateTime.UtcNow - _lastPing).TotalMilliseconds > _timeoutMs;
            if (expired) _on = false;
        }
        if (expired) TurnedOff?.Invoke();
    }

    public void Dispose() => _watchdog.Dispose();
}
