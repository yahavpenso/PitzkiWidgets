using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Fx = Pitzki.Core.Fx;
using PitzkiWidgets.Core;
using PitzkiWidgets.Widgets;

namespace PitzkiWidgets;

/// <summary>
/// One card on the desktop. It holds whichever widget was asked for, is dragged about with the
/// mouse, remembers where it was left, and can be made bigger or smaller from its own menu.
/// </summary>
public partial class WidgetWindow : Window
{
    public Placed Placed { get; private set; }

    /// <summary>This card, and the ones joined to it, in the order they are drawn.</summary>
    public List<Placed> Parts { get; }

    /// <summary>How big each card inside the frame came out, for the tests.</summary>
    internal string PartSizes => string.Join(" + ", _bodies.Select(b => $"{b.ActualWidth:0}x{b.ActualHeight:0}"));

    /// <summary>A hairline between two cards that have been joined, so the join can be seen.</summary>
    static UIElement Seam(bool across) => new Border
    {
        Background = (Brush)Application.Current.FindResource("CardEdgeBrush"),
        Width = across ? 1 : double.NaN,
        Height = across ? double.NaN : 1,
        Opacity = 0.5,
        Margin = across ? new Thickness(11, 2, 11, 2) : new Thickness(2, 10, 2, 10),
        HorizontalAlignment = across ? HorizontalAlignment.Center : HorizontalAlignment.Stretch,
        VerticalAlignment = across ? VerticalAlignment.Stretch : VerticalAlignment.Center,
    };

    readonly List<FrameworkElement> _bodies = new();
    bool _dragging;
    Point _grabbed;

    /// <summary>
    /// A card on the desktop. <paramref name="kept"/> is the insides of a card that was already on the
    /// desktop and has just left a frame: it is used as it is, and the window shows at once, with no pop.
    /// </summary>
    public WidgetWindow(Placed placed, FrameworkElement? kept = null)
    {
        InitializeComponent();
        Placed = placed;

        // the card itself, and anything joined to it, inside the one frame
        Parts = new List<Placed> { placed };
        if (kept != null) _bodies.Add(kept);
        else
        {
            Parts.AddRange(App.Board.JoinedTo(placed));
            foreach (var part in Parts) _bodies.Add(WidgetBody.Make(part));
        }
        Assemble();

        Grain.Background = Core.Grain.Paper;

        double size = Math.Clamp(placed.Scale, 0.7, 2);
        // the real size is set at once: growing the layout scale would resize the window every frame,
        // which is too slow to animate, so the pop on the way in is a render scale instead
        Zoom.ScaleX = Zoom.ScaleY = size;
        bool still = kept != null || SelfTest.Wanted;      // the tests photograph the card as it is, not mid-pop
        var pop = new ScaleTransform(still ? 1 : 0.82, still ? 1 : 0.82);
        var rise = new TranslateTransform(0, still ? 0 : 10);
        Root.RenderTransformOrigin = new Point(0.5, 0.5);
        Root.RenderTransform = new TransformGroup { Children = { pop, rise } };
        _pop = pop;
        _rise = rise;
        if (kept != null) { Opacity = 1; ShowActivated = false; }
        Left = placed.X;
        Top = placed.Y;

        Menu();
        Glass.Opacity = App.Board.Glass ? 1 : 0;
        MouseEnter += (_, _) => { Fade(Grip, IsLocked || Parts.Count > 1 ? 0 : 1); Lines(true); Fade(Lift, 1); GlassFollow(Mouse.GetPosition(Glass), true); };
        MouseLeave += (_, _) => { Fade(Grip, 0); Lines(false); Fade(Lift, 0); GlassFollow(default, false); };
        MouseMove += (_, e) => GlassFollow(e.GetPosition(Glass), true);
        MouseEnter += (_, _) => { _away?.Stop(); Untuck(); };
        MouseLeave += (_, _) =>
        {
            if (!Placed.Peek) return;
            _away ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
            _away.Tick -= Away;
            _away.Tick += Away;
            _away.Start();
        };
        MouseLeftButtonDown += Grab;
        MouseMove += Drag;
        MouseLeftButtonUp += Dropped;
        Loaded += (_, _) =>
        {
            if (!SelfTest.Wanted) OnScreen();
            if (Placed.Peek && !SelfTest.Wanted) Tuck();
            if (kept != null || HoldEntrance) return;      // held cards are let in by App, all together, once things are calm
            // a card added later still waits for everything queued before it, so its first frames are not lost
            Dispatcher.BeginInvoke(() => Enter(0), System.Windows.Threading.DispatcherPriority.Background);
        };
    }

