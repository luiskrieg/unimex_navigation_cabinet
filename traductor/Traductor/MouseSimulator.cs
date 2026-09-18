using System.Runtime.InteropServices;

namespace Traductor;

public enum Direction { Up, Down, Left, Right }

/// <summary>
/// Traduce direcciones lógicas en movimiento y clic reales del mouse vía
/// SendInput — para Chrome y el juego es una acción física indistinguible de
/// un mouse de verdad (la misma vía que usa "Mouse Keys" de accesibilidad,
/// que ya se validó a mano contra un juego de producción).
///
/// El movimiento es <b>relativo</b> a propósito (no absoluto): Windows ya
/// detiene el cursor en el borde de la pantalla por sí solo con
/// MOUSEEVENTF_MOVE relativo (#414 CA4.2), así que no hace falta que este
/// programa conozca la resolución ni la geometría de la pantalla para eso.
/// La única vez que hace falta una posición absoluta es al <b>entrar</b> en
/// modo juego (centrar el cursor, CA414.3) — ver <see cref="GameModeController"/>.
/// </summary>
public sealed class MouseSimulator
{
    private readonly CursorConfig _cfg;
    private int _accel;

    public MouseSimulator(CursorConfig cfg) => _cfg = cfg;

    /// #414 CA4 y CA4.1 — paso corto al pulsar, crece progresivamente al
    /// mantener hasta un máximo, y arranca corto otra vez al soltar y volver
    /// a pulsar.
    ///
    /// Los números salieron de `game-pointer.service.ts` (el puntero virtual
    /// del Guest) para que la sensación fuera idéntica dentro y fuera del
    /// gabinete, pero ya <b>no</b> coinciden: sobre la pantalla real el mismo
    /// paso se sentía demasiado rápido y se bajó por config. Si algún día se
    /// quiere volver a emparejar las dos sensaciones, hay que mover también
    /// los del Guest.
    public void Move(Direction dir, bool isRepeat)
    {
        _accel = isRepeat ? Math.Min(_accel + 1, _cfg.AccelMax) : 0;
        var step = _cfg.StepBase + _accel * _cfg.StepGrowth;
        var (dx, dy) = dir switch
        {
            Direction.Up => (0, -step),
            Direction.Down => (0, step),
            Direction.Left => (-step, 0),
            Direction.Right => (step, 0),
            _ => (0, 0),
        };
        SendRelativeMove(dx, dy);
    }

    /// #414 CA5.1 — el botón baja al pulsar y sube al soltar, con la misma
    /// duración: nunca un `click()` sintético de una sola vez.
    public void ButtonDown() => SendButton(NativeMethods.MOUSEEVENTF_LEFTDOWN);

    public void ButtonUp() => SendButton(NativeMethods.MOUSEEVENTF_LEFTUP);

    private static void SendRelativeMove(int dx, int dy)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new NativeMethods.INPUTUNION
            {
                mi = new NativeMethods.MOUSEINPUT
                {
                    dx = dx,
                    dy = dy,
                    dwFlags = NativeMethods.MOUSEEVENTF_MOVE,
                },
            },
        };
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
    }

    private static void SendButton(uint flag)
    {
        var input = new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_MOUSE,
            u = new NativeMethods.INPUTUNION
            {
                mi = new NativeMethods.MOUSEINPUT { dwFlags = flag },
            },
        };
        NativeMethods.SendInput(1, new[] { input }, Marshal.SizeOf<NativeMethods.INPUT>());
    }
}
