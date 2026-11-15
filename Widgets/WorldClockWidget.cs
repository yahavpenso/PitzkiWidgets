using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Shapes;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// The time in three cities, with how far ahead or behind they are and a sun or moon dot. A click on
/// a city's name moves it to the next city on the list; the choice stays on the card.
/// </summary>
public sealed class WorldClockWidget : UserControl
{
    /// <summary>The cities to choose from, with Windows' own name for their time zone.</summary>
    internal static readonly (string City, string Zone)[] Cities =
    {
        ("New York", "Eastern Standard Time"), ("London", "GMT Standard Time"), ("Tokyo", "Tokyo Standard Time"),
        ("Los Angeles", "Pacific Standard Time"), ("Chicago", "Central Standard Time"), ("Paris", "Romance Standard Time"),
        ("Berlin", "W. Europe Standard Time"), ("Moscow", "Russian Standard Time"), ("Dubai", "Arabian Standard Time"),
        ("Delhi", "India Standard Time"), ("Bangkok", "SE Asia Standard Time"), ("Beijing", "China Standard Time"),
        ("Sydney", "AUS Eastern Standard Time"), ("São Paulo", "E. South America Standard Time"),
        ("Jerusalem", "Israel Standard Time"), ("Athens", "GTB Standard Time"),
    };

    readonly Placed _placed;
    readonly List<(TextBlock city, TextBlock time, TextBlock diff, Ellipse dot)> _rows = new();

    /// <summary>For the tests: the cities on the card and the time each shows.</summary>
    internal string Shown => string.Join(", ", _rows.Select(r => $"{r.city.Text} {r.time.Text} {r.diff.Text}"));

    public WorldClockWidget(Placed placed)
    {
        _placed = placed;
        if (_placed.Items.Count != 3) _placed.Items = new() { "New York", "London", "Tokyo" };

        var box = new StackPanel { Width = 196 };
        box.Children.Add(Title("World clock"));
        for (int i = 0; i < 3; i++)
        {
            int at = i;
            var line = new Grid { Margin = new Thickness(0, 3, 0, 3) };
            foreach (var w in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto })
                line.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
            var dot = new Ellipse { Width = 7, Height = 7, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            var names = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Cursor = Cursors.Hand, ToolTip = "Click for another city", Background = System.Windows.Media.Brushes.Transparent };
            var city = new TextBlock { FontSize = 12.5, FontWeight = FontWeights.SemiBold };
            var diff = new TextBlock { Style = Caption };
            names.Children.Add(city);
            names.Children.Add(diff);
            names.MouseLeftButtonDown += (_, e) => e.Handled = true;
            names.MouseLeftButtonUp += (_, e) => { e.Handled = true; Next(at); };
            var time = new TextBlock { Style = Big, FontSize = 22, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(names, 1);
            Grid.SetColumn(time, 2);
            line.Children.Add(dot);
            line.Children.Add(names);
            line.Children.Add(time);
            box.Children.Add(line);
            _rows.Add((city, time, diff, dot));
        }
        Content = box;
        this.Every(1, Tick);
    }

    /// <summary>Moves one row to the next city on the list that is not already on the card.</summary>
    internal void Next(int row)
    {
        int at = Array.FindIndex(Cities, c => c.City == _placed.Items[row]);
        for (int step = 1; step <= Cities.Length; step++)
        {
            var next = Cities[(at + step + Cities.Length) % Cities.Length].City;
            if (_placed.Items.Contains(next)) continue;
            _placed.Items[row] = next;
            break;
        }
        App.Board.Save();
        Live.Nudge(_rows[row].city, this);
        Tick();
    }

    void Tick()
    {
        var here = DateTime.Now;
        for (int i = 0; i < _rows.Count; i++)
        {
            var (city, time, diff, dot) = _rows[i];
            var pick = Cities.FirstOrDefault(c => c.City == _placed.Items[i]);
            if (pick.City == null) pick = Cities[i];
            DateTime there;
            try { there = TimeZoneInfo.ConvertTime(here, TimeZoneInfo.FindSystemTimeZoneById(pick.Zone)); }
            catch { there = here; }

            double hours = Math.Round((there - here).TotalMinutes / 30) / 2;
            string ahead = hours == 0 ? "same time" : (hours > 0 ? "+" : "−") + Math.Abs(hours).ToString("0.#", CultureInfo.InvariantCulture) + " h";
            string day = there.Date > here.Date ? " · tomorrow" : there.Date < here.Date ? " · yesterday" : "";

            city.Say(pick.City);
            diff.Say(ahead + day);
            time.Say(there.ToString("HH:mm", CultureInfo.InvariantCulture));
            bool sunUp = there.Hour >= 7 && there.Hour < 19;
            dot.Fill = Live.Blend(dot.Fill, Of(sunUp ? "WarnBrush" : "AccentBrush"), this);
            dot.ToolTip = sunUp ? "Day there" : "Night there";
        }
    }
}
