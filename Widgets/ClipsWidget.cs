using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pitzki.Core;
using Windows.Storage;
using Windows.Storage.FileProperties;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Your newest recording or screenshot, as a picture you can click to open. It looks in the places a
/// PC keeps them: Videos, Pictures (and its Screenshots folder) and Steam's own screenshot folders.
/// The arrows browse the last few, and a line underneath counts today's. Only file names and the
/// pictures Windows already makes for its own folder views are read; nothing is moved, copied or sent.
/// </summary>
public sealed class ClipsWidget : UserControl
{
    static readonly string[] Films = { ".mp4", ".mkv", ".mov", ".webm", ".avi" };
    static readonly string[] Stills = { ".png", ".jpg", ".jpeg", ".bmp", ".webp" };

    /// <summary>How many of the newest are kept to browse through.</summary>
    const int Keep = 8;

    public sealed record Clip(string Path, DateTime When, bool Film, string Place);

    readonly Image _picture = new() { Stretch = Stretch.UniformToFill };
    readonly Border _frame;
    readonly TextBlock _caption = new() { FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis };
    readonly TextBlock _count = new() { FontSize = 10.5, Foreground = Of("SubtleBrush"), Margin = new Thickness(0, 6, 0, 0), TextWrapping = TextWrapping.Wrap };
    readonly TextBlock _place = new() { FontSize = 11.5, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap, Visibility = Visibility.Collapsed };
    readonly TextBlock _which = new() { FontSize = 10.5, Foreground = Of("SubtleBrush"), VerticalAlignment = VerticalAlignment.Center };
    readonly Border _play;
    readonly StackPanel _shown = new();
    readonly Dictionary<string, ImageSource> _thumbs = new(StringComparer.OrdinalIgnoreCase);

    List<Clip> _latest = new();
    int _at, _drawnAt;
    bool _scanning;

    public ClipsWidget()
    {
        _play = new Border
        {
            Width = 30, Height = 30, CornerRadius = new CornerRadius(15), Background = new SolidColorBrush(Color.FromArgb(0xA0, 0, 0, 0)),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visibility = Visibility.Collapsed,
            Child = new TextBlock { Text = "▶", FontSize = 12, Foreground = Brushes.White, Margin = new Thickness(2, 0, 0, 0), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center },
        };

        // a dark strip along the bottom of the picture so the words stay readable over any image
        var strip = new Border
        {
            VerticalAlignment = VerticalAlignment.Bottom, Padding = new Thickness(8, 14, 8, 5), Child = _caption,
            Background = new LinearGradientBrush(Color.FromArgb(0x00, 0, 0, 0), Color.FromArgb(0xB8, 0, 0, 0), 90),
        };

        _frame = new Border
        {
            Width = 166, Height = 96, CornerRadius = new CornerRadius(10), Background = Of("ChipBrush"), ClipToBounds = true,
            Cursor = Cursors.Hand, Child = new Grid { Children = { _picture, _play, strip } },
        };
        _frame.MouseLeftButtonDown += (_, e) => e.Handled = true;
        _frame.MouseLeftButtonUp += (_, e) => { e.Handled = true; Open(); };

        var browse = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        browse.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        browse.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        browse.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var back = Key("‹", () => Go(-1), size: 16, tip: "Older");
        var next = Key("›", () => Go(+1), size: 16, tip: "Newer");
        _which.HorizontalAlignment = HorizontalAlignment.Center;
        browse.Children.Add(back);
        Grid.SetColumn(_which, 1);
        browse.Children.Add(_which);
        Grid.SetColumn(next, 2);
        browse.Children.Add(next);

        _shown.Children.Add(_frame);
        _shown.Children.Add(browse);

        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Clips"));
        box.Children.Add(_place);
        box.Children.Add(_shown);
        box.Children.Add(_count);
        Content = box;

        _place.Text = "Looking for recordings...";
        _place.Visibility = Visibility.Visible;
        _shown.Visibility = Visibility.Collapsed;
        this.Every(60, Look);
    }

    // ----- finding them -----

    async void Look()
    {
        if (_scanning) return;
        _scanning = true;
        try
        {
            var found = await Task.Run(Scan);
            string was = _latest.Count > 0 ? _latest[0].Path : "";
            _latest = found;
            if (was != "" && found.Count > 0 && found[0].Path != was) Core.Buzz.Tell("clip", this);
            if (found.Count == 0 || found[0].Path != was) _at = 0;       // something new came in: show it
            _at = Math.Min(_at, Math.Max(found.Count - 1, 0));
            await Draw();
        }
        catch (Exception bad) { Debug.WriteLine(bad); }
        finally { _scanning = false; }
    }

