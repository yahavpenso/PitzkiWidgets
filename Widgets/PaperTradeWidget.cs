using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using static PitzkiWidgets.Widgets.MarketBits;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// A pretend portfolio: $10,000 of play money, real prices. You buy and sell real US stocks at the price
/// they have right now and see whether you would have made money. No real money is ever involved, and
/// nothing is sent anywhere: the pretend account is kept in <c>placed.Text</c> as a little JSON.
/// </summary>
public sealed class PaperTradeWidget : UserControl
{
    public const double Start = 10_000;

    public sealed class Account
    {
        public double Cash { get; set; } = Start;
        public Dictionary<string, double[]> Shares { get; set; } = new();   // symbol -> { how many, paid in all }
        public int Trades { get; set; }
    }

    readonly Placed _placed;
    Account _account;
    readonly TextBlock _total = new() { Style = Big, FontSize = 24 };
    readonly TextBlock _gain = new() { FontSize = 12 };
    readonly TextBlock _cash = Small();
    readonly StackPanel _rows = new() { Margin = new Thickness(0, 6, 0, 6) };
    readonly TextBox _symbol = Field("Stock", 40, 80), _amount = Field("How many", 10, 76);
    readonly TextBlock _said = new() { FontSize = 11, TextWrapping = TextWrapping.Wrap, Foreground = Of("SubtleBrush"), Margin = new Thickness(0, 4, 0, 0) };
    readonly Dictionary<string, (Grid Row, TextBlock Sub, TextBlock Value, TextBlock Pct)> _lines = new();
    double _was;
    bool _has;

    internal Account State => _account;
    internal string TotalText => _total.Text;
    internal string SaidText => _said.Text;
    internal Task Refresh() => RefreshAsync();
    internal Task Trade(string symbol, string amount, bool buy) { _symbol.Text = symbol; _amount.Text = amount; return TradeAsync(buy); }

    public PaperTradeWidget(Placed placed)
    {
        _placed = placed;
        _account = Load(placed.Text);
        var reset = Key("", Reset, icon: true, size: 11, tip: "Start again with $10,000");

        var form = new StackPanel { Orientation = Orientation.Horizontal };
        form.Children.Add(Boxed(_symbol, new Thickness(0, 0, 4, 0)));
        form.Children.Add(Boxed(_amount, new Thickness(0, 0, 4, 0)));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        buttons.Children.Add(Pill("Buy", () => _ = TradeAsync(true)));
        buttons.Children.Add(Pill("Sell", () => _ = TradeAsync(false), quiet: true));
        FocusOnClick(this, _symbol, _amount);

        var box = new StackPanel { Width = 240 };
        box.Children.Add(Head("Pretend portfolio", reset));
        box.Children.Add(_total);
        box.Children.Add(_gain);
        box.Children.Add(_cash);
        box.Children.Add(_rows);
        box.Children.Add(form);
        box.Children.Add(buttons);
        box.Children.Add(_said);
        Content = box;
        _said.Text = "Play money only. Buy real US stocks at today's price.";
        this.Every(60, () => _ = RefreshAsync());
    }

    static Account Load(string text)
    {
        try { if (text.TrimStart().StartsWith("{")) return JsonSerializer.Deserialize<Account>(text) ?? new Account(); }
        catch { }
        return new Account();
    }

    void Save()
    {
        _placed.Text = JsonSerializer.Serialize(_account);
        App.Board.Save();
    }

