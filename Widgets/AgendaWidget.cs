using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// What is coming up in the calendar of this PC: the next few things, the one happening now marked,
/// and the day written above each group. It only reads, and only from Windows itself.
/// </summary>
public sealed class AgendaWidget : UserControl
{
    const int Show = 4;

    readonly StackPanel _rows = new();
    readonly TextBlock _head = new() { Style = Caption, Margin = new Thickness(0, 0, 0, 8) };
    bool _asking;

    /// <summary>How many were found, for the tests.</summary>
    internal int Found { get; private set; } = -1;

    public AgendaWidget()
    {
        var box = new StackPanel { Width = 184 };
        box.Children.Add(_head);
        box.Children.Add(_rows);
        Content = box;
        _head.Text = "NEXT UP";
        _rows.Children.Add(new TextBlock { Text = "Looking in your calendar...", Style = Caption, TextWrapping = TextWrapping.Wrap });
        this.Every(300, Tick);
    }

    async void Tick()
    {
        if (_asking) return;
        _asking = true;
        List<Agenda.Entry>? soon = null;
        try { soon = await Agenda.SoonAsync(DateTime.Now); }
        catch { }
        _asking = false;
        Draw(soon, DateTime.Now);
    }

    /// <summary>Paints a list. The tests hand it a made-up one with a made-up "now".</summary>
    string _listed = "\u0001";

    internal void Draw(List<Agenda.Entry>? soon, DateTime now)
    {
        // when the things on the list are the same ones, only the times are new: no need to slide them in again
        string listed = soon == null ? "\u0002" : string.Join("\u0000", soon.Take(Show).Select(e => e.What + e.Starts.Ticks));
        bool changed = listed != _listed;
        _listed = listed;
        try { Fill(soon, now); }
        finally
        {
            if (changed)
                for (int i = 0; i < _rows.Children.Count; i++) Live.Arrive(_rows.Children[i], this, i);
        }
    }

    void Fill(List<Agenda.Entry>? soon, DateTime now)
    {
        _rows.Children.Clear();
        Found = soon?.Count ?? -1;

        if (soon == null)
        {
            _head.Say("NEXT UP");
            _rows.Children.Add(new TextBlock { Text = "Windows would not open the calendar on this PC.", Style = Caption, TextWrapping = TextWrapping.Wrap });
            return;
        }

        if (soon.Count == 0)
        {
            _head.Say(now.ToString("ddd d MMM", CultureInfo.InvariantCulture).ToUpperInvariant());
            _rows.Children.Add(new TextBlock { Text = "Nothing in the next week. Enjoy it.", Style = Caption, TextWrapping = TextWrapping.Wrap });
            return;
        }

        _head.Say(soon.Count == 1 ? "1 THING COMING UP" : soon.Count <= Show ? $"{soon.Count} COMING UP" : $"{Show} OF {soon.Count} COMING UP");
        string day = "";
        bool shown = false;
        foreach (var entry in soon.Take(Show))
        {
            shown = entry.Starts.Date != now.Date && entry.Starts.ToString("dddd d MMM", CultureInfo.InvariantCulture) == day;
            // a line saying which day, whenever the day changes and it is not today
            string its = entry.Starts.Date == now.Date ? "" : entry.Starts.ToString("dddd d MMM", CultureInfo.InvariantCulture);
            if (its != day && its.Length > 0)
            {
                day = its;
                shown = true;
                _rows.Children.Add(new TextBlock { Text = its, Style = Caption, Opacity = 0.75, Margin = new Thickness(0, _rows.Children.Count > 0 ? 7 : 0, 0, 2) });
            }
            _rows.Children.Add(Line(entry, now, shown));
        }
    }

    static UIElement Line(Agenda.Entry entry, DateTime now, bool dayShown)
    {
        bool on = entry.Now(now);
        var when = new TextBlock
        {
            Text = Agenda.When(entry, now, dayShown), FontSize = 11, FontWeight = FontWeights.SemiBold, Width = 60,
            FlowDirection = FlowDirection.LeftToRight,
            Foreground = Of(on ? "GoodBrush" : "AccentBrush"), VerticalAlignment = VerticalAlignment.Top,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        var what = new StackPanel();
        what.Children.Add(new TextBlock
        {
            Text = entry.What, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
            ToolTip = entry.Where.Length > 0 ? $"{entry.What}\n{entry.Where}" : entry.What,
        });
        if (entry.Where.Length > 0)
            what.Children.Add(new TextBlock
            {
                Text = entry.Where, FontSize = 10, Foreground = Of("SubtleBrush"),
                TextTrimming = TextTrimming.CharacterEllipsis,
            });

        var line = new Grid { Margin = new Thickness(0, 3, 0, 3) };
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        line.Children.Add(when);
        Grid.SetColumn(what, 1);
        line.Children.Add(what);

        if (!on) return line;
        // what is going on right now gets a soft wash behind it
        var wash = new Border { CornerRadius = new CornerRadius(7), Background = Of("ChipBrush"), Margin = new Thickness(-6, 0, -6, 0) };
        return new Grid { Children = { wash, line } };
    }
}
