using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Fx = Pitzki.Core.Fx;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// The last few things you copied, to put back with one click. Nothing is ever written to disk: the
/// list lives only while the card is on the desktop and is gone the moment it is taken off or the
/// app is shut. Anything a password manager marks as private is skipped, and so is anything copied
/// while Windows is set not to keep clipboard history.
/// </summary>
public sealed class ClipboardWidget : UserControl
{
    const int Keep = 4, Longest = 90;

    /// <summary>The formats a password manager puts on the clipboard to say "do not keep this".</summary>
    static readonly string[] Private =
    {
        "ExcludeClipboardContentFromMonitorProcessing",
        "CanIncludeInClipboardHistory",
        "CanUploadToCloudClipboard",
    };

    readonly List<string> _kept = new();
    readonly StackPanel _rows = new();
    string _last = "";

    /// <summary>How many lines are on the card, for the tests.</summary>
    internal int Count => _kept.Count;

    public ClipboardWidget()
    {
        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Copied"));
        box.Children.Add(_rows);
        Content = box;
        Draw();
        this.Every(1.5, Look);
        // nothing outlives the card
        // (a card joined to another, or pulled out of one, is unloaded and loaded again at once: that is not the end of it)
        Unloaded += (_, _) => Dispatcher.BeginInvoke(() => { if (!IsLoaded) _kept.Clear(); }, System.Windows.Threading.DispatcherPriority.Background);
    }

    /// <summary>What is on the clipboard now, or "" when there is nothing to keep.</summary>
    static string OnClipboard()
    {
        try
        {
            if (!Clipboard.ContainsText()) return "";
            var data = Clipboard.GetDataObject();
            // a password manager says plainly that this one is not to be kept
            if (data != null && Private.Any(f => data.GetDataPresent(f))) return "";
            return Clipboard.GetText();
        }
        catch { return ""; }   // another program had the clipboard open: try again next time
    }

    void Look() => Took(OnClipboard());

    /// <summary>Something was copied. The tests call this with made-up text instead of a real clipboard.</summary>
    internal void Took(string text)
    {
        text = (text ?? "").Trim();
        if (text.Length == 0 || text == _last) return;
        _last = text;
        _kept.Remove(text);                 // something copied again moves back to the top
        _kept.Insert(0, text);
        while (_kept.Count > Keep) _kept.RemoveAt(_kept.Count - 1);
        Draw();
    }

    internal void Forget()
    {
        _kept.Clear();
        _last = "";
        Draw();
    }

    void Draw()
    {
        _rows.Children.Clear();
        if (_kept.Count == 0)
        {
            _rows.Children.Add(new TextBlock
            {
                Text = "Copy something and it shows up here. Nothing is saved to disk.",
                Style = Caption, TextWrapping = TextWrapping.Wrap,
            });
            Live.Arrive(_rows.Children[0], this);
            return;
        }

        for (int i = 0; i < _kept.Count; i++) _rows.Children.Add(Line(_kept[i], i == 0));
        var feet = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        feet.Children.Add(Pill("Forget them", Forget, quiet: true));
        _rows.Children.Add(feet);
        // what was just copied drops in at the top; the button arrives with the first one
        Live.Arrive(_rows.Children[0], this);
        if (_kept.Count == 1) Live.Arrive(feet, this, 1);
    }

    UIElement Line(string text, bool newest)
    {
        // one line, whatever was copied: the newlines in it are shown as arrows
        string said = text.Replace("\r", "").Replace("\n", " ⏎ ");
        if (said.Length > Longest) said = said[..Longest] + "…";

        var words = new TextBlock
        {
            Text = said, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Of(newest ? "TextBrush" : "SubtleBrush"), Margin = new Thickness(7, 4, 7, 4),
            ToolTip = "Click to copy it again",
        };
        var wash = new Border { CornerRadius = new CornerRadius(8), Background = Of("ChipBrush"), Opacity = newest ? 0.55 : 0 };
        var row = new Grid { Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(0, 1, 0, 1), Children = { wash, words } };
        row.MouseEnter += (_, _) => Fx.Animate(wash, OpacityProperty, 1, 120);
        row.MouseLeave += (_, _) => Fx.Animate(wash, OpacityProperty, newest ? 0.55 : 0, 220);
        row.MouseLeftButtonDown += (_, e) => e.Handled = true;      // the card is dragged, the row is not
        row.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            _last = text;                                           // putting it back is not "something new was copied"
            try { Clipboard.SetText(text); } catch { }
        };
        return row;
    }
}
