namespace Traductor;

/// <summary>
/// #414 CA3 — "con la apariencia del puntero del Guest": mientras dura el
/// modo juego, el cursor del sistema completo cambia a un ícono propio
/// (exportado del mismo prototipo visual del puntero del Guest). Se
/// restaura siempre: al salir del modo juego, y también si el proceso
/// termina de golpe, para no dejar el cursor de <b>todo Windows</b> —no solo
/// del juego— con una apariencia rota (R-Gab? ver plan §5).
///
/// El archivo `.cur` es un asset de diseño pendiente de recibir (ver plan
/// §7): si no existe, este componente simplemente no cambia el cursor y el
/// resto del traductor sigue funcionando con el cursor por defecto de
/// Windows.
/// </summary>
public sealed class CursorAppearance
{
    private readonly string _cursorFilePath;
    private bool _applied;

    public CursorAppearance(string cursorFilePath)
    {
        _cursorFilePath = cursorFilePath;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Restore();
    }

    public void Apply()
    {
        if (_applied) return;
        if (!File.Exists(_cursorFilePath)) return;

        var handle = NativeMethods.LoadCursorFromFile(_cursorFilePath);
        if (handle == 0) return;

        var copy = NativeMethods.CopyIcon(handle);
        if (NativeMethods.SetSystemCursor(copy, NativeMethods.OCR_NORMAL))
        {
            _applied = true;
        }
    }

    public void Restore()
    {
        if (!_applied) return;
        // Vuelve el esquema de cursores del sistema al que estaba activo
        // antes (Windows recarga el esquema guardado, no un cursor suelto).
        NativeMethods.SystemParametersInfo(NativeMethods.SPI_SETCURSORS, 0, 0, 0);
        _applied = false;
    }
}
