using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PitzkiWidgets.Core;

/// <summary>
/// A speckle of paper grain, tiled over a surface so it is not a flat slab of colour. It is drawn
/// once and shared by every card, and always from the same seed, so it never crawls about between
/// one window and the next.
/// </summary>
public static class Grain
{
    public static readonly ImageBrush Paper = Make();

    static ImageBrush Make()
    {
        const int side = 128;
        var dots = new byte[side * side * 4];
        var rng = new Random(7);
        for (int i = 0; i < side * side; i++)
        {
            byte much = (byte)rng.Next(0, 13);     // premultiplied white: every channel is the alpha
            dots[i * 4] = much;
            dots[i * 4 + 1] = much;
            dots[i * 4 + 2] = much;
            dots[i * 4 + 3] = much;
        }
        var tile = BitmapSource.Create(side, side, 96, 96, PixelFormats.Pbgra32, null, dots, side * 4);
        tile.Freeze();

        var brush = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(0, 0, side, side),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
        };
        brush.Freeze();
        return brush;
    }
}
