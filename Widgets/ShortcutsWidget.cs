using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PitzkiWidgets.Core;
using Fx = Pitzki.Core.Fx;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Up to six programs, folders or files, each with its own icon, one click from being opened. The plus
/// opens a file picker, and anything dropped on the card is added too. A right click on one takes it off.
/// Only the paths are kept, in this card's own list; nothing is copied or changed.
/// </summary>
public sealed class ShortcutsWidget : UserControl
{
    const int Most = 6;

    readonly Placed _placed;
    readonly UniformGrid _tiles = new() { Columns = 3 };

    public ShortcutsWidget(Placed placed)
    {
        _placed = placed;

        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Shortcuts"));
        box.Children.Add(_tiles);
        Content = box;

        // files dragged from Explorer onto the card are added to it
        AllowDrop = true;
        Drop += (_, e) =>
        {
            if (e.Data.GetData(DataFormats.FileDrop) is string[] dropped)
                foreach (string path in dropped) Add(path);
        };

        Draw();
    }

    /// <summary>How many shortcuts are on the card, which is what the test looks at.</summary>
    internal int Count => _placed.Items.Count;
    int _arrive = -1;

    internal void Add(string path)
    {
        if (path.Length == 0 || _placed.Items.Count >= Most) return;
        if (_placed.Items.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase))) return;
        _placed.Items.Add(path);
        App.Board.Save();
        _arrive = _placed.Items.Count - 1;
        Draw();
    }

    void Take(string path)
    {
        _placed.Items.RemoveAll(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase));
        App.Board.Save();
        Draw();
    }

    void Choose()
    {
        var picker = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Pick a program or file for the card",
            Filter = "Programs and shortcuts|*.exe;*.lnk;*.url;*.bat|All files|*.*",
            CheckFileExists = true,
        };
        if (picker.ShowDialog() == true) Add(picker.FileName);
    }

    internal static void Launch(string path)
    {
        try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
        catch { }   // a program that has been uninstalled is not worth a crash
    }

    // ----- drawing -----

    void Draw()
    {
        _tiles.Children.Clear();
        for (int i = 0; i < _placed.Items.Count; i++)
        {
            if (i != _arrive) { _tiles.Children.Add(Tile(_placed.Items[i])); continue; }
            // the one just added grows in; it is wrapped so the tile keeps its own press bounce
            var holder = new Border { Child = Tile(_placed.Items[i]) };
            _tiles.Children.Add(holder);
            Live.Arrive(holder, this);
            if (Live.Showing(this)) Pitzki.Core.Fx.Pop(holder, from: 0.6);
        }
        _arrive = -1;
        if (_placed.Items.Count < Most) _tiles.Children.Add(Plus());
        _tiles.Rows = (int)Math.Ceiling(_tiles.Children.Count / 3.0);
    }

    UIElement Tile(string path)
    {
        bool there = File.Exists(path) || Directory.Exists(path);

        var picture = new Image { Width = 28, Height = 28, Source = Icon(path), HorizontalAlignment = HorizontalAlignment.Center };
        var name = new TextBlock
        {
            Text = Path.GetFileNameWithoutExtension(path.TrimEnd('\\', '/')) is { Length: > 0 } n ? n : path,
            FontSize = 10, Foreground = Of("SubtleBrush"), TextTrimming = TextTrimming.CharacterEllipsis,
            TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 3, 0, 0),
        };
        var stack = new StackPanel { Margin = new Thickness(2, 6, 2, 5), Opacity = there ? 1 : 0.4 };
        stack.Children.Add(picture);
        stack.Children.Add(name);

        var wash = new Border { CornerRadius = new CornerRadius(10), Background = Of("ChipBrush"), Opacity = 0 };
        var tile = new Grid
        {
            Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(1),
            ToolTip = there ? path : path + "\n(not found)",
            RenderTransformOrigin = new Point(0.5, 0.5), RenderTransform = new ScaleTransform(1, 1),
        };
        tile.Children.Add(wash);
        tile.Children.Add(stack);

        var scale = (ScaleTransform)tile.RenderTransform;
        tile.MouseEnter += (_, _) => Fx.Animate(wash, OpacityProperty, 1, 120);
        tile.MouseLeave += (_, _) =>
        {
            Fx.Animate(wash, OpacityProperty, 0, 200);
            Fx.Animate(scale, ScaleTransform.ScaleXProperty, 1, 120);
            Fx.Animate(scale, ScaleTransform.ScaleYProperty, 1, 120);
        };
        tile.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Fx.Animate(scale, ScaleTransform.ScaleXProperty, 0.9, 80);
            Fx.Animate(scale, ScaleTransform.ScaleYProperty, 0.9, 80);
        };
        tile.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Fx.Animate(scale, ScaleTransform.ScaleXProperty, 1, 280, Fx.Spring);
            Fx.Animate(scale, ScaleTransform.ScaleYProperty, 1, 280, Fx.Spring);
            Launch(path);
        };

        var menu = new ContextMenu();
        var open = new MenuItem { Header = "Open" };
        open.Click += (_, _) => Launch(path);
        var off = new MenuItem { Header = "Take it off this card" };
        off.Click += (_, _) => Take(path);
        menu.Items.Add(open);
        menu.Items.Add(off);
        tile.ContextMenu = menu;
        return tile;
    }

    /// <summary>The empty square at the end that adds one more.</summary>
    UIElement Plus()
    {
        var sign = new TextBlock
        {
            Text = "+", FontSize = 20, Foreground = Of("SubtleBrush"),
            HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
        };
        var ring = new Border
        {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1.2),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)), Child = sign,
            HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 7, 0, 0),
        };
        var tile = new Grid
        {
            Background = Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(1),
            ToolTip = "Add a program or file. You can also drop one here.",
        };
        var inner = new StackPanel { Margin = new Thickness(2, 0, 2, 5) };
        inner.Children.Add(ring);
        inner.Children.Add(new TextBlock { Text = "Add", FontSize = 10, Foreground = Of("SubtleBrush"), TextAlignment = TextAlignment.Center, Margin = new Thickness(0, 4, 0, 0) });
        tile.Children.Add(inner);
        tile.MouseEnter += (_, _) => sign.Foreground = Of("TextBrush");
        tile.MouseLeave += (_, _) => sign.Foreground = Of("SubtleBrush");
        tile.MouseLeftButtonDown += (_, e) => e.Handled = true;
        tile.MouseLeftButtonUp += (_, e) => { e.Handled = true; Choose(); };
        return tile;
    }

    // ----- icons -----

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential, CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    struct ShFileInfo
    {
        public IntPtr Icon;
        public int Index;
        public uint Attributes;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 260)] public string Display;
        [System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.ByValTStr, SizeConst = 80)] public string Type;
    }

    [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string path, uint attributes, ref ShFileInfo info, uint size, uint flags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr icon);

    const uint GetIcon = 0x100, UseGivenAttributes = 0x10, NormalFile = 0x80, Folder = 0x10;

    /// <summary>
    /// The icon Windows shows for that file in Explorer: a program's own, a folder's folder, a shortcut's
    /// target. A path that is not there any more gets the plain icon for its kind rather than none.
    /// </summary>
    internal static ImageSource? Icon(string path)
    {
        bool real = File.Exists(path) || Directory.Exists(path);
        bool folder = Directory.Exists(path) || path.EndsWith(Path.DirectorySeparatorChar) || path.EndsWith('/');

        var info = new ShFileInfo();
        uint flags = GetIcon | (real ? 0u : UseGivenAttributes);
        IntPtr found = SHGetFileInfo(path, folder ? Folder : NormalFile, ref info, (uint)System.Runtime.InteropServices.Marshal.SizeOf<ShFileInfo>(), flags);
        if (found == IntPtr.Zero || info.Icon == IntPtr.Zero) return null;

        try
        {
            var picture = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            picture.Freeze();
            return picture;
        }
        catch { return null; }
        finally { DestroyIcon(info.Icon); }
    }
}
