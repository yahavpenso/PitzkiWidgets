using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Fx = Pitzki.Core.Fx;

namespace PitzkiWidgets.Core;

/// <summary>
/// How every card shows a reading that changed: never a jump, always the move from the old to the new.
/// Words roll out and the new ones roll in, colours blend across, and rows that are new on a card
/// slide in one after another. The very first reading, and anything on a card that is not on screen
/// yet, is simply put there, so a card never opens with something rolling.
/// </summary>
public static class Live
{
    /// <summary>For the tests: how many changes were shown moving rather than put straight in.</summary>
    public static int Moves { get; private set; }

    /// <summary>For the tests: rolls that have started and not yet landed. Once things settle it is 0.</summary>
    public static int Underway { get; private set; }

    /// <summary>For the tests: told what moved, each time something does.</summary>
    public static Action<string>? Watch;

    static void Moved(string what)
    {
        Moves++;
        Watch?.Invoke(what);
    }

    /// <summary>The value a block is on its way to, while it is rolling.</summary>
    sealed class Roll { public string To = ""; }
    static readonly ConditionalWeakTable<TextBlock, Roll> Rolling = new();

    /// <summary>True when the element is on a card that is on screen, so a change is worth showing.</summary>
    public static bool Showing(FrameworkElement e) => e.IsLoaded && e.IsVisible;

