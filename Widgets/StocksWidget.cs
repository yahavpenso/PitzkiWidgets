using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PitzkiWidgets.Core;
using Fx = Pitzki.Core.Fx;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>One stock as the market has it right now: today's prices, the price now and yesterday's close.</summary>
public sealed record StockQuote(string Symbol, string Name, string Currency, double Price, double PreviousClose, List<double> Day, bool Open)
{
    public double Change => Price - PreviousClose;
    public double Percent => PreviousClose > 0 ? 100 * Change / PreviousClose : 0;
}

/// <summary>
/// The stock market on the desktop: a few stocks of your choosing, each with its price now, how much it
/// moved today, and a little line of the day against yesterday's close. Prices come from Yahoo Finance's
/// public chart page (no account, no key), every minute. The pencil changes which stocks are shown.
/// </summary>
public sealed class StocksWidget : UserControl
{
    public const string DefaultSymbols = "AAPL, NVDA, TSLA, MSFT, ^GSPC";
    const int MostSymbols = 6;

    readonly Placed _placed;
    readonly string _defaults;
    readonly TextBlock _bell;
    readonly StackPanel _rows = new();
    readonly StackPanel _show = new();
    readonly StackPanel _edit = new();
    readonly TextBlock _state = new() { Style = Caption, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 4, 8) };
    readonly TextBox _box;
    readonly Dictionary<string, Line> _lines = new(StringComparer.OrdinalIgnoreCase);
    bool _busy;

    /// <summary>For the tests: what each row shows now.</summary>
    internal IEnumerable<(string Symbol, string Price, string Move)> Shown =>
        _lines.Values.Select(l => (l.Symbol.Text, l.Price.Text, l.Move.Text));
    internal Task Refresh() => RefreshAsync();
    internal void Feed(IEnumerable<StockQuote> quotes) { foreach (var q in quotes) ShowQuote(q); }

    static readonly HttpClient Http = MakeHttp();
    static HttpClient MakeHttp()
    {
        var http = new HttpClient(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All }) { Timeout = TimeSpan.FromSeconds(15) };
        http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PitzkiWidgets");
        return http;
    }

    /// <summary>The steps the bell goes through: off, then a note when a symbol moved this much today.</summary>
    static readonly int[] BellSteps = { 0, 2, 3, 5, 10 };

    readonly string _range;

    public StocksWidget(Placed placed, string title = "Stocks", string defaults = DefaultSymbols, string range = "1d")
    {
        _placed = placed;
        _range = range;
        _defaults = defaults;

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(Title(title));
        Grid.SetColumn(_state, 1);
        head.Children.Add(_state);
        _bell = Key(BellWord(), Ring, size: 10.5, tip: "A note from the tray when one of them moves a lot today. Click to change.");
        _bell.Margin = new Thickness(0, -4, 2, 0);
        Grid.SetColumn(_bell, 2);
        head.Children.Add(_bell);
        var pencil = Key("", Editing, icon: true, size: 11.5, tip: "Choose the stocks");
        pencil.Margin = new Thickness(0, -4, -4, 0);
        Grid.SetColumn(pencil, 3);
        head.Children.Add(pencil);

        _show.Children.Add(_rows);

        _box = new TextBox
        {
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 12.5, MaxLength = 80,
            Foreground = Of("TextBrush"), CaretBrush = Of("TextBrush"), Padding = new Thickness(9, 5, 9, 5),
        };
        _box.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter) { Save(); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.Escape) { ShowRows(); e.Handled = true; }
        };
        _edit.Children.Add(new TextBlock
        {
            Text = "Names or symbols, with commas (up to 6): S&P 500, Apple, NVDA, Bitcoin, TEVA.TA",
            FontSize = 11, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 6),
        });
        _edit.Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = Of("ChipBrush"), Child = _box });
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        buttons.Children.Add(Pill("Save", Save));
        buttons.Children.Add(Pill("Cancel", ShowRows, quiet: true));
        _edit.Children.Add(buttons);
        _edit.Visibility = Visibility.Collapsed;

        var box = new StackPanel { Width = 232 };
        box.Children.Add(head);
        box.Children.Add(_show);
        box.Children.Add(_edit);
        Content = box;

        Build();
        this.Every(60, () => _ = RefreshAsync());
    }

    /// <summary>The card is dragged by its edges; a click on the box is a click to type.</summary>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (!_box.IsKeyboardFocusWithin && _box.IsMouseOver) _box.Focus();
    }

    List<string> Symbols => Parse(_placed.Text.Length > 0 ? _placed.Text : _defaults);

    string BellWord() => _placed.Number > 0 ? $"🔔 {_placed.Number}%" : "🔕";

    /// <summary>The bell: off, 2%, 3%, 5%, 10%, and round again.</summary>
    void Ring()
    {
        int at = Array.IndexOf(BellSteps, _placed.Number);
        _placed.Number = BellSteps[(at + 1) % BellSteps.Length];
        App.Board.Save();
        _bell.Say(BellWord());
        foreach (var line in _lines.Values) line.Rang = false;
    }

    internal static List<string> Parse(string text) => text
        .Split(new[] { ',', ' ', ';', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(s => s.ToUpperInvariant()).Where(s => s.Length <= 15 && s.All(c => char.IsLetterOrDigit(c) || c is '.' or '^' or '-' or '=' or '&'))
        .Distinct().Take(MostSymbols).ToList();

    // ----- the rows -----

    /// <summary>One stock's row: the symbol and its name, the day's line, the price and the move.</summary>
    sealed class Line
    {
        public readonly Grid Row = new() { Margin = new Thickness(0, 3, 0, 4) };
        public readonly TextBlock Symbol = new() { FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        public readonly TextBlock Name = new() { FontSize = 10, Foreground = Of("SubtleBrush"), TextTrimming = TextTrimming.CharacterEllipsis };
        public readonly DayLine Chart = new() { Width = 58, Height = 24, Margin = new Thickness(6, 0, 6, 0), VerticalAlignment = VerticalAlignment.Center };
        public readonly TextBlock Price = new() { FontSize = 12.5, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Right };
        public readonly TextBlock Move = new() { FontSize = 10.5, HorizontalAlignment = HorizontalAlignment.Right, Foreground = Of("SubtleBrush") };
        public double WasPrice, WasPercent;
        public bool Has, Rang;

        public Line(string symbol)
        {
            Row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            Row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Symbol.Text = symbol;
            Name.Text = "...";
            left.Children.Add(Symbol);
            left.Children.Add(Name);
            var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            Price.Text = "--";
            right.Children.Add(Price);
            right.Children.Add(Move);
            Grid.SetColumn(Chart, 1);
            Grid.SetColumn(right, 2);
            Row.Children.Add(left);
            Row.Children.Add(Chart);
            Row.Children.Add(right);
        }
    }

    /// <summary>Makes a row for every symbol chosen, keeping the rows that are still wanted.</summary>
    void Build()
    {
        var wanted = Symbols;
        foreach (var gone in _lines.Keys.Where(k => !wanted.Contains(k, StringComparer.OrdinalIgnoreCase)).ToList())
        {
            _rows.Children.Remove(_lines[gone].Row);
            _lines.Remove(gone);
        }
        int order = 0;
        foreach (var symbol in wanted)
        {
            if (!_lines.TryGetValue(symbol, out var line))
            {
                line = _lines[symbol] = new Line(symbol);
                _rows.Children.Add(line.Row);
                Live.Arrive(line.Row, this, order++);
            }
            else
            {
                _rows.Children.Remove(line.Row);
                _rows.Children.Add(line.Row);
            }
        }
        if (wanted.Count == 0) _state.Say("No stocks");
    }

    async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            var symbols = Symbols;
            var quotes = await Task.WhenAll(symbols.Select(FetchAsync));
            int got = 0;
            bool open = false;
            foreach (var (symbol, quote) in symbols.Zip(quotes))
            {
                if (quote == null)
                {
                    if (_lines.TryGetValue(symbol, out var line) && !line.Has) line.Name.Say("Not found");
                    continue;
                }
                got++;
                open |= quote.Open;
                ShowQuote(quote);
            }
            _state.Say(got == 0 && symbols.Count > 0 ? "Offline" : open ? "Market open" : "Market closed");
            _state.Tint(open ? Of("GoodBrush") : Of("SubtleBrush"));
            ToolTip = $"Yahoo Finance · {DateTime.Now:HH:mm}";
        }
        finally { _busy = false; }
    }

    void ShowQuote(StockQuote q)
    {
        if (!_lines.TryGetValue(q.Symbol, out var line)) return;
        var (sign, scale) = Money(q.Currency);
        double price = q.Price * scale;
        string priceText = sign + price.ToString(price >= 1000 ? "#,0.00" : "0.00", CultureInfo.InvariantCulture);
        if (line.Has) line.Price.Count(priceText, price, line.WasPrice);
        else line.Price.Say(priceText);
        if (line.Has && Math.Abs(price - line.WasPrice) > 1e-9) Live.Nudge(line.Price, this);
        line.WasPrice = price;

        double pct = q.Percent;
        string arrow = pct > 0.005 ? "▲" : pct < -0.005 ? "▼" : "•";
        line.Move.Say($"{arrow} {Math.Abs(pct).ToString("0.00", CultureInfo.InvariantCulture)}%");
        line.Move.Tint(pct > 0.005 ? Of("GoodBrush") : pct < -0.005 ? Of("BadBrush") : Of("SubtleBrush"));
        line.WasPercent = pct;

        line.Name.Say(q.Name.Length > 0 ? q.Name : q.Symbol);
        if (_placed.Number > 0 && Math.Abs(pct) >= _placed.Number && !line.Rang)
        {
            line.Rang = true;
            App.Alert(q.Symbol + (pct > 0 ? "+" : "-") + _placed.Number, $"{q.Symbol} {(pct > 0 ? "is up" : "is down")} {Math.Abs(pct):0.0}% today",
                $"{(q.Name.Length > 0 ? q.Name : q.Symbol)} is at {priceText}");
            Live.Nudge(line.Row, this);
        }
        line.Row.ToolTip = $"{q.Name}\nYesterday's close {sign}{(q.PreviousClose * scale).ToString("0.00", CultureInfo.InvariantCulture)}";
        line.Chart.Set(q.Day, q.PreviousClose, pct >= 0 ? ((SolidColorBrush)Of("GoodBrush")).Color : ((SolidColorBrush)Of("BadBrush")).Color, Live.Showing(this));
        line.Has = true;
    }

    /// <summary>What a price is written with. Tel Aviv prices come in agorot, so they are shown in shekels.</summary>
    static (string Sign, double Scale) Money(string currency) => currency switch
    {
        "USD" => ("$", 1), "EUR" => ("€", 1), "GBP" => ("£", 1), "GBp" => ("£", 0.01),
        "ILS" => ("₪", 1), "ILA" => ("₪", 0.01), "JPY" => ("¥", 1),
        "" => ("", 1), var other => (other + " ", 1),
    };

    // ----- the market -----

    async Task<StockQuote?> FetchAsync(string symbol)
    {
        try
        {
            string json = await Http.GetStringAsync($"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?range={_range}&interval={(_range == "1d" ? "5m" : "1h")}");
            return Read(symbol, json, DateTimeOffset.UtcNow);
        }
        catch { return null; }
    }

    /// <summary>Yahoo's chart page for one symbol, as a quote. null when it knows no such symbol.</summary>
    internal static StockQuote? Read(string symbol, string json, DateTimeOffset now)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("chart", out var chart) || !chart.TryGetProperty("result", out var results)
            || results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0) return null;
        var result = results[0];
        var meta = result.GetProperty("meta");
        double price = Num(meta, "regularMarketPrice");
        if (price <= 0) return null;
        double previous = Num(meta, "chartPreviousClose");
        if (previous <= 0) previous = Num(meta, "previousClose");
        string name = Str(meta, "shortName");
        if (name.Length == 0) name = Str(meta, "longName");

        var day = new List<double>();
        if (result.TryGetProperty("indicators", out var ind) && ind.TryGetProperty("quote", out var quote) && quote.GetArrayLength() > 0
            && quote[0].TryGetProperty("close", out var closes) && closes.ValueKind == JsonValueKind.Array)
            foreach (var c in closes.EnumerateArray())
                if (c.ValueKind == JsonValueKind.Number) day.Add(c.GetDouble());
        if (day.Count == 0 || Math.Abs(day[^1] - price) > 1e-9) day.Add(price);

        bool open = false;
        if (meta.TryGetProperty("currentTradingPeriod", out var period) && period.TryGetProperty("regular", out var regular))
        {
            long start = (long)Num(regular, "start"), end = (long)Num(regular, "end"), t = now.ToUnixTimeSeconds();
            open = t >= start && t < end;
        }
        return new StockQuote(symbol, name, Str(meta, "currency"), price, previous, day, open);
    }

    static double Num(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : 0;

    static string Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    // ----- choosing the stocks -----

    void Editing()
    {
        _box.Text = string.Join(", ", Symbols);
        _show.Visibility = Visibility.Collapsed;
        _edit.Visibility = Visibility.Visible;
        Live.Arrive(_edit, this);
    }

    void ShowRows()
    {
        bool back = _edit.Visibility == Visibility.Visible;
        _edit.Visibility = Visibility.Collapsed;
        _show.Visibility = Visibility.Visible;
        if (back) Live.Arrive(_show, this);
    }

    void Save() => _ = SaveAsync();

    async Task SaveAsync()
    {
        var chosen = await Pitzki.Core.Market.ResolveAsync(_box.Text, MostSymbols);
        _placed.Text = chosen.Count == 0 ? "" : string.Join(", ", chosen);
        App.Board.Save();
        ShowRows();
        Build();
        _ = RefreshAsync();
    }

    /// <summary>
    /// Today's prices as a line, scaled to the day's own low and high, with yesterday's close as a faint
    /// dashed line through it. A new day's line draws itself in from the left; a fresh price after that
    /// only moves the end of the line.
    /// </summary>
    public sealed class DayLine : FrameworkElement
    {
        List<double> _values = new();
        double _base;
        Color _colour = Colors.Gray;

        public static readonly DependencyProperty DrawnProperty = DependencyProperty.Register(
            nameof(Drawn), typeof(double), typeof(DayLine), new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

        public double Drawn { get => (double)GetValue(DrawnProperty); set => SetValue(DrawnProperty, value); }

        internal int Points => _values.Count;

        public void Set(List<double> values, double baseline, Color colour, bool animate, bool fresh = false)
        {
            bool first = _values.Count == 0 || fresh;
            _values = values.ToList();
            _base = baseline;
            _colour = colour;
            if (first && animate)
                BeginAnimation(DrawnProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(900)) { EasingFunction = Fx.EaseOut });
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w <= 0 || h <= 0 || _values.Count < 2) return;
            double lo = Math.Min(_values.Min(), _base > 0 ? _base : double.MaxValue), hi = Math.Max(_values.Max(), _base);
            if (hi - lo < 1e-9) { hi += 1; lo -= 1; }
            double Y(double v) => h - 2 - (v - lo) / (hi - lo) * (h - 4);

            if (_base > 0)
            {
                var dash = new Pen(new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)), 1) { DashStyle = new DashStyle(new double[] { 2, 3 }, 0) };
                dc.DrawLine(dash, new Point(0, Y(_base)), new Point(w, Y(_base)));
            }

            int shown = Math.Max(2, (int)Math.Round(_values.Count * Math.Clamp(Drawn, 0, 1)));
            double step = w / (_values.Count - 1);
            var geometry = new StreamGeometry();
            using (var g = geometry.Open())
            {
                g.BeginFigure(new Point(0, Y(_values[0])), false, false);
                for (int i = 1; i < shown; i++) g.LineTo(new Point(i * step, Y(_values[i])), true, true);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(_colour), 1.4) { LineJoin = PenLineJoin.Round }, geometry);
            var end = new Point((shown - 1) * step, Y(_values[shown - 1]));
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(0x50, _colour.R, _colour.G, _colour.B)), null, end, 3.5, 3.5);
            dc.DrawEllipse(new SolidColorBrush(_colour), null, end, 1.8, 1.8);
        }
    }
}
