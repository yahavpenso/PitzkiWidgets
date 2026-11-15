using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PitzkiWidgets.Core;
using PitzkiWidgets.Widgets;

namespace PitzkiWidgets;

/// <summary>
/// PITZKI_WIDGETS_TEST=1 PITZKI_WIDGETS_ONLY=film: one short clip of every card (and a few of the bigger
/// features) for the README, as numbered PNG frames in %TEMP%\piztki_widgets\film\&lt;name&gt;. Every card that
/// would show something of this PC's own (the calendar, the clipboard, Steam, Discord, games, music...)
/// is given made-up things instead, so nothing personal ends up in a public picture.
/// </summary>
static partial class SelfTest
{
    const double FilmScale = 1.5, Fps = 12;

    sealed record Cue(double At, Func<Task> Do);
    static Cue At(double at, Action act) => new(at, () => { act(); return Task.CompletedTask; });
    static Cue AtAsync(double at, Func<Task> act) => new(at, act);

    /// <summary>Films whatever windows are given for that long, doing each cue when its moment comes.</summary>
    static async Task Film(string root, string name, double seconds, Func<IEnumerable<Window>> windows, Rect? area, List<string> log, params Cue[] cues)
    {
        var frames = new List<BitmapSource>();
        var order = cues.OrderBy(c => c.At).ToList();
        int next = 0;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            while (next < order.Count && order[next].At <= clock.Elapsed.TotalSeconds)
            {
                try { await order[next].Do(); } catch (Exception bad) { log.Add($"film {name}: cue at {order[next].At} broke: {bad.Message}"); }
                next++;
            }
            var shot = Frame(windows().ToList(), area);
            if (shot != null) frames.Add(shot);
            double wait = frames.Count * (1000.0 / Fps) - clock.ElapsedMilliseconds;
            await Task.Delay(Math.Max(1, (int)wait));
        }
        string folder = Path.Combine(root, name);
        if (Directory.Exists(folder)) Directory.Delete(folder, true);
        Directory.CreateDirectory(folder);
        for (int i = 0; i < frames.Count; i++)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(frames[i]));
            using var file = File.Create(Path.Combine(folder, $"{i:0000}.png"));
            encoder.Save(file);
        }
        log.Add($"film {name}: {frames.Count} frames");
    }

    /// <summary>The windows drawn where they stand inside the area (or, for one window, just that window), on nothing.</summary>
    static BitmapSource? Frame(List<Window> windows, Rect? area)
    {
        windows = windows.Where(w => w.IsLoaded && w.ActualWidth > 0 && w.Content is Visual).ToList();
        if (windows.Count == 0) return null;
        var box = area ?? new Rect(windows[0].Left, windows[0].Top, windows[0].ActualWidth, windows[0].ActualHeight);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, box.Width, box.Height));
            foreach (var w in windows)
            {
                int pw = (int)Math.Ceiling(w.ActualWidth * FilmScale), ph = (int)Math.Ceiling(w.ActualHeight * FilmScale);
                var one = new RenderTargetBitmap(pw, ph, 96 * FilmScale, 96 * FilmScale, PixelFormats.Pbgra32);
                one.Render((Visual)w.Content);
                dc.DrawImage(one, new Rect(w.Left - box.X, w.Top - box.Y, w.ActualWidth, w.ActualHeight));
            }
        }
        var all = new RenderTargetBitmap((int)Math.Ceiling(box.Width * FilmScale), (int)Math.Ceiling(box.Height * FilmScale), 96 * FilmScale, 96 * FilmScale, PixelFormats.Pbgra32);
        all.Render(visual);
        all.Freeze();
        return all;
    }

    /// <summary>A made-up picture: a sky, a sun and hills, in colours of its own, so no real photo is shown.</summary>
    static void Picture(string path, int w, int h, Color top, Color bottom, Color hills, Color sun, int seed)
    {
        var rng = new Random(seed);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(top, bottom, 90), null, new Rect(0, 0, w, h));
            dc.DrawEllipse(new SolidColorBrush(sun), null, new Point(w * (0.25 + rng.NextDouble() * 0.5), h * 0.35), h * 0.12, h * 0.12);
            for (int layer = 0; layer < 3; layer++)
            {
                var geo = new StreamGeometry();
                using (var g = geo.Open())
                {
                    double baseY = h * (0.62 + layer * 0.12);
                    g.BeginFigure(new Point(0, h), true, true);
                    g.LineTo(new Point(0, baseY), true, false);
                    for (int x = 0; x <= w; x += w / 6)
                        g.QuadraticBezierTo(new Point(x + w / 12.0, baseY - h * (0.05 + rng.NextDouble() * 0.12)), new Point(x + w / 6.0, baseY), true, false);
                    g.LineTo(new Point(w, h), true, false);
                }
                var c = Color.FromArgb(255, (byte)(hills.R * (1 - layer * 0.25)), (byte)(hills.G * (1 - layer * 0.25)), (byte)(hills.B * (1 - layer * 0.25)));
                dc.DrawGeometry(new SolidColorBrush(c), null, geo);
            }
        }
        var bmp = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    static BitmapSource Cover()
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new LinearGradientBrush(Color.FromRgb(0xFF, 0x6B, 0x9A), Color.FromRgb(0x6A, 0x4C, 0xFF), 45), null, new Rect(0, 0, 200, 200));
            dc.DrawEllipse(null, new Pen(new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 6), new Point(100, 100), 55, 55);
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(200, 255, 255, 255)), null, new Point(100, 100), 14, 14);
        }
        var bmp = new RenderTargetBitmap(200, 200, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    static double Dollars(string shown) =>
        double.TryParse(new string(shown.Where(c => char.IsDigit(c) || c == '.').ToArray()), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : 0;

    static async Task FilmAll(App app, string dir, List<string> log)
    {
        string root = Path.Combine(dir, "film");
        string made = Path.Combine(dir, "film-src");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(made);
        string? only = Environment.GetEnvironmentVariable("PITZKI_FILM");   // one name, to redo just that clip
        bool Want(string name) => string.IsNullOrEmpty(only) || only.Split(',').Contains(name);

        // ----- made-up things for every card that would otherwise show this PC's own -----
        bool hadShekels = Pitzki.Core.MoneyPrefs.Shekels;
        Pitzki.Core.MoneyPrefs.Shekels = false;             // dollars, for a README most people read in English
        var started = DateTime.Now;
        PitzkiWidget.Clock = () => DateTime.Today.AddHours(14).Add(DateTime.Now - started);   // daytime, or Pitzki is asleep
        ClipboardWidget.Demo = true;
        SoundWidget.Demo = true;
        ClaudeUsageWidget.Demo = true;
        DiscordWidget.Demo = true;

        ScreenTimeWidget.File = Path.Combine(dir, "screentime-film.json");
        if (File.Exists(ScreenTimeWidget.File)) File.Delete(ScreenTimeWidget.File);
        ScreenTimeWidget.Pretend = "Visual Studio Code";
        foreach (var (prog, minutes) in new[] { ("Visual Studio Code", 142.0), ("Google Chrome", 95.0), ("Minecraft", 64.0), ("Spotify", 21.0) })
            ScreenTimeWidget.Counter.Add(DateTime.Today, prog, minutes * 60);
        for (int d = 1; d < 7; d++) ScreenTimeWidget.Counter.Add(DateTime.Today.AddDays(-d), "Google Chrome", (90 + d * 41 % 180) * 60);

        string holiday = Path.Combine(made, "Holiday");
        Directory.CreateDirectory(holiday);
        var looks = new[]
        {
            (Color.FromRgb(0x5B, 0x9B, 0xFF), Color.FromRgb(0xFF, 0xD3, 0xA0), Color.FromRgb(0x3E, 0x8E, 0x5A), Color.FromRgb(0xFF, 0xF2, 0xB0)),
            (Color.FromRgb(0x2A, 0x1F, 0x5C), Color.FromRgb(0xFF, 0x7A, 0x59), Color.FromRgb(0x5A, 0x3A, 0x7A), Color.FromRgb(0xFF, 0xC8, 0x6B)),
            (Color.FromRgb(0x87, 0xD7, 0xF5), Color.FromRgb(0xE8, 0xF8, 0xFF), Color.FromRgb(0x9A, 0xB8, 0xC9), Color.FromRgb(0xFF, 0xFF, 0xFF)),
            (Color.FromRgb(0x10, 0x18, 0x3A), Color.FromRgb(0x38, 0x4A, 0x8C), Color.FromRgb(0x1E, 0x2A, 0x4A), Color.FromRgb(0xF0, 0xF0, 0xFF)),
        };
        for (int i = 0; i < looks.Length; i++)
            Picture(Path.Combine(holiday, $"photo{i + 1}.png"), 640, 420, looks[i].Item1, looks[i].Item2, looks[i].Item3, looks[i].Item4, i + 3);
        string shots = Path.Combine(made, "Screenshots");
        Directory.CreateDirectory(shots);
        var clips = new List<ClipsWidget.Clip>();
        for (int i = 0; i < 4; i++)
        {
            string p = Path.Combine(shots, $"shot{i + 1}.png");
            Picture(p, 640, 360, looks[3 - i].Item1, looks[3 - i].Item2, looks[3 - i].Item3, looks[3 - i].Item4, 20 + i);
            clips.Add(new ClipsWidget.Clip(p, DateTime.Now.AddHours(-2 - i * 20), false, "Screenshots"));
        }
        ClipsWidget.Pretend = clips;

        NewsWidget.Pretend = new()
        {
            new("Sky Racers", "Update 2.4: night tracks", "Two new tracks after dark, headlights for every car, and a fix for the lap timer.", DateTime.Now.AddHours(-20), ""),
            new("Block Quest", "Patch 1.12", "The ember caves are open. Smoother loading and fewer crashes on older PCs.", DateTime.Now.AddDays(-3), ""),
            new("Space Miners", "Hotfix 0.9.3", "Fixed the drill getting stuck on asteroids and a sound that kept looping.", DateTime.Now.AddDays(-5), ""),
        };

        string lastGame = Path.Combine(made, "lastgame.json");
        File.WriteAllText(lastGame, System.Text.Json.JsonSerializer.Serialize(new
        {
            MatchSummary = true, LastMatchGame = "Counter-Strike 2", LastMatchHead = "Won 13:9 on Mirage",
            LastMatchDetail = "24 kills · 15 deaths · MVP x4", LastMatchWhen = DateTime.Today.AddDays(-1).AddHours(22).AddMinutes(40).Ticks,
            LastMatchIcon = "", LastMatchMore = "Counter-Strike 2 · Mirage · 13:9\nADR 92 · HS 48%\nPremier · 38 min",
        }));
        Core.LastGame.Pretend = lastGame;

        var skins = new List<Pitzki.Core.Cs2InvItem>
        {
            new("★ Karambit | Doppler (Factory New)", 1, 984.20, true),
            new("AWP | Asiimov (Field-Tested)", 1, 112.40, true),
            new("Desert Eagle | Blaze (Factory New)", 1, 618.75, true),
            new("AK-47 | Redline (Field-Tested)", 2, 38.10, true),
            new("M4A1-S | Printstream (Minimal Wear)", 1, 214.00, true),
            new("Fever Case", 6, 0.92, true),
        };
        Pitzki.Core.Cs2Inventory.Use(skins, DateTime.UtcNow);
        double worth = skins.Sum(s => s.Worth);
        var invHistory = new List<Pitzki.Core.Cs2InvPoint>();
        for (int d = 12; d >= 1; d--) invHistory.Add(new() { Day = DateTime.Today.AddDays(-d), Value = Math.Round(worth * (0.86 + 0.011 * (12 - d)) + (d % 3) * 6, 2) });
        invHistory.Add(new() { Day = DateTime.Today, Value = Math.Round(worth, 2) });
        Pitzki.Core.Cs2Inventory.UseHistory(invHistory);
        Pitzki.Core.Cs2Inventory.UseDay(DateTime.Today.AddDays(-1), skins.ToDictionary(s => s.Name, s => s.Name.Contains("Karambit") ? s.Price / 1.08 : s.Name.Contains("Asiimov") ? s.Price * 1.03 : s.Price));

        var cards = new List<Pitzki.Core.SteamItem>
        {
            new("753-Winter Sale 2025 (Profile Background)", "Winter Sale 2025", "Background", "Steam", 1, 0.42, true),
            new("753-Sky Racers Trading Card 3", "Sky Racers card", "Trading card", "Sky Racers", 4, 0.11, true),
            new("753-:cozycat:", ":cozycat:", "Emoticon", "Steam", 2, 0.31, true),
            new("753-Block Quest Foil Card", "Block Quest foil", "Trading card", "Block Quest", 1, 1.85, true),
        };
        var steamHistory = new List<Pitzki.Core.Cs2InvPoint>();
        double steamWorth = cards.Sum(c => c.Worth);
        for (int d = 8; d >= 0; d--) steamHistory.Add(new() { Day = DateTime.Today.AddDays(-d), Value = Math.Round(steamWorth * (0.9 + 0.0125 * (8 - d)), 2) });
        Pitzki.Core.SteamItems.Use(cards, cards.ToDictionary(c => c.Name, c => c.Price), steamHistory);

        // ----- every card -----
        var plan = new Dictionary<string, (double Seconds, int Settle, Func<WidgetWindow, Cue[]> Cues, Action<Placed>? Setup)>
        {
            ["pitzki"] = (9, 900, w => new[] { At(0.3, () => FindMascot(w)?.StartHobby("juggle")) }, null),
            ["clock"] = (3, 900, w => Array.Empty<Cue>(), null),
            ["calendar"] = (3, 900, w => Array.Empty<Cue>(), null),
            ["weather"] = (9, 3500, w => new[]
            {
                At(0.1, () => (w.Body.Content as WeatherWidget)?.Weather("rain")),
                At(3.0, () => (w.Body.Content as WeatherWidget)?.Weather("snow")),
                At(6.0, () => (w.Body.Content as WeatherWidget)?.Weather("sun")),
            }, null),
            ["notes"] = (5, 900, w => Typing(w, "\nand the charger"), p => p.Text = "Shopping list:\nmilk, bread"),
            ["tasks"] = (5, 900, w => new[] { At(1.5, () => (w.Body.Content as TasksWidget)?.Add("Book the flight")) },
                p => p.Items.AddRange(new[] { "1|Send the invoice", "0|Call the dentist", "0|Water the plants" })),
            ["timer"] = (4, 900, w => Array.Empty<Cue>(), p => { p.Number = 10; p.Until = DateTime.Now.AddMinutes(7).AddSeconds(42); }),
            ["pomodoro"] = (4, 900, w => new[] { At(0.6, () => (w.Body.Content as PomodoroWidget)?.Begin()) }, null),
            ["countdown"] = (3, 900, w => Array.Empty<Cue>(), p => { p.Name = "Trip to Rome"; p.Until = DateTime.Today.AddDays(12); p.Since = DateTime.Today.AddDays(-18); }),
            ["agenda"] = (4, 900, w => new[] { At(0.2, () => (w.Body.Content as AgendaWidget)?.Draw(new List<Agenda.Entry>
            {
                new("Team call", DateTime.Now.AddMinutes(-10), TimeSpan.FromMinutes(30), false, "Online"),
                new("Dentist", DateTime.Now.AddMinutes(50), TimeSpan.FromHours(1), false, "Main St 12"),
                new("Pick up the parcel", DateTime.Today.AddDays(1).AddHours(17), TimeSpan.FromMinutes(30), false, ""),
                new("Game night", DateTime.Today.AddDays(2).AddHours(20), TimeSpan.FromHours(3), false, ""),
            }, DateTime.Now)) }, null),
            ["calc"] = (7, 900, w => new[]
            {
                AtAsync(0.8, () => Sum(w, "12*(3+4)")),
                AtAsync(2.6, () => Sum(w, "5 km to mi")),
                AtAsync(4.4, () => Sum(w, "100 f to c")),
            }, p => p.Text = "20 usd to eur"),
            ["worldclock"] = (5, 900, w => new[] { At(2.0, () => (w.Body.Content as WorldClockWidget)?.Next(0)) }, null),
            ["photos"] = (7, 2500, w => new[] { At(2.0, () => (w.Body.Content as PhotosWidget)?.Next()), At(4.5, () => (w.Body.Content as PhotosWidget)?.Next()) }, p => p.Text = holiday),
            ["shortcuts"] = (3, 1400, w => Array.Empty<Cue>(), p => p.Items.AddRange(new[]
            {
                Path.Combine(Environment.SystemDirectory, "notepad.exe"), Path.Combine(Environment.SystemDirectory, "calc.exe"),
                Path.Combine(Environment.SystemDirectory, "mspaint.exe"), Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", "explorer.exe"),
            })),
            ["stocks"] = (5, 4500, w => new[] { At(1.5, () =>
            {
                if (w.Body.Content is not StocksWidget s || !s.Shown.Any()) return;
                var first = s.Shown.First();
                double now = Dollars(first.Price);
                if (now > 0) s.Feed(new[] { new StockQuote(first.Symbol, "Apple Inc.", "USD", Math.Round(now * 1.012, 2), now / 1.004, new List<double> { now / 1.004, now * 0.998, now * 1.004, now * 1.012 }, true) });
            }) }, p => p.Text = "AAPL, NVDA, MSFT, TSLA, ^GSPC"),
            ["chart"] = (7, 3500, w => new[] { At(2.5, () => (w.Body.Content as ChartWidget)?.Pick("1y")) }, p => p.Text = "NVDA"),
            ["portfolio"] = (4, 800, w => Array.Empty<Cue>(), null),
            ["indices"] = (3, 3500, w => Array.Empty<Cue>(), null),
            ["markets"] = (3, 2000, w => Array.Empty<Cue>(), null),
            ["paper"] = (6, 1200, w => new[]
            {
                AtAsync(0.8, () => (w.Body.Content as PaperTradeWidget)!.Trade("AAPL", "10", buy: true)),
                AtAsync(3.0, () => (w.Body.Content as PaperTradeWidget)!.Trade("NVDA", "15", buy: true)),
            }, null),
            ["guess"] = (5, 3500, w => new[] { At(1.5, () => (w.Body.Content as GuessWidget)?.Press(true)) }, p => p.Text = "MSFT"),
            ["marketnews"] = (3, 6000, w => Array.Empty<Cue>(), p => p.Text = "AAPL, NVDA, TSLA"),
            ["crypto"] = (3, 3500, w => Array.Empty<Cue>(), null),
            ["rates"] = (3, 3500, w => Array.Empty<Cue>(), null),
            ["convert"] = (6, 2500, w => new[]
            {
                AtAsync(1.0, () => (w.Body.Content as ConverterWidget)!.Convert("50", "EUR", "USD")),
                AtAsync(3.5, () => (w.Body.Content as ConverterWidget)!.Convert("0.01", "BTC", "USD")),
            }, null),
            ["system"] = (6, 900, w => new[] { At(0.1, () => (w.Body.Content as SystemWidget)?.Demo()) }, null),
            ["drives"] = (3, 900, w => Array.Empty<Cue>(), null),
            ["batteries"] = (3, 900, w => new[] { At(0.1, () => (w.Body.Content as BatteriesWidget)?.Demo()) }, null),
            ["clipboard"] = (6, 600, w => new[]
            {
                At(0.1, () => (w.Body.Content as ClipboardWidget)?.Forget()),
                At(0.5, () => (w.Body.Content as ClipboardWidget)?.Took("Shopping list")),
                At(1.8, () => (w.Body.Content as ClipboardWidget)?.Took("#7F6AE8")),
                At(3.1, () => (w.Body.Content as ClipboardWidget)?.Took("https://example.com/recipe")),
                At(4.4, () => (w.Body.Content as ClipboardWidget)?.Took("Meeting moved to 4 pm")),
            }, null),
            ["clips"] = (4, 2500, w => Array.Empty<Cue>(), null),
            ["screentime"] = (3, 1200, w => Array.Empty<Cue>(), null),
            ["claude"] = (5, 800, w => new[]
            {
                At(0.1, () => (w.Body.Content as ClaudeUsageWidget)?.Feed(Limits(23, 48, 61))),
                At(2.5, () => (w.Body.Content as ClaudeUsageWidget)?.Feed(Limits(37, 52, 66))),
            }, null),
            ["music"] = (7, 900, w => new[] { At(0.1, () => (w.Body.Content as MusicWidget)?.Media.ShowDemo("Midnight Drive", "The Pixel Cats", Cover(), TimeSpan.FromSeconds(214), TimeSpan.FromSeconds(71))) }, null),
            ["discord"] = (6, 900, w => new[]
            {
                At(0.1, () => (w.Body.Content as DiscordWidget)?.Feed(People(true, false), "Gaming night", false, false)),
                At(3.0, () => (w.Body.Content as DiscordWidget)?.Feed(People(false, true), "Gaming night", true, false)),
            }, null),
            ["sound"] = (5, 600, w => new[]
            {
                At(0.1, () => (w.Body.Content as SoundWidget)?.ShowDemo("Discord", "", "Gaming night · 3", Apps(0.8, 0.55, 0.35))),
                At(2.5, () => (w.Body.Content as SoundWidget)?.ShowDemo("Discord", "", "Gaming night · 3", Apps(0.8, 0.25, 0.7))),
            }, null),
            ["steam"] = (6, 600, w => new[]
            {
                At(0.1, () => { if (w.Body.Content is SteamWidget s) { s.Demo = ("Space Miners", "2.1 GB of 6.4 GB  ·  33%", 0.33); s.Tick(); } }),
                At(2.0, () => { if (w.Body.Content is SteamWidget s) { s.Demo = ("Space Miners", "3.6 GB of 6.4 GB  ·  56%", 0.56); s.Tick(); } }),
                At(4.0, () => { if (w.Body.Content is SteamWidget s) { s.Demo = ("Space Miners", "5.2 GB of 6.4 GB  ·  81%", 0.81); s.Tick(); } }),
            }, null),
            ["cs2inv"] = (5, 1200, w => new[] { At(2.0, () =>
            {
                Pitzki.Core.Cs2Inventory.Use(skins.Select((s, k) => k == 0 ? s with { Price = s.Price * 1.06 } : s).ToList(), DateTime.UtcNow);
                (w.Body.Content as InventoryWidget)?.Redraw();
            }) }, null),
            ["networth"] = (3, 1200, w => new[] { At(0.1, () => (w.Body.Content as NetWorthWidget)?.Redraw()) }, null),
            ["skinwatch"] = (4, 1200, w => new[] { AtAsync(0.1, async () =>
            {
                await Pitzki.Core.Cs2Skins.WaitForPricesAsync();
                if (w.Body.Content is not SkinWatchWidget s) return;
                foreach (var (typed, price) in new[] { ("redline field", 45.0), ("asiimov awp field", 90.0), ("printstream m4a1 minimal", 200.0) })
                    if (s.Find(typed) is { Count: > 0 } found) s.Watch(found[0], price);
            }) }, null),
            ["cases"] = (4, 1200, w => new[] { AtAsync(0.1, async () =>
            {
                var t = DateTime.Now;
                while (!Pitzki.Core.Cs2Cases.Ready && DateTime.Now - t < TimeSpan.FromSeconds(90)) await Task.Delay(500);
                (w.Body.Content as CasesWidget)?.Redraw();
            }) }, null),
            ["steamitems"] = (3, 1200, w => new[] { At(0.1, () => (w.Body.Content as SteamItemsWidget)?.Redraw()) }, null),
            ["news"] = (4, 2500, w => Array.Empty<Cue>(), null),
            ["robrun"] = (3, 1500, w => Array.Empty<Cue>(), null),
        };

        foreach (var (kind, _, _, _) in WidgetBody.Kinds)
        {
            if (!Want(kind)) continue;
            if (!plan.TryGetValue(kind, out var how)) { log.Add($"film {kind}: no plan"); continue; }
            var placed = new Placed { Kind = kind, X = Far, Y = Far };
            how.Setup?.Invoke(placed);
            if (kind == "portfolio")
            {
                var (pw, pc) = await OpenCard<PortfolioWidget>(placed, 800);
                await pc.Add("Apple", "10", "150");
                await pc.Add("NVDA", "8", "95");
                await pc.Add("MSFT", "4", "310");
                await Task.Delay(4000);
                await Film(root, kind, how.Seconds, () => new[] { pw }, null, log);
                pw.Close();
                continue;
            }
            var window = new WidgetWindow(placed);
            window.Show();
            await Task.Delay(how.Settle);
            // the cards that need a moment to fetch (skins, cases) do it in their first cue, before filming starts
            var cues = how.Cues(window);
            var early = cues.Where(c => c.At <= 0.1).ToList();
            foreach (var c in early) { try { await c.Do(); } catch (Exception bad) { log.Add($"film {kind}: setup broke: {bad.Message}"); } }
            await Task.Delay(early.Count > 0 ? 900 : 0);
            await Film(root, kind, how.Seconds, () => new[] { window }, null, log, cues.Except(early).ToArray());
            window.Close();
        }

        // ----- Pitzki's scenes, on a stage of their own -----
        foreach (var scene in new[] { "guitar", "pizza", "case", "skate", "telescope", "cocoa" })
        {
            if (!Want("life-" + scene)) continue;
            var him = new Pitzki.Controls.Mascot { Width = 300, Height = 240 };
            var stage = new Grid { Width = 300, Height = 240, Background = Brushes.Transparent };
            stage.Children.Add(him);
            var window = new Window
            {
                WindowStyle = WindowStyle.None, AllowsTransparency = true, Background = Brushes.Transparent, ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false, ShowActivated = false, Left = Far, Top = Far, SizeToContent = SizeToContent.WidthAndHeight, Content = stage,
            };
            window.Show();
            await Task.Delay(700);
            if (scene == "case") Pitzki.Controls.Mascot.CaseForce = "gold";
            him.StartHobby(scene);
            Pitzki.Controls.Mascot.CaseForce = "";
            await Film(root, "life-" + scene, scene == "case" ? 12 : 8, () => new[] { window }, null, log);
            window.Close();
        }

        // ----- the picker: the rows arrive, then a slow scroll down -----
        if (Want("picker"))
        {
            var picker = new PickerWindow { Left = Far, Top = Far };
            picker.Show();
            var scroll = new List<Cue>();
            for (int i = 0; i <= 48; i++) { double part = i / 48.0; scroll.Add(At(3.0 + i * 0.1, () => picker.Scroll.ScrollToVerticalOffset(picker.Scroll.ScrollableHeight * part * 0.55))); }
            await Film(root, "picker", 8.5, () => new[] { picker }, null, log, scroll.ToArray());
            picker.Close();
        }

        // ----- two cards dropped together become one -----
        if (Want("join"))
        {
            var room = new Rect(Far, Far, 1000, 800);
            WidgetWindow.RoomForTests = room;
            App.Board.Widgets.Clear();
            var lead = new Placed { Kind = "clock", X = Far + 100, Y = Far + 100 };
            var mate = new Placed { Kind = "weather", X = Far + 380, Y = Far + 100 };
            App.Board.Widgets.Add(lead);
            App.Board.Widgets.Add(mate);
            app.Redraw();
            await Task.Delay(1500);
            IEnumerable<Window> Both() => App.Board.Widgets.Select(p => (Window?)app.Window(p.Id)).Where(w => w != null).Distinct()!;
            await Film(root, "join", 5, Both, new Rect(Far + 80, Far + 80, 560, 260), log,
                At(1.2, () => app.Join(mate, lead, across: true)));
            foreach (var p in App.Board.Widgets.ToList()) app.Remove(p);
            WidgetWindow.RoomForTests = null;
        }

        ClipboardWidget.Demo = SoundWidget.Demo = ClaudeUsageWidget.Demo = DiscordWidget.Demo = false;
        Pitzki.Core.MoneyPrefs.Shekels = hadShekels;
        PitzkiWidget.Clock = () => DateTime.Now;
        ScreenTimeWidget.Pretend = null;
        ClipsWidget.Pretend = null;
        NewsWidget.Pretend = null;
        Core.LastGame.Pretend = null;
    }

    /// <summary>The sum written in the calculator's box as well as worked out, so the picture matches.</summary>
    static Task Sum(WidgetWindow w, string sum)
    {
        if (Find<TextBox>(w) is { } box) box.Text = sum;
        return (w.Body.Content as CalcWidget)!.Work(sum);
    }

    static Cue[] Typing(WidgetWindow w, string words)
    {
        var cues = new List<Cue>();
        for (int i = 0; i < words.Length; i++)
        {
            char c = words[i];
            cues.Add(At(0.8 + i * 0.16, () => { if (Find<TextBox>(w) is { } box) { box.Text += c; box.CaretIndex = box.Text.Length; } }));
        }
        return cues.ToArray();
    }

    static List<Pitzki.Core.ClaudeLimit> Limits(double session, double week, double model) => new()
    {
        new("session", "5-hour limit", session, DateTime.Now.AddHours(2.4)),
        new("weekly_all", "Weekly · all models", week, DateTime.Now.AddDays(3).AddHours(5)),
        new("weekly_scoped", "Weekly · Opus", model, DateTime.Now.AddDays(3).AddHours(5)),
    };

    static List<(string Id, string Name, string? Avatar, bool Talking, bool Silent)> People(bool first, bool second) => new()
    {
        ("1", "Alex", null, first, false), ("2", "Maya", null, second, false), ("3", "Sam", null, false, true),
    };

    static IList<(string Name, string Exe, double Volume)> Apps(double a, double b, double c)
    {
        string win = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";
        return new List<(string, string, double)>
        {
            ("Music", Path.Combine(win, "explorer.exe"), a),
            ("Browser", @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe", b),
            ("Game", Path.Combine(Environment.SystemDirectory, "mspaint.exe"), c),
        };
    }
}