    ScaleTransform _pop = null!;
    TranslateTransform _rise = null!;
    bool _entered;

    /// <summary>Set before it is shown: the card stays hidden until <see cref="Enter"/> is called.</summary>
    internal bool HoldEntrance { get; set; }

    /// <summary>It fades up, rises a little and springs to its size, rather than snapping onto the desktop.</summary>
    internal void Enter(int wait)
    {
        if (_entered) return;
        _entered = true;
        Fx.Animate(this, OpacityProperty, 1, 420, Fx.EaseOut, wait);
        Fx.Animate(_pop, ScaleTransform.ScaleXProperty, 1, 650, Fx.Spring, wait);
        Fx.Animate(_pop, ScaleTransform.ScaleYProperty, 1, 650, Fx.Spring, wait);
        Fx.Animate(_rise, TranslateTransform.YProperty, 0, 560, Fx.EaseOut, wait);
        EntranceLog(wait);
    }

    /// <summary>
    /// For checking the way in: with PITZKI_WIDGETS_ENTRANCE set to a file, how many frames were drawn while
    /// this card faded in is written there, so it can be seen that it arrived over many frames and did not just appear.
    /// </summary>
    void EntranceLog(int wait)
    {
        if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ENTRANCE") is not { Length: > 0 } file) return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        int frames = 0, between = 0;
        void Frame(object? s, EventArgs e)
        {
            frames++;
            if (Opacity > 0.02 && Opacity < 0.98) between++;
            if (clock.ElapsedMilliseconds < wait + 1200) return;
            CompositionTarget.Rendering -= Frame;
            lock (typeof(WidgetWindow))
                System.IO.File.AppendAllText(file, $"{DateTime.Now:HH:mm:ss.fff} {Placed.Kind}: wait {wait} ms, {frames} frames, {between} of them mid-fade, ends at opacity {Opacity:0.00} scale {_pop.ScaleX:0.00}{Environment.NewLine}");
        }
        CompositionTarget.Rendering += Frame;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        Desk.KeepOnDesktop(this, Typed);
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        foreach (var body in _bodies) (body as IDisposable)?.Dispose();
    }

