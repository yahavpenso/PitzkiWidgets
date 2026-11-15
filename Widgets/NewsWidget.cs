using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// What changed in the last patch of the games you have installed. Steam publishes each game's news
/// openly, with patch notes marked as such, so there is no account and no key: the only thing asked is
/// "what is the newest patch of this game", once for each of your recent games, every half hour.
/// The three most recent are shown; clicking one opens the full notes in the browser.
/// </summary>
public sealed class NewsWidget : UserControl
{
    const string Api = "https://api.steampowered.com/ISteamNews/GetNewsForApp/v2/?appid={0}&count=10&maxlength=400";

    /// <summary>How many of the games, most recently played first, are asked about.</summary>
    const int Games = 12;

    public sealed record Patch(string Game, string Title, string Words, DateTime When, string Url);

    static readonly HttpClient Web = new() { Timeout = TimeSpan.FromSeconds(10) };

    readonly SteamLocal _steam = new();
    readonly StackPanel _list = new();
    readonly TextBlock _say = new() { FontSize = 11.5, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap };
    bool _asking;
    string _listed = "";

    public NewsWidget()
    {
        var box = new StackPanel { Width = 184 };
        box.Children.Add(Title("Game updates"));
        box.Children.Add(_say);
        box.Children.Add(_list);
        Content = box;

        _say.Text = "Looking for patches...";
        this.Every(1800, Ask);
    }

    // ----- asking -----

    async void Ask()
    {
        if (_asking) return;
        _asking = true;
        try
        {
            var found = await Task.Run(Gather);
            Show(found.Patches, found.Reason);
        }
        catch (Exception bad)
        {
            Show(new List<Patch>(), "Couldn't read the news.");
            Debug.WriteLine(bad);
        }
        finally { _asking = false; }
    }

    /// <summary>The newest patch of each recent game, newest first. A reason is given when there is nothing to show.</summary>
    (List<Patch> Patches, string Reason) Gather()
    {
        if (!_steam.Installed) return (new(), "No Steam here, so no games to ask about.");

        var mine = _steam.Games().Where(g => g.Installed).OrderByDescending(g => g.LastPlayed).Take(Games).ToList();
        if (mine.Count == 0) return (new(), "No installed Steam games yet.");

        int reached = 0;
        var patches = new List<Patch>();
        var gate = new object();
        Parallel.ForEach(mine, new ParallelOptions { MaxDegreeOfParallelism = 4 }, game =>
        {
            try
            {
                string json = Web.GetStringAsync(string.Format(Api, game.App)).GetAwaiter().GetResult();
                lock (gate) reached++;
                if (Newest(game.Name, json) is { } patch) lock (gate) patches.Add(patch);
            }
            catch { }   // one game's news being down is not a reason to show nothing
        });

        if (reached == 0) return (new(), "Couldn't reach Steam.");
        return (patches.OrderByDescending(p => p.When).ToList(), patches.Count == 0 ? "None of your games has published a patch lately." : "");
    }

    /// <summary>The first item Steam tags as patch notes, or null when a game has none.</summary>
    internal static Patch? Newest(string game, string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("appnews", out var news) || !news.TryGetProperty("newsitems", out var items)) return null;

        foreach (var item in items.EnumerateArray())
        {
            if (!item.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Array) continue;
            if (!tags.EnumerateArray().Any(t => t.GetString() == "patchnotes")) continue;

            string title = Text(item, "title");
            string url = Text(item, "url");
            long stamp = item.TryGetProperty("date", out var date) && date.TryGetInt64(out long d) ? d : 0;
            string words = Plain(Text(item, "contents"));
            // notes that open with the game's own name, which the card has just said, start after it
            if (words.StartsWith(game, StringComparison.OrdinalIgnoreCase)) words = words[game.Length..].TrimStart(' ', '·', '-', ':');
            return new Patch(game, title, words, DateTimeOffset.FromUnixTimeSeconds(stamp).LocalDateTime, url);
        }
        return null;
    }

    static string Text(JsonElement item, string field) =>
        item.TryGetProperty(field, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? "" : "";

    /// <summary>
    /// Steam's notes arrive as BBCode with backslashes for line breaks. This turns them into one clean
    /// line of words, with a dot where a new point started.
    /// </summary>
    internal static string Plain(string notes)
    {
        notes = " " + notes;
        notes = Regex.Replace(notes, @"\[/?[a-zA-Z*][^\]]*\]", " ");       // [list] [b] [url=...] and the like
        notes = Regex.Replace(notes, @"https?://\S+", " ");                  // a link is not something to read
        notes = Regex.Replace(notes, @"[\r\n]+", " ");                       // real line breaks
        notes = Regex.Replace(notes, @"\s*\\+\s*", " · ");                   // the backslashes that stand for new lines
        notes = Regex.Replace(notes, @"\*\*|__|`", "");                     // markdown bold and code
        notes = Regex.Replace(notes, @"\s#{1,6}\s*", " · ");                // markdown headings
        notes = Regex.Replace(notes, @"(?<=\s)[-*•]\s+", " · ");             // markdown bullets
        notes = Regex.Replace(notes, @"\s+", " ").Trim(' ', '·');
        notes = Regex.Replace(notes, @"( · ){2,}", " · ");
        return notes;
    }

    // ----- showing -----

    void Show(List<Patch> patches, string reason)
    {
        // the same patches as last time are left alone; a new set slides in one after another
        string listed = string.Join("\u0000", patches.Take(3).Select(p => p.Game + p.When.Ticks));
        if (reason.Length > 0) _say.Say(reason);
        Live.Show(_say, reason.Length > 0, this);
        if (listed == _listed && _list.Children.Count > 0) return;
        if (_listed != "" && listed != "") Core.Buzz.Tell("news", this); // a newer update than the ones already shown
        _listed = listed;
        _list.Children.Clear();
        foreach (var patch in patches.Take(3)) _list.Children.Add(Line(patch));
        for (int i = 0; i < _list.Children.Count; i++) Live.Arrive(_list.Children[i], this, i);
    }

    /// <summary>Which patches are on show, for the test to read.</summary>
    internal int Shown => _list.Children.Count;

    UIElement Line(Patch patch)
    {
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new TextBlock { Text = patch.Game, FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        var age = new TextBlock { Text = Since(patch.When), FontSize = 10, Foreground = Of("AccentBrush"), VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        Grid.SetColumn(age, 1);
        head.Children.Add(age);

        // the first words of the notes, which say more than a title that is always "... Update"
        string said = patch.Words.Length > 0 ? patch.Words : patch.Title;
        var words = new TextBlock
        {
            Text = said, FontSize = 10.5, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap,
            TextTrimming = TextTrimming.CharacterEllipsis, MaxHeight = 28, Margin = new Thickness(0, 1, 0, 0),
        };

        var line = new StackPanel { Background = System.Windows.Media.Brushes.Transparent, Cursor = Cursors.Hand, Margin = new Thickness(0, 0, 0, 8), ToolTip = patch.Title };
        line.Children.Add(head);
        line.Children.Add(words);
        line.MouseLeftButtonDown += (_, e) => e.Handled = true;
        line.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            if (Uri.TryCreate(patch.Url, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http")
                try { Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true }); } catch { }
        };
        line.MouseEnter += (_, _) => words.Tint(Of("TextBrush"));
        line.MouseLeave += (_, _) => words.Tint(Of("SubtleBrush"));
        return line;
    }
}
