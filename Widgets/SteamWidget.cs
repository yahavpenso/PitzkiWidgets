using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Steam as this PC knows it: what is downloading and how far along, how many games are running,
/// and when nothing is happening, the one you played last. No account and no website involved.
/// </summary>
public sealed class SteamWidget : UserControl
{
    readonly SteamLocal _steam = new();
    readonly TextBlock _headline = new() { FontSize = 14, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _under = new() { FontSize = 11, Foreground = Of("SubtleBrush"), Margin = new Thickness(0, 1, 0, 0), TextWrapping = TextWrapping.Wrap };
    readonly Meter _along = new(166) { Margin = new Thickness(0, 9, 0, 0) };

    DateTime _looked;
    string _lastPlayed = "";

    public SteamWidget()
    {
        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Steam"));
        box.Children.Add(_headline);
        box.Children.Add(_under);
        box.Children.Add(_along);
        Content = box;
        this.Every(5, Tick);
    }

    void Tick()
    {
        if (!_steam.Installed)
        {
            _headline.Say("No Steam here");
            _under.Say("Nothing to show until Steam is installed.");
            Live.Show(_along, false, this);
            return;
        }

        _steam.Poll();

        if (_steam.Download is { } job)
        {
            _headline.Say(job.Name.Length > 0 ? job.Name : "Downloading");
            _under.Say($"{DiskWatch.Size(job.Done)} of {DiskWatch.Size(job.Total)}  ·  {Math.Round(job.Part * 100)}%");
            Live.Show(_along, true, this);
            _along.Glide(job.Part, 4500, steady: true);      // a reading every five seconds: keep it moving between them
            _along.Colour = Of("AccentBrush");
            return;
        }

        Live.Show(_along, false, this);

        if (_steam.Running > 0)
        {
            _headline.Say(_steam.Running == 1 ? "A game is running" : $"{_steam.Running} games running");
            _under.Say("Steam says you are playing.");
            return;
        }

        // the library is a lot of files to read, so it is only looked at every few minutes
        if (_lastPlayed.Length == 0 || DateTime.Now - _looked > TimeSpan.FromMinutes(5))
        {
            _looked = DateTime.Now;
            try
            {
                var last = _steam.Games().Where(g => g.LastPlayed > DateTime.MinValue)
                    .OrderByDescending(g => g.LastPlayed).FirstOrDefault();
                _lastPlayed = last is null ? "" : $"{last.Name}\u0000{Ago(last.LastPlayed)}\u0000{Math.Round(last.Hours)}";
            }
            catch { _lastPlayed = ""; }
        }

        if (_lastPlayed.Length == 0)
        {
            _headline.Say("Steam is quiet");
            _under.Say("Nothing running, nothing downloading.");
            return;
        }

        var bits = _lastPlayed.Split('\u0000');
        _headline.Say(bits[0]);
        _under.Say($"Last played {bits[1]}  ·  {bits[2]} h in all");
    }

    static string Ago(DateTime when)
    {
        var gone = DateTime.Now - when;
        if (gone < TimeSpan.FromMinutes(90)) return "just now";
        if (gone < TimeSpan.FromDays(1)) return $"{(int)gone.TotalHours} h ago";
        if (gone < TimeSpan.FromDays(2)) return "yesterday";
        if (gone < TimeSpan.FromDays(30)) return $"{(int)gone.TotalDays} days ago";
        return when.ToString("d MMM yyyy");
    }
}
