using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PitzkiWidgets.Core;
using Fx = Pitzki.Core.Fx;

namespace PitzkiWidgets.Widgets;

/// <summary>What there is to choose from, and the small pieces every widget is drawn out of.</summary>
public static class WidgetBody
{
    /// <summary>Every kind, with the group the picker files it under, in the order the picker shows them.</summary>
    public static readonly (string Kind, string Name, string Blurb, string Group)[] Kinds =
    {
        ("pitzki",   "Pitzki",       "He sits on the desktop and keeps you company", "Everyday"),
        ("clock",    "Clock",        "The time and today's date", "Everyday"),
        ("calendar", "Calendar",     "This month, with today marked", "Everyday"),
        ("weather",  "Weather",      "The temperature outside, and Pitzki out in it", "Everyday"),
        ("notes",    "Note",         "A sticky note you write straight on the desktop", "Everyday"),
        ("tasks",    "Tasks",        "A short list you tick off, right on the desktop", "Everyday"),
        ("timer",    "Timer",        "Count down minutes, and Pitzki dances when it is up", "Everyday"),
        ("pomodoro", "Focus rounds", "25 minutes of work, 5 of rest, and Pitzki keeps count", "Everyday"),
        ("countdown","Countdown",    "How many days are left until a date that matters", "Everyday"),
        ("agenda",   "What's next",  "The next things in the calendar of this PC", "Everyday"),
        ("calc",     "Calculator",   "Sums, units and money, answered as you type", "Everyday"),
        ("worldclock","World clock", "The time in three cities around the world", "Everyday"),
        ("photos",   "Photos",       "A picture frame for a folder of your photos", "Everyday"),
        ("shortcuts","Shortcuts",    "Your favourite programs, one click away", "Everyday"),

        ("stocks",   "Stocks",       "The stock market: your stocks, today's move and a line of the day", "Money"),
        ("chart",    "Stock chart",  "One stock big, over a day, a week, a month or a year", "Money"),
        ("portfolio","My portfolio", "The shares you own: what they are worth and how much you're up", "Money"),
        ("indices",  "Indexes",      "S&P 500, Nasdaq, Dow Jones and Tel Aviv 35", "Money"),
        ("markets",  "Market hours", "When New York and Tel Aviv open and close, counting down", "Money"),
        ("paper",    "Pretend portfolio", "$10,000 of play money: buy and sell real stocks, no real money", "Money"),
        ("guess",    "Higher or lower?", "Guess if a stock goes up tomorrow, and keep score", "Money"),
        ("marketnews","Market news", "A fresh headline about each of your stocks", "Money"),
        ("crypto",   "Crypto",       "Bitcoin, Ethereum and Solana, live, with a line of the day", "Money"),
        ("rates",    "Dollar & euro","The dollar and the euro in shekels, with a line of the week", "Money"),
        ("convert",  "Money converter", "Dollars, shekels, euros and coins, at the rate right now", "Money"),

        ("system",   "This PC",      "Processor, memory, the network, and a graph of the last minute", "Your PC"),
        ("drives",   "Drives",       "How much room is left on each drive", "Your PC"),
        ("batteries","Batteries",    "Headphones, mouse and controllers, and the laptop itself", "Your PC"),
        ("clipboard","Copied",       "The last few things you copied, to put back in one click", "Your PC"),
        ("clips",    "Clips",        "Your newest recording or screenshot, ready to open", "Your PC"),
        ("screentime","Screen time", "How long you were at the PC today, and on what", "Your PC"),
        ("claude",   "Claude usage", "How much of your Claude plan is used, and when it resets", "Your PC"),

        ("music",    "Music",        "Whatever is playing, with the cover and the buttons", "Music and games"),
        ("discord",  "Discord",      "Your voice channel: who is in, who is talking, Mute and Deafen", "Music and games"),
        ("sound",    "Sound",        "The microphone, Discord, and a volume for every app", "Music and games"),
        ("steam",    "Steam",        "Downloads, what is running, what you played last", "Music and games"),
        ("cs2inv",   "CS2 inventory","What your CS2 skins are worth, and how it moved since yesterday", "Music and games"),
        ("networth", "All my Steam", "Your CS2 inventory and Steam items together, in one number", "Music and games"),
        ("skinwatch","Skin watch list", "Skins you want, with a note when one drops to your price", "Music and games"),
        ("cases",    "Is opening worth it?", "Your CS2 cases: what opening one gives back on average", "Music and games"),
        ("steamitems","Steam items", "What your Steam trading cards, backgrounds and emoticons are worth", "Music and games"),
        ("news",     "Game updates", "What changed in the latest patch of your games", "Music and games"),
        ("robrun",   "Last game",    "How your last game went, any game (ROB RUN live)", "Music and games"),
    };