    /// <summary>The folders a PC keeps these in, each only once.</summary>
    static IEnumerable<(string Folder, string Place)> Places()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        foreach (string film in new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), Path.Combine(home, "Videos") })
            if (film.Length > 0 && seen.Add(film)) yield return (film, "Videos");

        foreach (string still in new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), Path.Combine(home, "Pictures") })
            if (still.Length > 0 && seen.Add(still)) yield return (still, "Pictures");

        // Steam keeps each game's screenshots in userdata\<account>\760\remote\<game>\screenshots
        var steam = new SteamLocal();
        if (steam.Installed)
        {
            string users = Path.Combine(steam.Folder, "userdata");
            if (Directory.Exists(users))
                foreach (string account in Directory.EnumerateDirectories(users))
                {
                    string remote = Path.Combine(account, "760", "remote");
                    if (!Directory.Exists(remote)) continue;
                    foreach (string game in Directory.EnumerateDirectories(remote))
                    {
                        string shots = Path.Combine(game, "screenshots");
                        if (Directory.Exists(shots) && seen.Add(shots)) yield return (shots, "Steam");
                    }
                }
        }
    }

    internal static List<Clip> Scan()
    {
        var all = new List<Clip>();
        int looked = 0;

        foreach (var (folder, place) in Places())
            Walk(folder, place, depth: place == "Steam" ? 0 : 2, all, ref looked);

        return all.OrderByDescending(c => c.When).Take(Keep).ToList();
    }

    /// <summary>The files in a folder and the folders one or two levels below it. It gives up after a few thousand, so a huge folder cannot stall it.</summary>
    static void Walk(string folder, string place, int depth, List<Clip> into, ref int looked)
    {
        try
        {
            foreach (var path in Directory.EnumerateFiles(folder))
            {
                if (++looked > 4000) return;
                string ext = Path.GetExtension(path).ToLowerInvariant();
                bool film = Array.IndexOf(Films, ext) >= 0;
                if (!film && Array.IndexOf(Stills, ext) < 0) continue;
                into.Add(new Clip(path, File.GetLastWriteTime(path), film, place));
            }
            if (depth == 0) return;
            foreach (var below in Directory.EnumerateDirectories(folder))
            {
                string name = Path.GetFileName(below);
                if (name.StartsWith('.') || name.Equals("thumbnails", StringComparison.OrdinalIgnoreCase)) continue;
                Walk(below, place, depth - 1, into, ref looked);
            }
        }
        catch { }   // a folder that cannot be read is simply skipped
    }

    // ----- showing one -----

    void Go(int by)
    {
        if (_latest.Count == 0) return;
        _at = Math.Clamp(_at - by, 0, _latest.Count - 1);       // "newer" is to the right, so it goes back along the list
        _ = Draw();
    }

    void Open()
    {
        if (_at >= _latest.Count) return;
        try { Process.Start(new ProcessStartInfo(_latest[_at].Path) { UseShellExecute = true }); } catch { }
    }

    internal int Found => _latest.Count;
    internal bool Pictured => _picture.Source != null;

    async Task Draw()
    {
        int way = Math.Sign(_drawnAt - _at);         // which way the browsing went, for the picture to slide
        _drawnAt = _at;
        if (_latest.Count == 0)
        {
            Live.Show(_shown, false, this);
            Live.Show(_place, true, this);
            _place.Say("No recordings or screenshots found yet.");
            _count.Say("Videos, Pictures and Steam screenshots are checked.");
            return;
        }

        var clip = _latest[_at];
        Live.Show(_place, false, this);
        Live.Show(_shown, true, this);
        _caption.Say($"{(clip.Film ? "Clip" : "Screenshot")}  ·  {Since(clip.When)}");
        Live.Show(_play, clip.Film, this);
        _which.Say($"{_at + 1} of {_latest.Count}", quick: true, down: way < 0);
        _frame.ToolTip = $"{Path.GetFileName(clip.Path)}  ({clip.Place})";

        int films = _latest.Count(c => c.Film && c.When.Date == DateTime.Today);
        int stills = _latest.Count(c => !c.Film && c.When.Date == DateTime.Today);
        _count.Say(films + stills == 0 ? "Nothing new today"
            : $"Today: {Many(films, "clip")}{(films > 0 && stills > 0 ? ", " : "")}{(stills > 0 ? Many(stills, "screenshot") : "")}");

        var thumb = await Thumb(clip);
        if (_latest.Count > _at && _latest[_at] == clip) Live.Picture(_picture, thumb, dx: 14 * (way == 0 ? 1 : way));
    }

    static string Many(int n, string what) => n == 0 ? "" : n == 1 ? $"1 {what}" : $"{n} {what}s";

    /// <summary>The picture Windows itself shows for the file, for a film as well as a screenshot.</summary>
    async Task<ImageSource?> Thumb(Clip clip)
    {
        if (_thumbs.TryGetValue(clip.Path, out var have)) return have;
        try
        {
            var file = await StorageFile.GetFileFromPathAsync(clip.Path);
            using var thumb = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 320);
            if (thumb == null || thumb.Size == 0) return null;

            using var stream = thumb.AsStreamForRead();
            var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            memory.Position = 0;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = memory;
            bitmap.EndInit();
            bitmap.Freeze();
            _thumbs[clip.Path] = bitmap;
            return bitmap;
        }
        catch { return null; }   // a file that vanished, or has no preview: the grey card stays
    }
}
