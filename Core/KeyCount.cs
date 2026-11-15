using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PitzkiWidgets.Core;

/// <summary>
/// Counts how many keys were pressed in the last few seconds, so Pitzki can type along when someone
/// types fast. Only the moment of each press is kept (for a few seconds): never which key, never any
/// text. Windows' low-level keyboard hook on the UI thread; the callback does nothing but note the time.
/// </summary>
public static class KeyCount
{
    const int WhKeyboardLl = 13, WmKeyDown = 0x100, WmSysKeyDown = 0x104;
    delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern IntPtr GetModuleHandle(string? name);

    static IntPtr _hook;
    static HookProc? _proc; // kept alive: the hook calls it for as long as the app runs
    static readonly Queue<long> _presses = new();

    /// <summary>How many times Print Screen has been pressed (the one key whose identity is looked at).</summary>
    public static int PrintScreens;

    /// <summary>For the self-test: pretend this many presses happened in the window asked about.</summary>
    internal static int? Pretend;

    public static void Start()
    {
        if (_hook != IntPtr.Zero) return;
        _proc = (code, wParam, lParam) =>
        {
            if (code >= 0 && (wParam == WmKeyDown || wParam == WmSysKeyDown))
            {
                _presses.Enqueue(Stopwatch.GetTimestamp());
                if (Marshal.ReadInt32(lParam) == 0x2C) PrintScreens++; // VK_SNAPSHOT
                while (_presses.Count > 200) _presses.Dequeue();
            }
            return CallNextHookEx(_hook, code, wParam, lParam);
        };
        _hook = SetWindowsHookEx(WhKeyboardLl, _proc, GetModuleHandle(null), 0);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => { if (_hook != IntPtr.Zero) UnhookWindowsHookEx(_hook); };
    }

    /// <summary>Key presses in the last <paramref name="seconds"/>.</summary>
    public static int Recent(double seconds)
    {
        if (Pretend is { } p) return p;
        long since = Stopwatch.GetTimestamp() - (long)(seconds * Stopwatch.Frequency);
        while (_presses.Count > 0 && _presses.Peek() < since) _presses.Dequeue();
        return _presses.Count;
    }
}
