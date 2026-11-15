using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PitzkiWidgets.Core;
using Fx = Pitzki.Core.Fx;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// A short list to tick off. Click a line to tick it, the cross at its end to drop it, and type in the
/// box underneath to add one. Every card keeps its own list, so there can be one for work and one for home.
/// Each line is kept as "0|words" while open and "1|words" once done.
/// </summary>
public sealed class TasksWidget : UserControl
{
    /// <summary>A card is a card, not a document: past this many lines the box for a new one goes away.</summary>
    const int Most = 7;

    readonly Placed _placed;
    readonly TextBlock _title = Title("Tasks");
    readonly StackPanel _lines = new();
    readonly TextBox _add;
    readonly TextBlock _hint;
    readonly TextBlock _clear;
    int _arrive = -1, _ticked = -1;

    public TasksWidget(Placed placed)
    {
        _placed = placed;

        _add = new TextBox
        {
            Background = Brushes.Transparent, BorderThickness = new Thickness(0), FontSize = 12,
            Foreground = Of("TextBrush"), CaretBrush = Of("TextBrush"), Padding = new Thickness(0, 3, 0, 3),
            MaxLength = 60, VerticalContentAlignment = VerticalAlignment.Center,
        };
        _add.KeyDown += (_, e) =>
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            Add(_add.Text);
            _add.Clear();
            e.Handled = true;
        };

        // a faint line of words until something is typed, so the box does not look like a gap
        _hint = new TextBlock { Text = "Add a task, press Enter", Style = Caption, FontWeight = FontWeights.Normal, IsHitTestVisible = false, Margin = new Thickness(0, 4, 0, 0) };
        _add.TextChanged += (_, _) => _hint.Visibility = _add.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;

        var field = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        field.Children.Add(_add);
        field.Children.Add(_hint);

        _clear = new TextBlock { Text = "Clear done", Style = Caption, Cursor = Cursors.Hand, Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed };
        _clear.MouseLeftButtonDown += (_, e) => e.Handled = true;
        _clear.MouseLeftButtonUp += (_, e) => { e.Handled = true; ClearDone(); };
        _clear.MouseEnter += (_, _) => _clear.Foreground = Of("TextBrush");
        _clear.MouseLeave += (_, _) => _clear.Foreground = Of("SubtleBrush");

        var box = new StackPanel { Width = 184 };
        box.Children.Add(_title);
        box.Children.Add(_lines);
        box.Children.Add(field);
        box.Children.Add(_clear);
        Content = box;

