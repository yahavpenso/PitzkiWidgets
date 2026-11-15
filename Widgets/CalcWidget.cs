using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Pitzki's search-box sums as a card: type "12*(3+4)", "5 km to miles" or "20 usd to ils" and the
/// answer appears under it as you type. Currency comes from the same free daily rates Pitzki uses.
/// A click on the answer copies it. The last thing typed stays on the card.
/// </summary>
public sealed class CalcWidget : UserControl
{
    readonly Placed _placed;
    readonly TextBox _sum;
    readonly TextBlock _answer = new() { Style = Big, FontSize = 24, TextTrimming = TextTrimming.CharacterEllipsis, Cursor = Cursors.Hand, ToolTip = "Click to copy" };
    readonly TextBlock _kind = new() { Style = Caption, Margin = new Thickness(0, 2, 0, 0) };
    int _asked;

    /// <summary>For the tests: what the card answers right now.</summary>
    internal string Answer => _answer.Text;

    public CalcWidget(Placed placed)
    {
        _placed = placed;
        _sum = new TextBox
        {
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 13, MaxLength = 80,
            Foreground = Of("TextBrush"), CaretBrush = Of("TextBrush"), Padding = new Thickness(9, 6, 9, 6), Text = placed.Text,
        };
        var hint = new TextBlock
        {
            Text = "12*7, 5 km to mi, 20 usd to ils", FontSize = 12, Foreground = Of("SubtleBrush"), IsHitTestVisible = false,
            Margin = new Thickness(10, 7, 0, 0), Visibility = placed.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed,
        };
        _sum.TextChanged += (_, _) =>
        {
            hint.Visibility = _sum.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            _ = Work(_sum.Text);
        };
        _sum.LostKeyboardFocus += (_, _) => { if (_placed.Text == _sum.Text) return; _placed.Text = _sum.Text; App.Board.Save(); };

        _answer.MouseLeftButtonDown += (_, e) => e.Handled = true;
        _answer.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            string plain = _answer.Text.Split(' ')[0].Replace(",", "");
            if (plain.Length == 0 || plain == "—") return;
            try { Clipboard.SetText(plain); _kind.Say("Copied " + plain); Live.Nudge(_answer, this); } catch { }
        };

        var box = new StackPanel { Width = 200 };
        box.Children.Add(Title("Calculator"));
        box.Children.Add(new Border { CornerRadius = new CornerRadius(8), Background = Of("ChipBrush"), Child = new Grid { Children = { _sum, hint } } });
        var result = new StackPanel { Margin = new Thickness(2, 8, 0, 0) };
        result.Children.Add(_answer);
        result.Children.Add(_kind);
        box.Children.Add(result);
        Content = box;

        _ = Work(placed.Text);
    }

    /// <summary>The card is dragged by its edges; a click on the box is a click to type.</summary>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (!_sum.IsKeyboardFocusWithin && _sum.IsMouseOver) _sum.Focus();
    }

    /// <summary>Works the text out: a sum, units, or money (which has to ask for the rate).</summary>
    internal async Task Work(string text)
    {
        int ask = ++_asked;
        text = text.Trim();
        if (text.Length == 0) { _answer.Say("—"); _kind.Say("Type a sum or a conversion"); return; }

        if (Calc.TryEvaluate(text, out double value)) { _answer.Say(Calc.Show(value)); _kind.Say("Sum"); return; }
        if (Calc.TryParseQuestion(text, out double amount, out string from, out string to))
        {
            if (Calc.TryConvertUnits(amount, from, to, out string answer)) { _answer.Say(answer); _kind.Say("Units"); return; }
            if (Calc.Currency(from) is { } fromCode && Calc.Currency(to) is { } toCode)
            {
                _kind.Say("Asking for today's rate…");
                string? money = await Rates.ConvertAsync(amount, fromCode, toCode);
                if (ask != _asked) return;           // something newer was typed while waiting
                _answer.Say(money ?? "—");
                _kind.Say(money != null ? "Money · today's rate" : "Couldn't get the rate right now");
                return;
            }
        }
        if (double.TryParse(text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double plain))
        {
            _answer.Say(Calc.Show(plain)); _kind.Say("Type + - * / after it"); return;
        }
        _answer.Say("—");
        _kind.Say("Not a sum I know");
    }
}
