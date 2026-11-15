using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace PitzkiWidgets.Core;

/// <summary>
/// Makes a window live on the desktop: behind every other window, out of the taskbar and out of
/// Alt-Tab, and never stealing the focus when it is clicked.
///
/// Windows has no proper place for this, so it is done the way desktop widgets have always been
/// done: the window is pushed to the bottom of the stack and put back there whenever Windows tries
/// to raise it. It is a tool window, so "show desktop" and minimising everything leave it where it
/// is rather than sweeping it away with the rest.
/// </summary>
public static class Desk
{
    /// <summary>For a real-mouse test only: the cards float above everything, so the test never clicks the windows behind them.</summary>
    public static bool OnTopForTest => Environment.GetEnvironmentVariable("PITZKI_WIDGETS_TOP") == "1";

    const int GwlExStyle = -20;
    const int ExToolWindow = 0x00000080, ExNoActivate = 0x08000000;
    const uint SwpNoSize = 0x0001, SwpNoMove = 0x0002, SwpNoActivate = 0x0010;
    const int WmWindowPosChanging = 0x0046;
    static readonly IntPtr Bottom = new(1);

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr window, int index, int value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int cx, int cy, uint flags);

    /// <summary>What Windows passes with WM_WINDOWPOSCHANGING, so the window can be put back at the bottom.</summary>
    [StructLayout(LayoutKind.Sequential)]
    struct WindowPos
    {
        public IntPtr Window, After;
        public int X, Y, Width, Height;
        public uint Flags;
    }

    /// <summary>Sends the window under every other one, cards included.</summary>
    public static void ToBottom(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (OnTopForTest) return;
        if (handle != IntPtr.Zero) SetWindowPos(handle, Bottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);
    }

    /// <summary>A frame that now holds a card you type into has to be allowed the focus; one that does not, never takes it.</summary>
    public static void Typing(Window window, bool typedInto)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        int style = GetWindowLong(handle, GwlExStyle);
        style = typedInto ? style & ~ExNoActivate : style | ExNoActivate;
        SetWindowLong(handle, GwlExStyle, style);
    }

    /// <summary>
    /// Call once the window has a handle (on SourceInitialized or later). A card that is typed into
    /// has to be allowed the focus, so it keeps the bottom of the stack but not the no-focus rule.
    /// </summary>
    public static void KeepOnDesktop(Window window, bool typedInto = false)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        if (OnTopForTest) { window.Topmost = true; SetWindowLong(handle, GwlExStyle, GetWindowLong(handle, GwlExStyle) | ExToolWindow | (typedInto ? 0 : ExNoActivate)); return; }
        int style = GetWindowLong(handle, GwlExStyle) | ExToolWindow;
        if (!typedInto) style |= ExNoActivate;
        SetWindowLong(handle, GwlExStyle, style);
        SetWindowPos(handle, Bottom, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);

        // every time Windows wants to raise it, it is asked to stay at the bottom instead
        HwndSource.FromHwnd(handle)?.AddHook((IntPtr _, int message, IntPtr _, IntPtr lParam, ref bool _) =>
        {
            if (message != WmWindowPosChanging) return IntPtr.Zero;
            var place = Marshal.PtrToStructure<WindowPos>(lParam);
            place.After = Bottom;
            Marshal.StructureToPtr(place, lParam, false);
            return IntPtr.Zero;
        });
    }
}
