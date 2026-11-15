using System.Windows;

namespace PitzkiWidgets.Core;

/// <summary>
/// Little news passed from the cards to Pitzki's card: a new game update, a new clip, the Claude limit
/// nearly used up, a card dropped next to another, two cards joined, a card taken off. Cards just tell;
/// whoever listens (Pitzki) decides what to do with it. Nothing is stored.
/// </summary>
public static class Buzz
{
    /// <summary>What happened, the card it happened on (if any), a number and a word that go with it.</summary>
    public static event Action<string, FrameworkElement?, double, string>? Happened;

    public static void Tell(string what, FrameworkElement? from = null, double value = 0, string detail = "")
    {
        try { Happened?.Invoke(what, from, value, detail); }
        catch { /* a listener's trouble must never break the card that told */ }
    }
}
