using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Pitzki.Controls;

namespace PitzkiWidgets.Core;

/// <summary>
/// Pitzki out of his card: a small see-through window of his own, always on top, that walks him along the
/// bottom of the screens (from one screen to the other), climbs up the sides of other windows and sits on
/// their tops, and pesters the other cards. When he is done he whistles, a rope drops from his card, he
/// grabs it and is pulled back in. Other programs' windows are only looked at (where they are), never
/// moved or touched; the cards he tugs are put back exactly where they were.
/// Everything is driven from one async script; a click on him, a game or a full-screen window starting,
/// or the card going away, sends him home early.
/// </summary>
public sealed class Roam
{
    // the little window: the mascot stands at the bottom middle; his feet are at Feet inside it
    const double WinW = 150, WinH = 124;
    static readonly Point Feet = new(75, 102);

    readonly Window _card;
    readonly Mascot _him = new() { Width = 104, Height = 86, Small = true };
    readonly Window _window;
    readonly Random _rng = new();
    double _scale = 1;
    Point _at;                 // his feet, in screen DIPs
    bool _home, _clicked;
    readonly List<string> _log = new();

    /// <summary>True while any Pitzki is out walking (only one at a time).</summary>
    public static bool Out { get; private set; }

    /// <summary>What he did, step by step (for the self-test).</summary>
    public static IReadOnlyList<string> LastLog { get; private set; } = Array.Empty<string>();

    /// <summary>For the self-test: a faster walk, and fewer stops.</summary>
    internal static double SpeedForTest = 1;

    Roam(Window card)
    {
        _card = card;
        var grid = new Grid { Width = WinW, Height = WinH, Background = Brushes.Transparent };
        grid.Children.Add(new Border { Width = 104, Height = 86, VerticalAlignment = VerticalAlignment.Top, HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(Feet.X - 52, Feet.Y - 64, 0, 0), Child = _him, Background = Brushes.Transparent });
        _window = new Window
        {
            WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false, ShowActivated = false, Topmost = true, Width = WinW, Height = WinH, Content = grid, Focusable = false,
        };
        // only his own pixels catch the mouse: a click on him sends him home
        grid.Background = null;
        _window.PreviewMouseLeftButtonUp += (_, _) => { if (!_clicked) { _clicked = true; _him.Speak("ok, ok!"); } };
        _window.SourceInitialized += (_, _) => NoActivate(new System.Windows.Interop.WindowInteropHelper(_window).Handle);
    }

    /// <summary>
    /// Sends the Pitzki of this card out for a walk. <paramref name="hide"/> and <paramref name="show"/> hide the
    /// card's own Pitzki while he is away and bring him back. Returns when he is home again.
    /// </summary>
    public static async Task Go(Window card, Action hide, Action show, Func<bool> stop)
    {
        if (Out) return;
        Out = true;
        var roam = new Roam(card);
        try { await roam.Run(hide, show, stop); }
        catch (Exception bad) { roam._log.Add("broke: " + bad.Message); }
        finally
        {
            roam._window.Close();
            show();
            Out = false;
            LastLog = roam._log.ToList();
        }
    }

    // ---------- the walk ----------

    async Task Run(Action hide, Action show, Func<bool> stop)
    {
        var card = CardRect();
        _window.Left = card.X + card.Width / 2 - Feet.X;
        _window.Top = card.Y + card.Height / 2 + 20 - Feet.Y;
        _window.Show();
        _scale = VisualTreeHelper.GetDpi(_window).DpiScaleX;
        await Frames(3);          // let him start up first, or his own idle float would take the pose back
        _him.Puppet = true;
        hide();

        // out of the card with a jump, down to the ground under it
        _at = new Point(card.X + card.Width / 2, card.Y + card.Height / 2 + 20);
        Place();
        _him.Speak("wheee!");
        _log.Add("out of the card");
        await Jump(new Point(_at.X, Ground(_at.X)), 0.9);

        // a few places to visit: windows to climb and sit on, cards to bother, the other screen
        var visits = new List<Func<Task>>();
        foreach (var top in TopsOfWindows().OrderBy(_ => _rng.Next()).Take(2)) visits.Add(() => VisitWindow(top));
        foreach (var other in OtherCards().OrderBy(_ => _rng.Next()).Take(2)) visits.Add(() => VisitCard(other));
        if (Screens().Count > 1) visits.Add(VisitOtherScreen);
        foreach (var visit in visits.OrderBy(_ => _rng.Next()).Take(SpeedForTest > 1 ? 3 : 4))
        {
            if (Stopping(stop)) break;
            await visit();
        }

        // home: under the card, a whistle, the rope, and up he goes
        await GoHome(stop);
    }

