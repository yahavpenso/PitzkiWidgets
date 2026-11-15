using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// What Windows says is playing - Spotify, YouTube in a browser, anything that tells Windows -
/// with the cover, and buttons that really pause and skip.
/// </summary>
public sealed class MusicWidget : UserControl
{
    readonly MediaService _media = new();
    readonly Image _cover = new() { Width = 46, Height = 46, Stretch = Stretch.UniformToFill };
    readonly Border _coverBox;
    readonly TextBlock _title = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _artist = new() { FontSize = 11, Foreground = Of("SubtleBrush"), TextTrimming = TextTrimming.CharacterEllipsis };
    readonly Meter _along = new(166);
    readonly TextBlock _play;
    readonly TextBlock _at = new() { FontSize = 10, Foreground = Of("SubtleBrush") };
    readonly TextBlock _end = new() { FontSize = 10, Foreground = Of("SubtleBrush"), HorizontalAlignment = HorizontalAlignment.Right };
    bool _scrubbing;

    // the words of the song, the line being sung and a faint one after it
    readonly LyricsService _lyrics = new();
    readonly TextBlock _sung = new() { FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, MaxHeight = 36 };
    readonly TextBlock _next = new() { FontSize = 10.5, Foreground = Of("SubtleBrush"), TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center };
    readonly StackPanel _words = new() { Margin = new Thickness(0, 8, 0, 0) };
    int _line = -2;

    /// <summary>For the tests: the line on the card now, and whether the song has timed words.</summary>
    internal string Sung => _sung.Text;
    internal bool HasLyrics => _lyrics.HasLyrics;
    /// <summary>For the README clips: its ShowDemo puts a made-up song on the card.</summary>
    internal MediaService Media => _media;