    public static string NameOf(string kind) => Kinds.FirstOrDefault(k => k.Kind == kind).Name ?? kind;

    /// <summary>Builds one. An unknown kind gets a card saying so rather than bringing the app down.</summary>
    public static FrameworkElement Make(Placed placed) => placed.Kind switch
    {
        "notes" => new NotesWidget(placed),
        "calendar" => new CalendarWidget(),
        "clock" => new ClockWidget(),
        "pitzki" => new PitzkiWidget(),
        "system" => new SystemWidget(placed),
        "drives" => new DrivesWidget(),
        "weather" => new WeatherWidget(),
        "stocks" => new StocksWidget(placed),
        "chart" => new ChartWidget(placed),
        "portfolio" => new PortfolioWidget(placed),
        "indices" => new StocksWidget(placed, "Indexes", "^GSPC, ^IXIC, ^DJI, TA35.TA"),
        "markets" => new MarketClockWidget(),
        "paper" => new PaperTradeWidget(placed),
        "guess" => new GuessWidget(placed),
        "marketnews" => new MarketNewsWidget(placed),
        "crypto" => new StocksWidget(placed, "Crypto", "BTC-USD, ETH-USD, SOL-USD"),
        "rates" => new StocksWidget(placed, "Dollar & euro", "ILS=X, EURILS=X", range: "5d"),
        "convert" => new ConverterWidget(placed),
        "cs2inv" => new InventoryWidget(),
        "steamitems" => new SteamItemsWidget(),
        "networth" => new NetWorthWidget(),
        "skinwatch" => new SkinWatchWidget(placed),
        "cases" => new CasesWidget(),
        "music" => new MusicWidget(),
        "steam" => new SteamWidget(),
        "robrun" => new RobRunWidget(),
        "tasks" => new TasksWidget(placed),
        "timer" => new TimerWidget(placed),
        "pomodoro" => new PomodoroWidget(placed),
        "countdown" => new CountdownWidget(placed),
        "shortcuts" => new ShortcutsWidget(placed),
        "agenda" => new AgendaWidget(),
        "clipboard" => new ClipboardWidget(),
        "news" => new NewsWidget(),
        "clips" => new ClipsWidget(),
        "batteries" => new BatteriesWidget(),
        "screentime" => new ScreenTimeWidget(),
        "photos" => new PhotosWidget(placed),
        "calc" => new CalcWidget(placed),
        "worldclock" => new WorldClockWidget(placed),
        "sound" => new SoundWidget(),
        "claude" => new ClaudeUsageWidget(),
        "discord" => new DiscordWidget(),
        _ => new TextBlock { Text = $"No widget called \"{placed.Kind}\"", Style = Caption },
    };

    public static Style Caption => (Style)Application.Current.FindResource("Caption");
    public static Style Big => (Style)Application.Current.FindResource("Big");
    public static Brush Of(string key) => (Brush)Application.Current.FindResource(key);

    /// <summary>A heading above a widget's numbers.</summary>
    public static TextBlock Title(string text) => new()
    {
        Text = text.ToUpperInvariant(), Style = Caption, Margin = new Thickness(0, 0, 0, 8),
    };

