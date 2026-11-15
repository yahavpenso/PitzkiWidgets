using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>This month laid out as a grid, with today ringed in the accent colour.</summary>
public sealed class CalendarWidget : UserControl
{
    static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-GB");

    readonly TextBlock _month = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 7) };
    readonly Grid _days = new();
    DateTime _drawn = DateTime.MinValue;

    public CalendarWidget()
    {
        for (int i = 0; i < 7; i++) _days.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
        for (int i = 0; i < 7; i++) _days.RowDefinitions.Add(new RowDefinition { Height = new GridLength(22) });

        var box = new StackPanel();
        box.Children.Add(Title("Calendar"));
        box.Children.Add(_month);
        box.Children.Add(_days);
        Content = box;

        // once an hour is plenty: nothing here changes faster than a day
        this.Every(3600, Draw);
    }

    void Draw()
    {
        var today = DateTime.Today;
        if (today == _drawn) return;
        _drawn = today;

        _month.Say(today.ToString("MMMM yyyy", En));
        _days.Children.Clear();

        // Monday first, the way a week is read here
        string[] heads = { "M", "T", "W", "T", "F", "S", "S" };
        for (int i = 0; i < 7; i++) Put(new TextBlock
        {
            Text = heads[i], Style = Caption, FontSize = 9.5,
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        }, i, 0);

        var first = new DateTime(today.Year, today.Month, 1);
        int before = ((int)first.DayOfWeek + 6) % 7;      // how many empty squares the month starts with
        int length = DateTime.DaysInMonth(today.Year, today.Month);

        for (int day = 1; day <= length; day++)
        {
            int cell = before + day - 1;
            bool now = day == today.Day;

            var number = new TextBlock
            {
                Text = day.ToString(), FontSize = 11.5,
                Foreground = Of(now ? "TextBrush" : "SubtleBrush"),
                FontWeight = now ? FontWeights.SemiBold : FontWeights.Normal,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };

            if (!now) { Put(number, cell % 7, cell / 7 + 1); continue; }

            var ring = new Border
            {
                Width = 22, Height = 20, CornerRadius = new CornerRadius(7), Background = Of("AccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
                Child = number,
            };
            Put(ring, cell % 7, cell / 7 + 1);
            Live.Nudge(ring, this);            // midnight: the ring pops onto the new day
        }
    }

    void Put(UIElement what, int column, int row)
    {
        Grid.SetColumn(what, column);
        Grid.SetRow(what, row);
        _days.Children.Add(what);
    }
}