    public MusicWidget()
    {
        _coverBox = new Border
        {
            Width = 46, Height = 46, CornerRadius = new CornerRadius(10), Background = Of("ChipBrush"),
            ClipToBounds = true, Child = _cover, VerticalAlignment = VerticalAlignment.Center,
        };

        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0), Width = 92 };
        words.Children.Add(_title);
        words.Children.Add(_artist);

        _play = Key("⏵", () => _media.PlayPause());
        _play.FontSize = 15;
        _play.Margin = new Thickness(4, 0, 0, 0);

        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(_coverBox);
        line.Children.Add(words);
        line.Children.Add(_play);

        // the bar can be grabbed anywhere along it to move through the song
        var track = new Border { Background = Brushes.Transparent, Padding = new Thickness(0, 7, 0, 3), Cursor = Cursors.Hand, Child = _along, ToolTip = "Drag to move through it" };
        track.MouseLeftButtonDown += (_, e) => { e.Handled = true; _scrubbing = true; track.CaptureMouse(); Scrub(e.GetPosition(_along).X, false); };
        track.MouseMove += (_, e) => { if (_scrubbing) Scrub(e.GetPosition(_along).X, false); };
        track.MouseLeftButtonUp += (_, e) =>
        {
            if (!_scrubbing) return;
            e.Handled = true;
            _scrubbing = false;
            track.ReleaseMouseCapture();
            Scrub(e.GetPosition(_along).X, true);
        };

        var times = new Grid { Margin = new Thickness(0, 1, 0, 0) };
        times.Children.Add(_at);
        times.Children.Add(_end);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0), HorizontalAlignment = HorizontalAlignment.Center };
        buttons.Children.Add(Key("⏮", () => _media.Previous()));
        buttons.Children.Add(Key("⏭", () => _media.Next()));

        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Now playing"));
        box.Children.Add(line);
        box.Children.Add(track);
        box.Children.Add(times);
        box.Children.Add(buttons);
        _words.Children.Add(_sung);
        _words.Children.Add(_next);
        _words.Visibility = Visibility.Collapsed;
        box.Children.Add(_words);
        Content = box;

        _lyrics.Changed += () => Dispatcher.BeginInvoke(() => { _line = -2; Sing(); });

        _media.Changed += Show;
        Show();
        // once only: joining or pulling the card out loads it again
        bool asked = false;
        Loaded += async (_, _) => { if (asked) return; asked = true; await _media.InitAsync(); };
        this.Every(1, Along);
        this.Every(0.4, Sing);      // a line of words lasts seconds: looking twice a second is plenty
    }

    /// <summary>Moves the words along with the song: the new line rolls up into place.</summary>
    void Sing()
    {
        bool timed = _lyrics.HasLyrics && _media.HasSession;
        Live.Show(_words, timed, this);
        if (!timed) { _line = -2; return; }
        int at = _lyrics.IndexAt(_media.Position);
        if (at == _line) return;
        _line = at;
        var lines = _lyrics.Lines;
        string now = at >= 0 ? lines[at].text : "";
        string after = at + 1 < lines.Count ? lines[at + 1].text : "";
        _sung.Say(now.Length > 0 ? now : "♪");
        _next.Say(after);
    }

    /// <summary>One of the little buttons. The card itself is being dragged, so a press is swallowed here.</summary>
    static TextBlock Key(string glyph, Action press)
    {
        var key = new TextBlock
        {
            Text = glyph, FontSize = 13, Foreground = Of("SubtleBrush"),
            Cursor = Cursors.Hand, Margin = new Thickness(9, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center,
        };
        key.MouseLeftButtonDown += (_, e) => e.Handled = true;
        key.MouseLeftButtonUp += (_, e) => { e.Handled = true; press(); };
        key.MouseEnter += (_, _) => key.Foreground = Of("TextBrush");
        key.MouseLeave += (_, _) => key.Foreground = Of("SubtleBrush");
        return key;
    }

    /// <summary>The bar was grabbed at that spot: show that moment, and on letting go, really go there.</summary>
    void Scrub(double x, bool go)
    {
        if (_media.Duration <= TimeSpan.Zero) return;
        double part = Math.Clamp(x / _along.Width, 0, 1);
        _along.Snap(part);                      // under the finger, so no gliding
        var at = TimeSpan.FromSeconds(_media.Duration.TotalSeconds * part);
        _at.Text = Clock(at);
        if (go) _media.Seek(at);
    }

    /// <summary>"3:07", or "1:02:14" for something over an hour.</summary>
    internal static string Clock(TimeSpan time)
    {
        if (time < TimeSpan.Zero) time = TimeSpan.Zero;
        int whole = (int)time.TotalSeconds;
        return whole >= 3600 ? $"{whole / 3600}:{whole % 3600 / 60:00}:{whole % 60:00}" : $"{whole / 60}:{whole % 60:00}";
    }

    void Show()
    {
        bool something = _media.HasSession && _media.Title.Length > 0;
        if (something) _lyrics.Load(_media.Title, _media.Artist, _media.Duration);
        else _lyrics.Clear();
        _title.Say(something ? _media.Title : "Nothing playing");
        _artist.Say(something ? _media.Artist : "Put something on");
        _play.Say(_media.IsPlaying ? "⏸" : "⏵", quick: true);
        Live.Picture(_cover, _media.Art, dx: 10);
        Fx.Animate(_coverBox, OpacityProperty, something ? 1 : 0.4, 300);
        Live.Show(_along, something && _media.Duration > TimeSpan.Zero, this, Visibility.Hidden);
        if (_media.ArtColor is { } colour) _along.Colour = new SolidColorBrush(Palette.Bright(colour));
        Along();
    }

    void Along()
    {
        if (_scrubbing) return;                 // while it is being dragged, the finger decides where it is
        if (_media.Duration <= TimeSpan.Zero)
        {
            _along.Glide(0, 400);
            _at.Say("");
            _end.Say("");
            return;
        }
        // a second's worth of song is played over the second until the next reading, at an even pace;
        // a jump (a new song, a skip) eases there instead
        double part = _media.Position.TotalSeconds / _media.Duration.TotalSeconds;
        if (Math.Abs(part - _along.Part) > 0.04) _along.Glide(part, 450);
        else _along.Glide(part, 1000, steady: true);
        _at.Say(Clock(_media.Position), quick: true);
        _end.Say("-" + Clock(_media.Duration - _media.Position), quick: true, down: true);
    }
}