    /// <summary>
    /// Puts new words in a block. When they differ from what is there, the old ones slide up and fade,
    /// and the new ones come up from underneath. <paramref name="quick"/> is for things that change every
    /// second, like a countdown, where a slower roll would never finish before the next one.
    /// <paramref name="down"/> rolls the other way, for a number that went down.
    /// </summary>
    public static void Say(this TextBlock block, string text, bool quick = false, bool down = false)
    {
        text ??= "";
        if (Rolling.TryGetValue(block, out var roll)) { roll.To = text; return; }   // the roll already under way lands on this
        if (block.Text == text) return;

        var move = Mover(block);
        if (move == null || !Showing(block) || block.Text.Length == 0 || text.Length == 0)
        {
            block.Text = text;
            return;
        }

        Moved("roll " + block.Text + " -> " + text);
        Underway++;
        roll = new Roll { To = text };
        Rolling.Add(block, roll);
        int outMs = quick ? 70 : 120, inMs = quick ? 150 : 260;
        double lift = Math.Clamp(block.FontSize * 0.35, 4, 12) * (down ? -1 : 1);

        Fx.Animate(move, TranslateTransform.YProperty, -lift, outMs, Fx.EaseIn);
        Fx.Animate(block, UIElement.OpacityProperty, 0, outMs, Fx.EaseIn, done: () =>
        {
            Rolling.Remove(block);
            Underway--;
            block.Text = roll.To;
            move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(lift, 0, TimeSpan.FromMilliseconds(inMs)) { EasingFunction = Fx.EaseOut });
            var back = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(inMs)) { EasingFunction = Fx.EaseOut };
            back.Completed += (_, _) => block.BeginAnimation(UIElement.OpacityProperty, null);
            block.BeginAnimation(UIElement.OpacityProperty, back);
        });
    }

    /// <summary>A number that is said as words: rolls up when it grew and down when it shrank.</summary>
    public static void Count(this TextBlock block, string text, double now, double before, bool quick = false) =>
        block.Say(text, quick, down: now < before);

    /// <summary>The translate the roll moves, made when the block has none yet. Null when it already has another kind.</summary>
    static TranslateTransform? Mover(UIElement e)
    {
        switch (e.RenderTransform)
        {
            case TranslateTransform t when !t.IsFrozen: return t;
            case TransformGroup g when !g.IsFrozen:
                foreach (var child in g.Children) if (child is TranslateTransform t2 && !t2.IsFrozen) return t2;
                return null;
            case var other when other == null || other == Transform.Identity:
                // the same pair Fx uses, so a later Fx.Appear on it reuses it rather than replacing it
                var group = new TransformGroup();
                group.Children.Add(new ScaleTransform(1, 1));
                var move = new TranslateTransform();
                group.Children.Add(move);
                e.RenderTransform = group;
                e.RenderTransformOrigin = new Point(0.5, 0.5);
                return move;
            default: return null;
        }
    }

    /// <summary>
    /// A brush of the card's own that blends from the colour it has to the one wanted, rather than
    /// switching. Pass what is painted now; it hands back the brush to paint with.
    /// </summary>
    public static Brush Blend(Brush? now, Brush wanted, FrameworkElement owner, int ms = 450)
    {
        if (wanted is not SolidColorBrush { Color: var to }) return wanted;
        if (now is SolidColorBrush { IsFrozen: false } mine)
        {
            if (mine.Color == to && mine.HasAnimatedProperties == false) return mine;
            if (!Showing(owner)) { mine.BeginAnimation(SolidColorBrush.ColorProperty, null); mine.Color = to; return mine; }
            var from = mine.Color;
            mine.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(from, to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Fx.EaseOut });
            Moved("blend " + from + " -> " + to);
            return mine;
        }

        var fresh = new SolidColorBrush(now is SolidColorBrush { Color: var was } && Showing(owner) ? was : to);
        if (fresh.Color != to)
        {
            fresh.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(to, TimeSpan.FromMilliseconds(ms)) { EasingFunction = Fx.EaseOut });
            Moved("blend -> " + to);
        }
        return fresh;
    }

    /// <summary>Gives a block new text colour, blended across.</summary>
    public static void Tint(this TextBlock block, Brush wanted) => block.Foreground = Blend(block.Foreground, wanted, block);

    /// <summary>Grows or shrinks a block's writing smoothly, for a number that turns into a word.</summary>
    public static void Size(this TextBlock block, double size)
    {
        if (Math.Abs(block.FontSize - size) < 0.01 && !block.HasAnimatedProperties) return;
        if (!Showing(block)) { block.BeginAnimation(TextBlock.FontSizeProperty, null); block.FontSize = size; return; }
        Fx.Animate(block, TextBlock.FontSizeProperty, size, 320, Fx.EaseOut);
    }

    /// <summary>
    /// A row that is new on a card slides in, a little after the one above it. <paramref name="order"/>
    /// is its place among the ones arriving together. Does nothing on a card that is not showing yet.
    /// </summary>
    public static void Arrive(UIElement e, FrameworkElement card, int order = 0)
    {
        if (!Showing(card)) return;
        Moved("arrive " + e.GetType().Name);
        Fx.Appear(e, dx: 0, dy: 7, ms: 300, delay: Math.Min(order, 8) * 45);
    }

    /// <summary>Shows or hides something by fading it, rather than it blinking in or out.</summary>
    public static void Show(UIElement e, bool show, FrameworkElement card, Visibility hidden = Visibility.Collapsed)
    {
        bool shown = e.Visibility == Visibility.Visible;
        if (shown == show && !(show && e.HasAnimatedProperties && e.Opacity < 1)) return;
        if (!Showing(card))
        {
            e.BeginAnimation(UIElement.OpacityProperty, null);
            e.Visibility = show ? Visibility.Visible : hidden;
            return;
        }
        Moved((show ? "show " : "hide ") + e.GetType().Name);
        if (show)
        {
            e.Visibility = Visibility.Visible;
            Fx.Animate(e, UIElement.OpacityProperty, 1, 260, Fx.EaseOut);
            if (e.Opacity >= 1) { e.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = Fx.EaseOut }); }
        }
        else Fx.Animate(e, UIElement.OpacityProperty, 0, 180, Fx.EaseIn, done: () =>
        {
            if (e.Opacity > 0.01) return;          // shown again before the fade ended
            e.Visibility = hidden;
            e.BeginAnimation(UIElement.OpacityProperty, null);
        });
    }

    /// <summary>
    /// Swaps a picture by fading the old one out and the new one in, sliding a little toward
    /// <paramref name="dx"/> when browsing sideways through several.
    /// </summary>
    public static void Picture(Image image, ImageSource? source, double dx = 0)
    {
        if (ReferenceEquals(image.Source, source)) return;
        if (!Showing(image) || image.Source == null)
        {
            image.Source = source;
            if (Showing(image) && source != null) { Moved("picture"); Fx.Appear(image, dx: dx, dy: 0, ms: 280); }
            return;
        }
        Moved("picture");
        Fx.Disappear(image, dx: -dx, dy: 0, ms: 110, done: () =>
        {
            image.Source = source;
            Fx.Appear(image, dx: dx, dy: 0, ms: 260);
        });
    }

    /// <summary>A small bounce, for something that just finished or just ticked over (a round done, a task ticked).</summary>
    public static void Nudge(UIElement e, FrameworkElement card)
    {
        if (!Showing(card)) return;
        Moved("nudge " + e.GetType().Name);
        Fx.Pop(e, from: 0.82, ms: 420);
    }
}