    /// <summary>The right-click menu: how big it is, and taking it off the desktop.</summary>
    void Menu()
    {
        var menu = new ContextMenu();
        foreach (var (name, scale) in new[] { ("Small", 0.85), ("Normal", 1.0), ("Big", 1.25), ("Huge", 1.6) })
        {
            var item = new MenuItem { Header = name, IsCheckable = true, IsChecked = Math.Abs(Placed.Scale - scale) < 0.01 };
            item.Click += (_, _) => Resize(scale);
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var lockIt = new MenuItem { Header = "Lock in place", IsCheckable = true, IsChecked = Placed.Locked };
        lockIt.Click += (_, _) => { Placed.Locked = lockIt.IsChecked; App.Board.Save(); if (IsLocked) Fade(Grip, 0); };
        menu.Items.Add(lockIt);
        var snapIt = new MenuItem { Header = "Line up with other cards", IsCheckable = true, IsChecked = !App.Board.NoSnap, ToolTip = "Hold Alt while dragging to place a card freely" };
        snapIt.Click += (_, _) => { App.Board.NoSnap = !snapIt.IsChecked; App.Board.Save(); };
        menu.Items.Add(snapIt);
        var glassIt = new MenuItem { Header = "Liquid glass", IsCheckable = true, IsChecked = App.Board.Glass };
        glassIt.Click += (_, _) => ((App)Application.Current).SetGlass(glassIt.IsChecked);
        menu.Items.Add(glassIt);
        var level = new MenuItem { Header = "Glass: " + App.Levels[Math.Clamp(App.Board.GlassLevel, 0, 2)], StaysOpenOnClick = true };
        level.Click += (_, _) =>
        {
            ((App)Application.Current).SetGlassLevel((App.Board.GlassLevel + 1) % 3);
            level.Header = "Glass: " + App.Levels[App.Board.GlassLevel];
        };
        menu.Items.Add(level);
        var peek = new MenuItem { Header = "Hide at the screen edge", IsCheckable = true, IsChecked = Placed.Peek, ToolTip = "The card tucks behind the nearest edge and slides out when you touch it" };
        peek.Click += (_, _) => { Placed.Peek = peek.IsChecked; App.Board.Save(); if (Placed.Peek) Tuck(); else Untuck(); };
        menu.Items.Add(peek);
        var only = new MenuItem { Header = "Shows in: " + Shows(), StaysOpenOnClick = true };
        only.Click += (_, _) =>
        {
            var all = new[] { "" }.Concat(App.Profiles).ToList();
            Placed.Only = all[(all.IndexOf(Placed.Only) + 1) % all.Count];
            App.Board.Save();
            only.Header = "Shows in: " + Shows();
            ShowIfFits();
        };
        menu.Items.Add(only);
        var split = new MenuItem { Header = "Split off" };
        menu.Items.Add(split);
        menu.Items.Add(new Separator());
        var more = new MenuItem { Header = "Widgets…" };
        more.Click += (_, _) => ((App)Application.Current).ShowPicker();
        menu.Items.Add(more);
        var off = new MenuItem { Header = "Take this off" };
        off.Click += (_, _) => ((App)Application.Current).Remove(Placed);
        menu.Items.Add(off);
        menu.Opened += (_, _) =>
        {
            int i = 0;
            foreach (var scale in new[] { 0.85, 1.0, 1.25, 1.6 })
                if (menu.Items[i++] is MenuItem item) item.IsChecked = Math.Abs(Placed.Scale - scale) < 0.01;
            lockIt.IsChecked = Placed.Locked;
            snapIt.IsChecked = !App.Board.NoSnap;
            glassIt.IsChecked = App.Board.Glass;
            level.Header = "Glass: " + App.Levels[Math.Clamp(App.Board.GlassLevel, 0, 2)];
            peek.IsChecked = Placed.Peek;
            only.Header = "Shows in: " + Shows();

            // one entry for each card joined to this one, and nothing at all when it stands alone
            split.Items.Clear();
            foreach (var part in Parts.Count > 1 ? Parts : new List<Placed>())
            {
                var one = new MenuItem { Header = Widgets.WidgetBody.NameOf(part.Kind) };
                var its = part;
                one.Click += (_, _) => ((App)Application.Current).Split(its);
                split.Items.Add(one);
            }
            split.Visibility = split.Items.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        };
        ContextMenu = menu;
    }

    // ----- profiles -----

    string Shows() => Placed.Only == "" ? "every profile" : Placed.Only;

    /// <summary>Puts the card on the desktop if it belongs to the profile that is on, and away if it does not.</summary>
    internal void ShowIfFits()
    {
        bool fits = App.Fits(Placed.Only);
        if (fits && !IsVisible) Show();
        else if (!fits && IsVisible) Hide();
    }

    // ----- hiding at the screen edge -----

    const double Strip = 18;
    bool _tucked;
    System.Windows.Threading.DispatcherTimer? _away;

    /// <summary>True while the card sits behind the screen's edge.</summary>
    internal bool Tucked => _tucked;

    /// <summary>The tests give the card a room of their own to hide in, so nothing shows on the real screen.</summary>
    internal static Rect? RoomForTests { get => Core.Screens.Pretend; set => Core.Screens.Pretend = value; }

    /// <summary>Where the card's left edge goes to hide: behind the nearer side of the room, with a strip left showing.</summary>
    internal static double TuckedLeft(Rect card, Rect room) =>
        card.Left + card.Width / 2 < room.Left + room.Width / 2 ? room.Left - card.Width + Strip : room.Right - Strip;

    void Away(object? sender, EventArgs e) { _away?.Stop(); if (!IsMouseOver) Tuck(); }

    internal void Tuck()
    {
        if (!Placed.Peek || _dragging || _tucked || !IsVisible) return;
        _tucked = true;
        double to = TuckedLeft(new Rect(Left, Top, ActualWidth, ActualHeight), Room());
        Fx.Animate(this, LeftProperty, to, 260, Fx.EaseOut, 0, () => { if (_tucked) { BeginAnimation(LeftProperty, null); Left = to; } });
    }

    internal void Untuck()
    {
        if (!_tucked) return;
        _tucked = false;
        double to = Placed.X;
        Fx.Animate(this, LeftProperty, to, 220, Fx.EaseOut, 0, () => { if (!_tucked) { BeginAnimation(LeftProperty, null); Left = to; } });
    }

    // ----- liquid glass -----

    /// <summary>Turns the glass layer of this card on or off.</summary>
    internal void ApplyGlass(bool on) => Fx.Animate(Glass, OpacityProperty, on ? 1 : 0, 420);

    /// <summary>
    /// Everything inside the card is cut to its rounded shape. WPF does not do this by itself: a
    /// border's children keep their square corners, which is how a sheen or a glow ends up painting
    /// over a corner that should be round.
    /// </summary>
    void Inside_SizeChanged(object sender, SizeChangedEventArgs e) =>
        Inside.Clip = new RectangleGeometry(new Rect(0, 0, Inside.ActualWidth, Inside.ActualHeight), 15, 15);

    /// <summary>The glow under the pointer, in the card's own coordinates.</summary>
    void GlassFollow(Point at, bool inside)
    {
        if (!App.Board.Glass) return;
        if (inside)
        {
            GlassTouchT.X = at.X - 100;
            GlassTouchT.Y = at.Y - 100;
        }
        double want = inside ? 1 : 0;
        if (Math.Abs(GlassTouch.Opacity - want) > 0.01) Fx.Animate(GlassTouch, OpacityProperty, want, inside ? 220 : 420);
    }

    /// <summary>One slow pass of light across the glass.</summary>
    internal void Sheen()
    {
        if (!App.Board.Glass || !IsVisible || _covered) return;
        double width = Math.Max(120, Glass.ActualWidth + 160);
        var pass = new System.Windows.Media.Animation.DoubleAnimation(-170, width, TimeSpan.FromMilliseconds(1600))
        {
            EasingFunction = new System.Windows.Media.Animation.SineEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseInOut },
        };
        System.Windows.Media.Animation.Timeline.SetDesiredFrameRate(pass, App.MaxFps);
        GlassSheenT.BeginAnimation(TranslateTransform.XProperty, pass);
    }

