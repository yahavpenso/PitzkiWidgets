using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pitzki.Controls;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Pitzki himself, sitting on the desktop. He looks at the pointer, sleeps at night, and does
/// something when he is clicked. It is the same Pitzki as the notch app: the one drawing, shared.
/// </summary>
public sealed class PitzkiWidget : UserControl
{
    readonly Mascot _him = new() { Width = 104, Height = 86, Small = true };
    readonly TextBlock _word = new() { Style = Caption, HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 4, 0, 0) };
    readonly Random _random = new();

    public PitzkiWidget()
    {
        var box = new StackPanel { Width = 128 };
        box.Children.Add(_him);
        box.Children.Add(_word);
        Content = box;

        // the control itself takes the clicks: the mascot is see-through to the mouse
        Background = System.Windows.Media.Brushes.Transparent;
        MouseMove += (_, e) =>
        {
            Whizz(e.GetPosition(this));
            if (_him.Hobby != "") return; // busy with his own thing: his eyes are on it, not on the pointer
            var where = e.GetPosition(_him);
            _him.LookAt(where.X / Math.Max(_him.ActualWidth, 1) * 2 - 1, where.Y / Math.Max(_him.ActualHeight, 1) * 2 - 1);
        };
        MouseLeave += (_, _) => { if (_him.Hobby == "") _him.LookAt(0, 0); };
        MouseRightButtonUp += (_, _) => { }; // leaves the card's own menu alone
        // The card's window takes the mouse as soon as the button goes down (that is how it is dragged), so the
        // button coming up never reaches this control. It is watched on the window instead: a press here that
        // is let go without the card having moved is a click.
        PreviewMouseLeftButtonDown += (_, _) => { Downs++; _downAt = Pointer(); _downWhen = DateTime.Now; };
        Loaded += (_, _) => Host(Window.GetWindow(this));
        Unloaded += (_, _) => Host(null);
        _him.HobbyDone += () =>
        {
            // about a minute from the start of one scene to the start of the next, however long it ran;
            // a click puts the minute back to the start
            if (_wavePending) Rested();
            else { _quietSince = _sceneStarted; _waitFor = Gap(); }
            if (_wavePending) { _wavePending = false; Dispatcher.BeginInvoke(() => _him.Wave()); }
        };

        this.Every(1, Life);
        _eyes = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(125) };
        _eyes.Tick += (_, _) => FollowPointer();
        Loaded += (_, _) => { _eyes.Start(); Buzz.Happened += Heard; };
        Unloaded += (_, _) => Buzz.Happened -= Heard;
        Unloaded += (_, _) => _eyes.Stop();
        Loaded += async (_, _) => { try { await _media.InitAsync(); } catch { } };
        Loaded += (_, _) => Greet();
    }

    // ---------- saying hello ----------

    internal static bool Greeted;

    /// <summary>For the self-test: pretend the computer has been on this many seconds (null = real).</summary>
    internal static double? UptimeForTest;

    /// <summary>
    /// Once per run of the app, on the first Pitzki card: if the computer itself was switched on in the last
    /// four minutes he "boots up" with it, otherwise he drops in and waves hello. (Cards rebuilt later, when
    /// joined or moved, do not greet again.)
    /// </summary>
    void Greet()
    {
        if (Greeted) return;
        Greeted = true;
        double uptime = UptimeForTest ?? Environment.TickCount64 / 1000.0;
        string scene = uptime < 240 ? "boot" : "hello";
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            if (_him.StartHobby(scene)) { _lastHobby = scene; _sceneStarted = DateTime.Now; }
        });
    }

    // ---------- clicks ----------

    Window? _host;
    Point? _downAt;
    DateTime _downWhen;

    static Point Pointer()
    {
        var p = System.Windows.Forms.Control.MousePosition;
        return new Point(p.X, p.Y);
    }

    void Host(Window? window)
    {
        if (_host != null) _host.PreviewMouseLeftButtonUp -= HostUp;
        _host = window;
        if (_host != null) _host.PreviewMouseLeftButtonUp += HostUp;
    }

    internal int Downs, Ups;

    void HostUp(object sender, MouseButtonEventArgs e)
    {
        Ups++;
        if (_downAt is not { } down) return;
        _downAt = null;
        var now = Pointer();
        bool still = Math.Abs(now.X - down.X) < 6 && Math.Abs(now.Y - down.Y) < 6;
        if (still && (DateTime.Now - _downWhen).TotalMilliseconds < 800) Clicked();
    }

    /// <summary>
    /// Poked: in the middle of a scene he puts it away (with a move) and says hi; asleep at night he wakes
    /// up for ten minutes; otherwise a wave, a hop, a sparkle or a glance.
    /// </summary>
    internal void Clicked()
    {
        Clicks++;
        // two clicks close together: off he goes for a walk
        if ((DateTime.Now - _lastClick).TotalMilliseconds < 450 && _him.Mood != MascotMood.Sleepy) { _lastClick = DateTime.MinValue; GoRoam(); return; }
        _lastClick = DateTime.Now;
        if (_him.Hobby != "") { _wavePending = true; _him.StopHobby(); }
        else if (Night && (_him.Mood == MascotMood.Sleepy || _him.Drowsy > 0)) Wake();
        else Poke();
        Rested();
    }

    /// <summary>For the self-test: how many clicks reached him.</summary>
    internal int Clicks;

    // ---------- reacting to the computer and the pointer ----------

    readonly Pitzki.Core.MediaService _media = new();
    readonly Pitzki.Core.SystemStats _stats = new();
    int _hotSeconds;
    DateTime _fannedAt = DateTime.MinValue, _woozyAt = DateTime.MinValue;
    readonly Queue<(DateTime When, double Way)> _strokes = new();
    Point _lastPointer = new(double.NaN, double.NaN);

    /// <summary>For the self-test: pretend music is playing / the processor is this busy (null = real).</summary>
    internal static bool? MusicForTest;
    internal static double? CpuForTest;

    /// <summary>
    /// The pointer whizzing back and forth over him makes him dizzy: at least three changes of direction
    /// and 600 px of travel within 0.6 s.
    /// </summary>
    void Whizz(Point now)
    {
        if (!double.IsNaN(_lastPointer.X))
        {
            double dx = now.X - _lastPointer.X;
            if (Math.Abs(dx) > 0.5) _strokes.Enqueue((DateTime.Now, dx));
        }
        _lastPointer = now;
        while (_strokes.Count > 0 && (DateTime.Now - _strokes.Peek().When).TotalSeconds > 0.6) _strokes.Dequeue();
        double travel = _strokes.Sum(s => Math.Abs(s.Way));
        int turns = 0;
        double before = 0;
        foreach (var (_, way) in _strokes) { if (before != 0 && Math.Sign(way) != Math.Sign(before)) turns++; before = way; }
        if (travel > 600 && turns >= 3 && (DateTime.Now - _woozyAt).TotalSeconds > 6 && _him.Hobby == "" && _him.Mood != MascotMood.Sleepy)
        {
            _woozyAt = DateTime.Now;
            _strokes.Clear();
            _him.Woozy();
        }
    }

    /// <summary>Dancing while music plays; sweating and fanning himself while the computer runs hot.</summary>
    void Senses()
    {
        bool awake = _him.Mood != MascotMood.Sleepy && _him.Drowsy == 0;
        bool music = MusicForTest ?? _media.IsPlaying;
        _him.Dancing = awake && music;
        Listen(_him.Dancing && IsVisible);

        Watch(awake);
        Extras(awake);
        Cards(awake);
        _stats.Update(network: false);    // he only feels the processor's heat
        double cpu = CpuForTest ?? _stats.Cpu;
        _hotSeconds = cpu > 0.85 ? _hotSeconds + 1 : 0;
        if (awake && _hotSeconds >= 5 && _him.Hobby == "" && (DateTime.Now - _fannedAt).TotalSeconds > 20)
        {
            _fannedAt = DateTime.Now;
            _him.Fan();
        }
    }

    // ---------- dancing on the song's own beat, like the notch Pitzki ----------

    readonly Pitzki.Core.BeatService _beats = new();
    Pitzki.Core.BeatEdge _beatEdge;
    System.Windows.Threading.DispatcherTimer? _beatClock;

    /// <summary>For the self-test: the tempo heard last (0 = none yet).</summary>
    internal double HeardBpm => _beats.Bpm;
    internal int BeatsDanced;

    /// <summary>
    /// While he dances, listens to what the speakers play (the same BeatService as the notch app: the sound
    /// is boiled down to three numbers on the spot, nothing is recorded) and dips on every beat of the song.
    /// Listening stops as soon as the music stops, so nothing runs when it is not needed.
    /// </summary>
    void Listen(bool on)
    {
        if (on == (_beatClock?.IsEnabled ?? false)) return;
        if (on)
        {
            if (!_beats.Running && !_beats.Start()) return; // no output device: the steady dance stays
            _beats.NewSong();
            _beatClock ??= MakeBeatClock();
            _beatClock.Start();
        }
        else
        {
            _beatClock?.Stop();
            _beats.Stop();
        }
    }

    System.Windows.Threading.DispatcherTimer MakeBeatClock()
    {
        var clock = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(33) };
        clock.Tick += (_, _) =>
        {
            if (_beats.TryGetBeat(out double phase, out double period) && _beatEdge.Step(phase, period))
            {
                _him.Beat(period);
                BeatsDanced++;
            }
        };
        Unloaded += (_, _) => Listen(false);
        return clock;
    }

    // ---------- typing, games, messages, muting ----------

    readonly Pitzki.Core.NotificationWatch _notes = Pitzki.Core.NotificationWatch.Shared;   // one poll of Windows serves every card
    Pitzki.Core.VolumeReader? _volume;
    bool _wasMuted, _mailWaiting;
    int _watchTicks;
    DateTime _typedAt = DateTime.MinValue;

    /// <summary>For the self-test: pretend this game is running ("" = none, null = look for real).</summary>
    internal static string? GameForTest;
    internal static bool? MutedForTest;
    internal bool Gaming { get; private set; }

    /// <summary>A message arrived from that app (also used by the self-test). Only Discord counts.</summary>
    internal void MessageFrom(string app) { if (app.Contains("Discord", StringComparison.OrdinalIgnoreCase)) _mailWaiting = true; }

    void Watch(bool awake)
    {
        if (_watchTicks++ == 0)
        {
            KeyCount.Start();
            _notes.Arrived += (app, _, _) => MessageFrom(app); // the text itself is never read or shown
            try { _volume = new Pitzki.Core.VolumeReader(); } catch { }
        }

        // a game running: headphones on (looked for every 5 s, the process list is not free)
        if (_watchTicks % 5 == 1) Gaming = (GameForTest ?? AutoGame.Find()) != "";
        _him.Headset = Gaming && awake;

        if (_watchTicks % 2 == 0) _notes.Poll();
        bool muted = MutedForTest ?? (_volume != null && _volume.TryRead(out _, out bool m) && m);
        bool justMuted = muted && !_wasMuted && _watchTicks > 2;
        _wasMuted = muted;

        if (!awake || _him.Hobby != "") return;
        if (_mailWaiting) { _mailWaiting = false; _him.Mail(); return; }
        if (justMuted) { _him.Shh(); return; }
        if (KeyCount.Recent(3) >= 12 && (DateTime.Now - _typedAt).TotalSeconds > 10)
        {
            _typedAt = DateTime.Now;
            _him.TypeAlong();
        }
    }

    // ---------- weather ----------

    readonly Sky _sky = new();
    DateTime _skyAsked = DateTime.MinValue;

    /// <summary>For the self-test: pretend the weather is this ("rain", "hot", "" = fine, null = real).</summary>
    internal static string? WeatherForTest;

    string Weather()
    {
        if (WeatherForTest != null) return WeatherForTest;
        if (App.Board.WeatherCity.Trim() == "") return "";
        if (DateTime.Now - _skyAsked > TimeSpan.FromMinutes(15)) { _skyAsked = DateTime.Now; _ = _sky.RefreshAsync(App.Board); }
        string kind = Sky.Kind(_sky.Code, _sky.Day);
        if (kind is "rain" or "storm") return "rain";
        return _sky.Code >= 0 && _sky.Temp >= 28 ? "hot" : "";
    }

    // ---------- holidays ----------

    /// <summary>Today's holiday scene, or "": Rosh Hashanah and 1 January, Hanukkah (all eight days),
    /// Purim (14 Adar, Adar II in a leap year), Christmas (24-26 December).</summary>
    internal static string Holiday(DateTime day)
    {
        day = day.Date;
        if (day is { Month: 1, Day: 1 }) return "newyear";
        if (day.Month == 12 && day.Day is >= 24 and <= 26) return "christmas";
        var hc = new System.Globalization.HebrewCalendar();
        int year = hc.GetYear(day), month = hc.GetMonth(day), date = hc.GetDayOfMonth(day);
        if (month == 1 && date <= 2) return "newyear";
        foreach (int y in new[] { year, year - 1 })
        {
            var hanukkah = hc.ToDateTime(y, 3, 25, 0, 0, 0, 0);
            if (day >= hanukkah && day < hanukkah.AddDays(8)) return "hanukkah";
        }
        int adar = hc.IsLeapYear(year) ? 7 : 6;
        if (month == adar && date == 14) return "purim";
        return "";
    }

    string _holidayDone = "";
    string _alienDay = "";
    internal static bool AlienForTest;

    // ---------- which scene, when ----------

    /// <summary>For the self-test: the birthday to use instead of the one saved ("dd/MM").</summary>
    internal static string? BirthdayForTest;
    string _birthdayDone = "";

    bool IsBirthday()
    {
        string day = BirthdayForTest ?? App.Board.Birthday;
        return day != "" && Clock().ToString("dd/MM") == day;
    }

    /// <summary>
    /// The next scene: on his birthday the cake comes first (and now and then again), in the morning
    /// coffee takes the place of chocolate milk, and at the weekend (Friday, Saturday) the TV is a movie.
    /// </summary>
    internal string NextScene()
    {
        var now = Clock();
        if (IsBirthday() && (_birthdayDone != now.ToString("yyyyMMdd") || _random.Next(4) == 0))
        {
            _birthdayDone = now.ToString("yyyyMMdd");
            if (_lastHobby != "birthday") return "birthday";
        }
        // the rare one: about once a day, a flying saucer
        if (_alienDay != now.ToString("yyyyMMdd") && (_random.Next(40) == 0 || AlienForTest))
        {
            _alienDay = now.ToString("yyyyMMdd");
            return "alien";
        }
        string holiday = Holiday(now);
        if (holiday != "" && (_holidayDone != now.ToString("yyyyMMdd") || _random.Next(4) == 0))
        {
            _holidayDone = now.ToString("yyyyMMdd");
            if (_lastHobby != holiday) return holiday;
        }
        string weather = Weather();
        if (weather == "rain" && _lastHobby != "umbrella" && _random.Next(3) == 0) return "umbrella";
        if (weather == "hot" && _lastHobby != "popsicle" && _random.Next(3) == 0) return "popsicle";
        var choices = Mascot.Hobbies.Where(h => h != _lastHobby).ToArray();
        string pick = choices[_random.Next(choices.Length)];
        if (pick == "cocoa" && now.Hour is >= 6 and < 11) pick = "coffee";
        if (pick == "tv" && now.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday) pick = "movie";
        return pick == _lastHobby ? choices[0] : pick;
    }

    /// <summary>For the self-test: the clock he goes by, and how long a night-time wake-up lasts.</summary>
    internal static Func<DateTime> Clock = () => DateTime.Now;
    internal static double AwakeFor = 1800, NodOffFor = 120;

    DateTime _awakeUntil = DateTime.MinValue;
    DateTime _nextYawn;

    static bool Night => Clock().Hour is >= 23 or < 6;

    /// <summary>Clicked while asleep: up for thirty minutes. The last two he slowly nods off again.</summary>
    internal void Wake()
    {
        _awakeUntil = Clock().AddSeconds(AwakeFor);
        _him.Drowsy = 0;
        _him.Mood = MascotMood.Happy;
        _him.WakeUp();
        Rested();
        Mind();
    }

    /// <summary>
    /// Asleep at night, awake in the day, and at night awake for a while after a click: wide awake
    /// first, then the eyes slowly close over the last two minutes, with a yawn now and then.
    /// </summary>
    void Mind()
    {
        var now = Clock();
        double left = (_awakeUntil - now).TotalSeconds;
        // in the day he dozes off too, when nobody has touched the computer for ten minutes
        bool dozing = !Night && IdleSeconds() >= DozeAfter;
        if (_dozing && !dozing) { _him.Mood = MascotMood.Happy; _him.WakeUp(); Rested(); }
        _dozing = dozing;
        bool up = (!Night || left > 0) && !dozing;
        double drowsy = Night && left > 0 && left < NodOffFor ? 1 - left / NodOffFor : 0;

        if (!up)
        {
            if (_him.Hobby is not ("" or "hello" or "boot")) _him.StopHobby(); // a greeting finishes, then he sleeps
            _him.Mood = MascotMood.Sleepy; // first, so the heavy eyes go straight on to shut, never open again
            _him.Drowsy = 0;
        }
        else
        {
            _him.Mood = MascotMood.Happy;
            _him.Drowsy = drowsy;
            if (drowsy > 0 && _him.Hobby == "" && now >= _nextYawn)
            {
                if (_nextYawn != default) _him.Yawn();
                _nextYawn = now.AddSeconds(Math.Max(NodOffFor / 6, 1) * (0.8 + _random.NextDouble() * 0.6));
            }
        }

        int hour = now.Hour;
        string word = dozing ? "zzz..."
            : !up ? "good night"
            : drowsy > 0 ? "so sleepy..."
            : Night ? "awake!"
            : hour switch { < 12 => "morning", < 17 => "afternoon", < 21 => "evening", _ => "good night" };
        if (word != _shown && !Roam.Out) { _shown = word; _word.Say(word); }
    }

    string _shown = "";
    bool _dozing;

    /// <summary>For the self-test: pretend nobody has touched the computer for this long (null = ask Windows).</summary>
    internal static double? IdleForTest;
    internal static double DozeAfter = 600;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct LastInput { public uint Size, Time; }
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput info);

    /// <summary>Seconds since the keyboard or mouse was last used, anywhere on the computer.</summary>
    static double IdleSeconds()
    {
        if (IdleForTest is { } pretend) return pretend;
        var info = new LastInput { Size = 8 };
        if (!GetLastInputInfo(ref info)) return 0;
        return unchecked((uint)Environment.TickCount - info.Time) / 1000.0;
    }

    // ---------- eyes on the pointer, wherever it is ----------

    System.Windows.Threading.DispatcherTimer? _eyes;
    double _lookX, _lookY;

    /// <summary>Eight times a second: his eyes follow the pointer across the whole screen, not just over the card.</summary>
    void FollowPointer()
    {
        if (!IsVisible || IsMouseOver || _him.Hobby != "" || _him.Busy || _him.Mood == MascotMood.Sleepy) return;
        var centre = _him.PointToScreen(new Point(_him.ActualWidth / 2, _him.ActualHeight / 2));
        var at = Pointer();
        double x = Math.Clamp((at.X - centre.X) / 500, -1, 1), y = Math.Clamp((at.Y - centre.Y) / 400, -1, 1);
        if (Math.Abs(x - _lookX) + Math.Abs(y - _lookY) < 0.08) return;
        _lookX = x; _lookY = y;
        _him.LookAt(x, y);
    }

    // ---------- out for a walk ----------

    DateTime _lastClick = DateTime.MinValue, _lastRoam = DateTime.Now;

    /// <summary>For the self-test: start a walk at the next chance.</summary>
    internal static bool RoamSoonForTest;
    internal bool Roaming => Roam.Out;

    /// <summary>
    /// Pitzki leaves the card and wanders the desktop (Core.Roam): the card shows an empty spot and
    /// "out exploring..." until he is pulled back in on his rope.
    /// </summary>
    internal async void GoRoam()
    {
        if (Roam.Out || !IsVisible) return;
        if (_him.Hobby != "") _him.StopHobby();
        var card = Window.GetWindow(this);
        if (card == null) return;
        _lastRoam = DateTime.Now;
        await Roam.Go(card,
            hide: () => { _him.Visibility = Visibility.Hidden; _shown = "out exploring..."; _word.Say(_shown); },
            show: () =>
            {
                _him.Visibility = Visibility.Visible;
                _shown = "";
                Dispatcher.BeginInvoke(() => { _him.Hop(); _him.Sparkle(); });
            },
            stop: () => _him.Mood == MascotMood.Sleepy || Gaming || Pitzki.Core.Native.IsForegroundFullscreen(IntPtr.Zero));
        _lastRoam = DateTime.Now;
        Rested();
    }

    /// <summary>Now and then (about every ten minutes at most) a walk instead of a scene; never while a game or
    /// something full-screen is up.</summary>
    bool TimeToRoam()
    {
        if (RoamSoonForTest) { RoamSoonForTest = false; return true; }
        if (Roam.Out || Gaming || Night || DateTime.Now - _lastRoam < TimeSpan.FromMinutes(8)) return false;
        if (Pitzki.Core.Native.IsForegroundFullscreen(IntPtr.Zero)) return false;
        return _random.Next(5) == 0;
    }

    // ---------- the other cards ----------

    readonly Queue<(string Kind, string Card, double X, double Y)> _reactions = new();
    System.Windows.Threading.DispatcherTimer? _pushTimer;

    /// <summary>Which way, -1..1 each, from Pitzki to that window (or to a card of that kind if no window is given).</summary>
    (double X, double Y) Toward(Window? window, string kind = "")
    {
        window ??= Application.Current.Windows.OfType<WidgetWindow>().FirstOrDefault(w => w.Parts.Any(p => p.Kind == kind));
        if (window == null || !IsVisible) return (0, 0);
        try
        {
            var mine = _him.PointToScreen(new Point(_him.ActualWidth / 2, _him.ActualHeight / 2));
            var theirs = window.PointToScreen(new Point(window.ActualWidth / 2, window.ActualHeight / 2));
            return (Math.Clamp((theirs.X - mine.X) / 400, -1, 1), Math.Clamp((theirs.Y - mine.Y) / 300, -1, 1));
        }
        catch { return (0, 0); }
    }

    void Queue(string kind, Window? from = null, string card = "")
    {
        var (x, y) = Toward(from, card);
        if (_reactions.Count < 4) _reactions.Enqueue((kind, card, x, y));
    }

    /// <summary>A card told something (see Core.Buzz).</summary>
    void Heard(string what, FrameworkElement? from, double value, string detail)
    {
        var window = from == null ? null : Window.GetWindow(from);
        bool mine = window != null && ReferenceEquals(window, Window.GetWindow(this));
        switch (what)
        {
            case "news": Queue("news", window); break;
            case "clip": Queue("clip", window); break;
            case "usage": Queue("usage", window); break;
            case "near" when mine:
                // a card held over his: he peeks at it, and if it hangs about he pushes it off
                Queue("peek", null, "");
                _pushTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.6) };
                _pushTimer.Stop();
                _pushTimer.Tick -= PushTick;
                _pushTimer.Tick += PushTick;
                _pushTimer.Start();
                break;
            case "away" when mine: _pushTimer?.Stop(); break;
            case "joined" when mine || detail.StartsWith("pitzki|"): Queue("hug", null, "") ; break;
            case "removed" when detail != "pitzki": Queue("bye"); break;
        }
    }

    void PushTick(object? sender, EventArgs e)
    {
        _pushTimer?.Stop();
        Queue("push");
    }

    LastGame? _lastGame;
    DateTime _lastGameSeen = DateTime.MinValue, _fullWarned = DateTime.MinValue, _moneyMoved = DateTime.MinValue;
    double _money = -1;
    string _song = "";
    int _rung = -1, _pollTicks;

    /// <summary>For the self-test: pretend drive C has this many GB free, and the money total is this.</summary>
    internal static double? FreeGbForTest, MoneyForTest;

    /// <summary>Once a second: what the other cards are showing, read from the same sources they use.</summary>
    void Cards(bool awake)
    {
        _pollTicks++;
        var now = Clock();

        // the clock: on the hour, a bell (once an hour)
        if (now.Minute == 0 && now.Hour != _rung) { if (_rung != -1) Queue("hour", null, "clock"); _rung = now.Hour; }
        else if (_rung == -1) _rung = now.Minute == 0 ? now.Hour : -2;

        // the music card: a new song
        string title = _media.Title;
        if (title != _song) { if (_song != "" && title != "") Queue("song", null, "music"); _song = title; }

        // drive C nearly full: at most once an hour
        if (_pollTicks % 60 == 5 || FreeGbForTest != null)
        {
            double freeGb = FreeGbForTest ?? (new System.IO.DriveInfo("C").AvailableFreeSpace / 1e9);
            if (freeGb < 2 && DateTime.Now - _fullWarned > TimeSpan.FromHours(1)) { _fullWarned = DateTime.Now; Queue("full", null, "drives"); }
        }

        // the CS2 inventory and Steam items worth together: up or down by 1% or more (at most every 10 minutes)
        if (_pollTicks % 30 == 7 || MoneyForTest != null)
        {
            double money = MoneyForTest ?? (Pitzki.Core.Cs2Inventory.Value + Pitzki.Core.SteamItems.Value);
            if (money > 0)
            {
                if (_money > 0 && Math.Abs(money - _money) / _money >= 0.01 && DateTime.Now - _moneyMoved > TimeSpan.FromMinutes(10))
                {
                    _moneyMoved = DateTime.Now;
                    Queue(money > _money ? "rich" : "poor", null, "networth");
                }
                _money = money;
            }
        }

        // the last game card: a game just finished
        if (_pollTicks % 30 == 11)
        {
            _lastGame ??= new LastGame();
            try { _lastGame.Poll(); } catch { }
            if (_lastGame.Now is { } game && game.When > _lastGameSeen)
            {
                bool fresh = _lastGameSeen != DateTime.MinValue && DateTime.Now - game.When < TimeSpan.FromMinutes(30);
                _lastGameSeen = game.When;
                if (fresh) Queue(GameResult(game.Head + " " + game.Detail), null, "robrun");
            }
        }

        // one reaction at a time, when he is free
        if (awake && _reactions.Count > 0 && _him.Hobby == "" && !_him.Busy)
        {
            var (kind, _, x, y) = _reactions.Dequeue();
            _him.React(kind, x, y);
        }
        else if (!awake) _reactions.Clear();
    }

    /// <summary>How a finished game went, from the words of its summary: win, lose, or just played.</summary>
    internal static string GameResult(string words)
    {
        string w = " " + words.ToLowerInvariant() + " ";
        if (w.Contains(" won ") || w.Contains(" win ") || w.Contains("victory") || w.Contains("#1 ") || w.Contains(" 1st")) return "win";
        if (w.Contains(" lost ") || w.Contains(" lose ") || w.Contains("defeat")) return "lose";
        return "played";
    }

    /// <summary>For the self-test: the reactions waiting.</summary>
    internal int ReactionsWaiting => _reactions.Count;

    // ---------- chatter, low batteries, screenshots ----------

    static readonly string[] Chatter =
    {
        "I'm bored", "is it lunch yet?", "nice wallpaper!", "you're doing great!", "*hums a song*", "did you drink water?",
        "I like this desk", "beep boop", "what are we doing today?", "is it the weekend?", "hello up there!", "that's a lot of tabs",
    };
    DateTime _nextChat = DateTime.Now.AddMinutes(3);
    DateTime _batteryAsked = DateTime.MinValue, _batteryWarned = DateTime.MinValue;
    bool _batteryWaiting;
    int _printsSeen;

    /// <summary>For the self-test: pretend a device has this battery percent (null = ask Windows).</summary>
    internal static int? BatteryForTest;
    internal static bool ChatSoonForTest;

    async void Extras(bool awake)
    {
        if (ChatSoonForTest) { ChatSoonForTest = false; _nextChat = DateTime.Now; }
        if (awake && _him.Hobby == "" && !_him.Busy && DateTime.Now >= _nextChat)
        {
            _nextChat = DateTime.Now.AddMinutes(3 + _random.NextDouble() * 2);
            _him.Bubble(Chatter[_random.Next(Chatter.Length)]);
        }

        // a screenshot (the Print Screen key; only that key is ever looked at)
        if (KeyCount.PrintScreens != _printsSeen)
        {
            _printsSeen = KeyCount.PrintScreens;
            if (awake && _him.Hobby == "") _him.Pose();
        }

        // headphones, mouse and so on: every ten minutes, warn about one at 15% or less at most every half hour
        if (DateTime.Now - _batteryAsked > TimeSpan.FromMinutes(10) || BatteryForTest != null)
        {
            _batteryAsked = DateTime.Now;
            int lowest = 100;
            if (BatteryForTest is { } pretend) lowest = pretend;
            else
            {
                try { foreach (var (_, percent) in await Pitzki.Core.DeviceBattery.ReadAsync()) lowest = Math.Min(lowest, percent); }
                catch { }
            }
            if (lowest <= 15 && DateTime.Now - _batteryWarned > TimeSpan.FromMinutes(30)) { _batteryWarned = DateTime.Now; _batteryWaiting = true; }
        }
        if (_batteryWaiting && awake && _him.Hobby == "" && !_him.Busy) { _batteryWaiting = false; _him.LowBattery(); }
    }

    /// <summary>For the self-test: how long he stands still before the first scene (seconds).</summary>
    internal static double FirstWait = -1;

    DateTime _quietSince = DateTime.Now;
    double _waitFor = FirstWait >= 0 ? FirstWait : 60;
    DateTime _sceneStarted = DateTime.Now;

    /// <summary>About one scene a minute: 55 to 65 seconds, so it never feels like a metronome.</summary>
    double Gap() => 55 + _random.NextDouble() * 10;
    string _lastHobby = "";
    bool _wavePending;

    void Rested()
    {
        _quietSince = DateTime.Now;
        _waitFor = Gap();
    }

    /// <summary>
    /// When nobody has touched him for a while he finds something to do: chocolate milk, a game,
    /// TV, bubble gum or juggling, never the same one twice running. Not at night: then he sleeps.
    /// </summary>
    internal string LifeState => $"quiet {(DateTime.Now - _quietSince).TotalSeconds:0.0}s of {_waitFor:0.0}s, visible={IsVisible}, mouse over={IsMouseOver}, mood={_him.Mood}, drowsy={_him.Drowsy}";

    void Life()
    {
        Mind();
        Senses();
        if (!IsVisible || _him.Hobby != "" || _him.Mood == MascotMood.Sleepy || _him.Drowsy > 0 || IsMouseOver) return;
        // music on: he dances between scenes; a scene still comes about once a minute, then back to dancing
        if ((DateTime.Now - _quietSince).TotalSeconds < _waitFor) return;
        // not when he will start nodding off before the scene is over
        if (Night && (_awakeUntil - Clock()).TotalSeconds < NodOffFor + 15) return;
        if (Gaming)
        {
            // a game is on: instead of a scene of his own, he cheers you on
            _him.Cheer();
            Rested();
            return;
        }
        if (TimeToRoam()) { GoRoam(); return; }
        string pick = NextScene();
        if (_him.StartHobby(pick)) { _lastHobby = pick; _sceneStarted = DateTime.Now; }
        else _waitFor = (DateTime.Now - _quietSince).TotalSeconds + 5; // busy with something short (a yawn, fanning): try again in a few seconds
    }

    /// <summary>A wave, a hop or a sparkle, never the same one twice running.</summary>
    void Poke()
    {
        switch (_random.Next(4))
        {
            case 0: _him.Wave(); break;
            case 1: _him.Hop(); break;
            case 2: _him.Sparkle(); break;
            default: _him.Glance(); break;
        }
    }
}