    /// <summary>
    /// One of the little buttons on a card. The card itself is dragged with the mouse, so a press is
    /// swallowed here, and the action happens when the button is let go over it.
    /// </summary>
    public static TextBlock Key(string glyph, Action press, bool icon = false, double size = 13, string? tip = null)
    {
        var key = new TextBlock
        {
            Text = glyph, FontSize = size, Foreground = Of("SubtleBrush"), Cursor = Cursors.Hand,
            Padding = new Thickness(5, 2, 5, 2), VerticalAlignment = VerticalAlignment.Center, ToolTip = tip,
        };
        if (icon) key.FontFamily = (FontFamily)Application.Current.FindResource("Icons");
        key.MouseLeftButtonDown += (_, e) => e.Handled = true;
        key.MouseLeftButtonUp += (_, e) => { e.Handled = true; press(); };
        key.MouseEnter += (_, _) => key.Tint(Of("TextBrush"));
        key.MouseLeave += (_, _) => key.Tint(Of("SubtleBrush"));
        return key;
    }

    /// <summary>A small rounded button with words on it: filled with the accent, or a quiet one.</summary>
    public static Border Pill(string text, Action press, bool quiet = false)
    {
        var words = new TextBlock
        {
            Text = text, FontSize = 11.5, FontWeight = FontWeights.SemiBold,
            Foreground = Of(quiet ? "TextBrush" : "OnAccentBrush"),
        };
        var pill = new Border
        {
            Child = words, CornerRadius = new CornerRadius(11), Padding = new Thickness(12, 4, 12, 4),
            Background = Of(quiet ? "ChipBrush" : "AccentBrush"), Cursor = Cursors.Hand,
            Margin = new Thickness(0, 0, 6, 0), RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new ScaleTransform(1, 1),
        };
        var scale = (ScaleTransform)pill.RenderTransform;
        pill.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Fx.Animate(scale, ScaleTransform.ScaleXProperty, 0.92, 80);
            Fx.Animate(scale, ScaleTransform.ScaleYProperty, 0.92, 80);
        };
        pill.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Fx.Animate(scale, ScaleTransform.ScaleXProperty, 1, 260, Fx.Spring);
            Fx.Animate(scale, ScaleTransform.ScaleYProperty, 1, 260, Fx.Spring);
            press();
        };
        pill.MouseLeave += (_, _) =>
        {
            Fx.Animate(scale, ScaleTransform.ScaleXProperty, 1, 160);
            Fx.Animate(scale, ScaleTransform.ScaleYProperty, 1, 160);
        };
        return pill;
    }

    /// <summary>"today", "yesterday", "3 days ago", "2 weeks ago": how long ago something was, in a few words.</summary>
    public static string Since(DateTime when)
    {
        var gone = DateTime.Now - when;
        if (gone < TimeSpan.FromMinutes(90)) return "just now";
        if (when.Date == DateTime.Today) return "today";
        if (when.Date == DateTime.Today.AddDays(-1)) return "yesterday";
        if (gone < TimeSpan.FromDays(14)) return $"{(int)gone.TotalDays} days ago";
        if (gone < TimeSpan.FromDays(60)) return $"{(int)(gone.TotalDays / 7)} weeks ago";
        return when.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A little line graph. Each series is a list of readings, newest on the right, drawn as a thin
    /// line, and filled underneath when it is the main one. A series with no fixed top scales itself to
    /// the highest reading it holds, but never to less than its floor, so quiet traffic does not look
    /// like a flood.
    /// </summary>
    public sealed class Spark : FrameworkElement
    {
        public sealed class Series
        {
            public readonly List<double> Values = new();
            public Color Colour;
            public bool Fill;
            /// <summary>The reading that is the top of the graph, or 0 to scale to what has been seen.</summary>
            public double Top;
            public double Floor = 1;
            /// <summary>Still read and kept, just not drawn: the user hid it with the card's pencil.</summary>
            public bool Hidden;
        }

        public readonly List<Series> Lines = new();
        readonly int _points;

        public Spark(double width, double height, int points)
        {
            Width = width; Height = height; _points = points;
            SnapsToDevicePixels = true;
            ClipToBounds = true;          // the newest reading slides in from past the right edge
        }

        /// <summary>
        /// How far along the graph still has to slide since the last reading came: 1 just after it came,
        /// 0 once it is in place. The lines are drawn that many steps to the right, so each new reading
        /// glides in from the edge and the whole graph scrolls along, instead of jumping a step.
        /// </summary>
        public static readonly DependencyProperty SlideProperty = DependencyProperty.Register(
            nameof(Slide), typeof(double), typeof(Spark), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Slide { get => (double)GetValue(SlideProperty); set => SetValue(SlideProperty, value); }

        /// <summary>Starts the slide for a reading that has just come in.</summary>
        void Scroll()
        {
            if (!Live.Showing(this)) { BeginAnimation(SlideProperty, null); return; }
            BeginAnimation(SlideProperty, new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(700)) { EasingFunction = Fx.EaseOut });
        }

        public Series Add(Color colour, bool fill, double top = 0, double floor = 1)
        {
            var series = new Series { Colour = colour, Fill = fill, Top = top, Floor = floor };
            Lines.Add(series);
            return series;
        }

        /// <summary>Adds a reading to a series, dropping the oldest once the graph is full.</summary>
        public void Push(Series series, double value)
        {
            series.Values.Add(double.IsFinite(value) ? Math.Max(value, 0) : 0);
            // one more than fits is kept: it is the reading sliding out past the left edge
            while (series.Values.Count > _points + 1) series.Values.RemoveAt(0);
            if (series == Lines[0]) Scroll();
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            // a faint floor to stand the lines on, and a half-way mark
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x22, 0xFF, 0xFF, 0xFF)), 1), new Point(0, h - 0.5), new Point(w, h - 0.5));
            dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(0x10, 0xFF, 0xFF, 0xFF)), 1), new Point(0, h / 2), new Point(w, h / 2));

            double step = w / Math.Max(_points - 1, 1);
            double slid = Slide * step;
            foreach (var line in Lines)
            {
                int n = line.Values.Count;
                if (n < 2 || line.Hidden) continue;

                double top = line.Top > 0 ? line.Top : Math.Max(line.Values.Max(), line.Floor);
                Point At(int i) => new(w + slid - (n - 1 - i) * step, h - 1.5 - Math.Clamp(line.Values[i] / top, 0, 1) * (h - 4));

                var stroke = new StreamGeometry();
                using (var g = stroke.Open())
                {
                    g.BeginFigure(At(0), false, false);
                    for (int i = 1; i < n; i++) g.LineTo(At(i), true, true);
                }
                stroke.Freeze();

                if (line.Fill)
                {
                    var area = new StreamGeometry();
                    using (var g = area.Open())
                    {
                        g.BeginFigure(new Point(At(0).X, h), true, true);
                        for (int i = 0; i < n; i++) g.LineTo(At(i), true, false);
                        g.LineTo(new Point(At(n - 1).X, h), true, false);
                    }
                    area.Freeze();
                    var wash = new LinearGradientBrush(
                        Color.FromArgb(0x66, line.Colour.R, line.Colour.G, line.Colour.B),
                        Color.FromArgb(0x00, line.Colour.R, line.Colour.G, line.Colour.B), 90);
                    dc.DrawGeometry(wash, null, area);
                }

                var pen = new Pen(new SolidColorBrush(line.Colour), line.Fill ? 1.6 : 1.2)
                { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
                dc.DrawGeometry(null, pen, stroke);

                // the main line's newest reading gets a dot with a soft halo: this is now
                if (!line.Fill) continue;
                var now = At(n - 1);
                now.X = Math.Min(now.X, w - 3);
                dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x40, line.Colour.R, line.Colour.G, line.Colour.B)), null, now, 4.5, 4.5);
                dc.DrawEllipse(new SolidColorBrush(line.Colour), null, now, 2.2, 2.2);
            }
        }
    }

    /// <summary>
    /// A thin rounded bar. Setting <see cref="Part"/> slides the fill to the new length rather than
    /// jumping there, and setting <see cref="Colour"/> blends the fill across to the new colour.
    /// </summary>
    public sealed class Meter : Border
    {
        readonly Border _fill;
        double _part;
        bool _set;

        public Meter(double width = 86)
        {
            Width = width; Height = 5; CornerRadius = new CornerRadius(3);
            Background = Of("ChipBrush"); VerticalAlignment = VerticalAlignment.Center;
            _fill = new Border { CornerRadius = new CornerRadius(3), HorizontalAlignment = HorizontalAlignment.Left, Background = Of("AccentBrush"), Width = 0 };
            Child = _fill;
        }

        /// <summary>0 to 1. Slides there over half a second.</summary>
        public double Part
        {
            get => _part;
            set => Glide(value, 500);
        }

        /// <summary>
        /// Slides to a length over the time given. A reading that comes every second glides for about
        /// that second, so the bar keeps moving rather than stepping. <paramref name="steady"/> keeps an
        /// even pace, for the bar of a song or a timer, where easing would make it lurch every tick.
        /// </summary>
        public void Glide(double part, int ms, bool steady = false)
        {
            part = Math.Clamp(double.IsFinite(part) ? part : 0, 0, 1);
            double to = Width * part;
            bool first = !_set;
            _set = true;
            _part = part;
            if (first || !Live.Showing(this) || ms <= 0)
            {
                _fill.BeginAnimation(WidthProperty, null);
                _fill.Width = to;
                return;
            }
            if (Math.Abs(_fill.ActualWidth - to) < 0.3 && !_fill.HasAnimatedProperties) return;
            var a = new System.Windows.Media.Animation.DoubleAnimation(to, TimeSpan.FromMilliseconds(ms));
            if (!steady) a.EasingFunction = Fx.EaseOut;
            // drawn only as often as it moves a visible amount: a song's bar creeping a pixel a second
            // needs a couple of frames, not sixty, while a real jump still slides at full speed
            double pixelsPerSecond = Math.Abs(to - _fill.ActualWidth) * 1000 / ms;
            System.Windows.Media.Animation.Timeline.SetDesiredFrameRate(a, (int)Math.Clamp(Math.Ceiling(pixelsPerSecond * 3), 4, App.MaxFps));
            _fill.BeginAnimation(WidthProperty, a);
        }

        /// <summary>How long the fill is drawn right now, which the tests read while it is sliding.</summary>
        internal double Drawn => _fill.ActualWidth;

        /// <summary>The colour the fill is painted right now.</summary>
        internal Color Painted => _fill.Background is SolidColorBrush b ? b.Color : Colors.Transparent;

        /// <summary>Straight there, for a bar being dragged by the mouse.</summary>
        public void Snap(double part) => Glide(part, 0);

        public Brush Colour { set => _fill.Background = Live.Blend(_fill.Background, value, this); }
    }

    /// <summary>A line of "name — value", with a bar between them when one is wanted.</summary>
    public sealed class Row : Grid
    {
        public readonly TextBlock Label = new() { Style = Caption, VerticalAlignment = VerticalAlignment.Center };
        public readonly TextBlock Value = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        public readonly Meter? Bar;

        public Row(bool bar, double barWidth = 70)
        {
            Margin = new Thickness(0, 3, 0, 3);
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Children.Add(Label);
            if (bar)
            {
                Bar = new Meter(barWidth) { Margin = new Thickness(8, 0, 8, 0), HorizontalAlignment = HorizontalAlignment.Stretch };
                Bar.Width = barWidth;
                SetColumn(Bar, 1);
                Children.Add(Bar);
            }
            SetColumn(Value, 2);
            Children.Add(Value);
        }
    }

    /// <summary>A timer that stops itself when the widget leaves the desktop.</summary>
    public static DispatcherTimer Every(this FrameworkElement owner, double seconds, Action tick)
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (_, _) => tick();
        owner.Loaded += (_, _) => timer.Start();
        owner.Unloaded += (_, _) => timer.Stop();
        tick();
        return timer;
    }
}
