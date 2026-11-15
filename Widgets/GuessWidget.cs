using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using static PitzkiWidgets.Widgets.MarketBits;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// A guessing game: will this stock be higher or lower when the market closes on its next trading day
/// than it is right now? Press Up or Down; once that day's close is known the guess is settled, a point
/// for a right one, and the streak grows. The stock is in <c>placed.Text</c>, the open guess in
/// <c>placed.Name</c> ("day|price|up"), the score in <c>placed.Number</c>, the record in <c>placed.Items</c>.
/// </summary>
public sealed class GuessWidget : UserControl
{
    readonly Placed _placed;
    readonly TextBlock _symbol = new() { FontSize = 13, FontWeight = FontWeights.SemiBold };
    readonly TextBlock _price = new() { Style = Big, FontSize = 22 };
    readonly TextBlock _move = new() { FontSize = 11 };
    readonly TextBlock _question = new() { FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 6) };
    readonly StackPanel _buttons = new() { Orientation = Orientation.Horizontal };
    readonly TextBlock _score = new() { FontSize = 12, FontWeight = FontWeights.SemiBold };
    readonly TextBlock _record = Small();
    readonly TextBox _box = Field("Another stock, like Tesla", 40, 150);
    double _was;
    bool _has;

    internal string QuestionText => _question.Text;
    internal string ScoreText => _score.Text;
    internal Task Refresh() => RefreshAsync();
    internal void Press(bool up) => Guess(up);

    string Symbol => _placed.Text.Trim().Length > 0 ? _placed.Text.Trim().ToUpperInvariant() : "AAPL";
    double _now;

    public GuessWidget(Placed placed)
    {
        _placed = placed;
        _buttons.Children.Add(Pill("▲ Higher", () => Guess(true)));
        _buttons.Children.Add(Pill("▼ Lower", () => Guess(false), quiet: true));

        var top = new Grid();
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel();
        left.Children.Add(_symbol);
        left.Children.Add(_move);
        _price.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(_price, 1);
        top.Children.Add(left);
        top.Children.Add(_price);

        var change = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        change.Children.Add(Boxed(_box, new Thickness(0, 0, 4, 0)));
        change.Children.Add(Pill("Switch", Switch, quiet: true));
        _box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Switch(); };
        FocusOnClick(this, _box);

        var box = new StackPanel { Width = 236 };
        box.Children.Add(Head("Higher or lower?"));
        box.Children.Add(top);
        box.Children.Add(_question);
        box.Children.Add(_buttons);
        box.Children.Add(new Border { Height = 1, Background = Of("ChipBrush"), Margin = new Thickness(0, 10, 0, 6) });
        box.Children.Add(_score);
        box.Children.Add(_record);
        box.Children.Add(change);
        Content = box;
        ShowScore();
        this.Every(120, () => _ = RefreshAsync());
    }

    (DateTime Day, double Price, bool Up)? Open
    {
        get
        {
            var p = _placed.Name.Split('|');
            if (p.Length != 3 || !DateTime.TryParse(p[0], CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
                || !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var price)) return null;
            return (day, price, p[2] == "up");
        }
    }

    void Guess(bool up)
    {
        if (Open != null || _now <= 0) return;
        _placed.Name = string.Join("|", DateTime.Today.ToString("yyyy-MM-dd"), _now.ToString(CultureInfo.InvariantCulture), up ? "up" : "down");
        App.Board.Save();
        Live.Nudge(_question, this);
        _ = RefreshAsync();
    }

    void Switch() => _ = SwitchAsync();

    async Task SwitchAsync()
    {
        var symbol = (await Market.ResolveAsync(_box.Text, 1)).FirstOrDefault();
        if (symbol == null) return;
        if (Open != null) { _record.Say("Wait for your guess to be settled first"); return; }
        _placed.Text = symbol;
        _box.Text = "";
        App.Board.Save();
        _has = false;
        _ = RefreshAsync();
    }

    async Task RefreshAsync()
    {
        string symbol = Symbol;
        var q = await Market.QuoteAsync(symbol);
        _symbol.Say(symbol);
        if (q == null) { _price.Say("--"); _question.Say("Can't find this stock, or offline."); return; }
        _now = q.Price;
        string text = Market.Price(q.Price, q.Currency);
        var (_, scale) = Market.Money(q.Currency);
        if (_has) _price.Count(text, q.Price * scale, _was); else _price.Say(text);
        _was = q.Price * scale;
        _has = true;
        _move.Say(Market.Percent(q.DayPercent) + " today");
        _move.Tint(UpDown(q.DayPercent));

        if (Open is { } open)
        {
            // settled by the first trading day's close after the day the guess was made
            var days = await Market.QuoteAsync(symbol, "1mo");
            double? close = null;
            if (days != null)
                for (int i = 0; i < days.Times.Count; i++)
                {
                    var day = DateTimeOffset.FromUnixTimeSeconds(days.Times[i]).LocalDateTime.Date;
                    if (day > open.Day && (i < days.Times.Count - 1 || !days.Open)) { close = days.Values[i]; break; }
                }
            if (close is { } c)
            {
                bool right = open.Up ? c > open.Price : c < open.Price;
                Settle(right, open.Price, c, q.Currency);
            }
            else
            {
                _question.Say($"You said {(open.Up ? "higher ▲" : "lower ▼")} than {Market.Price(open.Price, q.Currency)}. It's settled when the next trading day closes.");
                Live.Show(_buttons, false, this);
            }
        }
        if (Open == null)
        {
            _question.Say($"Will {symbol} close higher or lower than {text} on its next trading day?");
            Live.Show(_buttons, true, this);
        }
        ShowScore();
    }

    void Settle(bool right, double from, double to, string currency)
    {
        if (right) _placed.Number++;
        _placed.Items.Insert(0, $"{DateTime.Today:yyyy-MM-dd}|{Symbol}|{(right ? 1 : 0)}");
        while (_placed.Items.Count > 50) _placed.Items.RemoveAt(_placed.Items.Count - 1);
        _placed.Name = "";
        App.Board.Save();
        App.Alert("guess" + DateTime.Now.Ticks, right ? "You guessed right!" : "Not this time",
            $"{Symbol} went from {Market.Price(from, currency)} to {Market.Price(to, currency)}");
        _record.Say(right ? $"Right! {Market.Price(from, currency)} → {Market.Price(to, currency)}" : $"Wrong: {Market.Price(from, currency)} → {Market.Price(to, currency)}");
    }

    void ShowScore()
    {
        var all = _placed.Items.Select(i => i.Split('|')).Where(p => p.Length == 3).ToList();
        int streak = all.TakeWhile(p => p[2] == "1").Count();
        int played = all.Count;
        _score.Say($"{_placed.Number} {(_placed.Number == 1 ? "point" : "points")} · {played} played" + (streak >= 2 ? $" · 🔥 {streak} in a row" : ""));
        if (_record.Text.Length == 0) _record.Text = played == 0 ? "A point for every right guess." : $"{(played > 0 ? 100 * all.Count(p => p[2] == "1") / played : 0)}% right so far";
    }
}