    bool Stopping(Func<bool> stop)
    {
        if (_clicked || stop() || !_card.IsVisible) { if (!_home) _log.Add("called home early"); _home = true; }
        return _home;
    }

    async Task VisitWindow(Rect top)
    {
        _log.Add($"to a window at {top.X:0},{top.Y:0} ({top.Width:0} wide)");
        await ClimbOnto(top);
        if (_home) return;
        await Sit(3.4 / SpeedForTest, new[] { "nice view!", "hello up here!", "comfy", "what's in here?" });
        await Knock();
        await JumpDown();
    }

    async Task VisitCard(WidgetWindow other)
    {
        var r = other.Seen();
        _log.Add($"to a card ({other.Placed.Kind})");
        if (_rng.Next(2) == 0)
        {
            await ClimbOnto(new Rect(r.X, r.Y, r.Width, 0));
            if (_home) return;
            await Sit(2.4 / SpeedForTest, new[] { "my card now", "hehe" });
            await Tug(other);
            await JumpDown();
        }
        else
        {
            // stand beside it and give it a shove (it wobbles, and settles back where it was)
            await WalkTo(r.X - 30 < 0 ? r.Right + 30 : r.X - 30);
            await Tug(other);
        }
    }

    async Task VisitOtherScreen()
    {
        var screens = Screens();
        var here = screens.FirstOrDefault(s => s.Contains(new Point(_at.X, _at.Y - 5)), screens[0]);
        var other = screens.First(s => s != here);
        double x = other.X + other.Width * (0.25 + 0.5 * _rng.NextDouble());
        _log.Add("to the other screen");
        await WalkTo(x);
        _him.Speak(_rng.Next(2) == 0 ? "new screen!" : "ooh, what's here?");
        await Wait(1.2);
    }

    async Task GoHome(Func<bool> stop)
    {
        _home = true;
        var card = CardRect();
        if (Math.Abs(_at.Y - Ground(_at.X)) > 2) await JumpDown();
        double x = card.X + card.Width / 2;
        await WalkTo(x);
        _log.Add("whistle");
        _him.Expression("O", 0.4);
        _him.Speak("♪ fweeet!", 9);
        await Wait(1.0);

        // the rope comes down from the bottom of the card to just over his head
        double from = card.Bottom - 4, to = _at.Y - 70;
        var rope = new Rope(x, from, Math.Max(to, from + 10));
        rope.Show();
        _log.Add("rope");
        await Animate(0.7, k => rope.Length(k));
        _him.Expression("grin", 0.4);
        // grab it, and be pulled up into the card
        await Animate(0.3, k => _him.Pose(10 * k, -26 * k, -10 * k, -26 * k, 0, 0));
        double start = _at.Y, end = card.Y + card.Height / 2 + 20;
        await Animate(Math.Max(0.6, (start - end) / 420), k =>
        {
            _at = new Point(x, Lerp(start, end, k * k));
            Place();
            rope.Length(1, _at.Y - 70);
            _him.Pose(10, -26 + 3 * Math.Sin(k * 20), -10, -26 - 3 * Math.Sin(k * 20), 0, 0);
        });
        rope.Close();
        _log.Add("home");
    }

    // ---------- moves ----------

    async Task WalkTo(double x)
    {
        double y = _at.Y, startX = _at.X, distance = Math.Abs(x - startX);
        if (distance < 2) return;
        double dir = Math.Sign(x - startX);
        _him.LookAt(dir, 0);
        _him.Expression("grin");
        double seconds = distance / (150 * SpeedForTest);
        await Animate(seconds, k =>
        {
            double step = k * distance / 22;
            _at = new Point(Lerp(startX, x, k), y);
            Place();
            _him.Pose(-3 * Math.Sin(step * Math.PI), 2 * Math.Abs(Math.Cos(step * Math.PI)), 3 * Math.Sin(step * Math.PI), 2 * Math.Abs(Math.Sin(step * Math.PI)),
                4 * dir, -3 * Math.Abs(Math.Sin(step * Math.PI)));
        });
        _him.Pose(0, 0, 0, 0, 0, 0);
    }

    async Task Climb(double toY)
    {
        double startY = _at.Y, distance = Math.Abs(toY - startY);
        if (distance < 2) return;
        _him.LookAt(0, -1);
        _him.Expression("flat", 0.8);
        await Animate(distance / (100 * SpeedForTest), k =>
        {
            double step = k * distance / 14;
            _at = new Point(_at.X, Lerp(startY, toY, k));
            Place();
            double a = Math.Sin(step * Math.PI);
            _him.Pose(4, -14 + 8 * a, -4, -14 - 8 * a, 3 * a, 0);
        });
    }

