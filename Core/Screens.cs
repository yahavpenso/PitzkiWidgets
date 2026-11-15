using System.Windows;
using System.Windows.Media;

namespace PitzkiWidgets.Core;

/// <summary>
/// More than one screen. WPF's own <c>SystemParameters.WorkArea</c> only ever describes the main
/// screen, so a card on the second one would line up against edges that are not there and hide
/// itself off the wrong side. Here Windows is asked which screen a card is actually on, and its
/// work area (the screen without the taskbar) is handed back in the units WPF places windows in.
/// </summary>
public static class Screens
{
    /// <summary>The tests put a room of their own here, so nothing has to happen on a real screen.</summary>
    internal static Rect? Pretend;

    /// <summary>Everything, every screen together, for keeping a card somewhere it can be seen.</summary>
    public static Rect All =>
        Pretend ?? (SystemParameters.VirtualScreenWidth > 0
            ? new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop,
                SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight)
            : SystemParameters.WorkArea);

    /// <summary>The work area of the screen this window is mostly on.</summary>
    public static Rect RoomFor(Window window)
    {
        if (Pretend is { } made) return made;
        var where = new Rect(window.Left, window.Top, Math.Max(window.ActualWidth, 1), Math.Max(window.ActualHeight, 1));
        if (double.IsNaN(where.X) || double.IsNaN(where.Y)) return SystemParameters.WorkArea;
        return Room(where, window);
    }

    /// <summary>The work area of the screen that holds this rectangle, in the same units it is given in.</summary>
    public static Rect Room(Rect where, Visual? forScale = null)
    {
        if (Pretend is { } made) return made;
        try
        {
            var (toDevice, fromDevice) = Scale(forScale);
            var device = new System.Drawing.Rectangle(
                (int)Math.Round(where.X * toDevice.X), (int)Math.Round(where.Y * toDevice.Y),
                (int)Math.Round(where.Width * toDevice.X), (int)Math.Round(where.Height * toDevice.Y));
            var screen = System.Windows.Forms.Screen.FromRectangle(device).WorkingArea;
            return new Rect(screen.X * fromDevice.X, screen.Y * fromDevice.Y, screen.Width * fromDevice.X, screen.Height * fromDevice.Y);
        }
        catch { return SystemParameters.WorkArea; }      // a screen unplugged mid-question is not worth a crash
    }

    /// <summary>The work area of the screen the mouse is on, where a new card is put.</summary>
    public static Rect RoomAtMouse()
    {
        if (Pretend is { } made) return made;
        try
        {
            var at = System.Windows.Forms.Control.MousePosition;
            var screen = System.Windows.Forms.Screen.FromPoint(at).WorkingArea;
            var (_, fromDevice) = Scale(null);
            return new Rect(screen.X * fromDevice.X, screen.Y * fromDevice.Y, screen.Width * fromDevice.X, screen.Height * fromDevice.Y);
        }
        catch { return SystemParameters.WorkArea; }
    }

    /// <summary>How many device pixels one of WPF's own units is here, and the other way round.</summary>
    static (Point To, Point From) Scale(Visual? visual)
    {
        if (visual != null && PresentationSource.FromVisual(visual)?.CompositionTarget is { } target)
        {
            var to = target.TransformToDevice;
            var from = target.TransformFromDevice;
            if (to.M11 > 0 && to.M22 > 0) return (new Point(to.M11, to.M22), new Point(from.M11, from.M22));
        }
        return (new Point(1, 1), new Point(1, 1));
    }
}
