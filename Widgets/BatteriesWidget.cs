using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// How full the batteries are: wireless headphones, a mouse, a game controller (anything that tells
/// Windows its charge, the same number Windows shows in its Bluetooth settings), and the laptop
/// itself when this is one. Red under 15%, amber under 30%.
/// </summary>
public sealed class BatteriesWidget : UserControl
{
    readonly StackPanel _rows = new();
    readonly TextBlock _empty = new()
    {
        FontSize = 11.5, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed,
    };
    bool _asking;

    public BatteriesWidget()
    {
        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Batteries"));
        box.Children.Add(_rows);
        box.Children.Add(_empty);
        Content = box;
        this.Every(60, Ask);
    }

    async void Ask()
    {
        if (_asking) return;
        _asking = true;
        try
        {
            var devices = await DeviceBattery.ReadAsync();
            Show(devices.Select(d => (d.name, d.percent, false)).ToList());
        }
        catch { Show(new List<(string, int, bool)>()); }
        finally { _asking = false; }
    }

    /// <summary>
    /// Shows a made-up set, so the test can picture the card with something on it: a PC with no
    /// Bluetooth battery to read has an empty card, and an empty card says very little about the real one.
    /// </summary>
    internal void Demo() => Show(new List<(string, int, bool)>
    {
        ("Headphones", 64, false), ("Mouse", 18, false), ("Controller", 91, false), ("Keyboard", 9, false),
    }, demo: true);

    internal int Shown => _rows.Children.Count;

    void Show(List<(string Name, int Percent, bool Charging)> found, bool demo = false)
    {
        // the laptop itself, first, when there is one
        if (!demo && Battery.HasBattery) found.Insert(0, ("This PC", Battery.Percent, Battery.PluggedIn));

        // a row per battery, kept rather than rebuilt, so a charge that changed rolls and its bar slides
        while (_rows.Children.Count > found.Count) _rows.Children.RemoveAt(_rows.Children.Count - 1);
        for (int i = 0; i < found.Count; i++)
        {
            var (name, percent, charging) = found[i];
            if (i >= _rows.Children.Count)
            {
                var fresh = new Row(true, 56);
                fresh.Label.MaxWidth = 74;
                fresh.Label.TextTrimming = TextTrimming.CharacterEllipsis;
                _rows.Children.Add(fresh);
                Live.Arrive(fresh, this, i);
            }
            var row = (Row)_rows.Children[i];
            row.Label.Say(name.ToUpperInvariant());
            row.Label.ToolTip = name;
            row.Value.Say(percent + "%" + (charging ? " ⚡" : ""));
            if (row.Bar != null)
            {
                row.Bar.Part = percent / 100.0;
                row.Bar.Colour = Of(percent <= 15 && !charging ? "BadBrush" : percent <= 30 && !charging ? "WarnBrush" : "GoodBrush");
            }
        }

        Live.Show(_empty, _rows.Children.Count == 0, this);
        if (_rows.Children.Count == 0)
            _empty.Text = "Nothing here reports a battery. Wireless headphones, mice and controllers that tell Windows their charge will show up.";
    }
}