    async Task Jump(Point to, double seconds)
    {
        var from = _at;
        double peak = Math.Min(from.Y, to.Y) - 50;
        _him.Expression("O", 1.2);
        await Animate(seconds / SpeedForTest, k =>
        {
            double y = (1 - k) * (1 - k) * from.Y + 2 * (1 - k) * k * (2 * peak - (from.Y + to.Y) / 2) + k * k * to.Y;
            _at = new Point(Lerp(from.X, to.X, k), y);
            Place();
            _him.Pose(-4, -14, 4, -14, 0, 0);
        });
        _him.Pose(0, 0, 0, 0, 0, 0);
        _him.Expression("grin");
        await Animate(0.25, k => _him.Pose(0, 3 * Math.Sin(Math.PI * k), 0, 3 * Math.Sin(Math.PI * k), 0, 3 * Math.Sin(Math.PI * k)));
    }

    async Task JumpDown()
    {
        double x = _at.X + (_rng.Next(2) == 0 ? -40 : 40);
        await Jump(new Point(x, Ground(x)), 0.8);
    }

    /// <summary>From the ground up onto the top edge of a rectangle: walk to its side, hop up to its bottom
    /// corner if it floats above the ground, climb the side, and step onto the top.</summary>
    async Task ClimbOnto(Rect top)
    {
        if (Math.Abs(_at.Y - Ground(_at.X)) > 2) await JumpDown();
        bool left = Math.Abs(_at.X - top.X) < Math.Abs(_at.X - top.Right);
        double sideX = left ? top.X - 6 : top.Right + 6;
        await WalkTo(sideX);
        if (_home) return;
        double ground = Ground(sideX);
        double bottom = top.Bottom > top.Y + 1 ? Math.Min(top.Bottom, ground) : ground;
        if (bottom < ground - 20) await Jump(new Point(sideX, bottom), 0.6);
        await Climb(top.Y);
        await Jump(new Point(left ? top.X + 30 : top.Right - 30, top.Y), 0.4);
        await WalkTo(top.X + top.Width * (0.3 + 0.4 * _rng.NextDouble()));
    }

    async Task Sit(double seconds, string[] lines)
    {
        _him.Speak(lines[_rng.Next(lines.Length)], 7);
        _him.Expression("grin", 0.5);
        double swing = _rng.NextDouble() * 6;
        await Animate(seconds, k =>
        {
            _him.Pose(-6, 6, 6, 6, 3 * Math.Sin(k * 9 + swing), 4);
            if ((int)(k * 4) % 2 == 0) _him.LookAt(Math.Sin(k * 5), 0.6); else _him.LookAt(-0.6, 0.3);
        });
        _him.Pose(0, 0, 0, 0, 0, 0);
    }

    async Task Knock()
    {
        _him.Speak("knock knock!", 7);
        _him.Expression("small");
        await Animate(1.2, k => _him.Pose(0, 0, -6 + 4 * Math.Max(0, Math.Sin(k * 30)), 10, 0, 0));
        _him.Pose(0, 0, 0, 0, 0, 0);
    }

    /// <summary>Gives a card a wobble, then puts it back exactly where it was (nothing is saved).</summary>
    async Task Tug(WidgetWindow other)
    {
        double left = other.Left, topY = other.Top;
        _him.Speak(_rng.Next(2) == 0 ? "hehe" : "boop!", 8);
        _him.Expression("laugh", 0.35);
        await Animate(1.2, k =>
        {
            double shake = 7 * Math.Sin(k * 40) * (1 - k);
            other.Left = left + shake;
            _him.Pose(12, -2 + shake * 0.3, 12, -2 - shake * 0.3, 4, 0);
        });
        other.Left = left; other.Top = topY;
        _him.Pose(0, 0, 0, 0, 0, 0);
        _him.Expression("grin");
    }

    async Task Wait(double seconds) => await Animate(seconds, _ => { });

    async Task Animate(double seconds, Action<double> frame)
    {
        var began = DateTime.Now;
        while (true)
        {
            double k = Math.Min(1, (DateTime.Now - began).TotalSeconds / Math.Max(seconds, 0.01));
            frame(k);
            if (k >= 1) return;
            await Task.Delay(33);
        }
    }

    static Task Frames(int n) => Task.Delay(33 * n);

    void Place()
    {
        _window.Left = _at.X - Feet.X;
        _window.Top = _at.Y - Feet.Y;
    }

    static double Lerp(double a, double b, double k) => a + (b - a) * k;

    // ---------- the desktop: screens, the ground, other windows, other cards ----------

    Rect CardRect() => _card is WidgetWindow w ? w.Seen() : new Rect(_card.Left, _card.Top, _card.ActualWidth, _card.ActualHeight);

