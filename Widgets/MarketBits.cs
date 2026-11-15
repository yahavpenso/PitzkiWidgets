using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>Small pieces the market cards share: a typing box with a hint, a heading with keys, a link that opens in the browser.</summary>
public static class MarketBits
{
    public static TextBox Field(string hint, int most, double width = double.NaN) => new()
    {
        Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 12.5, MaxLength = most, Width = width,
        Foreground = Of("TextBrush"), CaretBrush = Of("TextBrush"), Padding = new Thickness(8, 4, 8, 4), Tag = hint,
    };

    /// <summary>A box with a rounded chip behind it and its hint showing until something is typed.</summary>
    public static Border Boxed(TextBox field, Thickness? margin = null)
    {
        var hint = new TextBlock
        {
            Text = (string)field.Tag, FontSize = 12, Foreground = Of("SubtleBrush"), IsHitTestVisible = false,
            Margin = new Thickness(9, 5, 0, 0),
        };
        field.TextChanged += (_, _) => hint.Visibility = field.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        hint.Visibility = field.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        return new Border
        {
            CornerRadius = new CornerRadius(8), Background = Of("ChipBrush"), Margin = margin ?? new Thickness(0, 0, 0, 6),
            Child = new Grid { Children = { field, hint } },
        };
    }

    /// <summary>The card is dragged by its edges; a click on one of its boxes is a click to type.</summary>
    public static void FocusOnClick(UserControl card, params TextBox[] fields)
    {
        card.PreviewMouseLeftButtonUp += (_, _) =>
        {
            foreach (var field in fields)
                if (!field.IsKeyboardFocusWithin && field.IsMouseOver && field.IsVisible) { field.Focus(); break; }
        };
    }

    /// <summary>A heading with keys on the right.</summary>
    public static Grid Head(string title, params UIElement[] keys)
    {
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.Children.Add(Title(title));
        int col = 1;
        foreach (var key in keys)
        {
            head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            if (key is FrameworkElement f && f.Margin == default) f.Margin = new Thickness(0, -4, -2, 0);
            Grid.SetColumn(key, col++);
            head.Children.Add(key);
        }
        return head;
    }

    public static void Open(string url)
    {
        if (!url.StartsWith("https://", StringComparison.Ordinal)) return;
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    /// <summary>Green for up, red for down, grey for no move.</summary>
    public static Brush UpDown(double move) => Of(move > 0.005 ? "GoodBrush" : move < -0.005 ? "BadBrush" : "SubtleBrush");

    public static Color UpDownColour(double move) => ((SolidColorBrush)UpDown(move)).Color;

    /// <summary>A small "$" / "₪" key: switches skin and item values between dollars and shekels, in both apps.</summary>
    public static TextBlock MoneyKey(Action redraw)
    {
        TextBlock? key = null;
        key = Key(Pitzki.Core.MoneyPrefs.Shekels ? "₪" : "$", () =>
        {
            Pitzki.Core.MoneyPrefs.Shekels = !Pitzki.Core.MoneyPrefs.Shekels;
            key!.Say(Pitzki.Core.MoneyPrefs.Shekels ? "₪" : "$");
            redraw();
        }, size: 12, tip: "Dollars or shekels");
        key.Margin = new Thickness(0, -4, 0, 0);
        key.FontWeight = FontWeights.SemiBold;
        // switched somewhere else (another card, or Pitzki): show it here too
        void Follow() => key.Dispatcher.BeginInvoke(() => key.Say(Pitzki.Core.MoneyPrefs.Shekels ? "₪" : "$"));
        key.Loaded += (_, _) => Pitzki.Core.MoneyPrefs.Changed += Follow;
        key.Unloaded += (_, _) => Pitzki.Core.MoneyPrefs.Changed -= Follow;
        return key;
    }

    /// <summary>A line of small grey words.</summary>
    public static TextBlock Small(string text = "") => new()
    {
        Text = text, FontSize = 10.5, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0),
    };

    /// <summary>A plain click on any element that does not start a drag of the card.</summary>
    public static void Click(FrameworkElement e, Action press)
    {
        e.Cursor = Cursors.Hand;
        e.MouseLeftButtonDown += (_, a) => a.Handled = true;
        e.MouseLeftButtonUp += (_, a) => { a.Handled = true; press(); };
    }
}
