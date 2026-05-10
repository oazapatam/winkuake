using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Interop;
using WinKuake.Models;
using WinKuake.Native;

namespace WinKuake.Services;

/// <summary>
/// Hotkey global con dos estrategias en cascada:
///  1. <c>RegisterHotKey</c> (mecanismo limpio de Win32, baja latencia).
///  2. Si <c>RegisterHotKey</c> falla con <c>ERROR_HOTKEY_ALREADY_REGISTERED</c>
///     o si la tecla está siendo "comida" por un low-level hook ajeno
///     (utilidades OEM tipo FnHotkeyUtility), se instala un
///     <c>WH_KEYBOARD_LL</c> propio. Los hooks recientes corren primero (LIFO),
///     así que WinKuake recibe la tecla antes que la utilidad OEM y la
///     "consume" devolviendo 1 desde el procedimiento del hook.
/// </summary>
public sealed class HotkeyService : IDisposable
{
    private const int HotkeyId = 0xBEE1;

    private HwndSource? _source;
    private IntPtr _hwnd;
    private bool _registered;

    private NativeMethods.LowLevelKeyboardProc? _hookProc;
    private IntPtr _hookHandle = IntPtr.Zero;
    private uint _hookVk;
    private NativeMethods.HotkeyModifiers _hookModifiers;

    public event Action? HotkeyPressed;

    /// <summary>True si la captura del hotkey está activa por el camino que sea.</summary>
    public bool IsActive => _registered || _hookHandle != IntPtr.Zero;

    /// <summary>True si tuvimos que recurrir al hook porque RegisterHotKey falló.</summary>
    public bool UsingLowLevelHook => _hookHandle != IntPtr.Zero;

