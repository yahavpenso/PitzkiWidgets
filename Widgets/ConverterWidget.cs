using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using static PitzkiWidgets.Widgets.MarketBits;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// A quick money converter: type an amount, pick the two currencies (or a coin), and the answer is there
/// as you type, at the market's rate right now (Yahoo Finance). The last pair is kept in <c>placed.Text</c>.
/// </summary>
public sealed class ConverterWidget : UserControl
{
    public static readonly string[] Units = { "USD", "ILS", "EUR", "GBP", "JPY", "BTC", "ETH" };
    static readonly HashSet<string> Coins = new() { "BTC", "ETH", "SOL" };

    readonly Placed _placed;
    readonly TextBox _amount = Field("Amount", 18, 110);
    readonly TextBlock _answer = new() { Style = Big, FontSize = 24, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _rate = Small();
    readonly StackPanel _from = new() { Orientation = Orientation.Horizontal }, _to = new() { Orientation = Orientation.Horizontal };
    string _f = "USD", _t = "ILS";
    double _was;
    bool _has;
    int _gen;

    internal string AnswerText => _answer.Text;
    internal Task Convert(string amount, string from, string to) { _f = from; _t = to; DrawUnits(); _amount.Text = amount; return WorkAsync(); }

    public ConverterWidget(Placed placed)
    {
        _placed = placed;
        var parts = placed.Text.Split('>');
        if (parts.Length == 2 && Units.Contains(parts[0]) && Units.Contains(parts[1])) { _f = parts[0]; _t = parts[1]; }
        if (_amount.Text.Length == 0) _amount.Text = "100";
        _amount.TextChanged += (_, _) => _ = WorkAsync();
        FocusOnClick(this, _amount);

        var swap = Key("", () => { (_f, _t) = (_t, _f); Keep(); DrawUnits(); _ = WorkAsync(); }, icon: true, size: 12, tip: "Swap");
        var top = new StackPanel { Orientation = Orientation.Horizontal };
        top.Children.Add(Boxed(_amount, new Thickness(0, 0, 6, 0)));
        top.Children.Add(swap);

        var box = new StackPanel { Width = 250 };
        box.Children.Add(Title("Money converter"));
        box.Children.Add(top);
        box.Children.Add(Small("From"));
        box.Children.Add(_from);
        box.Children.Add(Small("To"));
        box.Children.Add(_to);
        _answer.Margin = new Thickness(0, 8, 0, 0);
        box.Children.Add(_answer);
        box.Children.Add(_rate);
        Content = box;

        DrawUnits();
        this.Every(120, () => _ = WorkAsync());
    }

    void Keep()
    {
        _placed.Text = _f + ">" + _t;
        App.Board.Save();
    }

    void DrawUnits()
    {
        foreach (var (panel, from) in new[] { (_from, true), (_to, false) })
        {
            panel.Children.Clear();
            foreach (string unit in Units)
            {
                bool on = unit == (from ? _f : _t);
                var chip = new Border
                {
                    CornerRadius = new CornerRadius(7), Padding = new Thickness(5, 2, 5, 2), Margin = new Thickness(0, 2, 3, 2),
                    Background = on ? Of("AccentBrush") : Of("ChipBrush"),
                    Child = new TextBlock { Text = unit, FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = on ? Of("OnAccentBrush") : Of("TextBrush") },
                };
                string u = unit;
                bool f = from;
                Click(chip, () =>
                {
                    if (f) _f = u; else _t = u;
                    Keep();
                    DrawUnits();
                    _ = WorkAsync();
                });
                panel.Children.Add(chip);
            }
        }
    }

    /// <summary>Dollars for one of this unit, right now.</summary>
    static async Task<double> DollarsFor(string unit)
    {
        if (unit == "USD") return 1;
        if (Coins.Contains(unit)) return (await Market.QuoteAsync(unit + "-USD"))?.Price ?? 0;
        return (await Market.QuoteAsync(unit + "USD=X"))?.Price ?? 0;
    }

    async Task WorkAsync()
    {
        int gen = ++_gen;
        string text = _amount.Text.Trim().Replace(",", "");
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var amount) || amount < 0)
        {
            _answer.Say("—");
            _rate.Say(text.Length == 0 ? "Type an amount" : "That isn't a number");
            return;
        }
        double from = await DollarsFor(_f), to = await DollarsFor(_t);
        if (gen != _gen) return;
        if (from <= 0 || to <= 0) { _answer.Say("—"); _rate.Say("Couldn't get the rate. Offline?"); return; }
        double rate = from / to, result = amount * rate;
        string shown = Show(result, _t);
        if (_has) _answer.Count(shown, result, _was); else _answer.Say(shown);
        _was = result;
        _has = true;
        _rate.Say($"1 {_f} = {Show(rate, _t)} · live rate {DateTime.Now:HH:mm}");
    }

    internal static string Show(double value, string unit)
    {
        string sign = unit switch { "USD" => "$", "ILS" => "₪", "EUR" => "€", "GBP" => "£", "JPY" => "¥", _ => "" };
        string format = Coins.Contains(unit) ? (value < 1 ? "0.000000" : "#,0.0000") : Math.Abs(value) < 1 ? "0.0000" : "#,0.00";
        string number = value.ToString(format, CultureInfo.InvariantCulture);
        return sign.Length > 0 ? sign + number : number + " " + unit;
    }
}
