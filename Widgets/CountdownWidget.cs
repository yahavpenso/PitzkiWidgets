using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// How many days are left until something that matters: a flight, an exam, a birthday. It says the
/// number big, the name small, and has a thin bar for how far along the wait has got. The pencil
/// opens the two boxes where the name and the date are written.
/// </summary>
public sealed class CountdownWidget : UserControl
{
    readonly Placed _placed;
    readonly TextBlock _days = new() { Style = Big, FontSize = 40, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock _unit = new() { FontSize = 12, Foreground = Of("SubtleBrush"), Margin = new Thickness(8, 0, 0, 5), VerticalAlignment = VerticalAlignment.Bottom };
    readonly TextBlock _name = new() { FontSize = 13, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(1, 2, 0, 0) };
    readonly TextBlock _date = new() { FontSize = 11, Foreground = Of("SubtleBrush"), Margin = new Thickness(1, 0, 0, 0) };
    readonly Meter _along = new(166) { Margin = new Thickness(0, 9, 0, 0) };
    readonly StackPanel _show = new();
    readonly StackPanel _edit = new();
    readonly TextBox _nameBox, _dateBox;
    readonly TextBlock _trouble = new() { FontSize = 11, Foreground = Of("BadBrush"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };

    double _wasLeft;

    /// <summary>For the tests: draws it again after they changed the day, and lets them look at the moving parts.</summary>
    internal void Redraw() => Show();
    internal TextBlock Days => _days;
    internal Meter Along => _along;

    public CountdownWidget(Placed placed)
    {
        _placed = placed;

        // what is shown: the number, its unit, the name, the date and the bar
        var number = new StackPanel { Orientation = Orientation.Horizontal };
        number.Children.Add(_days);
        number.Children.Add(_unit);
        _show.Children.Add(number);
        _show.Children.Add(_name);
        _show.Children.Add(_date);
        _show.Children.Add(_along);

        // what is typed: a name and a date
        _nameBox = Field("What is it?", 40);
        _dateBox = Field("Day, e.g. 25/12/2026", 20);
        _edit.Children.Add(Boxed(_nameBox));
        _edit.Children.Add(Boxed(_dateBox));
        _edit.Children.Add(_trouble);
        var save = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        save.Children.Add(Pill("Save", Save));
        save.Children.Add(Pill("Cancel", () => Show(), quiet: true));
        _edit.Children.Add(save);
        foreach (var field in new[] { _nameBox, _dateBox })
            field.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) { Save(); e.Handled = true; } else if (e.Key == System.Windows.Input.Key.Escape) { Show(); e.Handled = true; } };

        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(Title("Countdown"));
        var pencil = Key("", () => Editing(), icon: true, size: 11.5, tip: "Change it");
        pencil.Margin = new Thickness(0, -4, -4, 0);
        Grid.SetColumn(pencil, 1);
        head.Children.Add(pencil);

        var box = new StackPanel { Width = 184 };
        box.Children.Add(head);
        box.Children.Add(_show);
        box.Children.Add(_edit);
        Content = box;

        if (_placed.Until is null) Editing(); else Show();
        this.Every(600, () => { if (_edit.Visibility != Visibility.Visible) Show(); });   // the day turns over at midnight
    }