    public bool TryRegister(IntPtr hwnd, AppSettings settings, out string? error)
    {
        error = null;
        _hwnd = hwnd;

        var mods = ParseModifiers(settings.HotkeyModifiers);
        var vk = ParseVk(settings.HotkeyKey);
        if (vk == 0)
        {
            error = $"Tecla desconocida: '{settings.HotkeyKey}'";
            return false;
        }

        _source = HwndSource.FromHwnd(hwnd);
        _source?.AddHook(WndProc);

        // Camino 1: RegisterHotKey (rápido, no afecta a otras teclas).
        if (NativeMethods.RegisterHotKey(hwnd, HotkeyId,
                (uint)(mods | NativeMethods.HotkeyModifiers.NoRepeat), (uint)vk))
        {
            _registered = true;
            return true;
        }

        // Camino 2: hook de bajo nivel — fuerza la captura aunque otra app la
        // tenga registrada o un hook OEM la esté interceptando.
        var win32 = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        if (TryInstallHook((uint)vk, mods))
        {
            var culprit = DetectLikelyCulprit();
            error = $"RegisterHotKey falló (Win32 {win32}). " +
                    $"Captura forzada vía low-level hook" +
                    (culprit is null ? "." : $" (probable bloqueador: {culprit}).");
            return true; // sí, true: el hotkey funciona aunque haya una nota.
        }

        error = $"No se pudo capturar {settings.HotkeyKey} ni con RegisterHotKey ni con hook (Win32 {win32}).";
        return false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == NativeMethods.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // -- Hook -------------------------------------------------------------

    private bool TryInstallHook(uint vk, NativeMethods.HotkeyModifiers mods)
    {
        _hookVk = vk;
        _hookModifiers = mods;
        // Mantenemos referencia al delegate para evitar que el GC lo recoja
        // mientras el OS lo tenga registrado.
        _hookProc = HookProc;

        var hModule = NativeMethods.GetModuleHandle(null);
        _hookHandle = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL, _hookProc, hModule, 0);

        return _hookHandle != IntPtr.Zero;
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var msg = wParam.ToInt32();
            if (msg == NativeMethods.WM_KEYDOWN || msg == NativeMethods.WM_SYSKEYDOWN)
            {
                var data = System.Runtime.InteropServices.Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                if (data.vkCode == _hookVk && ModifiersMatch(_hookModifiers))
                {
                    // No invocamos directo: el callback del hook corre en un
                    // contexto input-sync que prohíbe llamadas COM out
                    // (RPC_E_CANTCALLOUT_ININPUTSYNCCALL). Posponemos al
                    // dispatcher de la app para salir de ese contexto.
                    System.Windows.Application.Current?.Dispatcher
                        .BeginInvoke(new Action(() => HotkeyPressed?.Invoke()));
                    // Devolvemos 1 para "consumir" la tecla — ninguna otra app la verá.
                    return new IntPtr(1);
                }
            }
        }
        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private static bool ModifiersMatch(NativeMethods.HotkeyModifiers required)
    {
        bool ctrl  = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_CONTROL) & 0x8000) != 0;
        bool alt   = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_MENU)    & 0x8000) != 0;
        bool shift = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_SHIFT)   & 0x8000) != 0;
        bool win   = (NativeMethods.GetAsyncKeyState(NativeMethods.VK_LWIN)    & 0x8000) != 0
                  || (NativeMethods.GetAsyncKeyState(NativeMethods.VK_RWIN)    & 0x8000) != 0;

        // Comparación EXACTA: si el hotkey es F12 plano, no debe disparar con Ctrl+F12.
        return ctrl  == required.HasFlag(NativeMethods.HotkeyModifiers.Control) &&
               alt   == required.HasFlag(NativeMethods.HotkeyModifiers.Alt)     &&
               shift == required.HasFlag(NativeMethods.HotkeyModifiers.Shift)   &&
               win   == required.HasFlag(NativeMethods.HotkeyModifiers.Win);
    }

    // -- Parsing ----------------------------------------------------------

    private static NativeMethods.HotkeyModifiers ParseModifiers(IEnumerable<string> mods)
    {
        var result = NativeMethods.HotkeyModifiers.None;
        foreach (var m in mods)
        {
            switch (m.Trim().ToLowerInvariant())
            {
                case "ctrl": case "control": result |= NativeMethods.HotkeyModifiers.Control; break;
                case "alt":                  result |= NativeMethods.HotkeyModifiers.Alt;     break;
                case "shift":                result |= NativeMethods.HotkeyModifiers.Shift;   break;
                case "win": case "windows":  result |= NativeMethods.HotkeyModifiers.Win;     break;
            }
        }
        return result;
    }

    private static int ParseVk(string keyName)
    {
        if (Enum.TryParse<Key>(keyName, ignoreCase: true, out var key))
            return KeyInterop.VirtualKeyFromKey(key);
        return 0;
    }

    // -- Detección heurística del bloqueador -------------------------------

    private static readonly string[] KnownCulprits = new[]
    {
        "FnHotkeyUtility", "FnHotkeyCapsLKNumLK",   // Lenovo
        "NVIDIA Share", "GFExperience",              // NVIDIA
        "obs64", "obs32",                            // OBS recording
        "ShareX", "Greenshot",                       // screenshots
        "AutoHotkey",                                // scripting
        "LGHUB", "LGHUB Agent", "SetPoint",          // Logitech
        "Razer Synapse",                             // Razer
        "SteelSeriesGG", "SteelSeriesEngine"         // SteelSeries
    };

    /// <summary>Escanea procesos y devuelve el primer sospechoso de capturar la tecla, si lo hay.</summary>
    public static string? DetectLikelyCulprit()
    {
        try
        {
            var procs = Process.GetProcesses().Select(p => p.ProcessName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            return KnownCulprits.FirstOrDefault(c => procs.Contains(c));
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_registered)
        {
            NativeMethods.UnregisterHotKey(_hwnd, HotkeyId);
            _registered = false;
        }
        if (_hookHandle != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookHandle);
            _hookHandle = IntPtr.Zero;
            _hookProc = null;
        }
        _source?.RemoveHook(WndProc);
        _source = null;
    }
}