    async Task TradeAsync(bool buy)
    {
        string? symbol = (await Market.ResolveAsync(_symbol.Text, 1)).FirstOrDefault();
        if (symbol == null) { Say("Write a symbol, like AAPL", bad: true); return; }
        if (!double.TryParse(_amount.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var count) || count <= 0 || count != Math.Floor(count))
        {
            Say("How many? A whole number, like 5", bad: true);
            return;
        }
        var q = await Market.QuoteAsync(symbol);
        if (q == null) { Say($"No stock called {symbol}", bad: true); return; }
        if (q.Currency != "USD") { Say("Only stocks priced in dollars (US stocks)", bad: true); return; }

        double cost = q.Price * count;
        _account.Shares.TryGetValue(symbol, out var had);
        had ??= new[] { 0.0, 0.0 };
        if (buy)
        {
            if (cost > _account.Cash + 1e-6) { Say($"Not enough play money: {count:0} × {Market.Price(q.Price, "USD")} is ${cost:#,0.00}", bad: true); return; }
            _account.Cash -= cost;
            _account.Shares[symbol] = new[] { had[0] + count, had[1] + cost };
            Say($"Bought {count:0} {symbol} at {Market.Price(q.Price, "USD")}");
        }
        else
        {
            if (had[0] < count) { Say($"You only have {had[0]:0} {symbol}", bad: true); return; }
            double paidEach = had[0] > 0 ? had[1] / had[0] : 0;
            double profit = (q.Price - paidEach) * count;
            _account.Cash += cost;
            if (had[0] - count <= 0) _account.Shares.Remove(symbol);
            else _account.Shares[symbol] = new[] { had[0] - count, had[1] - paidEach * count };
            Say($"Sold {count:0} {symbol} at {Market.Price(q.Price, "USD")}: {(profit >= 0 ? "+" : "-")}${Math.Abs(profit):#,0.00}");
        }
        _account.Trades++;
        Save();
        _symbol.Text = _amount.Text = "";
        await RefreshAsync();
    }

    void Say(string text, bool bad = false)
    {
        _said.Say(text);
        _said.Tint(bad ? Of("BadBrush") : Of("TextBrush"));
    }

    void Reset()
    {
        _account = new Account();
        Save();
        foreach (var l in _lines.Values) _rows.Children.Remove(l.Row);
        _lines.Clear();
        Say("Started again with $10,000");
        _ = RefreshAsync();
    }

    async Task RefreshAsync()
    {
        double worth = _account.Cash;
        foreach (var (symbol, held) in _account.Shares.ToList())
        {
            var q = await Market.QuoteAsync(symbol);
            double price = q?.Price ?? (held[0] > 0 ? held[1] / held[0] : 0);
            double now = price * held[0], pct = held[1] > 0 ? 100 * (now - held[1]) / held[1] : 0;
            worth += now;
            if (!_lines.TryGetValue(symbol, out var line))
            {
                var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = new StackPanel();
                left.Children.Add(new TextBlock { Text = symbol, FontSize = 12, FontWeight = FontWeights.SemiBold });
                var sub = new TextBlock { FontSize = 10, Foreground = Of("SubtleBrush") };
                left.Children.Add(sub);
                var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
                var value = new TextBlock { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right };
                var p = new TextBlock { FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right };
                right.Children.Add(value);
                right.Children.Add(p);
                Grid.SetColumn(right, 1);
                row.Children.Add(left);
                row.Children.Add(right);
                line = (row, sub, value, p);
                _lines[symbol] = line;
                _rows.Children.Add(row);
                Live.Arrive(row, this);
            }
            line.Sub.Say($"{held[0]:0} × {Market.Price(price, "USD")}");
            line.Value.Say("$" + now.ToString("#,0.00", CultureInfo.InvariantCulture));
            line.Pct.Say(Market.Percent(pct));
            line.Pct.Tint(UpDown(pct));
        }
        foreach (var gone in _lines.Keys.Where(k => !_account.Shares.ContainsKey(k)).ToList())
        {
            _rows.Children.Remove(_lines[gone].Row);
            _lines.Remove(gone);
        }

        string text = "$" + worth.ToString("#,0.00", CultureInfo.InvariantCulture);
        if (_has) _total.Count(text, worth, _was); else _total.Say(text);
        _was = worth;
        _has = true;
        double gain = worth - Start;
        _gain.Say($"{(gain >= 0 ? "+" : "-")}${Math.Abs(gain).ToString("#,0.00", CultureInfo.InvariantCulture)} ({Market.Percent(100 * gain / Start)}) since the start");
        _gain.Tint(UpDown(gain));
        _cash.Say($"Cash ${_account.Cash.ToString("#,0.00", CultureInfo.InvariantCulture)} · {_account.Trades} trades");
    }
}
