using System.Windows;

namespace PitzkiWidgets.Core;

/// <summary>
/// Where a card ends up when it is let go near something it could line up with: the edge of another
/// card (flush with it, or a small gap beside it) or the edge of the screen. It is only arithmetic on
/// rectangles, so the test can check it without a mouse.
/// </summary>
public static class Snap
{
    /// <summary>
    /// <paramref name="moving"/> is the card as it is now, <paramref name="others"/> are the cards
    /// already on the desktop and <paramref name="screen"/> is the room there is. A line within
    /// <paramref name="reach"/> of an edge is pulled onto it, and the nearest one wins.
    /// </summary>
    public static Point Fit(Rect moving, IReadOnlyList<Rect> others, Rect screen, double reach = 10, double gap = 10)
    {
        double x = moving.X, y = moving.Y;

        // the screen's own edges, with a little air between a card and them
        var xs = new List<double> { screen.Left + gap, screen.Right - gap - moving.Width };
        var ys = new List<double> { screen.Top + gap, screen.Bottom - gap - moving.Height };

        foreach (var other in others)
        {
            // lining up with another card's edges is always welcome: it is what makes a tidy column or row
            xs.Add(other.Left);
            xs.Add(other.Right - moving.Width);
            ys.Add(other.Top);
            ys.Add(other.Bottom - moving.Height);

            // standing beside or under it, with a gap, only makes sense when the two actually face each other
            bool facingSideways = moving.Bottom > other.Top - reach * 3 && moving.Top < other.Bottom + reach * 3;
            if (facingSideways)
            {
                xs.Add(other.Right + gap);
                xs.Add(other.Left - gap - moving.Width);
            }
            bool facingUpDown = moving.Right > other.Left - reach * 3 && moving.Left < other.Right + reach * 3;
            if (facingUpDown)
            {
                ys.Add(other.Bottom + gap);
                ys.Add(other.Top - gap - moving.Height);
            }
        }

        return new Point(Nearest(x, xs, reach), Nearest(y, ys, reach));
    }

    /// <summary>
    /// The card <paramref name="moving"/> was let go of right against one of <paramref name="others"/>:
    /// which one, and whether it came to rest beside it rather than under it. Null when it is on its
    /// own. "Right against" means the two edges facing each other are no further apart than the gap
    /// they snap to, and that they face each other over most of their length.
    /// </summary>
    public static (int Which, bool Across)? Touching(Rect moving, IReadOnlyList<Rect> others, double gap = 10, double slack = 5, double share = 0.55)
    {
        double reach = gap + slack;
        int best = -1;
        bool across = false;
        double nearest = double.MaxValue;

        for (int i = 0; i < others.Count; i++)
        {
            var other = others[i];

            double sideways = Math.Min(Math.Abs(moving.Left - other.Right), Math.Abs(other.Left - moving.Right));
            double along = Overlap(moving.Top, moving.Bottom, other.Top, other.Bottom);
            if (sideways <= reach && along >= share * Math.Min(moving.Height, other.Height) && sideways < nearest)
            {
                nearest = sideways;
                best = i;
                across = true;
            }

            double upDown = Math.Min(Math.Abs(moving.Top - other.Bottom), Math.Abs(other.Top - moving.Bottom));
            double wide = Overlap(moving.Left, moving.Right, other.Left, other.Right);
            if (upDown <= reach && wide >= share * Math.Min(moving.Width, other.Width) && upDown < nearest)
            {
                nearest = upDown;
                best = i;
                across = false;
            }
        }
        return best < 0 ? null : (best, across);
    }

    /// <summary>
    /// The card <paramref name="moving"/> was put down on top of one of <paramref name="others"/>:
    /// its middle is inside it, or the two cover enough of each other. Which one (the most covered),
    /// and whether the moving card leans more to the side of it than above or below it, so it is
    /// joined beside rather than under. Null when it is not on top of anything.
    /// </summary>
    public static (int Which, bool Across)? Over(Rect moving, IReadOnlyList<Rect> others, double share = 0.3)
    {
        int best = -1;
        double most = 0;
        var middle = new Point(moving.Left + moving.Width / 2, moving.Top + moving.Height / 2);
        for (int i = 0; i < others.Count; i++)
        {
            var other = others[i];
            double covered = Overlap(moving.Left, moving.Right, other.Left, other.Right) * Overlap(moving.Top, moving.Bottom, other.Top, other.Bottom);
            double smaller = Math.Max(1, Math.Min(moving.Width * moving.Height, other.Width * other.Height));
            if ((other.Contains(middle) || covered >= share * smaller) && covered > most)
            {
                most = covered;
                best = i;
            }
        }
        if (best < 0) return null;
        var on = others[best];
        double dx = (middle.X - (on.Left + on.Width / 2)) / Math.Max(1, on.Width);
        double dy = (middle.Y - (on.Top + on.Height / 2)) / Math.Max(1, on.Height);
        return (best, Math.Abs(dx) > Math.Abs(dy));
    }

    /// <summary>What a card let go here would join: only one it was put on top of. Standing right beside a card is not enough.</summary>
    public static (int Which, bool Across)? Target(Rect moving, IReadOnlyList<Rect> others) =>
        Over(moving, others);

    static double Overlap(double from, double to, double otherFrom, double otherTo) =>
        Math.Max(0, Math.Min(to, otherTo) - Math.Max(from, otherFrom));

    static double Nearest(double now, List<double> lines, double reach)
    {
        double best = now, away = reach + 0.001;
        foreach (double line in lines)
        {
            double d = Math.Abs(line - now);
            if (d < away) { away = d; best = line; }
        }
        return best;
    }
}