    /// <summary>Every screen's working area (above the taskbar), in DIPs.</summary>
    List<Rect> Screens() => System.Windows.Forms.Screen.AllScreens
        .Select(s => new Rect(s.WorkingArea.X / _scale, s.WorkingArea.Y / _scale, s.WorkingArea.Width / _scale, s.WorkingArea.Height / _scale))
        .OrderBy(r => r.X).ToList();

    /// <summary>The floor at that x: the bottom of the working area of the screen it is on.</summary>
    double Ground(double x)
    {
        var screens = Screens();
        var under = screens.FirstOrDefault(s => x >= s.X && x < s.Right);
        if (under == default) under = x < screens[0].X ? screens[0] : screens[^1];
        return under.Bottom;
    }

    IEnumerable<WidgetWindow> OtherCards() =>
        Application.Current.Windows.OfType<WidgetWindow>().Where(w => !ReferenceEquals(w, _card) && w.IsVisible && w.Seen().Width > 60);

    /// <summary>
    /// The top edges of other programs' ordinary windows that he could sit on: visible, not minimized, not
    /// tool windows, at least 300 wide, with room above them for him, and on a screen.
    /// </summary>
    List<Rect> TopsOfWindows()
    {
        var found = new List<Rect>();
        var screens = Screens();
        uint me = (uint)Environment.ProcessId;
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h) || IsIconic(h) || GetWindowTextLength(h) == 0) return true;
            if ((GetWindowLong(h, -20) & 0x80) != 0) return true;                     // tool windows
            GetWindowThreadProcessId(h, out uint pid);
            if (pid == me) return true;
            if (DwmGetWindowAttribute(h, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;   // hidden by Windows
            if (DwmGetWindowAttribute(h, 9, out RECT r, Marshal.SizeOf<RECT>()) != 0 && !GetWindowRect(h, out r)) return true;
            var rect = new Rect(r.Left / _scale, r.Top / _scale, (r.Right - r.Left) / _scale, (r.Bottom - r.Top) / _scale);
            if (rect.Width < 300 || rect.Height < 150) return true;
            var screen = screens.FirstOrDefault(s => s.Contains(new Point(rect.X + rect.Width / 2, rect.Y + 5)));
            if (screen == default || rect.Y < screen.Y + 110) return true;                  // no room above it
            found.Add(new Rect(Math.Max(rect.X, screen.X + 10), rect.Y, Math.Min(rect.Right, screen.Right - 10) - Math.Max(rect.X, screen.X + 10), rect.Height));
            return true;
        }, IntPtr.Zero);
        return found;
    }

    // ---------- the rope ----------

    sealed class Rope
    {
        readonly Window _w;
        readonly Rectangle _line;
        readonly double _top;
        double _to;

        public Rope(double x, double top, double to)
        {
            _top = top; _to = to;
            var brush = new LinearGradientBrush(Color.FromRgb(0xC8, 0x9A, 0x5A), Color.FromRgb(0x9A, 0x6E, 0x3A), 0)
            { SpreadMethod = GradientSpreadMethod.Reflect, EndPoint = new Point(0, 0.02), MappingMode = BrushMappingMode.RelativeToBoundingBox };
            _line = new Rectangle { Width = 4, Height = 0, Fill = brush, RadiusX = 2, RadiusY = 2, VerticalAlignment = VerticalAlignment.Top };
            _w = new Window
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false, ShowActivated = false, Topmost = true, Left = x - 6, Top = top, Width = 12, Height = Math.Max(10, to - top + 10),
                Content = new Grid { Children = { _line } }, IsHitTestVisible = false,
            };
            _w.SourceInitialized += (_, _) => NoActivate(new System.Windows.Interop.WindowInteropHelper(_w).Handle, clickThrough: true);
        }

        public void Show() => _w.Show();
        public void Close() => _w.Close();

        /// <summary>How far down it has come (0..1), and optionally where its bottom end now is.</summary>
        public void Length(double part, double? end = null)
        {
            if (end is { } e) _to = Math.Max(e, _top + 4);
            _w.Height = Math.Max(10, _to - _top + 10);
            _line.Height = Math.Max(0, (_to - _top) * part);
        }
    }

    // ---------- Win32 ----------

    static void NoActivate(IntPtr handle, bool clickThrough = false)
    {
        int style = GetWindowLong(handle, -20);
        style |= 0x08000000 | 0x80;              // WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW
        if (clickThrough) style |= 0x20 | 0x80000; // WS_EX_TRANSPARENT | WS_EX_LAYERED
        SetWindowLong(handle, -20, style);
    }

    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    delegate bool EnumProc(IntPtr h, IntPtr l);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr h);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int index);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int index, int value);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out RECT value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmGetWindowAttribute(IntPtr h, int attr, out int value, int size);
}
