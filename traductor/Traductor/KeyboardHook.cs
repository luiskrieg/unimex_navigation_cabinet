using System.Runtime.InteropServices;

namespace Traductor;

/// <summary>
/// Hook global WH_KEYBOARD_LL. No decide nada de producto: solo reporta
/// pulsaciones (avisando si ya estaban abajo, o sea si son auto-repetición
/// del sistema operativo) y deja que el llamador (<see cref="GameModeController"/>)
/// decida, tecla por tecla, si debe tragarse —no llegar a Chrome— o seguir su
/// camino normal.
///
/// La detección de "auto-repetición" es propia: KBDLLHOOKSTRUCT no trae el
/// bit de repetición del WM_KEYDOWN clásico, así que se infiere llevando el
/// propio estado de qué teclas están abajo. Es justo lo que hace falta para
/// #414 CA5.1/5.2 — no confundir "mantener pulsado" con una ráfaga de clics.
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private static readonly HashSet<uint> KnownKeys =
        new(Enum.GetValues<Keys>().Select(k => (uint)k));

    private readonly NativeMethods.LowLevelKeyboardProc _proc;
    private readonly HashSet<Keys> _down = new();
    private nint _hookHandle;

    /// El llamador decide si esta tecla debe tragarse (true) o seguir su
    /// camino normal a la ventana activa (false). Si es null, nunca traga.
    public Func<Keys, bool>? ShouldSwallow { get; set; }

    /// (tecla, esAutoRepeticion).
    public event Action<Keys, bool>? KeyDown;
    public event Action<Keys>? KeyUp;

    public KeyboardHook()
    {
        // Referencia guardada a propósito: si el GC recoge el delegado,
        // Windows llama a un puntero muerto y el proceso truena.
        _proc = HookCallback;
    }

    public void Install()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _proc,
            NativeMethods.GetModuleHandle(module.ModuleName),
            0);

        if (_hookHandle == 0)
        {
            throw new InvalidOperationException(
                "SetWindowsHookEx falló instalando el hook de teclado — revisar permisos o antivirus (R-Gab4).");
        }
    }

    public void Uninstall()
    {
        if (_hookHandle != 0)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = 0;
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            var isKeyDown = wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN;
            var isKeyUp = wParam == NativeMethods.WM_KEYUP || wParam == NativeMethods.WM_SYSKEYUP;

            if (isKeyDown || isKeyUp)
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                if (KnownKeys.Contains(data.vkCode))
                {
                    var key = (Keys)data.vkCode;
                    var swallow = ShouldSwallow?.Invoke(key) ?? false;

                    if (isKeyDown)
                    {
                        var isRepeat = _down.Contains(key);
                        _down.Add(key);
                        KeyDown?.Invoke(key, isRepeat);
                    }
                    else
                    {
                        _down.Remove(key);
                        KeyUp?.Invoke(key);
                    }

                    if (swallow)
                    {
                        // No se llama a CallNextHookEx: la tecla no llega a Chrome.
                        return 1;
                    }
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Uninstall();
}
