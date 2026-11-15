using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pitzki.Controls;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// The weather where you are, as a little scene Pitzki is standing in: the rain really falls on
/// him and he puts up an umbrella, snow drifts past and he pulls on a hat, the sun turns behind
/// his head, and at night he nods off. The temperature is underneath, with one word for it.
/// </summary>
public sealed class WeatherWidget : UserControl
{
    readonly Sky _sky = new();
    readonly Mascot _him = new() { Width = 184, Height = 112 };
    readonly TextBlock _degrees = new() { Style = Big, FontSize = 26, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _word = new()
    {
        FontSize = 11.5, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap,
        VerticalAlignment = VerticalAlignment.Center, TextAlignment = TextAlignment.Right, Width = 104,
    };
    readonly Random _random = new();
    string _weather = "x";
    double _wasTemp;

    public WeatherWidget()
    {
        var under = new Grid { Margin = new Thickness(2, 2, 2, 0) };
        under.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        under.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        under.Children.Add(_degrees);
        Grid.SetColumn(_word, 1);
        under.Children.Add(_word);

        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Weather"));
        box.Children.Add(_him);
        box.Children.Add(under);
        Content = box;

        // he watches the pointer while it is over the card
        Background = Brushes.Transparent;
        MouseMove += (_, e) =>
        {
            var where = e.GetPosition(_him);
            _him.LookAt(where.X / Math.Max(_him.ActualWidth, 1) * 2 - 1, where.Y / Math.Max(_him.ActualHeight, 1) * 2 - 1);
        };
        MouseLeave += (_, _) => _him.LookAt(0, 0);
        PreviewMouseLeftButtonUp += (_, _) => Poke();

        _sky.Changed += Show;
        Show();
        this.Every(300, async () => await _sky.RefreshAsync(App.Board));
        this.Every(14, Poke);           // every so often he does something on his own
    }

    void Show()
    {
        if (_sky.Text.Length == 0)
        {
            _degrees.Say("--");
            _word.Say(_sky.Trouble.Length > 0 ? _sky.Trouble : "Looking...");
            Weather("");
            return;
        }
        double temp = Math.Round(_sky.Temp);
        _degrees.Count(temp + "°", temp, _wasTemp);
        _wasTemp = temp;
        _word.Say(Sky.Describe(_sky.Code));
        if (App.Board.WeatherPlace.Length > 0) ToolTip = App.Board.WeatherPlace;
        Weather(Sky.Kind(_sky.Code, _sky.Day));
    }

    /// <summary>Puts the weather around him, and dresses him for it.</summary>
    internal void Weather(string kind)
    {
        if (kind == _weather) return;
        _weather = kind;

        // the mascot draws its own rain, snow, cloud, fog and sun; a clear night is simply dark
        _him.Sky = kind == "moon" ? "" : kind;
        _him.Prop = kind is "rain" or "storm" ? "umbrella" : "";
        _him.Hat = kind == "snow" ? "Beanie" : "None";
        _him.Mood = kind switch
        {
            "moon" => MascotMood.Sleepy,
            "sun" => MascotMood.Happy,
            "storm" => MascotMood.Sad,
            _ => MascotMood.Normal,
        };

        if (kind.Length > 0) Poke();
    }

    /// <summary>Something small, in keeping with the weather.</summary>
    void Poke()
    {
        switch (_weather)
        {
            case "moon":
                if (_random.Next(3) == 0) _him.Glance();
                break;
            case "sun":
                if (_random.Next(2) == 0) _him.Sparkle(); else _him.Wave();
                break;
            case "rain":
            case "storm":
                if (_random.Next(2) == 0) _him.Shrug(); else _him.Glance();
                break;
            case "snow":
                if (_random.Next(2) == 0) _him.Hop(); else _him.Shrug();
                break;
            default:
                if (_random.Next(2) == 0) _him.Hop(); else _him.Glance();
                break;
        }
    }
}
