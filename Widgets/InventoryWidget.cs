using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// What the CS2 skins on this PC's Steam account are worth: the total big, how it moved since the day
/// before, a line of the last days, and the dearest things in it. The same count Pitzki's CS2 tab does
/// (Pitzki.Core.Cs2Inventory), and the two share what they saved, so each one's count helps the other.
/// </summary>
public sealed class InventoryWidget : UserControl
{
    readonly TextBlock _value = new() { Style = Big, FontSize = 26, VerticalAlignment = VerticalAlignment.Bottom };
    readonly TextBlock _move = new() { FontSize = 11.5, Foreground = Of("SubtleBrush"), Margin = new Thickness(0, 1, 0, 0) };
    readonly TextBlock _movers = new() { FontSize = 10.5, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 4, 0, 0) };
    readonly TextBlock _about = new() { FontSize = 10.5, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    readonly StocksWidget.DayLine _line = new() { Height = 30, Margin = new Thickness(0, 6, 0, 4) };
    readonly StackPanel _items = new();
    readonly List<(TextBlock Name, Meter Bar, TextBlock Price)> _rows = new();
    double _was;
    bool _has;

    const int Shown = 4;

    /// <summary>For the tests.</summary>
    internal string ValueText => _value.Text;
    internal string MoveText => _move.Text;
    internal string MoversText => string.Concat(_movers.Inlines.OfType<System.Windows.Documents.Run>().Select(r => r.Text));
    internal void Redraw() => Show();

    public InventoryWidget()
    {
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(Title("CS2 inventory"));
        var again = Key("", () => { _about.Say("Checking..."); Cs2Inventory.Start(force: true); }, icon: true, size: 11.5, tip: "Check the prices again");
        again.Margin = new Thickness(0, -4, -4, 0);
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(again, 2);
        head.Children.Add(again);
        var money = MarketBits.MoneyKey(Show);
        Grid.SetColumn(money, 1);
        head.Children.Add(money);

        for (int i = 0; i < Shown; i++)
        {
            var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
            var name = new TextBlock { FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
            var bar = new Meter(40) { Margin = new Thickness(6, 0, 6, 0) };
            var price = new TextBlock { FontSize = 11.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(bar, 1);
            Grid.SetColumn(price, 2);
            row.Children.Add(name);
            row.Children.Add(bar);
            row.Children.Add(price);
            row.Visibility = Visibility.Collapsed;
            _items.Children.Add(row);
            _rows.Add((name, bar, price));
        }

        var box = new StackPanel { Width = 212 };
        box.Children.Add(head);
        box.Children.Add(_value);
        box.Children.Add(_move);
        box.Children.Add(_line);
        box.Children.Add(_items);
        box.Children.Add(_movers);
        box.Children.Add(_about);
        Content = box;

        void Changed() => Dispatcher.BeginInvoke(Show);
        Loaded += (_, _) => { Cs2Inventory.Changed += Changed; MoneyPrefs.Changed += Changed; };
        Unloaded += (_, _) => { Cs2Inventory.Changed -= Changed; MoneyPrefs.Changed -= Changed; };
        Show();
        // it only really asks Steam every half an hour; this just notices when that time has come
        this.Every(300, () => { if (!SelfTest.Wanted) Cs2Inventory.Start(); Show(); });
    }

    /// <summary>What moved most since the day before: one up, one down, and a note from the tray for a move of 10% or more.</summary>
    void ShowMovers()
    {
        var movers = Cs2Inventory.Movers();
        var up = movers.Where(m => m.Percent > 0).OrderByDescending(m => m.Percent).FirstOrDefault();
        var down = movers.Where(m => m.Percent < 0).OrderBy(m => m.Percent).FirstOrDefault();
        string Name(Cs2InvItem i) => Cs2Inventory.Short(i.Name).TrimStart('★', ' ');
        _movers.Inlines.Clear();
        if (up.Item != null) _movers.Inlines.Add(new System.Windows.Documents.Run($"▲ {Name(up.Item)} +{up.Percent:0.0}%") { Foreground = Of("GoodBrush") });
        if (up.Item != null && down.Item != null) _movers.Inlines.Add(new System.Windows.Documents.Run("   "));
        if (down.Item != null) _movers.Inlines.Add(new System.Windows.Documents.Run($"▼ {Name(down.Item)} {down.Percent:0.0}%") { Foreground = Of("BadBrush") });
        Live.Show(_movers, movers.Count > 0, this);
        foreach (var m in movers.Where(m => Math.Abs(m.Percent) >= 10))
            App.Alert("skin|" + m.Item.Name + (m.Percent > 0 ? "+" : "-"), $"{Name(m.Item)} {(m.Percent > 0 ? "went up" : "went down")} {Math.Abs(m.Percent):0}%",
                $"Now {Cs2Inventory.Money(m.Item.Price)}, it was {Cs2Inventory.Money(m.Before)}");
    }

    void Show()
    {
        if (!Cs2Inventory.HasItems)
        {
            _value.Say("--");
            _move.Say("");
            _about.Say(Cs2Inventory.Problem switch
            {
                "private" => "Your Steam inventory is private. Set it to Public in Steam, then press the arrows.",
                "no steam" => "Open Steam and sign in to see what your skins are worth.",
                "offline" => "Couldn't reach Steam. Press the arrows to try again.",
                _ => "Counting your skins...",
            });
            Live.Show(_line, false, this);
            foreach (var (name, _, _) in _rows) Live.Show((UIElement)name.Parent, false, this);
            return;
        }

        double value = Cs2Inventory.Value;
        string text = Cs2Inventory.Money(value);
        if (_has) _value.Count(text, MoneyPrefs.Shown(value), _was); else _value.Say(text);
        _was = MoneyPrefs.Shown(value);
        _has = true;
        _value.Tint(Of("AccentBrush"));

        var before = Cs2Inventory.Before;
        if (before == null)
        {
            _move.Say("First count today");
            _move.Tint(Of("SubtleBrush"));
        }
        else
        {
            double diff = value - before.Value, part = before.Value > 0 ? 100 * diff / before.Value : 0;
            string since = before.Day == DateTime.Today.AddDays(-1) ? "yesterday" : before.Day.ToString("d MMM", System.Globalization.CultureInfo.InvariantCulture);
            bool same = Math.Abs(diff) < 0.005;
            _move.Say(same ? $"No change since {since}" : $"{(diff > 0 ? "▲" : "▼")} {Cs2Inventory.Money(Math.Abs(diff))} ({Math.Abs(part):0.0}%) since {since}");
            _move.Tint(same ? Of("SubtleBrush") : diff > 0 ? Of("GoodBrush") : Of("BadBrush"));
        }

        var history = Cs2Inventory.History.TakeLast(30).Select(p => p.Value).ToList();
        if (history.Count >= 2)
        {
            Live.Show(_line, true, this);
            var colour = ((SolidColorBrush)Of(history[^1] >= history[0] ? "GoodBrush" : "BadBrush")).Color;
            _line.Set(history, 0, colour, Live.Showing(this));
        }
        else Live.Show(_line, false, this);

        var top = Cs2Inventory.Items.Where(i => i.Price > 0).Take(Shown).ToList();
        double most = Math.Max(0.01, top.FirstOrDefault()?.Worth ?? 0.01);
        for (int i = 0; i < _rows.Count; i++)
        {
            var (name, bar, price) = _rows[i];
            var row = (UIElement)name.Parent;
            if (i >= top.Count) { Live.Show(row, false, this); continue; }
            var item = top[i];
            name.Say(Cs2Inventory.Short(item.Name).TrimStart('★', ' ') + (item.Count > 1 ? $" ×{item.Count}" : ""));
            name.ToolTip = item.Name;
            price.Say(Cs2Inventory.Money(item.Worth));
            bar.Glide(item.Worth / most, 700);
            bar.Colour = Of("AccentBrush");
            Live.Show(row, true, this);
        }

        ShowMovers();
        _about.Say($"{Cs2Inventory.Count} items · {Cs2Inventory.Priced} with a price · checked {Cs2Inventory.Fetched.ToLocalTime():HH:mm}");
        ToolTip = "Prices: Skinport's lowest asking price for each exact item";
    }
}
