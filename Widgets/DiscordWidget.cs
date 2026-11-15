using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using Fx = Pitzki.Core.Fx;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Discord on the desktop: the voice channel you are in, everyone in it with their picture, a green ring
/// around whoever is talking and a small red mark on whoever has their mic or sound off, Mute and Deafen
/// buttons, and how many Discord messages came in since you last looked. It talks to the Discord app
/// through the same sign-in Pitzki made (read only, never renewed), like the Discord line on the Sound card.
/// </summary>
public sealed class DiscordWidget : UserControl
{
    const double Wide = 236;

    readonly TextBlock _channel = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _talking = new() { FontSize = 11, Foreground = Of("SubtleBrush"), TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 1, 0, 6) };
    readonly Ellipse _dot = new() { Width = 8, Height = 8, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly WrapPanel _people = new() { Margin = new Thickness(0, 0, 0, 4) };
    readonly StackPanel _keys = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
    readonly TextBlock _muteWords, _deafWords;
    readonly Border _mute, _deaf;
    readonly TextBlock _mail = new() { FontSize = 11, Foreground = Of("SubtleBrush"), Margin = new Thickness(0, 8, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
    readonly TextBlock _state = new() { FontSize = 11, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap };
    readonly Dictionary<string, Face> _faces = new();
    readonly Pitzki.Core.NotificationWatch _notes = Pitzki.Core.NotificationWatch.Shared;   // one poll of Windows serves every card
    int _messages, _ticks;
    long _lookedAt = long.MinValue / 2;
    bool _running;
    bool _watching;

    /// <summary>For the tests.</summary>
    internal string Channel => _channel.Text;
    internal string State => _state.Text;
    internal string Talking => _talking.Text;
    internal int Faces => _faces.Count;
    internal string Mail => _mail.Text;
    internal void MessageFrom(string app) { if (app.Contains("Discord", StringComparison.OrdinalIgnoreCase)) { _messages++; ShowMail(); } }
    internal void Feed(List<(string Id, string Name, string? Avatar, bool Talking, bool Silent)> people, string channel, bool muted, bool deaf)
    {
        _frozen = true;                                  // the real Discord no longer redraws over the made-up channel
        Show(true, channel, people, muted, deaf);
    }
    bool _frozen;

    public DiscordWidget()
    {
        _mute = Pill("Mute", () => _ = SoundWidget.Voice.ToggleMute(), quiet: true);
        _deaf = Pill("Deafen", () => _ = SoundWidget.Voice.ToggleDeaf(), quiet: true);
        _muteWords = (TextBlock)_mute.Child;
        _deafWords = (TextBlock)_deaf.Child;
        _keys.Children.Add(_mute);
        _keys.Children.Add(_deaf);

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.Children.Add(_dot);
        Grid.SetColumn(_channel, 1);
        head.Children.Add(_channel);
        _dot.Fill = Of("SubtleBrush");

        _mail.MouseLeftButtonDown += (_, e) => e.Handled = true;
        _mail.MouseLeftButtonUp += (_, e) => { e.Handled = true; OpenDiscord(); };

        var box = new StackPanel { Width = Wide };
        box.Children.Add(Title("Discord"));
        box.Children.Add(head);
        box.Children.Add(_talking);
        box.Children.Add(_people);
        box.Children.Add(_keys);
        box.Children.Add(_state);
        box.Children.Add(_mail);
        Content = box;

        _channel.Text = "Looking for Discord...";
        _mail.Visibility = Visibility.Collapsed;
        _keys.Visibility = Visibility.Collapsed;
        _talking.Visibility = Visibility.Collapsed;

        SoundWidget.Voice.Changed += () => Dispatcher.BeginInvoke(Refresh);
        this.Every(2, Tick);
    }

    void Tick()
    {
        if (!_watching) { _watching = true; _notes.Arrived += (app, _, _) => Dispatcher.BeginInvoke(() => MessageFrom(app)); }   // only the app's name is used
        if (_ticks++ % 2 == 0) _notes.Poll();
        _ = SoundWidget.Voice.Keep();
        Refresh();
    }

    void Refresh()
    {
        if (_frozen) return;
        var rpc = SoundWidget.Voice.Rpc;
        bool ready = SoundWidget.Voice.Ready;
        // listing the processes is not free: once every ten seconds is enough to see Discord open or close
        if (Environment.TickCount64 - _lookedAt > 10_000) { _lookedAt = Environment.TickCount64; _running = Process.GetProcessesByName("Discord").Length > 0; }
        bool running = _running;
        if (!ready)
        {
            Show(false, "", new(), false, false);
            _channel.Say(running ? "Discord" : "Discord is closed");
            _state.Say(running ? "Turn on Discord voice in Pitzki's settings to see your channel here" : "Open Discord to see your voice channel");
            _dot.Fill = Live.Blend(_dot.Fill, Of("SubtleBrush"), this);
            return;
        }
        Show(true, rpc.Channel, rpc.People(), rpc.Muted, rpc.Deafened);
    }

    void Show(bool ready, string channel, List<(string Id, string Name, string? Avatar, bool Talking, bool Silent)> people, bool muted, bool deaf)
    {
        Live.Show(_keys, ready, this);
        if (!ready) { Clear(); Live.Show(_talking, false, this); return; }

        _state.Say("");
        _muteWords.Say(muted ? "Unmute" : "Mute", quick: true);
        _deafWords.Say(deaf ? "Undeafen" : "Deafen", quick: true);
        _mute.Background = Live.Blend(_mute.Background, Of(muted ? "BadBrush" : "ChipBrush"), this);
        _deaf.Background = Live.Blend(_deaf.Background, Of(deaf ? "BadBrush" : "ChipBrush"), this);

        if (channel.Length == 0)
        {
            _channel.Say("Not in a voice channel");
            _dot.Fill = Live.Blend(_dot.Fill, Of("SubtleBrush"), this);
            Clear();
            Live.Show(_talking, false, this);
            return;
        }

        _channel.Say(channel);
        _dot.Fill = Live.Blend(_dot.Fill, Of(muted || deaf ? "WarnBrush" : "GoodBrush"), this);
        var talking = people.Where(p => p.Talking).Select(p => p.Name).ToList();
        _talking.Say(talking.Count > 0 ? "Talking: " + string.Join(", ", talking)
            : people.Count == 1 ? "Just you" : $"{people.Count} people, nobody talking");
        Live.Show(_talking, true, this);

        foreach (var gone in _faces.Keys.Where(k => people.All(p => p.Id != k)).ToList())
        {
            var face = _faces[gone];
            _faces.Remove(gone);
            Fx.Disappear(face.Root, dx: 0, dy: 6, ms: 160, done: () => _people.Children.Remove(face.Root));
        }
        int order = 0;
        foreach (var p in people.OrderBy(p => p.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            if (!_faces.TryGetValue(p.Id, out var face))
            {
                face = _faces[p.Id] = new Face(p.Name, p.Avatar);
                _people.Children.Add(face.Root);
                Live.Arrive(face.Root, this, order++);
            }
            face.Set(p.Talking, p.Silent, this);
        }
    }

    void Clear()
    {
        foreach (var face in _faces.Values) _people.Children.Remove(face.Root);
        _faces.Clear();
    }

    void ShowMail()
    {
        _mail.Say(_messages == 1 ? "1 new message · open Discord" : $"{_messages} new messages · open Discord");
        Live.Show(_mail, _messages > 0, this);
        if (_messages > 0) Live.Nudge(_mail, this);
    }

    void OpenDiscord()
    {
        _messages = 0;
        Live.Show(_mail, false, this);
        try { Process.Start(new ProcessStartInfo("discord://-/channels/@me") { UseShellExecute = true }); } catch { }
    }

    // ----- one person -----

    /// <summary>A round picture (or the first letter), a ring that lights up green while they talk, a red mark when silent.</summary>
    sealed class Face
    {
        public readonly StackPanel Root = new() { Width = 46, Margin = new Thickness(0, 2, 4, 2) };
        readonly Ellipse _ring = new() { Width = 36, Height = 36, StrokeThickness = 2.2, Stroke = Brushes.Transparent };
        readonly Ellipse _off = new() { Width = 11, Height = 11, StrokeThickness = 1.6, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom };

        public Face(string name, string? avatar)
        {
            Root.ToolTip = name;
            var holder = new Grid { Width = 36, Height = 36, HorizontalAlignment = HorizontalAlignment.Center };
            var back = new Ellipse { Width = 31, Height = 31, Fill = Of("ChipBrush") };
            var letter = new TextBlock
            {
                Text = name.Length > 0 ? char.ToUpperInvariant(name[0]).ToString() : "?", FontSize = 13, FontWeight = FontWeights.SemiBold,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            holder.Children.Add(back);
            holder.Children.Add(letter);
            if (avatar != null)
            {
                try
                {
                    var picture = new BitmapImage();
                    picture.BeginInit();
                    picture.UriSource = new Uri(avatar);
                    picture.DecodePixelWidth = 64;
                    picture.CacheOption = BitmapCacheOption.OnLoad;
                    picture.EndInit();
                    holder.Children.Add(new Ellipse { Width = 31, Height = 31, Fill = new ImageBrush(picture) { Stretch = Stretch.UniformToFill } });
                }
                catch { }
            }
            holder.Children.Add(_ring);
            _off.Fill = Of("BadBrush");
            _off.Stroke = Of("ChipBrush");
            _off.Visibility = Visibility.Collapsed;
            holder.Children.Add(_off);
            Root.Children.Add(holder);
            Root.Children.Add(new TextBlock
            {
                Text = name, FontSize = 9.5, Foreground = Of("SubtleBrush"), TextTrimming = TextTrimming.CharacterEllipsis,
                HorizontalAlignment = HorizontalAlignment.Center, MaxWidth = 46, Margin = new Thickness(0, 2, 0, 0),
            });
        }

        public void Set(bool talking, bool silent, FrameworkElement owner)
        {
            _ring.Stroke = Live.Blend(_ring.Stroke, talking ? Of("GoodBrush") : Brushes.Transparent, owner);
            Live.Show(_off, silent, owner);
        }
    }
}