    // ----- under a window that fills the screen -----

    bool _covered;

    /// <summary>True while a window fills this card's screen and the card has stopped drawing.</summary>
    internal bool IsCovered => _covered;

    /// <summary>
    /// Nobody can see the card behind a window that fills its screen, so its insides are hidden: WPF then
    /// draws nothing for it, and Pitzki and the live values stop animating (they skip what is not seen).
    /// The timers keep going, so the card is up to date the moment it is uncovered.
    /// </summary>
    internal void Cover(bool covered)
    {
        if (covered == _covered || (covered && _dragging)) return;
        _covered = covered;
        Root.Visibility = covered ? Visibility.Hidden : Visibility.Visible;
    }

    void Resize(double scale)
    {
        Placed.Scale = scale;
        App.Board.Save();
        Fx.Animate(Zoom, ScaleTransform.ScaleXProperty, scale, 250, Fx.Spring);
        // once it has grown, it is kept on the screen, and where it ended up is remembered
        Fx.Animate(Zoom, ScaleTransform.ScaleYProperty, scale, 250, Fx.Spring, 0, () =>
        {
            OnScreen();
            Placed.X = Left;
            Placed.Y = Top;
            App.Board.Save();
        });
    }

    /// <summary>True when this card, or every card, has been told to stay put.</summary>
    internal bool IsLocked => Placed.Locked || App.Board.LockAll;

    void Grab(object sender, MouseButtonEventArgs e) => Begin(e.GetPosition(this));

    /// <summary>A card pulled out of this frame by its line: this window still has the mouse, and hands every move on to it.</summary>
    WidgetWindow? _carrying;

