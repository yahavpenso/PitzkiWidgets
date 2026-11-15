using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using static PitzkiWidgets.Widgets.MarketBits;

namespace PitzkiWidgets.Widgets;

/// <summary>One line of a portfolio: so many shares of a symbol, bought at a price (in the symbol's own currency).</summary>
public readonly record struct Holding(string Symbol, double Shares, double Paid)
{
    public string Saved => string.Join("|", Symbol, Shares.ToString(CultureInfo.InvariantCulture), Paid.ToString(CultureInfo.InvariantCulture));

    public static Holding? Parse(string saved)
    {
        var p = saved.Split('|');
        if (p.Length < 3 || p[0].Length == 0) return null;
        if (!double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var shares) || shares <= 0) return null;
        double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var paid);
        return new Holding(p[0], shares, Math.Max(0, paid));
    }
}

/// <summary>
/// "My portfolio": the shares you own, written in by hand (symbol, how many, the price paid for one). It
/// shows what they are worth now and how much you are up or down, each one and all together. The total
/// is in dollars; shares in shekels are turned into dollars at today's rate. Nothing is bought or sold:
/// it only does the sums. The holdings live in <c>placed.Items</c>.
/// </summary>
public sealed class PortfolioWidget : UserControl
{
    readonly Placed _placed;
    readonly TextBlock _total = new() { Style = Big, FontSize = 24 };
    readonly TextBlock _gain = new() { FontSize = 12 };
    readonly TextBlock _today = new() { FontSize = 10.5, Foreground = Of("SubtleBrush") };
    readonly StackPanel _rows = new(), _show = new(), _edit = new(), _editRows = new();
    readonly TextBox _symbol = Field("Stock", 40, 70), _shares = Field("How many", 12, 72), _paid = Field("Paid for 1", 14, 76);
    readonly TextBlock _trouble = new() { FontSize = 11, Foreground = Of("BadBrush"), TextWrapping = TextWrapping.Wrap };
    double _was;
    bool _has;
    readonly Dictionary<string, Line> _lines = new();
    int _gen;

    sealed class Line
    {
        public readonly Grid Row = new() { Margin = new Thickness(0, 2, 0, 2) };
        public readonly TextBlock Sub = new() { FontSize = 10, Foreground = Of("SubtleBrush") };
        public readonly TextBlock Value = new() { FontSize = 12, HorizontalAlignment = HorizontalAlignment.Right };
        public readonly TextBlock Pct = new() { FontSize = 10, HorizontalAlignment = HorizontalAlignment.Right };

