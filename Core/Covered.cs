using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PitzkiWidgets.Core;

/// <summary>
/// The cards live under every other window. When a window fills a whole screen (maximised, or a game in
/// full screen) nobody can see the cards on that screen, so they stop drawing until it moves away:
/// nothing changes for the cards themselves, they just cost nothing while they are hidden.
/// Looked at every 1.5 s, which is a single pass over the open windows.
/// </summary>
public static class Covered
{
    /// <summary>The covered screens changed.</summary>
    public static event Action? Changed;

    /// <summary>For the tests: true covers every screen, false none, null looks for real.</summary>
    public static bool? ForTest;

    /// <summary>For the tests and the log: the windows that cover a screen right now.</summary>
    public static string Why { get; private set; } = "";

    static readonly HashSet<IntPtr> CoveredScreens = new();
    static DispatcherTimer? _clock;

    public static void Start()
    {
        if (_clock != null) return;
        // PITZKI_WIDGETS_COVER=1 / 0 pretends every screen is / none is covered, for measuring
        var pretend = Environment.GetEnvironmentVariable("PITZKI_WIDGETS_COVER");
        if (pretend == "1") ForTest = true;
        else if (pretend == "0") ForTest = false;
        if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_COVERLOG") is { Length: > 0 } log)
            Changed += () => { try { System.IO.File.AppendAllText(log, $"{DateTime.Now:HH:mm:ss} {CoveredScreens.Count} covered: {Why}\n"); } catch { } };
        _clock = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1.5) };
        _clock.Tick += (_, _) => Look();
        _clock.Start();
        Look();      // at once, so a card that starts under a full-screen game never draws at all
    }

    /// <summary>True when the screen this window is on has a window filling it.</summary>
    public static bool Hides(Window window)
    {
        if (ForTest is { } all) return all;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return false;
        return CoveredScreens.Contains(MonitorFromWindow(handle, MonitorDefaultToNearest));
    }

    /// <summary>Works out which screens are covered now, and says so when that changed.</summary>
    public static void Look()
    {
        var now = new HashSet<IntPtr>();
        var why = new List<string>();
        try
        {
            uint mine = (uint)Environment.ProcessId;
            EnumWindows((window, _) =>
            {
                if (!IsWindowVisible(window) || IsIconic(window)) return true;
                GetWindowThreadProcessId(window, out uint pid);
                if (pid == mine) return true;
                int ex = GetWindowLong(window, GwlExStyle);
                if ((ex & (ExToolWindow | ExTransparent)) != 0) return true;   // overlays and click-through layers
                if (DwmGetWindowAttribute(window, DwmCloaked, out int cloaked, sizeof(int)) == 0 && cloaked != 0) return true; // on another virtual desktop, or a suspended store app
                string kind = ClassOf(window);
                if (kind is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return true;
                if (!GetWindowRect(window, out var box)) return true;
                var screen = MonitorFromWindow(window, MonitorDefaultToNearest);
                if (screen == IntPtr.Zero || now.Contains(screen)) return true;
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (!GetMonitorInfo(screen, ref info)) return true;
                var room = info.Work;
                const int slack = 2;
                if (box.Left <= room.Left + slack && box.Top <= room.Top + slack && box.Right >= room.Right - slack && box.Bottom >= room.Bottom - slack)
                {
                    now.Add(screen);
                    why.Add($"{kind} '{TitleOf(window)}'");
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { return; }

        Why = string.Join("; ", why);
        if (now.SetEquals(CoveredScreens)) return;
        CoveredScreens.Clear();
        CoveredScreens.UnionWith(now);
        Changed?.Invoke();
    }

    static string ClassOf(IntPtr window)
    {
        var text = new StringBuilder(128);
        GetClassName(window, text, text.Capacity);
        return text.ToString();
    }

    static string TitleOf(IntPtr window)
    {
        var text = new StringBuilder(80);
        GetWindowText(window, text, text.Capacity);
        return text.ToString();
    }

    const int GwlExStyle = -20, ExToolWindow = 0x80, ExTransparent = 0x20, DwmCloaked = 14;
    const uint MonitorDefaultToNearest = 2;

    [StructLayout(LayoutKind.Sequential)] struct Box { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo { public int Size; public Box Monitor, Work; public uint Flags; }
    delegate bool EnumProc(IntPtr window, IntPtr data);

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr window, int index);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window, out Box box);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr window, StringBuilder text, int max);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr window, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr screen, ref MonitorInfo info);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
