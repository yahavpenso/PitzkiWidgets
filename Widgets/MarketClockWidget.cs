using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using static PitzkiWidgets.Widgets.MarketBits;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// When the stock exchanges in New York and Tel Aviv open and close, with a clock counting down to the
/// next one. The hours come from the exchanges' own trading day as Yahoo reports it (asked every half an
/// hour), so holidays and short days are right; the counting is done here, every second.
/// </summary>
public sealed class MarketClockWidget : UserControl
{
    /// <summary>The exchanges, each with a symbol that trades there, and the days of the week it trades on.</summary>
    static readonly (string Name, string Symbol, DayOfWeek[] Days)[] Exchanges =
    {
        ("New York", "^GSPC", new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }),
        ("Tel Aviv", "TA35.TA", new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }),
    };

    sealed class Place
    {
        public long Start, End;     // today's (or the last) trading day, in Unix seconds
        public readonly TextBlock State = new() { FontSize = 12, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
        public readonly TextBlock When = new() { FontSize = 10.5, Foreground = Of("SubtleBrush"), HorizontalAlignment = HorizontalAlignment.Right };
        public readonly TextBlock Hours = new() { FontSize = 10.5, Foreground = Of("SubtleBrush") };
        public readonly Meter Day = new(232) { Margin = new Thickness(0, 4, 0, 0) };
    }

    readonly List<Place> _places = new();

    /// <summary>For the tests: what each exchange says, and a made-up "now".</summary>
    internal IEnumerable<string> Shown => _places.Select(p => p.State.Text + " | " + p.When.Text + " | " + p.Hours.Text);
    internal Func<DateTimeOffset> Now = () => DateTimeOffset.UtcNow;
    internal void Set(int which, long start, long end) { _places[which].Start = start; _places[which].End = end; Tick(); }
    internal Task Refresh() => RefreshAsync();

    public MarketClockWidget()
    {
        var box = new StackPanel { Width = 232 };
        box.Children.Add(Title("Market hours"));
        foreach (var (name, _, _) in Exchanges)
        {
            var place = new Place();
            var row = new Grid { Margin = new Thickness(0, 2, 0, 8) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = name, FontSize = 13, FontWeight = FontWeights.SemiBold });
            left.Children.Add(place.Hours);
            var right = new StackPanel();
            right.Children.Add(place.State);
            right.Children.Add(place.When);
            Grid.SetColumn(right, 1);
            row.Children.Add(left);
            row.Children.Add(right);
            var block = new StackPanel();
            block.Children.Add(row);
            block.Children.Add(place.Day);
            block.Margin = new Thickness(0, 0, 0, 6);
            box.Children.Add(block);
            _places.Add(place);
        }
        box.Children.Add(Small("Your time. Hours from the exchange itself, so holidays count."));
        Content = box;

        this.Every(1800, () => _ = RefreshAsync());
        this.Every(1, Tick);
    }

    async Task RefreshAsync()
    {
        for (int i = 0; i < Exchanges.Length; i++)
        {
            var q = await Market.QuoteAsync(Exchanges[i].Symbol);
            if (q == null || q.OpenAt <= 0) continue;
            _places[i].Start = q.OpenAt;
            _places[i].End = q.CloseAt;
        }
        Tick();
    }

    /// <summary>The next time this exchange opens after "now", from the hour it opened on its last trading day.</summary>
    internal static DateTimeOffset NextOpen(long start, DateTimeOffset now, DayOfWeek[] days, TimeZoneInfo zone)
    {
        var opened = TimeZoneInfo.ConvertTime(DateTimeOffset.FromUnixTimeSeconds(start), zone);
        var day = TimeZoneInfo.ConvertTime(now, zone).Date;
        for (int i = 0; i < 10; i++, day = day.AddDays(1))
        {
            if (!days.Contains(day.DayOfWeek)) continue;
            var local = day + opened.TimeOfDay;
            var at = new DateTimeOffset(local, zone.GetUtcOffset(local));
            if (at > now) return at;
        }
        return now.AddDays(1);
    }

    static TimeZoneInfo Zone(int which)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(which == 0 ? "Eastern Standard Time" : "Israel Standard Time"); }
        catch { return TimeZoneInfo.Local; }
    }

    void Tick()
    {
        var now = Now();
        for (int i = 0; i < _places.Count; i++)
        {
            var p = _places[i];
            if (p.Start <= 0) { p.State.Say("..."); continue; }
            var start = DateTimeOffset.FromUnixTimeSeconds(p.Start);
            var end = DateTimeOffset.FromUnixTimeSeconds(p.End);
            p.Hours.Say($"{start.ToLocalTime():HH:mm} – {end.ToLocalTime():HH:mm}");
            bool open = now >= start && now < end;
            if (open)
            {
                p.State.Say("Open");
                p.State.Tint(Of("GoodBrush"));
                p.When.Say("closes in " + Market.Span(end - now));
                p.Day.Glide((now - start).TotalSeconds / Math.Max(1, (end - start).TotalSeconds), 900, steady: true);
                p.Day.Colour = Of("GoodBrush");
            }
            else
            {
                var next = now < start ? start : NextOpen(p.Start, now, Exchanges[i].Days, Zone(i));
                p.State.Say("Closed");
                p.State.Tint(Of("SubtleBrush"));
                p.When.Say("opens in " + Market.Span(next - now));
                p.Day.Glide(0, 500);
            }
        }
    }
}
