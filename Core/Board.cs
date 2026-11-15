using System.IO;
using System.Text.Json;

namespace PitzkiWidgets.Core;

/// <summary>One widget the user has put on the desktop: which kind it is, and where it sits.</summary>
public sealed class Placed
{
    /// <summary>"clock", "pitzki", "system", "tasks", "timer", ... : one of the names in <c>WidgetBody.Kinds</c>.</summary>
    public string Kind { get; set; } = "";

    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>How big, 0.8 to 1.6. The user can make a widget bigger without it losing its shape.</summary>
    public double Scale { get; set; } = 1;

    /// <summary>What was written on it, for the kinds that are written on (the note card).</summary>
    public string Text { get; set; } = "";

    /// <summary>The lines a card keeps: its tasks ("0|text" open, "1|text" done) or the programs on a shortcuts card.</summary>
    public List<string> Items { get; set; } = new();

    /// <summary>A card's own number: the minutes on the timer.</summary>
    public int Number { get; set; }

    /// <summary>A moment a card is working towards: when the timer runs out, or the day a countdown is for.</summary>
    public DateTime? Until { get; set; }

    /// <summary>When the countdown was set, so its bar knows how far along it is.</summary>
    public DateTime? Since { get; set; }

    /// <summary>What a countdown is counting down to.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// The card this one has been joined to: the two are drawn inside one frame, as a single card.
    /// "" when it stands on its own. Only the one being joined to keeps a place on the desktop.
    /// </summary>
    public string Under { get; set; } = "";

    /// <summary>A joined card sits beside the one it was joined to, rather than under it.</summary>
    public bool Across { get; set; }

    /// <summary>Where the card shows: "" everywhere, or the name of the one profile it belongs to ("Work", "Game").</summary>
    public string Only { get; set; } = "";

    /// <summary>The card tucks itself behind the nearest screen edge, leaving a thin strip, and slides out when the pointer touches it.</summary>
    public bool Peek { get; set; }

    /// <summary>A card that has been told to stay where it is. Dragging it does nothing.</summary>
    public bool Locked { get; set; }

    /// <summary>Given to each one when it is added, so the right line is found again when it is moved or taken off.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
}

/// <summary>
/// What is on the desktop, kept in one small file so it all comes back the same way next time.
/// Nothing here is ever written to Pitzki's own settings: the two apps keep their own.
/// </summary>
public sealed class Board
{
    public List<Placed> Widgets { get; set; } = new();

    /// <summary>Whether the widgets start with Windows.</summary>
    public bool StartWithWindows { get; set; }

    /// <summary>Every card stays where it is, whatever its own setting says.</summary>
    public bool LockAll { get; set; }

    /// <summary>Liquid glass: cards let the desktop show through, catch light along their top edge, and shine where the pointer is.</summary>
    public bool Glass { get; set; } = true;

    /// <summary>How dark the glass is: 0 clearer, 1 normal, 2 darker.</summary>
    public int GlassLevel { get; set; } = 1;

    /// <summary>The profile on show: "" for every card, or a name such as "Work" or "Game". Cards that belong to another profile are put away.</summary>
    public string Profile { get; set; } = "";

    /// <summary>Two cards dropped right against each other stay two cards, instead of becoming one.</summary>
    public bool NoJoin { get; set; }

    /// <summary>While a game is running, the Game profile is put on by itself, and the one before comes back after.</summary>
    public bool AutoGame { get; set; } = true;

    /// <summary>Cards drift freely when they are dragged. Off by default: they line up with each other and with the screen's edges.</summary>
    public bool NoSnap { get; set; }

    /// <summary>The city on the weather card, and where the last lookup put it.</summary>
    /// <summary>The owner's birthday as "dd/MM" ("" = not set): on that day Pitzki has cake and a party hat.</summary>
    public string Birthday { get; set; } = "";

    public string WeatherCity { get; set; } = "";
    public string WeatherPlace { get; set; } = "";
    public double WeatherLat { get; set; }
    public double WeatherLon { get; set; }

    /// <summary>The cards joined to this one, in the order they were joined.</summary>
    public List<Placed> JoinedTo(Placed lead) => Widgets.Where(w => w.Under == lead.Id).ToList();

    /// <summary>The card that owns the frame this one is drawn in: itself, when it stands alone.</summary>
    public Placed Lead(Placed card) =>
        card.Under.Length == 0 ? card : Widgets.FirstOrDefault(w => w.Id == card.Under) ?? card;

    /// <summary>
    /// Nothing may be joined to a card that is itself joined to something, and nothing to itself.
    /// Anything pointing at a card that is no longer there is set free again.
    /// </summary>
    public void TidyJoins()
    {
        foreach (var card in Widgets)
        {
            if (card.Under.Length == 0) continue;
            var lead = Widgets.FirstOrDefault(w => w.Id == card.Under);
            if (lead == null || lead.Id == card.Id || lead.Under.Length > 0) card.Under = "";
        }
    }

    static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitzkiWidgets");
    /// <summary>PITZKI_WIDGETS_BOARD=&lt;file&gt; runs on another board (for measuring one card at a time).</summary>
    static string File_ => Environment.GetEnvironmentVariable("PITZKI_WIDGETS_BOARD") is { Length: > 0 } other ? other : Path.Combine(Folder, "board.json");

    public static Board Load()
    {
        try
        {
            if (File.Exists(File_) && JsonSerializer.Deserialize<Board>(File.ReadAllText(File_)) is { } saved)
            {
                saved.TidyJoins();
                return saved;
            }
        }
        catch { } // a file that cannot be read is not worth losing the app over: start afresh
        return new Board();
    }

    /// <summary>Writes the whole board to a file of the user's choosing. False when it could not be written.</summary>
    public bool Export(string file)
    {
        try { File.WriteAllText(file, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true })); return true; }
        catch { return false; }
    }

    /// <summary>Reads a backup back, or null when the file is not one (a backup always holds the Widgets list).</summary>
    public static Board? Import(string file)
    {
        try
        {
            string text = File.ReadAllText(file);
            using (var doc = JsonDocument.Parse(text))
                if (!doc.RootElement.TryGetProperty("Widgets", out var list) || list.ValueKind != JsonValueKind.Array) return null;
            var board = JsonSerializer.Deserialize<Board>(text);
            if (board == null) return null;
            board.Widgets.RemoveAll(w => string.IsNullOrEmpty(w.Kind));
            board.TidyJoins();
            return board;
        }
        catch { return null; }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            File.WriteAllText(File_, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { }
    }
}
