using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Threading;
using Pitzki.Core;

namespace PitzkiWidgets.Core;

/// <summary>
/// Puts the Game profile on while a game is running, and the profile that was on before back once the
/// game closes. A game is a program with a window that lives where a game store keeps its games
/// (the same list Pitzki uses), or Roblox. A launcher on its own is not a game.
/// </summary>
public sealed class AutoGame
{
    [DllImport("kernel32.dll")] static extern IntPtr OpenProcess(uint access, bool inherit, uint id);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, uint flags, StringBuilder path, ref int size);
    const uint QueryLimited = 0x1000;     // works even for games an anti-cheat protects

    static readonly HashSet<string> Launchers = new(StringComparer.OrdinalIgnoreCase)
    {
        "EpicGamesLauncher", "EpicWebHelper", "Battle.net", "RiotClientServices", "RiotClientUx", "EADesktop", "Origin",
        "UbisoftConnect", "upc", "GalaxyClient", "steamwebhelper", "CrashReportClient", "UnityCrashHandler64",
    };
    static readonly string[] Always = { "RobloxPlayerBeta", "ROB RUN", "Minecraft", "javaw" };

    readonly App _app;
    string _before = "";
    bool _switched;
    int _quiet;

    /// <summary>The game seen at the last look, or "" when none was running.</summary>
    public string Playing { get; private set; } = "";

    public AutoGame(App app)
    {
        _app = app;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        timer.Tick += (_, _) => Poll();
        timer.Start();
    }

    internal void Poll()
    {
        Playing = App.Board.AutoGame ? Find() : "";
        if (Playing.Length > 0)
        {
            _quiet = 0;
            if (_switched || App.Board.Profile == "Game") return;
            _before = App.Board.Profile;
            _switched = true;
            _app.SetProfile("Game");
            return;
        }

        // a loading screen can drop the window for a moment: wait for two quiet looks before switching back
        if (!_switched || ++_quiet < 2) return;
        _switched = false;
        if (App.Board.Profile == "Game") _app.SetProfile(_before);
    }

    /// <summary>The name of a running game, or "".</summary>
    internal static string Find()
    {
        // the app and the Pitzki card both ask every few seconds: one look serves both
        if (Environment.TickCount64 - _foundAt < 4000) return _found;
        _found = Look();
        _foundAt = Environment.TickCount64;
        return _found;
    }

    static string _found = "";
    static long _foundAt = long.MinValue / 2;

    /// <summary>
    /// One pass over the open windows gives the programs that have one; only those are looked at.
    /// (Asking every process for its main window walks every window once per process, which is slow.)
    /// </summary>
    static string Look()
    {
        var seen = new HashSet<uint>();
        try
        {
            EnumWindows((window, _) =>
            {
                if (IsWindowVisible(window) && GetWindow(window, GwOwner) == IntPtr.Zero)
                {
                    GetWindowThreadProcessId(window, out uint pid);
                    seen.Add(pid);
                }
                return true;
            }, IntPtr.Zero);
        }
        catch { return ""; }

        foreach (uint pid in seen)
        {
            try
            {
                string exe = PathOf(pid);
                if (IsGame(exe)) return Path.GetFileNameWithoutExtension(exe);
            }
            catch { }
        }
        return "";
    }

    delegate bool EnumProc(IntPtr window, IntPtr data);
    const uint GwOwner = 4;
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr data);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr window, uint relation);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);

    /// <summary>Whether the program at that path is a game rather than a launcher or anything else.</summary>
    internal static bool IsGame(string exe)
    {
        if (exe.Length == 0) return false;
        string program = Path.GetFileNameWithoutExtension(exe);
        if (Launchers.Contains(program) || exe.Contains(@"\Launcher\", StringComparison.OrdinalIgnoreCase)) return false;
        if (program.Equals("javaw", StringComparison.OrdinalIgnoreCase) && !exe.Contains("minecraft", StringComparison.OrdinalIgnoreCase)) return false;
        return Always.Any(a => program.Equals(a, StringComparison.OrdinalIgnoreCase)) || GameWatch.InGameLibrary(exe);
    }

    static string PathOf(uint pid)
    {
        IntPtr handle = OpenProcess(QueryLimited, false, pid);
        if (handle == IntPtr.Zero) return "";
        try
        {
            var path = new StringBuilder(1024);
            int size = path.Capacity;
            return QueryFullProcessImageName(handle, 0, path, ref size) ? path.ToString() : "";
        }
        finally { CloseHandle(handle); }
    }
}
