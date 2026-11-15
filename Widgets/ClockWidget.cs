using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>The time, the date under it, and a word for the part of the day.</summary>
public sealed class ClockWidget : UserControl
{
    readonly TextBlock _time = new() { Style = Big, FontSize = 38 };
    readonly TextBlock _date = new() { FontSize = 12, Foreground = Of("SubtleBrush"), Margin = new Thickness(0, 2, 0, 0) };
    readonly TextBlock _part = new() { Style = Caption, Foreground = Of("AccentBrush") };

    public ClockWidget()
    {
        var box = new StackPanel { Width = 164 };
        box.Children.Add(_part);
        box.Children.Add(_time);
        box.Children.Add(_date);
        Content = box;
        this.Every(1, Tick);
    }

    void Tick()
    {
        var now = DateTime.Now;
        _time.Say(now.ToString("HH:mm", CultureInfo.InvariantCulture));
        _date.Say(now.ToString("dddd, d MMMM", CultureInfo.GetCultureInfo("en-GB")));
        _part.Say(now.Hour switch
        {
            < 5 => "NIGHT",
            < 12 => "MORNING",
            < 17 => "AFTERNOON",
            < 21 => "EVENING",
            _ => "NIGHT",
        });
    }
}