    /// <summary>The mouse went down on the card at that spot. False when the card is locked and will not follow it.</summary>
    internal bool Begin(Point grabbed)
    {
        if (IsLocked) return false;
        if (_tucked) { _tucked = false; BeginAnimation(LeftProperty, null); }
        _dragging = true;
        _grabbed = grabbed;
        CaptureMouse();
        if (!SelfTest.Wanted) ((App)Application.Current).Above(this);
        return true;
    }

    void Drag(object sender, MouseEventArgs e)
    {
        if (!_dragging) return;

        // the pointer is in device pixels, the window in device-independent ones
        var now = PointToScreen(e.GetPosition(this));
        if (PresentationSource.FromVisual(this)?.CompositionTarget is { } target) now = target.TransformFromDevice.Transform(now);

        // held Alt means "exactly here"; otherwise it lines up with its neighbours and the screen's edges
        (_carrying ?? this).Follow(now, !App.Board.NoSnap && !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
    }

    /// <summary>The pointer is now here (in device-independent pixels): the card goes with it, lining up if asked to.</summary>
    internal void Follow(Point pointer, bool lineUp)
    {
        if (!_dragging) return;
        if (IsLocked) { _dragging = false; ReleaseMouseCapture(); return; }

        double left = pointer.X - _grabbed.X;
        double top = pointer.Y - _grabbed.Y;

        if (lineUp)
        {
            var fit = Core.Snap.Fit(Seen(left, top), ((App)Application.Current).CardsOtherThan(this), Room());
            left = fit.X - Inset;
            top = fit.Y - Inset;
        }
        // it stays whole on the screen the pointer is on: never half on one screen and half on the next,
        // and never past an edge where its own grip could end up out of reach
        (left, top) = KeptIn(left, top, Core.Screens.Room(new Rect(pointer.X, pointer.Y, 1, 1), this));
        Left = left;
        Top = top;

        // whatever it would join if let go here lights up, so you know before you let go
        if (!App.Board.NoJoin)
        {
            var app = (App)Application.Current;
            var neighbours = app.CardsBesides(this);
            var target = Core.Snap.Target(Seen(), neighbours.Select(n => n.Where).ToList());
            app.Hint(this, target is { } t ? neighbours[t.Which].Card : null);
        }
    }

    /// <summary>The card's own margin, which the shadow lives in: it is not part of what is seen.</summary>
    double Inset => 4 * Zoom.ScaleX;

    /// <summary>The card as the eye sees it, with its margin taken off, where it would be if its window sat at left, top.</summary>
    Rect Seen(double left, double top) =>
        new(left + Inset, top + Inset, Math.Max(ActualWidth - 2 * Inset, 1), Math.Max(ActualHeight - 2 * Inset, 1));

    /// <summary>Where the card stands now.</summary>
    internal Rect Seen() => Seen(Left, Top);

    /// <summary>The room a card has: the screen it is really on, without the taskbar.</summary>
    Rect Room() => Core.Screens.RoomFor(this);

    void Dropped(object sender, MouseButtonEventArgs e) => Let();

    internal void Let()
    {
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
        if (_carrying is { } carried)
        {
            _carrying = null;
            carried.Let();
            return;
        }
        ((App)Application.Current).Hint(this, null);
        if (!SelfTest.Wanted) OnScreen();
        Placed.X = Left;
        Placed.Y = Top;
        App.Board.Save();
        if (!App.Board.NoJoin) Join();
    }

    /// <summary>
    /// It was let go right against another card: the two become one. The card it was dropped on keeps
    /// its place, and this one moves inside its frame.
    /// </summary>
    void Join()
    {
        var app = (App)Application.Current;
        var neighbours = app.CardsBesides(this);
        var touching = Core.Snap.Target(Seen(), neighbours.Select(n => n.Where).ToList());
        if (touching is not { } found) return;
        app.Join(Placed, neighbours[found.Which].Card, found.Across);
    }

    // ----- cards joined into one frame -----

    /// <summary>True when one of the cards in this frame is typed into, so the frame has to take the focus.</summary>
    bool Typed => Parts.Any(p => p.Kind is "notes" or "tasks" or "countdown" or "calc");

    /// <summary>The line on top of each card in a frame, which pulls that card out when dragged.</summary>
    readonly List<Border> _lines = new();

    /// <summary>Takes a card's insides out of wherever they are drawn now, without closing or rebuilding them.</summary>
    void Unhook(FrameworkElement body)
    {
        if (ReferenceEquals(Body.Content, body)) Body.Content = null;
        else if (body.Parent is Panel panel) panel.Children.Remove(body);
    }

    /// <summary>Lays the cards of this frame out: one card on its own, or each one under or beside the last with a line on top.</summary>
    void Assemble()
    {
        foreach (var body in _bodies) Unhook(body);
        Body.Content = null;
        _lines.Clear();
        // the frame's own handle would sit over the cards' lines and take their clicks
        Grip.IsHitTestVisible = _bodies.Count == 1;
        if (_bodies.Count == 1) { Body.Content = _bodies[0]; return; }

        var together = new StackPanel { Orientation = Placed.Across ? Orientation.Horizontal : Orientation.Vertical };
        for (int i = 0; i < _bodies.Count; i++)
        {
            if (i > 0) together.Children.Add(Seam(Placed.Across));
            var cell = new Grid();
            cell.Children.Add(_bodies[i]);
            var line = Line(Parts[i]);
            _lines.Add(line);
            cell.Children.Add(line);
            together.Children.Add(cell);
        }
        Body.Content = together;
    }

    /// <summary>The line on top of one card in the frame. Grab it and that card comes out, following the mouse.</summary>
    Border Line(Placed part)
    {
        var pill = new Border
        {
            Width = 30, Height = 4, CornerRadius = new CornerRadius(2),
            Background = new SolidColorBrush(Color.FromArgb(0x88, 0xFF, 0xFF, 0xFF)),
            VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false,
        };
        var line = new Border
        {
            Height = 16, Margin = new Thickness(0, -13, 0, 0), VerticalAlignment = VerticalAlignment.Top,
            Background = Brushes.Transparent, Cursor = Cursors.SizeAll, Child = pill,
            ToolTip = "Drag to pull this card out", Opacity = IsMouseOver ? 1 : 0.45,
        };
        line.MouseEnter += (_, _) => Fx.Animate(pill, WidthProperty, 44, 160, Fx.EaseOut);
        line.MouseLeave += (_, _) => Fx.Animate(pill, WidthProperty, 30, 160, Fx.EaseOut);
        line.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            var mouse = Dip(PointToScreen(e.GetPosition(this)));
            PullOut(part, mouse);
        };
        return line;
    }

