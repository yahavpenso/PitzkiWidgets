using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Where today went: the total time at the PC, the programs it went to, and the last seven days as
/// little bars. The counting is Pitzki's own (<see cref="ScreenTime"/>), kept in a file of the widgets'
/// own so the two apps never write over each other. Nothing leaves the PC.
/// </summary>
public sealed class ScreenTimeWidget : UserControl
{
    // one counter for every card: two cards on the desktop must not count each second twice
    static ScreenTime? _counter;
    static DispatcherTimer? _ticking;
    internal static string File = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitzkiWidgets", "screentime.json");

    /// <summary>For the README clips: nothing real is counted, and this is shown as what is in use now.</summary>
    internal static string? Pretend;

    internal static ScreenTime Counter
    {
        get
        {
            if (_counter != null) return _counter;
            _counter = new ScreenTime(File);
            _ticking = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _ticking.Tick += (_, _) => { if (Pretend == null) _counter.Tick(IntPtr.Zero, true); };
            _ticking.Start();
            Application.Current.Exit += (_, _) => _counter.Save();
            return _counter;
        }
    }

    readonly TextBlock _total = new() { Style = Big, FontSize = 26 };
    readonly TextBlock _now = new() { Style = Caption, Margin = new Thickness(0, 0, 0, 6), TextTrimming = TextTrimming.CharacterEllipsis };
    readonly StackPanel _apps = new();
    readonly Grid _week = new() { Height = 34, Margin = new Thickness(0, 8, 0, 0) };
    readonly List<Border> _bars = new();
    double _shownTotal;

    /// <summary>How many programs are listed right now, for the tests.</summary>
    internal int Listed => _apps.Children.OfType<Row>().Count();

    public ScreenTimeWidget()
    {
        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Screen time today"));
        box.Children.Add(_total);
        box.Children.Add(_now);
        box.Children.Add(_apps);

        // a bar a day, today on the right in the accent colour
        for (int i = 0; i < 7; i++)
        {
            _week.ColumnDefinitions.Add(new ColumnDefinition());
            var cell = new Grid { Margin = new Thickness(2, 0, 2, 0) };
            cell.RowDefinitions.Add(new RowDefinition());
            cell.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var bar = new Border
            {
                CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Bottom, Height = 2,
                Background = Of(i == 6 ? "AccentBrush" : "ChipBrush"),
            };
            var day = new TextBlock
            {
                Text = DateTime.Today.AddDays(i - 6).ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)[..1],
                FontSize = 9, Foreground = Of("SubtleBrush"), HorizontalAlignment = HorizontalAlignment.Center,
            };
            Grid.SetRow(day, 1);
            cell.Children.Add(bar);
            cell.Children.Add(day);
            Grid.SetColumn(cell, i);
            _week.Children.Add(cell);
            _bars.Add(bar);
        }
        box.Children.Add(_week);
        Content = box;

        this.Every(5, Show);
    }

    void Show()
    {
        var counter = Counter;
        double total = counter.Total(DateTime.Today);
        _total.Count(total < 60 ? "0 m" : ScreenTime.Spell(total), total, _shownTotal);
        _shownTotal = total;
        string now = Pretend ?? counter.Now;
        _now.Say(now.Length > 0 ? "Now: " + now : "Counting while you use the PC");

        // the top four programs, each with a bar against the biggest
        var top = counter.Day(DateTime.Today).Take(4).ToList();
        while (_apps.Children.Count > top.Count) _apps.Children.RemoveAt(_apps.Children.Count - 1);
        while (_apps.Children.Count < top.Count)
        {
            var row = new Row(true, 52);
            row.Label.MaxWidth = 70;
            row.Label.TextTrimming = TextTrimming.CharacterEllipsis;
            _apps.Children.Add(row);
            Live.Arrive(row, this, _apps.Children.Count - 1);
        }
        double most = top.Count > 0 ? Math.Max(top[0].Seconds, 1) : 1;
        for (int i = 0; i < top.Count; i++)
        {
            var row = (Row)_apps.Children[i];
            row.Label.Say(top[i].App);
            row.ToolTip = top[i].App;
            row.Value.Say(ScreenTime.Spell(top[i].Seconds).Replace("under a minute", "<1 m"));
            row.Bar!.Glide(top[i].Seconds / most, 800);
        }

        var week = counter.Week();
        double longest = Math.Max(week.Max(d => d.Seconds), 60);
        for (int i = 0; i < 7 && i < week.Count; i++)
        {
            double h = 2 + 20 * week[i].Seconds / longest;
            var bar = _bars[i];
            bar.ToolTip = week[i].Day.ToString("dddd", System.Globalization.CultureInfo.InvariantCulture) + " · " + ScreenTime.Spell(week[i].Seconds);
            if (!Live.Showing(this)) { bar.Height = h; continue; }
            if (Math.Abs(bar.ActualHeight - h) < 0.5) continue;
            Fx.Animate(bar, HeightProperty, h, 700);
        }
    }
}
