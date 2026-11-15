using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>How much room is left on every drive, the fullest ones in red.</summary>
public sealed class DrivesWidget : UserControl
{
    readonly DiskWatch _disk = new();
    readonly StackPanel _rows = new();

    public DrivesWidget()
    {
        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Drives"));
        box.Children.Add(_rows);
        Content = box;
        this.Every(60, Tick);
    }

    void Tick()
    {
        _disk.Refresh();

        // a row per drive, kept rather than rebuilt so the bars do not blink every minute
        while (_rows.Children.Count > _disk.Drives.Count) _rows.Children.RemoveAt(_rows.Children.Count - 1);
        while (_rows.Children.Count > 0 && _rows.Children[0] is not Row) _rows.Children.RemoveAt(0);
        while (_rows.Children.Count < _disk.Drives.Count)
        {
            var fresh = new Row(true, 56);
            _rows.Children.Add(fresh);
            Live.Arrive(fresh, this, _rows.Children.Count - 1);     // a drive that was just plugged in slides in
        }

        for (int i = 0; i < _disk.Drives.Count; i++)
        {
            var drive = _disk.Drives[i];
            var row = (Row)_rows.Children[i];
            row.Label.Say(drive.Letter);                // the letter only: a volume name can be very long
            row.ToolTip = drive.Name;
            row.Value.Say(DiskWatch.Size(drive.Free) + " free");
            if (row.Bar == null) continue;
            row.Bar.Part = drive.Full;
            row.Bar.Colour = Of(drive.Full >= 0.95 ? "BadBrush" : drive.Full >= 0.85 ? "WarnBrush" : "AccentBrush");
        }

        if (_disk.Drives.Count != 0 || _rows.Children.Count != 0) return;
        _rows.Children.Add(new TextBlock { Text = "No drives to show", Style = Caption });
    }
}
