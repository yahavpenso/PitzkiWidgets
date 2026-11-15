using System.Windows;
using PitzkiWidgets.Core;

namespace PitzkiWidgets;

/// <summary>
/// Pitzki Widgets: little cards that live on the desktop. The app itself has no main window —
/// it keeps the board, puts each widget back where it was left, and opens the picker from the tray.
/// </summary>
public partial class App : Application
{
    public static Board Board { get; private set; } = new();

    readonly List<WidgetWindow> _open = new();
    System.Windows.Forms.NotifyIcon? _tray;
    PickerWindow? _picker;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        // every animation is drawn at most 30 times a second: on these see-through card windows each frame
        // is copied off the graphics card, so 60 costs twice as much and looks almost the same
        System.Windows.Media.Animation.Timeline.DesiredFrameRateProperty.OverrideMetadata(
            typeof(System.Windows.Media.Animation.Timeline), new FrameworkPropertyMetadata { DefaultValue = MaxFps });
        Pitzki.Controls.Mascot.MaxFps = MaxFps;
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        WaitForOld(e.Args);
        Board = Board.Load();
        Look(Board.Glass);       // every card paints itself from the glass brushes, or the flat ones

        if (SelfTest.Wanted) { SelfTest.Run(this); return; }

        // the cards wait hidden while they all load, and come in one after another once things are calm
        foreach (var placed in Board.Widgets.ToList()) Open(placed, hold: true);
        Core.Entrance.WhenCalm(() => _open.All(w => w.IsLoaded || !w.IsVisible), () =>
        {
            int turn = 0;
            // the ones that can be seen take turns, left to right; ones behind a full-screen window just come up
            foreach (var window in _open.OrderBy(w => w.Left).ThenBy(w => w.Top)) window.Enter(window.IsCovered || !window.IsVisible ? 0 : turn++ * 240);
        });
        Tray();
        StartSheens();
        _autoGame = new Core.AutoGame(this);
        // behind a window that fills the screen the cards stop drawing, and start again when it goes
        Core.Covered.Changed += CoverAll;
        Core.Covered.Start();