        Draw();
    }

    /// <summary>The card is dragged by its edges; a click on the box is a click to type.</summary>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (!_add.IsKeyboardFocusWithin && _add.IsMouseOver) _add.Focus();
    }

    // ----- the list -----

    (bool Done, string Words) Read(string line) =>
        line.Length > 1 && line[1] == '|' ? (line[0] == '1', line[2..]) : (false, line);

    static string Write(bool done, string words) => (done ? "1|" : "0|") + words;

    internal void Add(string words)
    {
        words = words.Trim();
        if (words.Length == 0 || _placed.Items.Count >= Most) return;
        _placed.Items.Add(Write(false, words));
        _arrive = _placed.Items.Count - 1;          // the new line slides in at the bottom
        Keep();
    }

    void Toggle(int at)
    {
        if (at < 0 || at >= _placed.Items.Count) return;
        var (done, words) = Read(_placed.Items[at]);
        _placed.Items[at] = Write(!done, words);
        _ticked = at;                              // its ring pops as it fills or empties
        Keep();
    }

    internal UIElement? LineAt(int at) => at < _lines.Children.Count ? _lines.Children[at] : null;
    internal void DropAt(int at) => Drop(at);

    void Drop(int at)
    {
        if (at < 0 || at >= _placed.Items.Count) return;
        string gone = _placed.Items[at];
        // the line slides off to the side first, then the list closes up
        Leave(new[] { at }, () =>
        {
            int now = _placed.Items.IndexOf(gone);
            if (now >= 0) _placed.Items.RemoveAt(now);
            Keep();
        });
    }

    void ClearDone()
    {
        var done = Enumerable.Range(0, _placed.Items.Count).Where(i => Read(_placed.Items[i]).Done).ToArray();
        Leave(done, () =>
        {
            _placed.Items.RemoveAll(line => Read(line).Done);
            Keep();
        });
    }

    /// <summary>Slides the lines away, one after another, then does what was asked. Straight away when the card is not on screen.</summary>
    void Leave(int[] lines, Action then)
    {
        var rows = lines.Where(i => i < _lines.Children.Count).Select(i => _lines.Children[i]).ToList();
        if (rows.Count == 0 || !Live.Showing(this)) { then(); return; }
        _lines.IsHitTestVisible = false;           // no second click while they are on their way out
        for (int i = 0; i < rows.Count; i++)
        {
            bool last = i == rows.Count - 1;
            var row = rows[i];
            Fx.Disappear(row, dx: 18, dy: 0, ms: 170 + i * 50, done: last ? () => { _lines.IsHitTestVisible = true; then(); } : null);
        }
    }

    void Keep()
    {
        App.Board.Save();
        Draw();
    }

    void Draw()
    {
        _lines.Children.Clear();
        for (int i = 0; i < _placed.Items.Count; i++) _lines.Children.Add(Line(i));

        if (_arrive >= 0 && _arrive < _lines.Children.Count) Live.Arrive(_lines.Children[_arrive], this);
        if (_ticked >= 0 && _ticked < _lines.Children.Count && _lines.Children[_ticked] is Grid { Children.Count: > 0 } row)
            Live.Nudge(row.Children[0], this);
        _arrive = _ticked = -1;

        int open = _placed.Items.Count(line => !Read(line).Done);
        _title.Say(_placed.Items.Count == 0 ? "TASKS" : $"TASKS  ·  {open} LEFT", quick: true);
        Live.Show(_clear, _placed.Items.Count > open, this);
        _add.IsEnabled = _placed.Items.Count < Most;
        _hint.Say(_placed.Items.Count < Most ? "Add a task, press Enter" : "That is plenty for one card");
    }

    /// <summary>One line: a ring that fills when it is done, the words, and a cross to take it off.</summary>
    UIElement Line(int at)
    {
        var (done, words) = Read(_placed.Items[at]);

        var ring = new Border
        {
            Width = 15, Height = 15, CornerRadius = new CornerRadius(8), BorderThickness = new Thickness(1.5),
            BorderBrush = done ? Of("AccentBrush") : Of("SubtleBrush"),
            Background = done ? Of("AccentBrush") : Brushes.Transparent,
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 9, 0),
            Child = done ? new TextBlock
            {
                Text = "✓", FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = Of("OnAccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            } : null,
        };

        var text = new TextBlock
        {
            Text = words, FontSize = 12.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
            Foreground = done ? Of("SubtleBrush") : Of("TextBrush"),
            TextDecorations = done ? TextDecorations.Strikethrough : null,
            ToolTip = words,
        };

        var cross = Key("×", () => Drop(at), size: 14, tip: "Take it off");
        cross.Opacity = 0;

        var row = new Grid { Margin = new Thickness(0, 3, 0, 3), Background = Brushes.Transparent, Cursor = Cursors.Hand };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(ring);
        Grid.SetColumn(text, 1);
        row.Children.Add(text);
        Grid.SetColumn(cross, 2);
        row.Children.Add(cross);

        row.MouseEnter += (_, _) => Fx.Animate(cross, OpacityProperty, 1, 120);
        row.MouseLeave += (_, _) => Fx.Animate(cross, OpacityProperty, 0, 180);
        row.MouseLeftButtonDown += (_, e) => e.Handled = true;
        row.MouseLeftButtonUp += (_, e) => { e.Handled = true; Toggle(at); };
        return row;
    }
}
