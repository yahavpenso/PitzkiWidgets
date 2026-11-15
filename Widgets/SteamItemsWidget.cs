using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// What the Steam account's own items are worth (trading cards, backgrounds, emoticons): the total big,
/// how it moved since the day before, a line of the last days, and the dearest things. Prices come from
/// the Steam Community Market one at a time (Pitzki.Core.SteamItems), so the total fills in as they come.
/// </summary>
public sealed class SteamItemsWidget : UserControl
{
    readonly TextBlock _value = new() { Style = Big, FontSize = 26, VerticalAlignment = VerticalAlignment.Bottom };
    readonly TextBlock _move = new() { FontSize = 11.5, Foreground = Of("SubtleBrush"), Margin = new Thickness(0, 1, 0, 0) };
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
    internal void Redraw() => Show();

    public SteamItemsWidget()
    {
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(Title("Steam items"));
        var again = Key("", () => { _about.Say("Checking..."); SteamItems.Start(force: true); }, icon: true, size: 11.5, tip: "Check the prices again");
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
        box.Children.Add(_about);
        Content = box;

        void Changed() => Dispatcher.BeginInvoke(Show);
        Loaded += (_, _) => { SteamItems.Changed += Changed; MoneyPrefs.Changed += Changed; };
        Unloaded += (_, _) => { SteamItems.Changed -= Changed; MoneyPrefs.Changed -= Changed; };
        Show();
        // it only really asks Steam every half an hour; this just notices when that time has come
        this.Every(300, () => { if (!SelfTest.Wanted) SteamItems.Start(); Show(); });
    }

    void Show()
    {
        if (!SteamItems.HasItems)
        {
            _value.Say("--");
            _move.Say("");
            _about.Say(SteamItems.Problem switch
            {
                "private" => "Your Steam inventory is private. Set it to Public in Steam, then press the arrows.",
                "no steam" => "Open Steam and sign in to see what your cards are worth.",
                "offline" => "Couldn't reach Steam. Press the arrows to try again.",
                _ => "Counting your cards...",
            });
            Live.Show(_line, false, this);
            foreach (var (name, _, _) in _rows) Live.Show((UIElement)name.Parent, false, this);
            return;
        }

        double value = SteamItems.Value;
        string text = Cs2Inventory.Money(value);
        if (_has) _value.Count(text, MoneyPrefs.Shown(value), _was); else _value.Say(text);
        _was = MoneyPrefs.Shown(value);
        _has = true;
        _value.Tint(Of("AccentBrush"));

        var before = SteamItems.Before;
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

        var history = SteamItems.History.TakeLast(30).Select(p => p.Value).ToList();
        if (history.Count >= 2)
        {
            Live.Show(_line, true, this);
            var colour = ((SolidColorBrush)Of(history[^1] >= history[0] ? "GoodBrush" : "BadBrush")).Color;
            _line.Set(history, 0, colour, Live.Showing(this));
        }
        else Live.Show(_line, false, this);

        var top = SteamItems.Items.Where(i => i.Price > 0).Take(Shown).ToList();
        double most = Math.Max(0.01, top.FirstOrDefault()?.Worth ?? 0.01);
        for (int i = 0; i < _rows.Count; i++)
        {
            var (name, bar, price) = _rows[i];
            var row = (UIElement)name.Parent;
            if (i >= top.Count) { Live.Show(row, false, this); continue; }
            var item = top[i];
            name.Say(item.Shown + (item.Count > 1 ? $" ×{item.Count}" : ""));
            name.ToolTip = (item.Game.Length > 0 ? item.Game + " · " : "") + item.Kind;
            price.Say(Cs2Inventory.Money(item.Worth));
            bar.Glide(item.Worth / most, 700);
            bar.Colour = Of("AccentBrush");
            Live.Show(row, true, this);
        }

        _about.Say(SteamItems.Waiting > 0
            ? $"{SteamItems.Count} items · pricing {SteamItems.Waiting} more..."
            : $"{SteamItems.Count} items · {SteamItems.ForSale} can be sold · checked {SteamItems.Fetched.ToLocalTime():HH:mm}");
        ToolTip = "Prices: the Steam Community Market's lowest price for each item";
    }
}
