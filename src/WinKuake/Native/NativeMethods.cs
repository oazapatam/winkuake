using System;
using System.Runtime.InteropServices;
using System.Text;

namespace WinKuake.Native;

/// <summary>
/// P/Invoke a Win32 utilizado para: hotkey global, manipulación de ventanas
/// (SetParent, SetWindowLong, MoveWindow), y enumeración de ventanas para
/// localizar el host de Windows Terminal a embeber.
/// </summary>
internal static class NativeMethods
{
    // -- Hotkey global -------------------------------------------------------
    public const int WM_HOTKEY = 0x0312;

    [Flags]
    public enum HotkeyModifiers : uint
    {
        None    = 0x0000,
        Alt     = 0x0001,
        Control = 0x0002,
        Shift   = 0x0004,
        Win     = 0x0008,
        NoRepeat = 0x4000
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    // -- Manipulación de ventanas -------------------------------------------
    public const int GWL_STYLE   = -16;
    public const int GWL_EXSTYLE = -20;

    public const uint WS_CAPTION     = 0x00C00000;
    public const uint WS_THICKFRAME  = 0x00040000;
    public const uint WS_MINIMIZE    = 0x20000000;
    public const uint WS_MAXIMIZE    = 0x01000000;
    public const uint WS_SYSMENU     = 0x00080000;
    public const uint WS_BORDER      = 0x00800000;
    public const uint WS_DLGFRAME    = 0x00400000;
    public const uint WS_CHILD       = 0x40000000;
    public const uint WS_VISIBLE     = 0x10000000;
    public const uint WS_POPUP       = 0x80000000;

    public const uint WS_EX_DLGMODALFRAME  = 0x00000001;
    public const uint WS_EX_CLIENTEDGE     = 0x00000200;
    public const uint WS_EX_STATICEDGE     = 0x00020000;
    public const uint WS_EX_WINDOWEDGE     = 0x00000100;
    public const uint WS_EX_TOOLWINDOW     = 0x00000080;
    public const uint WS_EX_NOACTIVATE     = 0x08000000;

    public const uint SWP_NOMOVE       = 0x0002;
    public const uint SWP_NOSIZE       = 0x0001;
    public const uint SWP_NOZORDER     = 0x0004;
    public const uint SWP_FRAMECHANGED = 0x0020;
    public const uint SWP_SHOWWINDOW   = 0x0040;
    public const uint SWP_NOACTIVATE   = 0x0010;

    public const int SW_HIDE        = 0;
    public const int SW_SHOWNORMAL  = 1;
    public const int SW_SHOWMAXIMIZED = 3;
    public const int SW_SHOWNA      = 8;
    public const int SW_SHOW        = 5;

    public static readonly IntPtr HWND_TOP    = new(0);
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll")]
    public static extern IntPtr GetParent(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLongPtr")]
    public static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowLong")]
    public static extern IntPtr GetWindowLongPtr32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLongPtr")]
    public static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowLong")]
    public static extern IntPtr SetWindowLongPtr32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    public static IntPtr GetWindowLongPtrSafe(IntPtr hWnd, int nIndex) =>
        IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : GetWindowLongPtr32(hWnd, nIndex);

    public static IntPtr SetWindowLongPtrSafe(IntPtr hWnd, int nIndex, IntPtr dwNewLong) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
            : SetWindowLongPtr32(hWnd, nIndex, dwNewLong);

    // -- Enumeración / búsqueda --------------------------------------------
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetFocus(IntPtr hWnd);

    // -- SendInput: sintetiza pulsaciones de teclado dirigidas al foco actual --
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint   dwFlags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    public const uint INPUT_KEYBOARD     = 1;
    public const uint KEYEVENTF_KEYDOWN  = 0x0000;
    public const uint KEYEVENTF_KEYUP    = 0x0002;

    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_MENU    = 0x12; // Alt
    public const ushort VK_SHIFT   = 0x10;
    public const ushort VK_LWIN    = 0x5B;
    public const ushort VK_TAB     = 0x09;
    public const ushort VK_OEM_PLUS  = 0xBB;
    public const ushort VK_OEM_COMMA = 0xBC;
    public const ushort VK_OEM_MINUS = 0xBD;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    // -- Low-level keyboard hook (WH_KEYBOARD_LL) ---------------------------
    // Permite interceptar F12 (u otra tecla) ANTES de que llegue al receptor
    // que la tenga registrada vía RegisterHotKey. Es como las utilidades OEM
    // (FnHotkeyUtility de Lenovo, GeForce Overlay, etc.) se "comen" teclas.
    public const int WH_KEYBOARD_LL = 13;

    public const int WM_KEYDOWN    = 0x0100;
    public const int WM_KEYUP      = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP   = 0x0105;
    public const uint WM_CLOSE     = 0x0010;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint   vkCode;
        public uint   scanCode;
        public uint   flags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn,
                                                  IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);

    public const ushort VK_RWIN = 0x5C;

    // -- Ayudantes de cadenas -----------------------------------------------
    public static string GetWindowTitle(IntPtr hWnd)
    {
        var len = GetWindowTextLength(hWnd);
        if (len <= 0) return string.Empty;
        var sb = new StringBuilder(len + 1);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static string GetWindowClassName(IntPtr hWnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }
}
