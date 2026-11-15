using System.IO;
using System.Text.Json;

namespace PitzkiWidgets.Core;

/// <summary>How the last game went, as Pitzki wrote it down. One for every game Pitzki follows.</summary>
public sealed record LastGameSummary(string Game, string Head, string Detail, DateTime When, string Icon, string[] More)
{
    /// <summary>Pitzki writes "playing since" while a Roblox visit is still going.</summary>
    public bool Ongoing => More.Length > 0 && More[0].Contains("playing since", StringComparison.OrdinalIgnoreCase);

    /// <summary>"last match" for the games that are played in matches, "last game" for the rest.</summary>
    public string Kind => Ongoing ? "playing now" : Game is "Fortnite" or "Counter-Strike 2" or "Rocket League" ? "last match" : "last game";
}

/// <summary>
/// Reads the last-game summary out of Pitzki's own settings file. Only reads it, never writes, and
/// only opens it again when the file has changed. With no Pitzki on the PC, there is simply nothing.
/// </summary>
public sealed class LastGame
{
    /// <summary>For tests: a different settings file to read.</summary>
    public static string? Pretend;

    static string FilePath => Pretend ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pitzki", "settings.json");

    DateTime _stamp;

    public LastGameSummary? Now { get; private set; }

    /// <summary>Cheap when nothing changed: one look at the file's date.</summary>
    public void Poll()
    {
        try
        {
            var file = new FileInfo(FilePath);
            if (!file.Exists) { Now = null; return; }
            if (file.LastWriteTimeUtc == _stamp) return;

            string text;
            using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
                text = reader.ReadToEnd();

            Now = Parse(text);
            _stamp = file.LastWriteTimeUtc;
        }
        catch
        {
            // Pitzki may be halfway through saving: look again next time
        }
    }

    public static LastGameSummary? Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        string Str(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        if (root.TryGetProperty("MatchSummary", out var on) && on.ValueKind == JsonValueKind.False) return null;
        string head = Str("LastMatchHead");
        if (head.Length == 0) return null;

        long ticks = root.TryGetProperty("LastMatchWhen", out var w) && w.TryGetInt64(out long t) ? t : 0;
        DateTime when = ticks > DateTime.MinValue.Ticks && ticks < DateTime.MaxValue.Ticks ? new DateTime(ticks) : DateTime.MinValue;
        string game = Str("LastMatchGame");
        var more = Str("LastMatchMore").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new LastGameSummary(game.Length > 0 ? game : "A game", head, Str("LastMatchDetail"), when, Str("LastMatchIcon"), more);
    }
}
