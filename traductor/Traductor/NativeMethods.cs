using System.Runtime.InteropServices;

namespace Traductor;

/// <summary>
/// Subconjunto de códigos de tecla virtual de Windows que nos importan. Los
/// valores son los VK_* estándar de la Win32 API (los mismos que
/// System.Windows.Forms.Keys usa por diseño), pero declarados aquí para no
/// cargar toda la referencia de WinForms en un programa sin ventanas.
/// </summary>
public enum Keys : uint
{
    Back = 0x08,
    Return = 0x0D,
    Escape = 0x1B,
    Space = 0x20,
    Left = 0x25,
    Up = 0x26,
    Right = 0x27,
    Down = 0x28,
    BrowserBack = 0xA6,
}

/// <summary>
/// P/Invoke a user32.dll: hook global de teclado (WH_KEYBOARD_LL), SendInput
/// para mover/clicar el mouse real, y el cambio de cursor del sistema.
/// Es deliberadamente la única clase con `unsafe`/interop del proyecto —
/// todo lo demás habla en tipos .NET normales.
/// </summary>
internal static class NativeMethods
{
    // ---- Hook de teclado de bajo nivel ----------------------------------

    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_KEYUP = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP = 0x0105;

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public nint dwExtraInfo;
    }

    public delegate nint LowLevelKeyboardProc(int nCode, nint wParam, nint lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, nint hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(nint hhk);

    [DllImport("user32.dll")]
    public static extern nint CallNextHookEx(nint hhk, int nCode, nint wParam, nint lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern nint GetModuleHandle(string? lpModuleName);

    // ---- SendInput: movimiento y clic reales del mouse ------------------

    public const int INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_MOVE = 0x0001;
    public const uint MOUSEEVENTF_ABSOLUTE = 0x8000;
    public const uint MOUSEEVENTF_VIRTUALDESK = 0x4000;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public nint dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUTUNION
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public INPUTUNION u;
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetCursorPos(int x, int y);

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
    }

    // ---- Apariencia del cursor del sistema ------------------------------

    public const uint OCR_NORMAL = 32512;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern nint LoadCursorFromFile(string lpFileName);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetSystemCursor(nint hcur, uint id);

    [DllImport("user32.dll")]
    public static extern nint CopyIcon(nint hIcon);

    public const uint SPI_SETCURSORS = 0x0057;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SystemParametersInfo(uint uiAction, uint uiParam, nint pvParam, uint fWinIni);
}