        public Line(string symbol)
        {
            Row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = symbol, FontSize = 12, FontWeight = FontWeights.SemiBold });
            left.Children.Add(Sub);
            var right = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
            right.Children.Add(Value);
            right.Children.Add(Pct);
            Grid.SetColumn(right, 1);
            Row.Children.Add(left);
            Row.Children.Add(right);
        }
    }

    internal string TotalText => _total.Text;
    internal string GainText => _gain.Text;
    internal IEnumerable<string> RowsText => _rows.Children.OfType<Grid>().Select(g => string.Join(" ", g.Children.OfType<FrameworkElement>()
        .SelectMany(e => e is StackPanel s ? s.Children.OfType<TextBlock>() : e is TextBlock t ? new[] { t } : Array.Empty<TextBlock>()).Select(t => t.Text)));
    internal Task Refresh() => RefreshAsync();
    internal Task<bool> Add(string symbol, string shares, string paid) { _symbol.Text = symbol; _shares.Text = shares; _paid.Text = paid; return AddHolding(); }

    List<Holding> Holdings => _placed.Items.Select(Holding.Parse).Where(h => h != null).Select(h => h!.Value).ToList();

    public PortfolioWidget(Placed placed)
    {
        _placed = placed;
        var pencil = Key("", Editing, icon: true, size: 11.5, tip: "Add or remove shares");

        _show.Children.Add(_total);
        _show.Children.Add(_gain);
        _show.Children.Add(_today);
        _rows.Margin = new Thickness(0, 8, 0, 0);
        _show.Children.Add(_rows);

        var line = new StackPanel { Orientation = Orientation.Horizontal };
        line.Children.Add(Boxed(_symbol, new Thickness(0, 0, 4, 6)));
        line.Children.Add(Boxed(_shares, new Thickness(0, 0, 4, 6)));
        line.Children.Add(Boxed(_paid, new Thickness(0, 0, 0, 6)));
        _edit.Children.Add(line);
        _edit.Children.Add(_trouble);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 8) };
        buttons.Children.Add(Pill("Add", () => _ = AddHolding()));
        buttons.Children.Add(Pill("Done", Back, quiet: true));
        _edit.Children.Add(buttons);
        _edit.Children.Add(_editRows);
        _edit.Visibility = Visibility.Collapsed;
        foreach (var f in new[] { _symbol, _shares, _paid })
            f.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) _ = AddHolding(); else if (e.Key == System.Windows.Input.Key.Escape) Back(); };
        FocusOnClick(this, _symbol, _shares, _paid);

        var box = new StackPanel { Width = 240 };
        box.Children.Add(Head("My portfolio", pencil));
        box.Children.Add(_show);
        box.Children.Add(_edit);
        Content = box;

        if (Holdings.Count == 0) { _total.Say("$0.00"); _gain.Say("Press the pencil and add the shares you own"); _gain.Tint(Of("SubtleBrush")); }
        this.Every(60, () => _ = RefreshAsync());
    }

    async Task RefreshAsync()
    {
        var holdings = Holdings;
        if (holdings.Count == 0) { _rows.Children.Clear(); _lines.Clear(); return; }
        int gen = ++_gen;
        var quotes = await Task.WhenAll(holdings.Select(h => Market.QuoteAsync(h.Symbol)));
        var rates = await Task.WhenAll(quotes.Select(q => q == null ? Task.FromResult(0.0) : Market.DollarsPerAsync(q.Currency)));
        if (gen != _gen) return;   // a newer look (after an add or a remove) is on its way
        double worth = 0, cost = 0, dayMove = 0;
        bool missing = false;
        for (int i = 0; i < holdings.Count; i++)
        {
            var h = holdings[i];
            var q = quotes[i];
            if (q == null) { missing = true; continue; }
            var (sign, scale) = Market.Money(q.Currency);
            double rate = rates[i];   // dollars for one unit of the quote's currency
            if (rate <= 0) { missing = true; continue; }
            double now = q.Price * h.Shares, paid = h.Paid / scale * h.Shares;   // the price paid is written in shekels or dollars, not agorot
            worth += now * rate;
            cost += paid * rate;
            dayMove += (q.Price - q.PreviousClose) * h.Shares * rate;
            double pct = paid > 0 ? 100 * (now - paid) / paid : 0;

            if (!_lines.TryGetValue(h.Symbol, out var line))
            {
                line = new Line(h.Symbol);
                _lines[h.Symbol] = line;
                _rows.Children.Add(line.Row);
                Live.Arrive(line.Row, this, _lines.Count);
            }
            line.Sub.Say($"{h.Shares:0.##} × {Market.Price(q.Price, q.Currency)}");
            line.Value.Say(sign + (now * scale).ToString("#,0.00", CultureInfo.InvariantCulture));
            line.Pct.Say(h.Paid > 0 ? Market.Percent(pct) : "");
            line.Pct.Tint(UpDown(pct));
        }
        foreach (var gone in _lines.Keys.Where(k => holdings.All(h => h.Symbol != k)).ToList())
        {
            _rows.Children.Remove(_lines[gone].Row);
            _lines.Remove(gone);
        }

        string text = "$" + worth.ToString("#,0.00", CultureInfo.InvariantCulture);
        if (_has) _total.Count(text, worth, _was); else _total.Say(text);
        _was = worth;
        _has = true;
        double gain = worth - cost, gainPct = cost > 0 ? 100 * gain / cost : 0;
        _gain.Say(cost > 0 ? $"{(gain >= 0 ? "+" : "-")}${Math.Abs(gain).ToString("#,0.00", CultureInfo.InvariantCulture)} ({Market.Percent(gainPct)}) since you bought" : "Add what you paid to see the gain");
        _gain.Tint(cost > 0 ? UpDown(gain) : Of("SubtleBrush"));
        _today.Say($"Today {(dayMove >= 0 ? "+" : "-")}${Math.Abs(dayMove).ToString("#,0.00", CultureInfo.InvariantCulture)}" + (missing ? " · some prices missing" : ""));
    }

    async Task<bool> AddHolding()
    {
        var symbol = (await Market.ResolveAsync(_symbol.Text, 1)).FirstOrDefault();
        bool okShares = double.TryParse(_shares.Text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var shares) && shares > 0;
        double paid = 0;
        bool okPaid = _paid.Text.Trim().Length == 0 || double.TryParse(_paid.Text.Trim().TrimStart('$', '₪'), NumberStyles.Float, CultureInfo.InvariantCulture, out paid) && paid >= 0;
        if (symbol == null || !okShares || !okPaid)
        {
            _trouble.Text = symbol == null ? "No stock by that name. Try a symbol, like AAPL" : !okShares ? "How many shares? Like 10 or 2.5" : "The price paid for one share, like 150.20";
            return false;
        }
        _trouble.Text = "";
        var list = Holdings.Where(h => h.Symbol != symbol).ToList();
        var had = Holdings.FirstOrDefault(h => h.Symbol == symbol);
        // buying more of one already there: the shares add up and the price paid is averaged
        if (had.Symbol != null)
        {
            double all = had.Shares + shares;
            paid = all > 0 ? (had.Shares * had.Paid + shares * paid) / all : paid;
            shares = all;
        }
        list.Add(new Holding(symbol, shares, paid));
        _placed.Items = list.Select(h => h.Saved).ToList();
        App.Board.Save();
        _symbol.Text = _shares.Text = _paid.Text = "";
        DrawEditRows();
        _ = RefreshAsync();
        return true;
    }

    void DrawEditRows()
    {
        _editRows.Children.Clear();
        foreach (var h in Holdings)
        {
            var row = new Grid { Margin = new Thickness(0, 1, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = $"{h.Symbol}  {h.Shares:0.##} @ {h.Paid:0.##}", FontSize = 11.5 });
            string symbol = h.Symbol;
            var remove = Key("", () =>
            {
                _placed.Items = Holdings.Where(x => x.Symbol != symbol).Select(x => x.Saved).ToList();
                App.Board.Save();
                DrawEditRows();
                _ = RefreshAsync();
            }, icon: true, size: 10, tip: "Remove");
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            _editRows.Children.Add(row);
        }
    }

    void Editing()
    {
        DrawEditRows();
        _show.Visibility = Visibility.Collapsed;
        _edit.Visibility = Visibility.Visible;
        Live.Arrive(_edit, this);
    }

    void Back()
    {
        _edit.Visibility = Visibility.Collapsed;
        _show.Visibility = Visibility.Visible;
        Live.Arrive(_show, this);
    }
}
