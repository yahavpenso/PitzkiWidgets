using System.Media;
using System.Windows;
using System.Windows.Controls;
using Pitzki.Controls;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Work in rounds: 25 minutes of focus, then a 5 minute break, and after every fourth round a long
/// break of 15. One press starts it and it carries on by itself, chiming at each change, until it is
/// stopped. It keeps when the round ends, so closing the app in the middle does not lose it, and it
/// counts the rounds finished today.
/// </summary>
public sealed class PomodoroWidget : UserControl
{
    public const int Focus = 25, Short = 5, Long = 15, Rounds = 4;

    readonly Placed _placed;
    readonly TextBlock _time = new() { Style = Big, FontSize = 32, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _note = new() { FontSize = 11, Foreground = Of("SubtleBrush"), Margin = new Thickness(1, 0, 0, 0) };
    readonly Meter _along = new(166) { Margin = new Thickness(0, 9, 0, 0) };
    readonly Mascot _him = new() { Width = 62, Height = 48, VerticalAlignment = VerticalAlignment.Center };
    readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
    readonly StackPanel _dots = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };

    string? _face;
    int _filled = -1;
    readonly TextBlock _today = new() { FontSize = 10.5, Foreground = Of("SubtleBrush"), Margin = new Thickness(4, -2, 0, 0) };

    public PomodoroWidget(Placed placed)
    {
        _placed = placed;
        // the round count is for today only
        if (_placed.Since?.Date != DateTime.Today) { _placed.Number = 0; _placed.Since = DateTime.Today; }
        if (_placed.Until is { } due && due < DateTime.Now.AddMinutes(-60)) { _placed.Until = null; _placed.Name = ""; }

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
        box.Children.Add(Title("Focus"));
        box.Children.Add(top);
        box.Children.Add(_along);
        box.Children.Add(_dots);
        box.Children.Add(_buttons);
        Content = box;

        Draw();
        this.Every(0.5, Tick);
    }

    /// <summary>"focus", "break", or "" when it is not running.</summary>
    internal string Phase => _placed.Name;

    /// <summary>The minutes of the break that follows the round that has just been finished (the long one on every fourth).</summary>
    internal static int BreakAfter(int roundsDone) => roundsDone > 0 && roundsDone % Rounds == 0 ? Long : Short;

    int Minutes => _placed.Name == "break" ? BreakAfter(_placed.Number) : Focus;
    DateTime Due => _placed.Until ?? DateTime.Now;
    TimeSpan Left => Due - DateTime.Now;

    internal void Begin()
    {
        _placed.Name = "focus";
        _placed.Until = DateTime.Now.AddMinutes(Focus);
        App.Board.Save();
        _him.Hop();
        Draw();
    }

    internal void Stop()
    {
        _placed.Name = "";
        _placed.Until = null;
        App.Board.Save();
        _him.Dancing = false;
        Draw();
    }

    /// <summary>Ends the round or the break now and goes on to the next.</summary>
    internal void Next()
    {
        if (_placed.Name == "focus") { _placed.Number++; _placed.Name = "break"; _placed.Until = DateTime.Now.AddMinutes(BreakAfter(_placed.Number)); }
        else { _placed.Name = "focus"; _placed.Until = DateTime.Now.AddMinutes(Focus); }
        App.Board.Save();
        _him.Hop();
        Draw();
    }

    void Tick()
    {
        if (_placed.Name == "") return;
        if (Left > TimeSpan.Zero) { Draw(); return; }
        _him.Celebrate();
        if (!SelfTest.Wanted) SystemSounds.Asterisk.Play();
        Next();
    }

    void Draw()
    {
        // four dots for the rounds, made once; the one that has just filled gives a little pop
        if (_dots.Children.Count == 0)
        {
            for (int i = 0; i < Rounds; i++)
                _dots.Children.Add(new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Margin = new Thickness(0, 0, 5, 0), Fill = Of("ChipBrush") });
            _dots.Children.Add(_today);
        }
        int done = _placed.Number % Rounds;
        for (int i = 0; i < Rounds; i++)
        {
            var dot = (System.Windows.Shapes.Ellipse)_dots.Children[i];
            dot.Fill = Live.Blend(dot.Fill, Of(i < done ? "AccentBrush" : "ChipBrush"), this);
            if (_filled >= 0 && i < done && i >= _filled) Live.Nudge(dot, this);
        }
        _filled = done;
        _today.Say($"{_placed.Number} today");

        bool fresh = _face != _placed.Name;
        _face = _placed.Name;
        if (fresh)
        {
            _buttons.Children.Clear();
            if (_placed.Name == "") _buttons.Children.Add(Pill("Start", Begin));
            else
            {
                _buttons.Children.Add(Pill("Skip", Next, quiet: true));
                _buttons.Children.Add(Pill("Stop", Stop, quiet: true));
            }
            for (int i = 0; i < _buttons.Children.Count; i++) Live.Arrive(_buttons.Children[i], this, i);
        }

        if (_placed.Name == "")
        {
            _time.Say($"{Focus:00}:00");
            _note.Say("25 min focus, then a break");
            Live.Show(_along, false, this);
            return;
        }

        var left = Left < TimeSpan.Zero ? TimeSpan.Zero : Left;
        int whole = (int)Math.Ceiling(left.TotalSeconds);
        _time.Say($"{whole / 60:00}:{whole % 60:00}", quick: true, down: !fresh);
        _note.Say(_placed.Name == "focus" ? $"Focus  ·  until {Due:HH:mm}" : $"{(Minutes == Long ? "Long break" : "Break")}  ·  until {Due:HH:mm}");
        Live.Show(_along, true, this);
        _along.Glide(1 - left.TotalSeconds / (Minutes * 60.0), fresh ? 450 : 520, steady: !fresh);
        _along.Colour = Of(_placed.Name == "focus" ? "AccentBrush" : "GoodBrush");
    }
}
