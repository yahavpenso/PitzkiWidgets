using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Pitzki.Controls;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// A countdown for a number of minutes. Pick the minutes, press start, and it counts down with a bar.
/// When it reaches zero it chimes, and Pitzki, who has been standing beside it the whole time, dances.
/// It remembers when it is due, so a restart while it is running does not lose it.
/// </summary>
public sealed class TimerWidget : UserControl
{
    /// <summary>The lengths the minus and plus buttons step through.</summary>
    static readonly int[] Lengths = { 1, 5, 10, 15, 20, 25, 30, 45, 60, 90 };

    enum State { Idle, Running, Done }

    readonly Placed _placed;
    readonly TextBlock _time = new() { Style = Big, FontSize = 32, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _note = new() { FontSize = 11, Foreground = Of("SubtleBrush"), Margin = new Thickness(1, 0, 0, 0) };
    readonly Meter _along = new(166) { Margin = new Thickness(0, 9, 0, 0) };
    readonly Mascot _him = new() { Width = 62, Height = 48, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
    readonly DispatcherTimer _calm = new() { Interval = TimeSpan.FromSeconds(9) };
    State _state = State.Idle;
    State? _face;

    public TimerWidget(Placed placed)
    {
        _placed = placed;
        if (_placed.Number <= 0) _placed.Number = 25;

        // a timer that ran out long ago, while the app was shut, is not worth announcing
        if (_placed.Until is { } due && due < DateTime.Now.AddMinutes(-30)) _placed.Until = null;

        var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        words.Children.Add(_time);
        words.Children.Add(_note);

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        top.Children.Add(words);
        Grid.SetColumn(_him, 1);
        top.Children.Add(_him);

        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Timer"));
        box.Children.Add(top);
        box.Children.Add(_along);
        box.Children.Add(_buttons);
        Content = box;

        _calm.Tick += (_, _) => { _calm.Stop(); _him.Dancing = false; _him.Mood = MascotMood.Normal; };
        Unloaded += (_, _) => _calm.Stop();

        _state = _placed.Until is null ? State.Idle : _placed.Until > DateTime.Now ? State.Running : State.Done;
        Draw();
        this.Every(0.5, Tick);
    }

    DateTime Due => _placed.Until ?? DateTime.Now;
    TimeSpan Left => Due - DateTime.Now;

    // ----- what the buttons do -----

    /// <summary>One step along the list of lengths, down (-1) or up (+1).</summary>
    void Step(int by)
    {
        int at = Array.IndexOf(Lengths, _placed.Number);
        if (at < 0) at = Array.IndexOf(Lengths, 25);
        _placed.Number = Lengths[Math.Clamp(at + by, 0, Lengths.Length - 1)];
        App.Board.Save();
        Draw();
    }

    internal void Begin()
    {
        _placed.Until = DateTime.Now.AddMinutes(_placed.Number);
        App.Board.Save();
        _state = State.Running;
        _him.Hop();
        Draw();
    }

    internal void Reset()
    {
        _placed.Until = null;
        App.Board.Save();
        _state = State.Idle;
        _calm.Stop();
        _him.Dancing = false;
        _him.Mood = MascotMood.Normal;
        Draw();
    }

    // ----- the clock itself -----

    void Tick()
    {
        if (_state != State.Running) return;
        if (Left > TimeSpan.Zero) { Draw(); return; }

        // it has run out
        _state = State.Done;
        _him.Mood = MascotMood.Happy;
        _him.Celebrate();
        _him.Dancing = true;
        _calm.Stop();
        _calm.Start();
        if (!SelfTest.Wanted) SystemSounds.Asterisk.Play();
        Draw();
    }

    /// <summary>Paints whatever state it is in.</summary>
    void Draw()
    {
        // the buttons belong to the face, so they are only made again when the face changes
        bool fresh = _face != _state;
        _face = _state;
        if (fresh)
        {
            _buttons.Children.Clear();
            if (_state == State.Idle)
            {
                _buttons.Children.Add(Pill("Start", Begin));
                _buttons.Children.Add(Key("−", () => Step(-1), size: 16, tip: "Shorter"));
                _buttons.Children.Add(Key("+", () => Step(+1), size: 16, tip: "Longer"));
            }
            else if (_state == State.Running) _buttons.Children.Add(Pill("Stop", Reset, quiet: true));
            else
            {
                _buttons.Children.Add(Pill("Again", Begin));
                _buttons.Children.Add(Pill("Clear", Reset, quiet: true));
            }
            for (int i = 0; i < _buttons.Children.Count; i++) Live.Arrive(_buttons.Children[i], this, i);
        }

        if (_state == State.Idle)
        {
            _time.Size(32);
            _time.Say($"{_placed.Number:00}:00", quick: true);
            _time.Tint(Of("TextBrush"));
            _note.Say(_placed.Number == 1 ? "1 minute" : $"{_placed.Number} minutes", quick: true);
            Live.Show(_along, false, this);
            return;
        }

        if (_state == State.Running)
        {
            _time.Size(32);
            var left = Left < TimeSpan.Zero ? TimeSpan.Zero : Left;
            int whole = (int)Math.Ceiling(left.TotalSeconds);
            _time.Say(whole >= 3600 ? $"{whole / 3600}:{whole % 3600 / 60:00}:{whole % 60:00}" : $"{whole / 60:00}:{whole % 60:00}", quick: true, down: true);
            _time.Tint(Of("TextBrush"));
            _note.Say($"of {_placed.Number} min  ·  until {Due:HH:mm}");
            Live.Show(_along, true, this);
            _along.Glide(1 - left.TotalSeconds / (_placed.Number * 60.0), fresh ? 450 : 520, steady: !fresh);
            _along.Colour = Of("AccentBrush");
            return;
        }

        _time.Say("Time's up");
        _time.Size(24);
        _time.Tint(Of("AccentBrush"));
        _note.Say($"{_placed.Number} min, done at {Due:HH:mm}");
        Live.Show(_along, true, this);
        _along.Part = 1;
        _along.Colour = Of("GoodBrush");
        if (fresh) Live.Nudge(_time, this);
    }
}