        // nothing on the desktop yet: show what there is to choose from
        if (Board.Widgets.Count == 0) ShowPicker();
    }

    System.Windows.Threading.DispatcherTimer? _sheens;
    Core.AutoGame? _autoGame;

    /// <summary>The fastest any animation on the cards is drawn.</summary>
    public const int MaxFps = 30;

    /// <summary>Every twenty seconds a sheen drifts across the cards, one after the other.</summary>
    void StartSheens()
    {
        _sheens = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        _sheens.Tick += async (_, _) =>
        {
            if (!Board.Glass) return;
            foreach (var window in _open.ToList())
            {
                window.Sheen();
                await Task.Delay(350);
            }
        };
        _sheens.Start();
    }

    /// <summary>The glass brushes, a little darker than Pitzki's own so a bright wallpaper does not wash the cards out.</summary>
    static void Look(bool on)
    {
        Pitzki.Core.GlassLook.Apply(on);
        if (!on) return;
        var pane = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromArgb(TopAlpha(), 0x1C, 0x1C, 0x26), System.Windows.Media.Color.FromArgb((byte)(TopAlpha() + 2), 0x0E, 0x0E, 0x16), new Point(0, 0), new Point(0, 1));
        pane.Freeze();
        Current.Resources["SurfaceBrush"] = pane;
        var rim = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromArgb(0x50, 255, 255, 255), System.Windows.Media.Color.FromArgb(0x1C, 255, 255, 255), new Point(0, 0), new Point(1, 1));
        rim.Freeze();
        Current.Resources["SurfaceEdgeBrush"] = rim;
        Current.Resources["CardBrush"] = Veil(0x1C, 0x0C, 0x06);
        Current.Resources["CardEdgeBrush"] = Veil(0x30, 0x0A, 0x04);
        Current.Resources["ChipBrush"] = Veil(0x24, 0x12, 0x0A);
    }

    /// <summary>How solid the glass body is, by the chosen level.</summary>
    static byte TopAlpha() => Board.GlassLevel switch { 0 => (byte)0xB4, 2 => (byte)0xFA, _ => (byte)0xEE };

    public static readonly string[] Profiles = { "Work", "Game" };
    public static readonly string[] Levels = { "Clearer", "Normal", "Darker" };

    /// <summary>The glass gets clearer or darker, for every card at once.</summary>
    public void SetGlassLevel(int level)
    {
        Board.GlassLevel = Math.Clamp(level, 0, 2);
        Board.Save();
        Look(Board.Glass);
    }

    /// <summary>Shows the cards that belong to this profile ("" shows all) and puts the others away.</summary>
    public void SetProfile(string profile)
    {
        Board.Profile = profile;
        Board.Save();
        foreach (var window in _open) window.ShowIfFits();
        _trayMenu?.Refresh();
    }

    /// <summary>True when a card with that profile tag shows under the profile that is on.</summary>
    public static bool Fits(string only) => only == "" || Board.Profile == "" || only == Board.Profile;

    /// <summary>A faint white wash, a little brighter at the top, for the rows and chips lying on the glass.</summary>
    static System.Windows.Media.Brush Veil(byte top, byte middle, byte bottom)
    {
        var brush = new System.Windows.Media.LinearGradientBrush(
            System.Windows.Media.Color.FromArgb(top, 255, 255, 255), System.Windows.Media.Color.FromArgb(bottom, 255, 255, 255), new Point(0, 0), new Point(0, 1));
        brush.GradientStops.Insert(1, new System.Windows.Media.GradientStop(System.Windows.Media.Color.FromArgb(middle, 255, 255, 255), 0.42));
        brush.Freeze();
        return brush;
    }

    /// <summary>Liquid glass on or off for every card, the picker and the menus, and remembered.</summary>
    public void SetGlass(bool on)
    {
        Board.Glass = on;
        Board.Save();
        Look(on);
        foreach (var window in _open) window.ApplyGlass(on);
        _trayMenu?.Refresh();
    }

    /// <summary>Each card hides its insides while its screen is covered, and shows them again after.</summary>
    internal void CoverAll()
    {
        foreach (var window in _open.ToList()) window.Cover(Core.Covered.Hides(window));
    }

    /// <summary>Puts one widget on the desktop.</summary>
    public void Open(Placed placed, bool hold = false)
    {
        if (placed.Under.Length > 0) return;      // it is drawn inside the card it was joined to
        var window = new WidgetWindow(placed) { HoldEntrance = hold };
        window.Loaded += (_, _) => window.Cover(Core.Covered.Hides(window));
        window.Closed += (_, _) => _open.Remove(window);
        _open.Add(window);
        if (Fits(placed.Only)) window.Show();
    }

    /// <summary>Adds a new widget of that kind, in the middle of the screen, and saves it.</summary>
    public void Add(string kind)
    {
        // on the screen the mouse is on, so it lands where you are looking
        var room = Core.Screens.RoomAtMouse();
        var placed = new Placed
        {
            Kind = kind,
            X = room.Left + room.Width / 2 - 110 + Board.Widgets.Count % 6 * 26,
            Y = room.Top + 120 + Board.Widgets.Count % 6 * 26,
        };
        Board.Widgets.Add(placed);
        Board.Save();
        Open(placed);
    }

    /// <summary>Takes one off the desktop for good.</summary>
    public void Remove(Placed placed)
    {
        Core.Buzz.Tell("removed", null, 0, placed.Kind);
        // a card inside a frame leaves it, and the rest of the frame stays exactly as it is
        var frame = Window(placed.Id);
        if (frame != null && frame.Parts.Count > 1)
        {
            var body = frame.Detach(placed);
            (body as IDisposable)?.Dispose();
            Board.Widgets.RemoveAll(w => w.Id == placed.Id);
            Board.Save();
            return;
        }
        foreach (var mine in Board.JoinedTo(placed)) mine.Under = "";
        Board.Widgets.RemoveAll(w => w.Id == placed.Id);
        Board.Save();
        foreach (var window in _open.Where(w => w.Placed.Id == placed.Id).ToList()) window.Close();
    }

    /// <summary>Builds every card of that kind again, for when something it reads has been changed.</summary>
    public void Again(string kind)
    {
        foreach (var window in _open.Where(w => w.Parts.Any(p => p.Kind == kind)).ToList())
        {
            var placed = window.Placed;
            window.Close();
            _open.Remove(window);
            Open(placed);
        }
    }

    /// <summary>Every other card as it is seen on the desktop, for a card being dragged to line up against.</summary>
    public IReadOnlyList<Rect> CardsOtherThan(WidgetWindow mover) =>
        _open.Where(w => !ReferenceEquals(w, mover) && w.IsVisible).Select(w => w.Seen()).ToList();

    /// <summary>Every other card on the desktop, with the card behind it, for working out what it was dropped against.</summary>
    public IReadOnlyList<(Placed Card, Rect Where)> CardsBesides(WidgetWindow mover) =>
        _open.Where(w => !ReferenceEquals(w, mover) && w.IsVisible).Select(w => (w.Placed, w.Seen())).ToList();

    /// <summary>
    /// Two cards become one: <paramref name="card"/> is drawn inside <paramref name="lead"/>'s frame,
    /// beside it or under it. A card already carrying others keeps them; a lead dropped onto something
    /// brings its own along.
    /// </summary>
    public void Join(Placed card, Placed lead, bool across)
    {
        if (card.Id == lead.Id || lead.Under == card.Id) return;
        if (lead.Under.Length > 0) lead = Board.Lead(lead);
        if (card.Id == lead.Id) return;

        var into = Window(lead.Id);
        var from = Window(card.Id);
        Core.Buzz.Tell("joined", into, 0, card.Kind + "|" + lead.Kind);

        // whatever was joined to the one being moved comes with it, in the order it is drawn
        var coming = new List<Placed> { card };
        coming.AddRange(Board.JoinedTo(card));
        bool first = Board.JoinedTo(lead).Count == 0;
        foreach (var mine in coming) mine.Under = lead.Id;
        if (first) lead.Across = across;   // the first join decides which way they sit

        // the board keeps them in the order they were joined, so they come back the same way
        foreach (var mine in coming) { Board.Widgets.Remove(mine); Board.Widgets.Add(mine); }
        Board.TidyJoins();
        Board.Save();

        // only the two cards involved change: the moving one's insides go into the other's frame.
        // Nothing else is closed or built again, so nothing on the desktop starts over.
        if (into == null || from == null || ReferenceEquals(into, from)) { Redraw(); return; }
        var bodies = from.Release();
        _open.Remove(from);
        from.Close();
        into.TakeIn(bodies);
    }

    /// <summary>One card leaves the frame it was joined into and stands on its own again, beside it.</summary>
    public void Split(Placed card)
    {
        var frame = Window(card.Id);
        if (frame == null || frame.Parts.Count < 2) return;
        double x = frame.Left + 26, y = frame.Top + 26;
        double scale = frame.Placed.Scale;
        var body = frame.Detach(card);
        card.X = x;
        card.Y = y;
        card.Scale = scale;
        Board.Save();
        Adopt(card, body);
    }

    /// <summary>
    /// A card that has just left a frame gets a window of its own, holding the very same insides,
    /// shown at once with no pop: it is the card that was already on the desktop.
    /// </summary>
    internal WidgetWindow Adopt(Placed card, FrameworkElement body)
    {
        var window = new WidgetWindow(card, body);
        window.Loaded += (_, _) => window.Cover(Core.Covered.Hides(window));
        window.Closed += (_, _) => _open.Remove(window);
        _open.Add(window);
        window.Show();
        return window;
    }

    /// <summary>Every card but this one goes beneath it, so the one being carried is seen above the rest.</summary>
    internal void Above(WidgetWindow mover)
    {
        foreach (var window in _open.Where(w => !ReferenceEquals(w, mover) && w.IsVisible)) Core.Desk.ToBottom(window);
    }

    /// <summary>The card a dragged one would join if it were let go now lights up; the rest go quiet.</summary>
    internal void Hint(WidgetWindow mover, Placed? target)
    {
        var lit = target == null ? null : Window(target.Id);
        foreach (var window in _open.Where(w => !ReferenceEquals(w, mover))) window.Glow(ReferenceEquals(window, lit));
    }

    /// <summary>Puts every card on the desktop again, for when what is joined to what has changed.</summary>
    public void Redraw()
    {
        foreach (var window in _open.ToList()) window.Close();
        _open.Clear();
        foreach (var placed in Board.Widgets.ToList()) Open(placed);
    }

    /// <summary>Puts a backup in place of what is on the desktop now, and saves it as the board.</summary>
    public void Restore(Board board)
    {
        Board = board;
        Board.Save();
        Look(Board.Glass);
        Redraw();
    }

    /// <summary>How many cards are on the desktop, for the tests.</summary>
    internal int OpenCount => _open.Count(w => w.IsVisible);

    /// <summary>The open card with that id, which is what the tests reach for.</summary>
    internal WidgetWindow? Window(string id) => _open.FirstOrDefault(w => w.Parts.Any(p => p.Id == id));

    /// <summary>True while a widget of that kind is on the desktop.</summary>
    public bool Has(string kind) => Board.Widgets.Any(w => w.Kind == kind);

    public void ShowPicker()
    {
        if (_picker is { IsVisible: true }) { _picker.Activate(); return; }
        _picker = new PickerWindow();
        _picker.Closed += (_, _) => _picker = null;
        _picker.Show();
        _picker.Activate();
    }

    void Tray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = TrayIcon(),
            Text = "Pitzki Widgets",
            Visible = true,
        };
        _tray.DoubleClick += (_, _) => ShowPicker();
        // right click opens a menu drawn like Pitzki's own (TrayMenu.xaml) instead of the plain Windows one
        _tray.MouseUp += (_, e) =>
        {
            if (e.Button == System.Windows.Forms.MouseButtons.Right) ShowTrayMenu();
        };
    }

    static readonly HashSet<string> Alerted = new();

    /// <summary>
    /// A note from the tray, by the clock: a stock that moved a lot, a skin that got cheaper. Each key is
    /// said once a day, so a price hovering on the line does not keep ringing.
    /// </summary>
    public static void Alert(string key, string title, string text)
    {
        if (!Alerted.Add(DateTime.Today.ToString("yyyyMMdd") + "|" + key)) return;
        AlertsShown.Add(title + " | " + text);
        if (SelfTest.Wanted) return;
        if (Current is App app && app._tray != null)
            app.Dispatcher.BeginInvoke(() => app._tray?.ShowBalloonTip(8000, title, text, System.Windows.Forms.ToolTipIcon.Info));
    }

    /// <summary>For the tests: every alert given.</summary>
    public static readonly List<string> AlertsShown = new();

    /// <summary>The app's own icon, straight out of the exe, with the plain one as a fallback.</summary>
    static System.Drawing.Icon TrayIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && System.Drawing.Icon.ExtractAssociatedIcon(exe) is { } mine) return mine;
        }
        catch { }
        return System.Drawing.SystemIcons.Application;
    }

    TrayMenu? _trayMenu;

    /// <summary>Opens the tray menu by the pointer; a second right click starts it afresh.</summary>
    internal TrayMenu ShowTrayMenu()
    {
        _trayMenu?.Close();
        var menu = new TrayMenu();
        _trayMenu = menu;
        menu.Closed += (_, _) => { if (ReferenceEquals(_trayMenu, menu)) _trayMenu = null; };
        menu.Show();
        menu.Activate(); // so that clicking elsewhere deactivates and dismisses it
        return menu;
    }

    /// <summary>Refresh: saves the board, closes, and opens again from the same exe,
    /// so a newer build that was put there since starting is the one that comes up.</summary>
    internal void Restart()
    {
        Board.Save();
        if (Environment.ProcessPath is { } exe)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, $"--after {Environment.ProcessId}") { UseShellExecute = false });
        Quit();
    }

    /// <summary>Started by Refresh: waits for the old copy to close first, so the cards are not drawn twice.</summary>
    static void WaitForOld(string[] args)
    {
        int i = Array.IndexOf(args, "--after");
        if (i < 0 || i + 1 >= args.Length || !int.TryParse(args[i + 1], out var pid)) return;
        try { System.Diagnostics.Process.GetProcessById(pid).WaitForExit(5000); } catch { }
    }

    internal void Quit()
    {
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        Shutdown();
    }
}
