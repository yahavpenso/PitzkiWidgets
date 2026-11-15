using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// A note you can write straight onto the desktop. Every card keeps its own words, so several of
/// them can sit side by side. What is typed is saved a moment after the typing stops.
/// </summary>
public sealed class NotesWidget : UserControl
{
    readonly Placed _placed;
    readonly TextBox _paper;
    readonly DispatcherTimer _save = new() { Interval = TimeSpan.FromSeconds(1.2) };

    public NotesWidget(Placed placed)
    {
        _placed = placed;

        _paper = new TextBox
        {
            Text = placed.Text,
            Width = 184, Height = 108,
            AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Background = System.Windows.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = Of("TextBrush"),
            CaretBrush = Of("TextBrush"),
            FontSize = 12.5,
            Padding = new Thickness(0, 2, 0, 0),
        };
        // the card is dragged by its edges; inside the paper the mouse belongs to the text
        _paper.PreviewMouseLeftButtonDown += (_, e) => e.Handled = false;
        _paper.TextChanged += (_, _) => { _save.Stop(); _save.Start(); };
        _save.Tick += (_, _) => { _save.Stop(); Keep(); };

        // a faint line of words while the note is empty, so it does not look broken
        var hint = new TextBlock
        {
            Text = "Click and write something", Style = Caption, FontWeight = FontWeights.Normal,
            Margin = new Thickness(0, 2, 0, 0), IsHitTestVisible = false,
        };
        void Hint() => hint.Visibility = _paper.Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        _paper.TextChanged += (_, _) => Hint();
        Hint();

        var box = new StackPanel();
        box.Children.Add(Title("Note"));
        box.Children.Add(new Grid { Children = { _paper, hint } });
        Content = box;

        Unloaded += (_, _) => Keep();
    }

    /// <summary>A note window has to be clickable, so it is the one card that takes the focus.</summary>
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (!_paper.IsKeyboardFocusWithin && _paper.IsMouseOver) _paper.Focus();
    }

    void Keep()
    {
        if (_placed.Text == _paper.Text) return;
        _placed.Text = _paper.Text;
        App.Board.Save();
    }
}
