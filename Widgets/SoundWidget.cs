using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Pitzki's Sound tab as a card: who is using the microphone (with Mute mic), the camera when it is
/// on, Discord's voice channel with Mute and Deafen, and a volume slider for every app making sound.
/// </summary>
public sealed class SoundWidget : UserControl
{
    const double Wide = 280;

    readonly Grid _mic;
    readonly Ellipse _micDot = new() { Width = 7, Height = 7, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _micWords = new() { FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    readonly Border _micKey;
    readonly TextBlock _micKeyWords;

    readonly TextBlock _camera = new() { FontSize = 11.5, Margin = new Thickness(14, 2, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis };

    readonly Grid _discord;
    readonly Ellipse _discordDot = new() { Width = 7, Height = 7, Margin = new Thickness(0, 0, 7, 0), VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _discordWords = new() { FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _muteWords, _deafWords;

    readonly StackPanel _apps = new() { Margin = new Thickness(0, 6, 0, 0) };
    readonly Dictionary<int, AppRow> _rows = new();
    bool _micMuted;

    /// <summary>For the tests: how many app rows are on the card, and whether the mic and Discord lines show.</summary>
    internal int Apps => _rows.Count;
    internal bool MicShown => _mic.Visibility == Visibility.Visible;
    internal bool DiscordShown => _discord.Visibility == Visibility.Visible;

    public SoundWidget()
    {
        _micKey = Pill("Mute mic", ToggleMic, quiet: true);
        _micKeyWords = (TextBlock)_micKey.Child;
        _mic = Line(_micDot, _micWords, _micKey);

        var mute = Pill("Mute", () => _ = Voice.ToggleMute(), quiet: true);
        var deaf = Pill("Deafen", () => _ = Voice.ToggleDeaf(), quiet: true);
        _muteWords = (TextBlock)mute.Child;
        _deafWords = (TextBlock)deaf.Child;
        var both = new StackPanel { Orientation = Orientation.Horizontal };
        both.Children.Add(mute);
        both.Children.Add(deaf);
        _discord = Line(_discordDot, _discordWords, both);
        _discord.Margin = new Thickness(0, 5, 0, 0);
        _discordDot.Fill = Of("SubtleBrush");

        var box = new StackPanel { Width = Wide };
        box.Children.Add(Title("Sound"));
        box.Children.Add(_mic);
        box.Children.Add(_camera);
        box.Children.Add(_discord);
        box.Children.Add(_apps);
        Content = box;

        _camera.Visibility = Visibility.Collapsed;
        _discord.Visibility = Visibility.Collapsed;
        Voice.Changed += () => Dispatcher.BeginInvoke(ShowDiscord);
        this.Every(2, Tick);
    }

    static Grid Line(Ellipse dot, TextBlock words, FrameworkElement end)
    {
        var line = new Grid();
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.Children.Add(dot);
        Grid.SetColumn(words, 1);
        line.Children.Add(words);
        Grid.SetColumn(end, 2);
        end.Margin = new Thickness(6, 0, 0, 0);
        line.Children.Add(end);
        return line;
    }

    void ToggleMic()
    {
        if (Mixer.SetMicMuted(!_micMuted)) _micMuted = !_micMuted;
        ShowMic();
    }

    void Tick()
    {
        ShowMic();
        string? camera = PrivacyWatch.CameraUser();
        if (camera != null) _camera.Say("Camera in use · " + camera);
        Live.Show(_camera, camera != null, this);
        _ = Voice.Keep();
        ShowDiscord();
        ShowApps();
    }

    void ShowMic()
    {
        bool hasMic = Mixer.TryGetMicMuted(out _micMuted);
        string? user = PrivacyWatch.MicrophoneUser();
        _micWords.Say(_micMuted ? "Microphone muted" : user != null ? "Microphone in use · " + user : "Microphone on, nobody using it");
        _micDot.Fill = Live.Blend(_micDot.Fill, Of(_micMuted ? "BadBrush" : user != null ? "WarnBrush" : "SubtleBrush"), this);
        _micKeyWords.Say(_micMuted ? "Unmute" : "Mute mic", quick: true);
        Live.Show(_mic, hasMic, this);
    }

    void ShowDiscord()
    {
        bool show = Voice.Ready;
        if (show)
        {
            _discordWords.Say(Voice.Rpc.Channel.Length > 0
                ? $"Discord · {Voice.Rpc.Channel} · {Voice.Rpc.MemberCount}"
                : "Discord · no voice channel");
            _discordWords.ToolTip = Voice.Rpc.SpeakingNames() is { Count: > 0 } talking ? "Talking: " + string.Join(", ", talking) : null;
            _discordDot.Fill = Live.Blend(_discordDot.Fill, Of(Voice.Rpc.ChannelId.Length > 0 ? "GoodBrush" : "SubtleBrush"), this);
            _muteWords.Say(Voice.Rpc.Muted ? "Unmute" : "Mute", quick: true);
            _deafWords.Say(Voice.Rpc.Deafened ? "Undeafen" : "Deafen", quick: true);
        }
        Live.Show(_discord, show, this);
    }

    void ShowApps()
    {
        var apps = Mixer.ReadApps().Where(a => a.ProcessId != 0).Take(8).ToList();
        var alive = apps.Select(a => a.ProcessId).ToHashSet();

        foreach (var gone in _rows.Keys.Where(pid => !alive.Contains(pid)).ToList())
        {
            _apps.Children.Remove(_rows[gone]);
            _rows.Remove(gone);
        }
        for (int i = 0; i < apps.Count; i++)
        {
            var app = apps[i];
            if (!_rows.TryGetValue(app.ProcessId, out var row))
            {
                row = new AppRow(app);
                _rows[app.ProcessId] = row;
                _apps.Children.Insert(Math.Min(i, _apps.Children.Count), row);
                Live.Arrive(row, this, i);
            }
            row.Update(app);
        }

        if (apps.Count == 0 && _apps.Children.Count == 0)
            _apps.Children.Add(new TextBlock { Text = "No app is making sound", Style = Caption, Margin = new Thickness(0, 4, 0, 0) });
        else if (apps.Count > 0)
            foreach (var note in _apps.Children.OfType<TextBlock>().ToList()) _apps.Children.Remove(note);
    }

    /// <summary>One app: its icon, its name, a slider to drag, the level, and a mute button.</summary>
    sealed class AppRow : Grid
    {
        readonly Image _icon = new() { Width = 16, Height = 16, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock _name = new() { FontSize = 11.5, Width = 78, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
        readonly Meter _level = new(86);
        readonly TextBlock _percent = new() { FontSize = 11, Width = 32, TextAlignment = TextAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        readonly TextBlock _mute;
        MixerApp _app;
        bool _dragging;

        public AppRow(MixerApp app)
        {
            _app = app;
            Margin = new Thickness(0, 3, 0, 3);
            foreach (var w in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto })
                ColumnDefinitions.Add(new ColumnDefinition { Width = w });

            if (app.ExePath != null) _icon.Source = ShortcutsWidget.Icon(app.ExePath);
            _mute = Key("", () => { _app.SetMuted(!_app.Muted); Update(_app); }, icon: true, size: 12, tip: "Mute this app");

            var track = new Border { Background = Brushes.Transparent, Padding = new Thickness(0, 6, 0, 6), Cursor = Cursors.Hand, Child = _level };
            track.MouseLeftButtonDown += (_, e) => { e.Handled = true; _dragging = true; track.CaptureMouse(); Drag(e.GetPosition(_level).X); };
            track.MouseMove += (_, e) => { if (_dragging) Drag(e.GetPosition(_level).X); };
            track.MouseLeftButtonUp += (_, e) => { if (!_dragging) return; e.Handled = true; _dragging = false; track.ReleaseMouseCapture(); };

            Add(_icon, 0); Add(_name, 1); Add(track, 2); Add(_percent, 3); Add(_mute, 4);
        }

        void Add(UIElement e, int column) { SetColumn(e, column); Children.Add(e); }

        void Drag(double x)
        {
            double part = Math.Clamp(x / _level.Width, 0, 1);
            _level.Snap(part);                      // under the finger, so no gliding
            _app.SetVolume(part);
            if (_app.Muted && part > 0) _app.SetMuted(false);
            _percent.Text = $"{Math.Round(part * 100)}%";
        }

        public void Update(MixerApp app)
        {
            _app = app;
            _name.Say(app.Name);
            ToolTip = app.Name;
            if (_dragging) return;
            _level.Glide(app.Muted ? 0 : app.Volume, 400);
            _level.Colour = Of(app.Muted ? "SubtleBrush" : "AccentBrush");
            _percent.Say(app.Muted ? "muted" : $"{Math.Round(app.Volume * 100)}%", quick: true);
            _mute.Say(app.Muted ? "" : "", quick: true);
        }
    }

    /// <summary>
    /// Discord's voice controls, through the same sign-in Pitzki already made. It only ever uses the
    /// token Pitzki saved and never renews it, because renewing would sign Pitzki itself out. When that
    /// token is missing or old, the Discord line just stays hidden.
    /// </summary>
    internal static class Voice
    {
        public static readonly DiscordRpc Rpc = new();
        public static event Action? Changed;
        static bool _busy, _hooked;
        static DateTime _retry = DateTime.MinValue;

        public static bool Ready => Rpc.Connected && Rpc.VoiceReady;

        public static async Task Keep()
        {
            if (_busy || Ready || DateTime.Now < _retry) return;
            _busy = true;
            _retry = DateTime.Now.AddSeconds(20);
            try
            {
                if (!_hooked) { _hooked = true; Rpc.Changed += () => Changed?.Invoke(); }
                var (id, access, on) = PitzkiSignIn();
                if (!on || id.Length == 0 || access.Length == 0) return;
                if (!Rpc.Connected && !await Rpc.ConnectAsync(id)) return;
                await Rpc.SignInForVoiceAsync("", access, "", mayAsk: false);
                Changed?.Invoke();
            }
            catch { }
            finally { _busy = false; }
        }

        public static async Task ToggleMute() { try { if (Ready) await Rpc.SetMutedAsync(!Rpc.Muted); } catch { } }
        public static async Task ToggleDeaf() { try { if (Ready) await Rpc.SetDeafenedAsync(!Rpc.Deafened); } catch { } }

        /// <summary>The Discord app id and token from Pitzki's own settings, read only.</summary>
        static (string id, string access, bool on) PitzkiSignIn()
        {
            try
            {
                string file = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pitzki", "settings.json");
                if (!System.IO.File.Exists(file)) return ("", "", false);
                using var doc = JsonDocument.Parse(System.IO.File.ReadAllText(file));
                var root = doc.RootElement;
                string Text(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
                bool on = root.TryGetProperty("DiscordVoice", out var voice) && voice.ValueKind == JsonValueKind.True;
                return (Text("DiscordAppId"), Text("DiscordAccess"), on);
            }
            catch { return ("", "", false); }
        }
    }
}
