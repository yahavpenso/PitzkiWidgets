using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// A picture frame: the photos in one folder (Pictures, unless another is chosen), a new one every
/// ten seconds, sliding across softly. A click opens the photo; the arrows step through by hand.
/// </summary>
public sealed class PhotosWidget : UserControl
{
    const double Wide = 210, High = 140;
    static readonly string[] Kinds = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".jfif" };

    readonly Placed _placed;
    readonly Image _photo = new() { Stretch = Stretch.UniformToFill, Width = Wide, Height = High };
    readonly TextBlock _empty = new() { Style = Caption, TextWrapping = TextWrapping.Wrap, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16) };
    readonly TextBlock _where = new() { Style = Caption, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
    List<string> _files = new();
    int _at = -1;
    DateTime _scanned = DateTime.MinValue;
    readonly Random _shuffle = new();

    /// <summary>For the tests: how many photos the folder has, and whether one is on the card.</summary>
    internal int Found => _files.Count;
    internal bool Pictured => _photo.Source != null;

    string Folder => _placed.Text.Length > 0 && Directory.Exists(_placed.Text)
        ? _placed.Text
        : Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);

    public PhotosWidget(Placed placed)
    {
        _placed = placed;

        var frame = new Grid { Width = Wide, Height = High, Cursor = Cursors.Hand, ToolTip = "Click to open this photo" };
        frame.Clip = new RectangleGeometry(new Rect(0, 0, Wide, High), 10, 10);
        frame.Children.Add(new Border { Background = Of("ChipBrush") });
        frame.Children.Add(_empty);
        frame.Children.Add(_photo);
        frame.MouseLeftButtonDown += (_, e) => e.Handled = true;      // the photo is for clicking; the card is dragged by its edge
        frame.MouseLeftButtonUp += (_, e) =>
        {
            if (_at < 0 || _at >= _files.Count) return;
            e.Handled = true;
            try { Process.Start(new ProcessStartInfo(_files[_at]) { UseShellExecute = true }); } catch { }
        };

        var bottom = new Grid { Margin = new Thickness(0, 6, 0, 0) };
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bottom.Children.Add(_where);
        var keys = new StackPanel { Orientation = Orientation.Horizontal };
        keys.Children.Add(Key("", () => Step(-1), icon: true, size: 11, tip: "Previous photo"));
        keys.Children.Add(Key("", () => Step(1), icon: true, size: 11, tip: "Next photo"));
        keys.Children.Add(Key("", Choose, icon: true, size: 12, tip: "Choose the folder"));
        Grid.SetColumn(keys, 1);
        bottom.Children.Add(keys);

        var box = new StackPanel { Width = Wide };
        box.Children.Add(frame);
        box.Children.Add(bottom);
        Content = box;

        this.Every(10, () => Step(1));
    }

    void Choose()
    {
        var pick = new Microsoft.Win32.OpenFolderDialog { Title = "Which folder of photos?", InitialDirectory = Folder };
        if (pick.ShowDialog() != true) return;
        _placed.Text = pick.FolderName;
        App.Board.Save();
        _scanned = DateTime.MinValue;
        _at = -1;
        Step(1);
    }

    /// <summary>Looks at the folder again every few minutes, so new photos join in.</summary>
    void Scan()
    {
        if (DateTime.Now - _scanned < TimeSpan.FromMinutes(5)) return;
        _scanned = DateTime.Now;
        try
        {
            _files = Directory.EnumerateFiles(Folder, "*", SearchOption.AllDirectories)
                .Where(f => Kinds.Contains(Path.GetExtension(f).ToLowerInvariant()))
                .Take(2000).OrderBy(_ => _shuffle.Next()).ToList();
        }
        catch { _files = new(); }
        _where.Say(Path.GetFileName(Folder.TrimEnd('\\')) + (_files.Count > 0 ? $" · {_files.Count}" : ""));
        _empty.Text = _files.Count == 0 ? "No photos in this folder yet.\nPick another with the folder button." : "";
    }

    void Step(int by)
    {
        Scan();
        if (_files.Count == 0) { Live.Picture(_photo, null); return; }
        // a file that cannot be read is skipped, but only a few times so a folder of broken files cannot hang it
        for (int tries = 0; tries < 5; tries++)
        {
            _at = ((_at + by) % _files.Count + _files.Count) % _files.Count;
            if (Load(_files[_at]) is { } picture)
            {
                Live.Picture(_photo, picture, dx: by >= 0 ? 14 : -14);
                _photo.ToolTip = Path.GetFileName(_files[_at]);
                return;
            }
            if (by == 0) by = 1;
        }
    }

    static ImageSource? Load(string file)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;       // the file is not kept open
            bitmap.UriSource = new Uri(file);
            bitmap.DecodePixelWidth = (int)(Wide * 2);           // sharp, without holding a 20-megapixel photo in memory
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch { return null; }
    }
}
