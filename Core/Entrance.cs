using System.Diagnostics;
using System.Windows.Media;

namespace PitzkiWidgets.Core;

/// <summary>
/// The cards' way in when the app starts. Right after start every card is loading its news, prices and
/// pictures at once, and the screen hardly draws for a few seconds: a card that animated in then would
/// lose every frame and just appear. So they all wait, hidden, until the drawing has run smoothly for a
/// moment, and then come in one after another.
/// </summary>
static class Entrance
{
    /// <summary>The gap between two drawn frames that still counts as smooth.</summary>
    const int Smooth = 70;
    /// <summary>How long it has to stay smooth before the cards come in.</summary>
    const int Settled = 900;
    /// <summary>Never wait longer than this, however busy it stays.</summary>
    const int Longest = 10000;

    /// <summary>Runs <paramref name="then"/> once <paramref name="ready"/> holds and the screen has been drawing smoothly for a moment.</summary>
    public static void WhenCalm(Func<bool> ready, Action then)
    {
        var since = Stopwatch.StartNew();
        var gap = Stopwatch.StartNew();
        var calm = Stopwatch.StartNew();
        void Frame(object? s, EventArgs e)
        {
            if (gap.ElapsedMilliseconds > Smooth || !ready()) calm.Restart();
            gap.Restart();
            if (calm.ElapsedMilliseconds < Settled && since.ElapsedMilliseconds < Longest) return;
            CompositionTarget.Rendering -= Frame;
            Gaps(since.ElapsedMilliseconds);
            then();
        }
        CompositionTarget.Rendering += Frame;
    }

    /// <summary>With PITZKI_WIDGETS_ENTRANCE set, writes down every long pause in drawing while the cards come in.</summary>
    static void Gaps(long waited)
    {
        if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ENTRANCE") is not { Length: > 0 } file) return;
        var log = new System.Text.StringBuilder($"{DateTime.Now:HH:mm:ss.fff} calm after {waited} ms{Environment.NewLine}");
        var clock = Stopwatch.StartNew();
        long last = 0;
        void Frame(object? s, EventArgs e)
        {
            long now = clock.ElapsedMilliseconds;
            if (now - last > 60) log.Append($"  pause {now - last} ms at {last}{Environment.NewLine}");
            last = now;
            if (now < 3000) return;
            CompositionTarget.Rendering -= Frame;
            lock (typeof(WidgetWindow)) System.IO.File.AppendAllText(file, log.ToString());
        }
        CompositionTarget.Rendering += Frame;
    }
}