    /// <summary>The lines brighten while the pointer is on the frame, and stay faintly there otherwise.</summary>
    void Lines(bool on)
    {
        foreach (var line in _lines) Fade(line, on ? 1 : 0.45);
    }

    /// <summary>A point on the screen in device pixels, in the device-independent ones windows are placed in.</summary>
    Point Dip(Point device) =>
        PresentationSource.FromVisual(this)?.CompositionTarget is { } target ? target.TransformFromDevice.Transform(device) : device;

    /// <summary>
    /// Gives up every card in this window, insides and all, so they can be drawn in another frame.
    /// Nothing is closed or rebuilt: the clock keeps ticking, the note keeps its words.
    /// </summary>
    internal List<(Placed Card, FrameworkElement Body)> Release()
    {
        var all = Parts.Zip(_bodies, (p, b) => (p, b)).ToList();
        foreach (var body in _bodies) Unhook(body);
        Body.Content = null;
        _bodies.Clear();
        return all;
    }

    /// <summary>Cards dropped onto this one join its frame, as they are, at the end.</summary>
    internal void TakeIn(List<(Placed Card, FrameworkElement Body)> coming)
    {
        foreach (var (card, body) in coming)
        {
            Parts.Add(card);
            _bodies.Add(body);
        }
        Assemble();
        Desk.Typing(this, Typed);
        Fade(Grip, 0);
        Glow(false);
        // the new arrival fades in where it now sits; everything already here stays put
        foreach (var (_, body) in coming)
        {
            body.Opacity = 0;
            Fx.Animate(body, OpacityProperty, 1, 220, Fx.EaseOut);
        }
        if (!SelfTest.Wanted) Dispatcher.BeginInvoke(OnScreen, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    /// <summary>
    /// One card leaves this frame, and its insides are handed back for a window of its own. When it is
    /// the card the frame belongs to, the next one takes the frame over where it stands.
    /// </summary>
    internal FrameworkElement Detach(Placed part)
    {
        int i = Parts.FindIndex(p => p.Id == part.Id);
        var body = _bodies[i];
        Parts.RemoveAt(i);
        _bodies.RemoveAt(i);
        Unhook(body);

        if (part.Id == Placed.Id)
        {
            var lead = Parts[0];
            lead.Under = "";
            lead.Across = part.Across;
            lead.Scale = part.Scale;
            lead.Only = part.Only;
            lead.X = Left;
            lead.Y = Top;
            foreach (var other in Parts.Skip(1)) other.Under = lead.Id;
            Placed = lead;
        }
        part.Under = "";
        Assemble();
        Desk.Typing(this, Typed);
        if (IsMouseOver) Lines(true);
        return body;
    }

    /// <summary>
    /// The line on top of a card was grabbed: the card comes out of the frame, gets its own window
    /// right where it was, and follows the mouse until it is let go (and may join something else).
    /// </summary>
    internal WidgetWindow? PullOut(Placed part, Point mouse)
    {
        if (IsLocked || Parts.Count < 2) return null;
        int i = Parts.FindIndex(p => p.Id == part.Id);
        if (i < 0) return null;
        var app = (App)Application.Current;

        // where the card is now, as the eye sees it, so its own window starts exactly there
        double scale = Zoom.ScaleX;
        var at = Dip(_bodies[i].PointToScreen(new Point(0, 0)));
        double left = at.X - scale * (4 + 1 + 14), top = at.Y - scale * (4 + 1 + 12);

        double size = Placed.Scale;
        var body = Detach(part);
        part.X = left;
        part.Y = top;
        part.Scale = size;
        Placed.X = Left;
        Placed.Y = Top;
        App.Board.Save();

        var alone = app.Adopt(part, body);
        alone.Left = left;
        alone.Top = top;
        alone.Carried(new Point(mouse.X - left, mouse.Y - top));
        if (!SelfTest.Wanted) app.Above(alone);

        // this window keeps the mouse and passes every move on
        _carrying = alone;
        _dragging = true;
        if (!SelfTest.Wanted) CaptureMouse();
        return alone;
    }

    /// <summary>This card is being carried by the mouse, held at that spot, though another window has the mouse.</summary>
    internal void Carried(Point grabbed)
    {
        _dragging = true;
        _grabbed = grabbed;
    }

    /// <summary>Lights the card up as the one a dragged card would join if let go now.</summary>
    internal void Glow(bool on)
    {
        if (_glowing == on) return;
        _glowing = on;
        Core.Buzz.Tell(on ? "near" : "away", this);
        Lift.BorderBrush = on ? (Brush)Application.Current.FindResource("AccentBrush") : new SolidColorBrush(Color.FromArgb(0x35, 0xFF, 0xFF, 0xFF));
        Lift.BorderThickness = new Thickness(on ? 2 : 1);
        Fade(Lift, on || IsMouseOver ? 1 : 0);
    }
    bool _glowing;

    internal bool Glowing => _glowing;
    internal int LineCount => _lines.Count;

    /// <summary>Keeps the card where it can be seen, in case a screen was unplugged since last time.</summary>
    void OnScreen()
    {
        if (_tucked) return;
        // the one screen it is mostly on, so a frame that grew when a card joined it does not spill
        // onto the next screen (the box around every screen together has corners no screen covers)
        (Left, Top) = KeptIn(Left, Top, Room());
    }

    /// <summary>Where the window goes so the card, as seen, sits wholly inside that room.</summary>
    (double Left, double Top) KeptIn(double left, double top, Rect room)
    {
        var seen = Seen(left, top);
        double x = Math.Clamp(seen.Left, room.Left, Math.Max(room.Left, room.Right - seen.Width));
        double y = Math.Clamp(seen.Top, room.Top, Math.Max(room.Top, room.Bottom - seen.Height));
        return (x - Inset, y - Inset);
    }

    static void Fade(UIElement what, double to) =>
        Fx.Animate(what, OpacityProperty, to, 180, Fx.EaseOut);
}
