using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// The games card. While ROB RUN is open it shows what is happening in it, read from the game's own
/// log. The rest of the time it shows how the last game went, whichever game it was (Roblox, CS2,
/// Fortnite, races, anything Pitzki followed), as Pitzki wrote it down.
/// </summary>
public sealed class RobRunWidget : UserControl
{
    readonly RobRunLog _log = new();
    readonly LastGame _last = new();
    readonly TextBlock _title = Title("ROB RUN");
    readonly Image _icon = new() { Width = 22, Height = 22, Stretch = Stretch.UniformToFill, Margin = new Thickness(0, 0, 8, 0) };
    readonly Border _iconFrame;
    readonly TextBlock _where = new() { FontSize = 17, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, MaxHeight = 44, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _what = new() { FontSize = 12, Foreground = Of("SubtleBrush"), Margin = new Thickness(0, 1, 0, 0), TextWrapping = TextWrapping.Wrap };
    readonly Row _time = new(false), _ping = new(false), _server = new(false), _when = new(false);
    readonly TextBlock _more = new() { FontSize = 11, Foreground = Of("SubtleBrush"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap, LineHeight = 15 };

    double _wasPing;
    string _iconPath = "";

    public RobRunWidget()
    {
        var box = new StackPanel { Width = 184 };

        _iconFrame = new Border { Child = _icon, CornerRadius = new CornerRadius(5), Visibility = Visibility.Collapsed, VerticalAlignment = VerticalAlignment.Center };
        _icon.Clip = new RectangleGeometry(new Rect(0, 0, 22, 22), 5, 5);
        _title.Margin = new Thickness(0);
        _title.VerticalAlignment = VerticalAlignment.Center;
        _title.TextTrimming = TextTrimming.CharacterEllipsis;
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        head.Children.Add(_iconFrame);
        head.Children.Add(_title);
        box.Children.Add(head);

        box.Children.Add(_where);
        box.Children.Add(_what);
        _time.Label.Text = "IN THERE";
        _ping.Label.Text = "PING";
        _server.Label.Text = "SERVER";
        _when.Label.Text = "WHEN";
        box.Children.Add(_time);
        box.Children.Add(_ping);
        box.Children.Add(_server);
        box.Children.Add(_when);
        box.Children.Add(_more);
        Content = box;
        this.Every(2, Tick);
    }

    void Tick()
    {
        _log.Poll();
        var live = _log.Live;

        if (live != null)
        {
            Playing(live);
            return;
        }

        _last.Poll();
        foreach (var row in new UIElement[] { _time, _ping, _server }) Live.Show(row, false, this);

        if (_last.Now is not { } game)
        {
            Head("Games", "");
            _where.Size(17);
            _where.Say("Not playing");
            _what.Say("After you play something, how it went shows here.");
            Live.Show(_when, false, this);
            Live.Show(_more, false, this);
            return;
        }

        Head(game.Game + " · " + game.Kind, game.Icon);
        _where.Size(game.Head.Length > 18 ? 15 : 17); // a long name gets two smaller lines
        _where.Say(game.Head);
        _what.Say(game.Detail);
        Live.Show(_what, game.Detail.Length > 0, this);

        _when.Value.Say(WhenText(game.When));
        Live.Show(_when, game.When > DateTime.MinValue, this);

        // the first line repeats the game, the head and the length: the rest is the news
        string more = string.Join("\n", game.More.Skip(1).Take(3));
        _more.Say(more);
        Live.Show(_more, more.Length > 0, this);
    }

    void Playing(RrLive live)
    {
        Head("ROB RUN", "");
        _where.Size(17);
        _where.Say(live.PlaceWord);
        _what.Say(live.Down ? "Down" : live.StateWord);
        Live.Show(_what, true, this);
        Live.Show(_when, false, this);
        Live.Show(_more, false, this);

        string clock = RrLive.Clock(live.RobSince ?? live.Since, DateTime.Now);
        _time.Value.Say(clock);
        Live.Show(_time, clock.Length > 0, this);

        _ping.Value.Count(live.Ping > 0 ? live.Ping + " ms" : "", live.Ping, _wasPing);
        _wasPing = live.Ping;
        _ping.Value.Tint(Of(live.Ping >= 120 ? "BadBrush" : live.Ping >= 70 ? "WarnBrush" : "GoodBrush"));
        Live.Show(_ping, live.Ping > 0, this);

        _server.Value.Say(live.Server);
        Live.Show(_server, live.Server.Length > 0 && live.Place != "menu", this);
    }

    /// <summary>The heading, and the game's own little picture beside it when Pitzki kept one.</summary>
    void Head(string title, string icon)
    {
        _title.Say(title.ToUpperInvariant());
        if (icon == _iconPath) return;
        _iconPath = icon;
        ImageSource? picture = null;
        if (icon.Length > 0 && File.Exists(icon))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // let go of the file at once
                bitmap.DecodePixelWidth = 44;
                bitmap.UriSource = new Uri(icon);
                bitmap.EndInit();
                bitmap.Freeze();
                picture = bitmap;
            }
            catch
            {
                // a broken picture is no reason to lose the card
            }
        }
        Live.Picture(_icon, picture);
        Live.Show(_iconFrame, picture != null, this);
    }

    /// <summary>"today 05:09", "yesterday 22:40", "3 days ago".</summary>
    static string WhenText(DateTime when)
    {
        if (when.Date == DateTime.Today) return "today " + when.ToString("HH:mm");
        if (when.Date == DateTime.Today.AddDays(-1)) return "yesterday " + when.ToString("HH:mm");
        return Since(when);
    }
}
