using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using static PitzkiWidgets.Widgets.MarketBits;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// One stock (or index, coin, rate) big: its price, how it moved over the range picked, and a large line
/// of that range: a day, five days, a month, half a year or a year. The symbol is written in the pencil's
/// box (kept in <c>placed.Text</c>), the range in <c>placed.Name</c>.
/// </summary>
public sealed class ChartWidget : UserControl
{
    static readonly (string Range, string Word)[] Ranges = { ("1d", "1D"), ("5d", "1W"), ("1mo", "1M"), ("6mo", "6M"), ("1y", "1Y") };

    readonly Placed _placed;
    readonly TextBlock _symbol = new() { FontSize = 13, FontWeight = FontWeights.SemiBold };
    readonly TextBlock _name = new() { FontSize = 10.5, Foreground = Of("SubtleBrush"), TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _price = new() { Style = Big, FontSize = 26 };
    readonly TextBlock _move = new() { FontSize = 12, Margin = new Thickness(0, 0, 0, 2) };
    readonly StocksWidget.DayLine _line = new() { Height = 92, Margin = new Thickness(0, 6, 0, 6) };
    readonly TextBlock _span = new() { FontSize = 10, Foreground = Of("SubtleBrush") };
    readonly StackPanel _pills = new() { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
    readonly StackPanel _show = new(), _edit = new();
    readonly TextBox _box = Field("A name or symbol: S&P 500, Apple, NVDA", 40);
    double _was;
    string _shownSymbol = "";

    internal string PriceText => _price.Text;
    internal string MoveText => _move.Text;
    internal int Points => _line.Points;
    internal Task Refresh() => RefreshAsync();
    internal Task Choose(string typed) { _box.Text = typed; return SaveAsync(); }
    internal void Pick(string range) { _placed.Name = range; _ = RefreshAsync(); DrawPills(); }

    string Symbol => _placed.Text.Trim().Length > 0 ? _placed.Text.Trim().ToUpperInvariant() : "AAPL";
    string Range => Ranges.Any(r => r.Range == _placed.Name) ? _placed.Name : "1d";

    public ChartWidget(Placed placed)
    {
        _placed = placed;
        var pencil = Key("", Editing, icon: true, size: 11.5, tip: "Choose the stock");

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var who = new StackPanel();
        who.Children.Add(_symbol);
        who.Children.Add(_name);
        var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        _price.HorizontalAlignment = HorizontalAlignment.Right;
        _move.HorizontalAlignment = HorizontalAlignment.Right;
        right.Children.Add(_price);
        right.Children.Add(_move);
        Grid.SetColumn(right, 1);
        top.Children.Add(who);
        top.Children.Add(right);

        _show.Children.Add(top);
        _show.Children.Add(_line);
        var under = new Grid();
        under.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        under.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        under.Children.Add(_pills);
        _span.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(_span, 1);
        under.Children.Add(_span);
        _show.Children.Add(under);

        _edit.Children.Add(Boxed(_box));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Pill("Show", Save));
        buttons.Children.Add(Pill("Cancel", ShowChart, quiet: true));
        _edit.Children.Add(buttons);
        _edit.Visibility = Visibility.Collapsed;
        _box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Save(); else if (e.Key == System.Windows.Input.Key.Escape) ShowChart(); };
        FocusOnClick(this, _box);

        var box = new StackPanel { Width = 260 };
        box.Children.Add(Head("Chart", pencil));
        box.Children.Add(_show);
        box.Children.Add(_edit);
        Content = box;

        DrawPills();
        this.Every(60, () => _ = RefreshAsync());
    }

    void DrawPills()
    {
        _pills.Children.Clear();
        foreach (var (range, word) in Ranges)
        {
            bool on = range == Range;
            var pill = new Border
            {
                CornerRadius = new CornerRadius(8), Padding = new Thickness(7, 2, 7, 2), Margin = new Thickness(0, 0, 3, 0),
                Background = on ? Of("AccentBrush") : Of("ChipBrush"),
                Child = new TextBlock { Text = word, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = on ? Of("OnAccentBrush") : Of("TextBrush") },
            };
            string r = range;
            Click(pill, () => { if (r != Range) Pick(r); });
            _pills.Children.Add(pill);
        }
    }

    async Task RefreshAsync()
    {
        string symbol = Symbol, range = Range;
        var q = await Market.QuoteAsync(symbol, range);
        if (symbol != Symbol || range != Range) return;   // changed while asking
        if (q == null)
        {
            _symbol.Say(symbol);
            _name.Say("Not found, or offline");
            _price.Say("--");
            _move.Say("");
            return;
        }
        bool sameSymbol = _shownSymbol == symbol;
        _shownSymbol = symbol;
        _symbol.Say(symbol);
        _name.Say(q.Name);
        string text = Market.Price(q.Price, q.Currency);
        var (_, scale) = Market.Money(q.Currency);
        if (sameSymbol) _price.Count(text, q.Price * scale, _was); else _price.Say(text);
        _was = q.Price * scale;
        string word = Ranges.First(r => r.Range == range).Word;
        _move.Say($"{Market.Percent(q.Percent)}  {(q.Change >= 0 ? "+" : "-")}{Market.Price(Math.Abs(q.Change), q.Currency)} · {word}");
        _move.Tint(UpDown(q.Percent));
        _line.Set(q.Values, q.Before, UpDownColour(q.Percent), Live.Showing(this), fresh: !sameSymbol || _line.Tag as string != range);
        _line.Tag = range;
        _span.Say(q.Open ? "Market open" : "Market closed");
        _span.Tint(q.Open ? Of("GoodBrush") : Of("SubtleBrush"));
    }

    void Editing()
    {
        _box.Text = _placed.Text;
        _show.Visibility = Visibility.Collapsed;
        _edit.Visibility = Visibility.Visible;
        Live.Arrive(_edit, this);
    }

    void ShowChart()
    {
        _edit.Visibility = Visibility.Collapsed;
        _show.Visibility = Visibility.Visible;
        Live.Arrive(_show, this);
    }

    void Save() => _ = SaveAsync();

    async Task SaveAsync()
    {
        var picked = await Market.ResolveAsync(_box.Text, 1);
        if (picked.Count > 0) _placed.Text = picked[0];
        App.Board.Save();
        ShowChart();
        _ = RefreshAsync();
    }
}