    /// <summary>The card is dragged by its edges; a click on a box is a click to type.</summary>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        foreach (var field in new[] { _nameBox, _dateBox })
            if (!field.IsKeyboardFocusWithin && field.IsMouseOver) field.Focus();
    }

    TextBox Field(string hint, int most) => new()
    {
        Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 12.5, MaxLength = most,
        Foreground = Of("TextBrush"), CaretBrush = Of("TextBrush"), Padding = new Thickness(9, 5, 9, 5), Tag = hint,
    };

    /// <summary>A box with a rounded chip behind it and its hint showing until something is typed.</summary>
    static UIElement Boxed(TextBox field)
    {
        var hint = new TextBlock
        {
            Text = (string)field.Tag, FontSize = 12.5, Foreground = Of("SubtleBrush"), IsHitTestVisible = false,
            Margin = new Thickness(10, 6, 0, 0),
        };
        field.TextChanged += (_, _) => hint.Visibility = field.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        return new Border
        {
            CornerRadius = new CornerRadius(8), Background = Of("ChipBrush"), Margin = new Thickness(0, 0, 0, 6),
            Child = new Grid { Children = { field, hint } },
        };
    }

    // ----- the two faces -----

    void Editing()
    {
        _nameBox.Text = _placed.Name;
        _dateBox.Text = _placed.Until is { } day ? day.ToString("d MMM yyyy", CultureInfo.InvariantCulture) : "";
        _trouble.Visibility = Visibility.Collapsed;
        _show.Visibility = Visibility.Collapsed;
        _edit.Visibility = Visibility.Visible;
        Live.Arrive(_edit, this);
    }

    void Show()
    {
        if (_placed.Until is not { } day) { Editing(); return; }
        // coming back from the boxes, the number slides in; already showing, it stays put
        bool already = _show.Visibility == Visibility.Visible && _edit.Visibility != Visibility.Visible;
        _edit.Visibility = Visibility.Collapsed;
        _show.Visibility = Visibility.Visible;
        if (!already) Live.Arrive(_show, this);

        int left = (day.Date - DateTime.Today).Days;
        _days.Count(left == 0 ? "Today" : Math.Abs(left).ToString(CultureInfo.InvariantCulture), Math.Abs(left), _wasLeft);
        _wasLeft = Math.Abs(left);
        _days.Size(left == 0 ? 30 : 40);
        _days.Tint(left == 0 ? Of("GoodBrush") : left < 0 ? Of("SubtleBrush") : Of("AccentBrush"));
        _unit.Say(left == 0 ? "" : Math.Abs(left) == 1 ? (left < 0 ? "day ago" : "day to go") : (left < 0 ? "days ago" : "days to go"));
        _name.Say(_placed.Name.Length > 0 ? _placed.Name : "Your day");
        _date.Say(day.ToString("dddd, d MMMM yyyy", CultureInfo.InvariantCulture));

        // the bar fills from the day it was set to the day itself
        if (_placed.Since is { } start && day.Date > start.Date && left >= 0)
        {
            Live.Show(_along, true, this);
            _along.Glide((DateTime.Today - start.Date).TotalDays / (day.Date - start.Date).TotalDays, 900);
            _along.Colour = Of("AccentBrush");
        }
        else Live.Show(_along, false, this);
    }

    void Save()
    {
        if (!TryDay(_dateBox.Text, out var day))
        {
            _trouble.Text = _dateBox.Text.Trim().Length == 0 ? "Write the day, like 25/12/2026" : "That does not look like a day. Try 25/12/2026";
            _trouble.Visibility = Visibility.Visible;
            return;
        }
        _placed.Name = _nameBox.Text.Trim();
        _placed.Until = day;
        _placed.Since = DateTime.Today;
        App.Board.Save();
        Show();
    }

    /// <summary>
    /// A day as people write it: 25/12/2026, 25.12.2026, 2026-12-25, 25 Dec 2026, or 25/12 for the next
    /// one coming. The year is the coming one when it is left off.
    /// </summary>
    internal static bool TryDay(string said, out DateTime day)
    {
        said = said.Trim();
        day = default;
        if (said.Length == 0) return false;

        string[] whole = { "d/M/yyyy", "d.M.yyyy", "d-M-yyyy", "yyyy-M-d", "yyyy/M/d", "d MMM yyyy", "d MMMM yyyy", "MMM d yyyy", "MMMM d yyyy", "d/M/yy", "d.M.yy" };
        if (DateTime.TryParseExact(said, whole, CultureInfo.InvariantCulture, DateTimeStyles.None, out day)) return true;

        // no year: the next time that day comes round
        string[] short_ = { "d/M", "d.M", "d-M", "d MMM", "d MMMM", "MMM d", "MMMM d" };
        if (DateTime.TryParseExact(said, short_, CultureInfo.InvariantCulture, DateTimeStyles.None, out var that))
        {
            day = new DateTime(DateTime.Today.Year, that.Month, that.Day);
            if (day < DateTime.Today) day = day.AddYears(1);
            return true;
        }

        return DateTime.TryParse(said, CultureInfo.CurrentCulture, DateTimeStyles.None, out day);
    }
}
