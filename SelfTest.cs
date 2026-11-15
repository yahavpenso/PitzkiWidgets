using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PitzkiWidgets.Core;
using PitzkiWidgets.Widgets;

namespace PitzkiWidgets;

/// <summary>
/// A quiet look at every widget without putting anything on the desktop: each one is opened far
/// off-screen, left to settle, and saved as a picture, with a line of words about what it said.
/// Run it with PITZKI_WIDGETS_TEST=1.
/// </summary>
static class SelfTest
{
    public static bool Wanted => Environment.GetEnvironmentVariable("PITZKI_WIDGETS_TEST") == "1";

    const double Far = -30000;

    /// <summary>
    /// Every card switch in the picker, pressed the way the mouse presses it: button down and up on the
    /// switch itself, then once more on the row's name. Each one must put its card out and take it back.
    /// </summary>
    static async Task PickerSwitches(App app, List<string> log)
    {
        var picker = new PickerWindow { Left = Far, Top = Far };
        picker.Show();
        await Task.Delay(2600);
        var ticks = Under<System.Windows.Controls.CheckBox>(picker.Lists).ToList();
        log.Add($"switches found: {ticks.Count}, cards on board {App.Board.Widgets.Count}");
        int bad = 0;
        var order = WidgetBody.Kinds.GroupBy(k => k.Group).SelectMany(g => g).ToList();
        foreach (var (tick, i) in ticks.Select((t, i) => (t, i)))
        {
            var kind = order[i].Kind;
            bool was = tick.IsChecked == true;
            // whatever really sits under the middle of the switch, the way the mouse would find it
            var mid = tick.TranslatePoint(new Point(tick.ActualWidth / 2, tick.ActualHeight / 2), picker.Lists);
            var hit = picker.Lists.InputHitTest(mid) as UIElement;
            var face = hit ?? (UIElement)VisualTreeHelper.GetChild(tick, 0);
            if (i < 3) log.Add($"  under the {kind} switch: {hit?.GetType().Name} (on its row: {hit != null && hit.IsDescendantOf((Visual)tick.Parent) || ReferenceEquals(hit, tick.Parent)})");
            Press(face);
            await Task.Delay(250);
            bool flipped = (tick.IsChecked == true) != was && app.Has(kind) != was;
            // and back, this time by the row's name
            var row = (FrameworkElement)tick.Parent;
            var name = Under<System.Windows.Controls.TextBlock>(row).First();
            Press(name);
            await Task.Delay(250);
            bool back = (tick.IsChecked == true) == was && app.Has(kind) == was;
            if (!flipped || !back) bad++;
            log.Add($"{kind,-11} was={was} switch-press -> {(flipped ? "ok" : "NOTHING")}, name-press -> {(back ? "ok" : "NOTHING")}  on={tick.IsChecked} has={app.Has(kind)}");
        }
        log.Add(bad == 0 ? "ALL SWITCHES WORK" : $"{bad} SWITCHES BROKEN");
        picker.Close();

        static void Press(UIElement on)
        {
            var down = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
                { RoutedEvent = System.Windows.Input.Mouse.MouseDownEvent };
            on.RaiseEvent(down);
            var up = new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, Environment.TickCount, System.Windows.Input.MouseButton.Left)
                { RoutedEvent = System.Windows.Input.Mouse.MouseUpEvent };
            on.RaiseEvent(up);
        }
    }

    static IEnumerable<T> Under<T>(DependencyObject top) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(top); i++)
        {
            var child = VisualTreeHelper.GetChild(top, i);
            if (child is T found) yield return found;
            foreach (var deeper in Under<T>(child)) yield return deeper;
        }
    }

    /// <summary>The Start menu switch, clicked through its automation peer exactly as a mouse click toggles it.</summary>
    static async Task StartMenuSwitch(List<string> log)
    {
        bool before = Core.StartMenu.There;
        var picker = new PickerWindow { Left = Far, Top = Far };
        picker.Show();
        await Task.Delay(800);
        var toggle = (System.Windows.Automation.Provider.IToggleProvider)
            new System.Windows.Automation.Peers.ToggleButtonAutomationPeer(picker.InStart);
        log.Add($"start menu switch at open: on={picker.InStart.IsChecked}, shortcut there={Core.StartMenu.There}");
        for (int i = 0; i < 4; i++)
        {
            toggle.Toggle();
            await Task.Delay(400);
            log.Add($"click {i + 1}: on={picker.InStart.IsChecked}, shortcut there={Core.StartMenu.There}");
        }
        picker.Close();
        var again = new PickerWindow { Left = Far, Top = Far };
        again.Show();
        await Task.Delay(500);
        log.Add($"reopened picker: on={again.InStart.IsChecked}, shortcut there={Core.StartMenu.There}");
        again.Close();
        Core.StartMenu.Set(before);
        log.Add($"put back as it was: shortcut there={Core.StartMenu.There} (was {before})");
    }

    /// <summary>The games card three ways: Pitzki's real last game, a made-up CS2 match, and no Pitzki at all.</summary>
    static async Task LastGameCheck(string dir, List<string> log)
    {
        async Task Show(string name)
        {
            var window = new WidgetWindow(Sample("robrun"));
            window.Show();
            await Task.Delay(1800);
            log.Add($"{name}: {Math.Round(window.ActualWidth)}x{Math.Round(window.ActualHeight)} | {Words(window)}");
            int faded = Faded(window);
            if (faded > 0) log.Add($"  {name}: {faded} piece(s) left faded (expect 0)");
            Snap(window, Path.Combine(dir, $"{name}.png"));
            window.Close();
        }

        Core.LastGame.Pretend = null;
        await Show("lastgame-real");

        string fake = Path.Combine(dir, "lastgame-test.json");
        File.WriteAllText(fake, System.Text.Json.JsonSerializer.Serialize(new
        {
            MatchSummary = true, LastMatchGame = "Counter-Strike 2", LastMatchHead = "Won 13:9 on Mirage",
            LastMatchDetail = "24 kills · 15 deaths · MVP x4", LastMatchWhen = DateTime.Today.AddDays(-1).AddHours(22).AddMinutes(40).Ticks,
            LastMatchIcon = "", LastMatchMore = "Counter-Strike 2 · Mirage · 13:9\nADR 92 · HS 48%\nPremier · 38 min",
        }));
        Core.LastGame.Pretend = fake;
        await Show("lastgame-cs2");

        Core.LastGame.Pretend = Path.Combine(dir, "no-such-pitzki.json");
        await Show("lastgame-none");

        File.WriteAllText(fake, "{ \"MatchSummary\": false, \"LastMatchHead\": \"x\" }");
        Core.LastGame.Pretend = fake;
        log.Add($"summary switched off in Pitzki -> nothing: {Core.LastGame.Parse(File.ReadAllText(fake)) == null}");
        Core.LastGame.Pretend = null;
    }

    /// <summary>
    /// Pitzki's scenes (chocolate milk, game, TV, gum, juggling): each one filmed on a big mascot off screen
    /// into a contact sheet, checked that he is back exactly as before, one stopped half-way, and the real
    /// card left alone until he starts one by himself.
    /// </summary>
    static async Task LifeCheck(string dir, List<string> log)
    {
        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") == "night") goto Night;
        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") == "minute")
        {
            // the real card, left alone for a while: when does each scene start (expect about a minute apart)?
            PitzkiWidget.FirstWait = 1;
            var lone = new WidgetWindow(Sample("pitzki"));
            lone.Show();
            await Task.Delay(500);
            var pitzki = FindMascot(lone)!;
            var watch = System.Diagnostics.Stopwatch.StartNew();
            string was = "";
            double lastStart = -1;
            while (watch.Elapsed.TotalSeconds < 150)
            {
                if (pitzki.Hobby != was)
                {
                    if (pitzki.Hobby != "")
                    {
                        log.Add($"minute: {watch.Elapsed.TotalSeconds:0.0}s start '{pitzki.Hobby}'" + (lastStart >= 0 ? $" ({watch.Elapsed.TotalSeconds - lastStart:0.0}s after the last start)" : ""));
                        lastStart = watch.Elapsed.TotalSeconds;
                    }
                    else log.Add($"minute: {watch.Elapsed.TotalSeconds:0.0}s '{was}' ended");
                    was = pitzki.Hobby;
                }
                await Task.Delay(200);
            }
            lone.Close();
            PitzkiWidget.FirstWait = -1;
            return;
        }
        var him = new Pitzki.Controls.Mascot { Width = 300, Height = 240 };
        var stage = new System.Windows.Controls.Grid { Width = 300, Height = 240, Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x1D, 0x26)) };
        stage.Children.Add(him);
        var window = new Window
        {
            WindowStyle = WindowStyle.None, ResizeMode = ResizeMode.NoResize, ShowInTaskbar = false, ShowActivated = false,
            Left = -20000, Top = -20000, SizeToContent = SizeToContent.WidthAndHeight, Content = stage,
        };
        window.Show();
        await Task.Delay(600);

        // PITZKI_LIFE=case opens one case of every grade instead of playing every scene once
        var runs = Environment.GetEnvironmentVariable("PITZKI_LIFE") switch
        {
            "case" => new[] { "blue", "purple", "pink", "red", "gold" }.Select(g => ("case", g)).ToArray(),
            // PITZKI_LIFE=new plays only the second batch of scenes (and the three kept for a time)
            "new" => new[] { "pizza", "guitar", "skate", "hoops", "balloon", "phone", "coffee", "movie", "birthday" }.Select(h => (h, "")).ToArray(),
            "reactions" or "beat" or "click" or "thirdcard" or "fourthcard" or "cards" or "roam" => Array.Empty<(string, string)>(),
            "fourth" => new[] { "pancake", "sweep", "bowling", "telescope", "drums", "yoyo", "alien" }.Select(h => (h, "")).ToArray(),
            "third" => new[] { "fishing", "paint", "yoga", "kite", "cube", "cat", "umbrella", "popsicle", "hanukkah", "christmas", "purim", "newyear" }.Select(h => (h, "")).ToArray(),
            "greet" => new[] { ("hello", ""), ("boot", "") },
            _ => Pitzki.Controls.Mascot.Hobbies.Concat(Pitzki.Controls.Mascot.Special).Select(h => (h, "")).ToArray(),
        };
        foreach (var (kind, force) in runs)
        {
            var frames = new List<RenderTargetBitmap>();
            Pitzki.Controls.Mascot.CaseForce = force;
            bool started = him.StartHobby(kind);
            Pitzki.Controls.Mascot.CaseForce = "";
            log.Add($"life {kind} {force}: started={started}");
            var clock = System.Diagnostics.Stopwatch.StartNew();
            while (him.Hobby != "" && clock.Elapsed.TotalSeconds < 20)
            {
                var shot = new RenderTargetBitmap(300, 240, 96, 96, PixelFormats.Pbgra32);
                shot.Render(stage);
                frames.Add(shot);
                await Task.Delay(500);
            }
            log.Add($"  ran {clock.Elapsed.TotalSeconds:0.0}s, {frames.Count} frames");
            if (force == "gold")
            {
                // a knife from the case goes on into that knife's tricks: film those too
                await Task.Delay(300);
                log.Add($"  then by itself: '{him.Hobby}' (expect karambit or balisong)");
                while (him.Hobby != "" && clock.Elapsed.TotalSeconds < 30)
                {
                    var shot = new RenderTargetBitmap(300, 240, 96, 96, PixelFormats.Pbgra32);
                    shot.Render(stage);
                    frames.Add(shot);
                    await Task.Delay(500);
                }
            }
            await Task.Delay(1800); // the last sparkles and words finish floating away
            log.Add($"  after: {him.LifeLeftovers()}");

            // a contact sheet, six frames to a row, each 200x160
            int cols = 6, rows = (frames.Count + cols - 1) / cols;
            var sheet = new DrawingVisual();
            using (var dc = sheet.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, cols * 200, rows * 160));
                for (int i = 0; i < frames.Count; i++)
                {
                    var spot = new Rect(i % cols * 200, i / cols * 160, 200, 160);
                    dc.DrawImage(frames[i], spot);
                    dc.DrawText(new FormattedText($"{i * 0.5:0.0}s", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"), 11, Brushes.Gray, 1), new Point(spot.X + 4, spot.Y + 2));
                }
            }
            var picture = new RenderTargetBitmap(cols * 200, Math.Max(rows, 1) * 160, 96, 96, PixelFormats.Pbgra32);
            picture.Render(sheet);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(picture));
            using (var file = File.Create(Path.Combine(dir, force == "" ? $"life-{kind}.png" : $"life-{kind}-{force}.png"))) encoder.Save(file);
        }

        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") == "click")
        {
            // a real click through Windows (cursor moved there, button pressed and let go) on a card asleep at 2 at night
            window.Close();
            PitzkiWidget.Greeted = true;
            PitzkiWidget.FirstWait = 1000;
            var clickBegan = DateTime.Now;
            PitzkiWidget.Clock = () => DateTime.Today.AddHours(2) + (DateTime.Now - clickBegan);
            var placed = Sample("pitzki");
            placed.X = 300; placed.Y = 300;
            var sleeper = new WidgetWindow(placed);
            sleeper.Show();
            await Task.Delay(2500);
            var who = Find<PitzkiWidget>(sleeper)!;
            var face = FindMascot(sleeper)!;
            log.Add($"click: before: mood {face.Mood}, clicks {who.Clicks}, window at {sleeper.Left},{sleeper.Top} {sleeper.ActualWidth}x{sleeper.ActualHeight}");
            Snap(sleeper, Path.Combine(dir, "click-before.png"));
            var centre = sleeper.PointToScreen(new Point(sleeper.ActualWidth / 2, sleeper.ActualHeight / 2 - 10));
            await RealClick((int)centre.X, (int)centre.Y);
            await Task.Delay(1500);
            log.Add($"click: after: mood {face.Mood} (expect Happy), clicks {who.Clicks} (expect 1), downs {who.Downs}, ups {who.Ups}, {UnderPoint}, host={(Window.GetWindow(who) == sleeper)}, window still at {sleeper.Left},{sleeper.Top}");
            Snap(sleeper, Path.Combine(dir, "click-after.png"));
            await Task.Delay(3000);
            await RealClick((int)centre.X, (int)centre.Y);
            await Task.Delay(800);
            log.Add($"click again (awake): clicks {who.Clicks} (expect 2), mood {face.Mood}");
            sleeper.Close();
            PitzkiWidget.Clock = () => DateTime.Now;
            PitzkiWidget.FirstWait = -1;
            return;
        }

        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") == "beat")
        {
            window.Close();
            // whatever really plays on this computer: does the card hear the tempo and dance on its beats?
            PitzkiWidget.Greeted = true;
            PitzkiWidget.FirstWait = 8; // a scene comes in the middle: he must dance, play it, and dance again
            var dancer = new WidgetWindow(Sample("pitzki"));
            dancer.Show();
            var who = Find<PitzkiWidget>(dancer)!;
            for (int i = 0; i < 16; i++)
            {
                await Task.Delay(2000);
                var m = FindMascot(dancer)!;
                log.Add($"beat +{(i + 1) * 2}s: dancing={m.Dancing}, scene='{m.Hobby}', bpm={who.HeardBpm:0.0}, beats danced={who.BeatsDanced}");
                if (i == 3) log.Add("  " + m.Blocked() + " | " + who.LifeState);
                if (i == 1) Snap(dancer, Path.Combine(dir, "beat-dancing.png"));
                if (i == 5) Snap(dancer, Path.Combine(dir, "beat-scene.png"));
            }
            Snap(dancer, Path.Combine(dir, "beat-card.png"));
            dancer.Close();
            PitzkiWidget.FirstWait = -1;
            return;
        }

        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") == "roam")
        {
            // a real walk across the real desktop: he is drawn on top of everything, so this is seen on screen
            window.Close();
            PitzkiWidget.Greeted = true;
            PitzkiWidget.FirstWait = 1000;
            var beganR = DateTime.Now;
            PitzkiWidget.Clock = () => DateTime.Today.AddHours(15) + (DateTime.Now - beganR);
            PitzkiWidget.IdleForTest = 0;
            Core.Roam.SpeedForTest = 2;
            var placedR = Sample("pitzki");
            placedR.X = 900; placedR.Y = 200;
            var cardR = new WidgetWindow(placedR);
            var buddy = Sample("clock");
            buddy.X = 400; buddy.Y = 500;
            var buddyCard = new WidgetWindow(buddy);
            cardR.Show();
            buddyCard.Show();
            await Task.Delay(2000);
            var wR = Find<PitzkiWidget>(cardR)!;
            var mR = FindMascot(cardR)!;
            log.Add($"before: card mascot visible={mR.IsVisible}, buddy card at {buddyCard.Left},{buddyCard.Top}");
            wR.GoRoam();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int shot = 0;
            await Task.Delay(1500);
            log.Add($"out: roaming={wR.Roaming}, card mascot visible={mR.IsVisible} (expect False), words '{Words(cardR)}'");
            while (wR.Roaming && watch.Elapsed.TotalSeconds < 120)
            {
                await Task.Delay(4000);
                if (shot < 12) ScreenShot(Path.Combine(dir, $"roam-{++shot:00}.png"));
            }
            await Task.Delay(1500);
            log.Add($"back after {watch.Elapsed.TotalSeconds:0}s: roaming={wR.Roaming}, card mascot visible={mR.IsVisible} (expect True), buddy card at {buddyCard.Left},{buddyCard.Top} (expect where it was)");
            foreach (var line in Core.Roam.LastLog) log.Add("  walk: " + line);
            log.Add($"  windows he could sit on now: see walk lines");
            cardR.Close();
            buddyCard.Close();
            Core.Roam.SpeedForTest = 1;
            PitzkiWidget.IdleForTest = null;
            PitzkiWidget.Clock = () => DateTime.Now;
            PitzkiWidget.FirstWait = -1;
            return;
        }

        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") == "cards")
        {
            // every reaction to the other cards on the big mascot: one picture each, half-way, and clean after
            var kinds = new[] { "full", "rich", "poor", "song", "hour", "news", "clip", "usage", "win", "lose", "played", "peek", "push", "hug", "bye" };
            var shots = new List<RenderTargetBitmap>();
            foreach (var kind in kinds)
            {
                him.React(kind, kind is "bye" or "hug" or "push" ? -1 : 0.6, -0.2);
                await Task.Delay(900);
                var shot = new RenderTargetBitmap(300, 240, 96, 96, PixelFormats.Pbgra32);
                shot.Render(stage);
                shots.Add(shot);
                await Task.Delay(2600);
                log.Add($"react {kind}: then {him.LifeLeftovers()}");
            }
            var sheet = new DrawingVisual();
            using (var dc = sheet.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, 1000, 480));
                for (int i = 0; i < shots.Count; i++)
                {
                    var spot = new Rect(i % 5 * 200, i / 5 * 160, 200, 160);
                    dc.DrawImage(shots[i], spot);
                    dc.DrawText(new FormattedText(kinds[i], System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 12, Brushes.Gray, 1), new Point(spot.X + 4, spot.Y + 2));
                }
            }
            var all = new RenderTargetBitmap(1000, 480, 96, 96, PixelFormats.Pbgra32);
            all.Render(sheet);
            var enc5 = new PngBitmapEncoder();
            enc5.Frames.Add(BitmapFrame.Create(all));
            using (var file = File.Create(Path.Combine(dir, "life-cards.png"))) enc5.Save(file);
            window.Close();

            // the real card: what the other cards tell reaches him, one reaction at a time
            PitzkiWidget.Greeted = true;
            PitzkiWidget.FirstWait = 1000;
            var began5 = DateTime.Now;
            PitzkiWidget.Clock = () => DateTime.Today.AddHours(15) + (DateTime.Now - began5);
            PitzkiWidget.IdleForTest = 0;
            var card5 = new WidgetWindow(Sample("pitzki"));
            card5.Show();
            await Task.Delay(2000);
            var w5 = Find<PitzkiWidget>(card5)!;
            var m5 = FindMascot(card5)!;
            async Task Expect(string name, Action act)
            {
                await Task.Delay(500);
                act();
                await Task.Delay(1600);
                log.Add($"card: {name} -> busy={m5.Busy} (expect True), waiting {w5.ReactionsWaiting}");
                await Task.Delay(3200);
            }
            await Expect("game update", () => Core.Buzz.Tell("news"));
            await Expect("new clip", () => Core.Buzz.Tell("clip"));
            await Expect("claude 92%", () => Core.Buzz.Tell("usage", null, 92));
            await Expect("card removed", () => Core.Buzz.Tell("removed", null, 0, "clock"));
            await Expect("card joined to his", () => Core.Buzz.Tell("joined", w5, 0, "clock|pitzki"));
            await Expect("drive C 1 GB free", () => PitzkiWidget.FreeGbForTest = 1);
            PitzkiWidget.FreeGbForTest = null;
            PitzkiWidget.MoneyForTest = 100;
            await Task.Delay(1500);
            await Expect("worth up 10%", () => PitzkiWidget.MoneyForTest = 110);
            PitzkiWidget.MoneyForTest = null;
            Core.Buzz.Tell("near", w5);
            await Task.Delay(1000);
            log.Add($"card held over his: busy={m5.Busy} (expect True, peeking)");
            await Task.Delay(2600);
            log.Add($"still held there: busy={m5.Busy} (expect True, pushing)");
            Core.Buzz.Tell("away", w5);
            await Task.Delay(2500);
            card5.Close();
            foreach (var (words, expect) in new[] { ("Won 13:9 on Mirage", "win"), ("Lost 8:13 on Inferno", "lose"), ("Victory Royale #1", "win"), ("Illegal Soccer 14 min", "played") })
                log.Add($"game result '{words}': {PitzkiWidget.GameResult(words)} (expect {expect})");
            PitzkiWidget.IdleForTest = null;
            PitzkiWidget.Clock = () => DateTime.Now;
            PitzkiWidget.FirstWait = -1;
            return;
        }

        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") is "fourth" or "fourthcard")
        {
            foreach (var (name, act) in new (string, Action)[] { ("battery", him.LowBattery), ("pose", him.Pose), ("bubble", () => him.Bubble("is it lunch yet?")) })
            {
                act();
                await Task.Delay(name == "pose" ? 500 : 1000);
                SnapStage(stage, Path.Combine(dir, $"react-{name}.png"));
                await Task.Delay(4200);
                log.Add($"{name} then: {him.LifeLeftovers()}");
            }
            window.Close();

            PitzkiWidget.Greeted = true;
            PitzkiWidget.FirstWait = 1000;
            var began4 = DateTime.Now;
            PitzkiWidget.Clock = () => DateTime.Today.AddHours(15) + (DateTime.Now - began4);
            PitzkiWidget.IdleForTest = 0;
            var placed4 = Sample("pitzki");
            placed4.X = 300; placed4.Y = 300;
            var card4 = new WidgetWindow(placed4);
            card4.Show();
            await Task.Delay(1500);
            var w4 = Find<PitzkiWidget>(card4)!;
            var m4 = FindMascot(card4)!;
            PitzkiWidget.IdleForTest = 700;
            await Task.Delay(1500);
            log.Add($"nobody at the computer 11+ min: mood {m4.Mood} (expect Sleepy), words '{Words(card4)}'");
            Snap(card4, Path.Combine(dir, "react-doze.png"));
            PitzkiWidget.IdleForTest = 0;
            await Task.Delay(1500);
            log.Add($"back at the computer: mood {m4.Mood} (expect Happy)");
            await Task.Delay(1500);
            PitzkiWidget.BatteryForTest = 10;
            await Task.Delay(1500);
            log.Add($"battery 10%: busy={m4.Busy} (expect True)");
            PitzkiWidget.BatteryForTest = null;
            await Task.Delay(3200);
            Core.KeyCount.PrintScreens++;
            await Task.Delay(1200);
            log.Add($"print screen: busy={m4.Busy} (expect True)");
            await Task.Delay(2600);
            PitzkiWidget.ChatSoonForTest = true;
            await Task.Delay(1300);
            log.Add($"chatter: bubble up={m4.HasBubble} (expect True)");
            Snap(card4, Path.Combine(dir, "react-chatter-card.png"));
            await Task.Delay(4500);
            // eyes on the pointer: the real cursor far to the left of the card, then far to the right
            var c4 = card4.PointToScreen(new Point(card4.ActualWidth / 2, card4.ActualHeight / 2));
            var was4 = System.Windows.Forms.Cursor.Position;
            SetCursorPos((int)c4.X - 700, (int)c4.Y);
            await Task.Delay(700);
            var left4 = m4.EyesAt;
            SetCursorPos((int)c4.X + 700, (int)c4.Y + 300);
            await Task.Delay(700);
            var right4 = m4.EyesAt;
            SetCursorPos(was4.X, was4.Y);
            log.Add($"eyes: pointer far left -> x {left4.X:0.0} (expect about -3.2); far right below -> x {right4.X:0.0}, y {right4.Y:0.0} (expect +3.2, +2.4)");
            card4.Close();
            PitzkiWidget.AlienForTest = true;
            log.Add($"rare scene: first pick '{new PitzkiWidget().NextScene()}' (expect alien)");
            PitzkiWidget.AlienForTest = false;
            PitzkiWidget.IdleForTest = null;
            PitzkiWidget.Clock = () => DateTime.Now;
            PitzkiWidget.FirstWait = -1;
            return;
        }

        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") is "third" or "thirdcard")
        {
            // the four new reactions on the big mascot, each snapped half-way and checked after
            foreach (var (name, act) in new (string, Action)[] { ("type", him.TypeAlong), ("cheer", him.Cheer), ("mail", him.Mail), ("shh", him.Shh) })
            {
                act();
                await Task.Delay(1000);
                SnapStage(stage, Path.Combine(dir, $"react-{name}.png"));
                await Task.Delay(2600);
                log.Add($"{name} then: {him.LifeLeftovers()}");
            }
            him.Headset = true;
            await Task.Delay(800);
            SnapStage(stage, Path.Combine(dir, "react-headset.png"));
            him.Headset = false;
            await Task.Delay(800);
            log.Add($"headset off then: {him.LifeLeftovers()}");
            window.Close();

            // the real card: each signal starts the right reaction
            PitzkiWidget.Greeted = true;
            PitzkiWidget.FirstWait = 1000;
            var thirdBegan = DateTime.Now;
            PitzkiWidget.Clock = () => DateTime.Today.AddHours(15) + (DateTime.Now - thirdBegan); // awake: the afternoon
            var thirdCard = new WidgetWindow(Sample("pitzki"));
            thirdCard.Show();
            await Task.Delay(1500);
            var who = Find<PitzkiWidget>(thirdCard)!;
            var m = FindMascot(thirdCard)!;
            Core.KeyCount.Pretend = 20;
            await Task.Delay(1500);
            log.Add($"typing fast: busy={m.Blocked().Contains("busy=True")} (expect True)");
            Core.KeyCount.Pretend = 0;
            await Task.Delay(3200);
            who.MessageFrom("Discord");
            await Task.Delay(1500);
            log.Add($"discord message: busy={m.Blocked().Contains("busy=True")} (expect True)");
            await Task.Delay(3000);
            who.MessageFrom("Mail");
            await Task.Delay(1500);
            log.Add($"other app message: busy={m.Blocked().Contains("busy=True")} (expect False)");
            PitzkiWidget.MutedForTest = false;
            await Task.Delay(1200);
            PitzkiWidget.MutedForTest = true;
            await Task.Delay(1500);
            log.Add($"muted: busy={m.Blocked().Contains("busy=True")} (expect True)");
            await Task.Delay(2600);
            PitzkiWidget.GameForTest = "cs2";
            await Task.Delay(6000);
            log.Add($"game on: headset={m.Headset} (expect True)");
            Snap(thirdCard, Path.Combine(dir, "react-game-card.png"));
            PitzkiWidget.GameForTest = "";
            await Task.Delay(6000);
            log.Add($"game off: headset={m.Headset} (expect False)");
            thirdCard.Close();
            PitzkiWidget.GameForTest = null; PitzkiWidget.MutedForTest = null; Core.KeyCount.Pretend = null;

            // holidays by date, and the weather's picks
            foreach (var (day, expect) in new[] { (new DateTime(2026, 12, 5), "hanukkah"), (new DateTime(2026, 12, 12), "hanukkah"), (new DateTime(2026, 12, 13), ""),
                (new DateTime(2026, 12, 25), "christmas"), (new DateTime(2027, 1, 1), "newyear"), (new DateTime(2026, 9, 12), "newyear"),
                (new DateTime(2027, 3, 23), "purim"), (new DateTime(2026, 11, 14), "") })
                log.Add($"holiday {day:yyyy-MM-dd}: '{PitzkiWidget.Holiday(day)}' (expect '{expect}')");
            var w = new PitzkiWidget();
            PitzkiWidget.Clock = () => new DateTime(2026, 11, 16, 15, 0, 0);
            PitzkiWidget.WeatherForTest = "rain";
            log.Add($"rain picks: umbrella {Enumerable.Range(0, 300).Count(_ => w.NextScene() == "umbrella")} of 300 (expect about 100)");
            PitzkiWidget.WeatherForTest = "hot";
            log.Add($"hot picks: popsicle {Enumerable.Range(0, 300).Count(_ => w.NextScene() == "popsicle")} of 300 (expect about 100)");
            PitzkiWidget.WeatherForTest = null;
            PitzkiWidget.Clock = () => new DateTime(2026, 12, 6, 15, 0, 0);
            log.Add($"hanukkah day, first pick: '{new PitzkiWidget().NextScene()}' (expect hanukkah)");
            PitzkiWidget.Clock = () => DateTime.Now;
            PitzkiWidget.FirstWait = -1;
            return;
        }

        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") == "greet")
        {
            window.Close();
            // the first card of a run greets: "boot" when the computer has only just come on, else "hello"
            foreach (var (uptime, expect) in new[] { (60.0, "boot"), (7200.0, "hello") })
            {
                PitzkiWidget.Greeted = false;
                PitzkiWidget.UptimeForTest = uptime;
                var greeter = new WidgetWindow(Sample("pitzki"));
                greeter.Show();
                await Task.Delay(1200);
                log.Add($"card opened, computer on {uptime}s: playing '{FindMascot(greeter)!.Hobby}' (expect {expect})");
                Snap(greeter, Path.Combine(dir, $"greet-card-{expect}.png"));
                var again = new WidgetWindow(Sample("pitzki"));
                again.Show();
                await Task.Delay(800);
                log.Add($"  a second card: playing '{FindMascot(again)!.Hobby}' (expect nothing)");
                again.Close();
                await Task.Delay(6000);
                greeter.Close();
            }
            PitzkiWidget.UptimeForTest = null;
            return;
        }

        if (Environment.GetEnvironmentVariable("PITZKI_LIFE") is "new" or "reactions")
        {
            // the reactions, on a big mascot: dizzy and fanning, each snapped half-way and checked after
            him.Woozy();
            await Task.Delay(700);
            SnapStage(stage, Path.Combine(dir, "react-woozy.png"));
            await Task.Delay(2200);
            log.Add($"woozy then: {him.LifeLeftovers()}");
            him.Fan();
            await Task.Delay(900);
            SnapStage(stage, Path.Combine(dir, "react-fan.png"));
            await Task.Delay(2600);
            log.Add($"fan then: {him.LifeLeftovers()}");
            window.Close();

            // the real card: music on -> dancing, no scenes; a busy processor -> he fans himself
            PitzkiWidget.FirstWait = 1;
            PitzkiWidget.MusicForTest = true;
            var card2 = new WidgetWindow(Sample("pitzki"));
            card2.Show();
            await Task.Delay(4000);
            var m2 = FindMascot(card2)!;
            log.Add($"music on: dancing={m2.Dancing} scene='{m2.Hobby}' (expect True and none)");
            Snap(card2, Path.Combine(dir, "react-dance.png"));
            PitzkiWidget.MusicForTest = false;
            PitzkiWidget.CpuForTest = 0.95;
            await Task.Delay(1500);
            log.Add($"music off: dancing={m2.Dancing}");
            card2.Close();
            PitzkiWidget.FirstWait = 1000;
            var card3 = new WidgetWindow(Sample("pitzki"));
            card3.Show();
            await Task.Delay(6400);
            Snap(card3, Path.Combine(dir, "react-hot-card.png"));
            log.Add($"cpu 95% for 6 s: busy fanning={FindMascot(card3)!.LifeLeftovers().Contains("busy")}");
            card3.Close();
            PitzkiWidget.CpuForTest = null;

            // which scene when: 8 in the morning on a Friday, and his birthday
            var w = new PitzkiWidget();
            PitzkiWidget.Clock = () => new DateTime(2026, 11, 13, 8, 0, 0);
            var picks = Enumerable.Range(0, 300).Select(_ => w.NextScene()).ToList();
            log.Add($"friday 8:00 picks: coffee {picks.Count(p => p == "coffee")}, cocoa {picks.Count(p => p == "cocoa")}, movie {picks.Count(p => p == "movie")}, tv {picks.Count(p => p == "tv")} (expect cocoa 0, tv 0)");
            PitzkiWidget.Clock = () => new DateTime(2026, 11, 16, 15, 0, 0);
            picks = Enumerable.Range(0, 300).Select(_ => w.NextScene()).ToList();
            log.Add($"monday 15:00 picks: coffee {picks.Count(p => p == "coffee")}, cocoa {picks.Count(p => p == "cocoa")}, movie {picks.Count(p => p == "movie")}, tv {picks.Count(p => p == "tv")} (expect coffee 0, movie 0)");
            PitzkiWidget.BirthdayForTest = "16/11";
            var w2 = new PitzkiWidget();
            log.Add($"birthday: first pick '{w2.NextScene()}' (expect birthday)");
            PitzkiWidget.BirthdayForTest = null;
            PitzkiWidget.Clock = () => DateTime.Now;
            PitzkiWidget.FirstWait = -1;
            return;
        }

        // stopped half-way: he must still put things away with a move and end up clean
        him.StartHobby("tv");
        await Task.Delay(3000);
        him.StopHobby();
        var mid = new RenderTargetBitmap(300, 240, 96, 96, PixelFormats.Pbgra32);
        await Task.Delay(200);
        mid.Render(stage);
        var enc2 = new PngBitmapEncoder();
        enc2.Frames.Add(BitmapFrame.Create(mid));
        using (var file = File.Create(Path.Combine(dir, "life-stopping.png"))) enc2.Save(file);
        await Task.Delay(1800);
        log.Add($"life stopped half-way: hobby='{him.Hobby}', {him.LifeLeftovers()}");

        // busy with something else: a scene must not start
        him.Prop = "ak";
        await Task.Delay(800);
        log.Add($"life while holding a gun: started={him.StartHobby("cocoa")} (expect False)");
        him.Prop = "";
        window.Close();

        // the real card, left alone: he starts something by himself
        PitzkiWidget.FirstWait = 1;
        var card = new WidgetWindow(Sample("pitzki"));
        card.Show();
        await Task.Delay(5200);
        var body = FindMascot(card);
        log.Add($"life card: playing '{body?.Hobby}' by himself (expect one of {string.Join("/", Pitzki.Controls.Mascot.Hobbies)})");
        Snap(card, Path.Combine(dir, "life-card.png"));
        card.Close();
        PitzkiWidget.FirstWait = -1;

    Night:
        // night: asleep; a click wakes him; near the end he nods off; then asleep again.
        // The test clock says 2 at night, and the 10 + 2 minutes are cut to 40 + 30 seconds.
        var began = DateTime.Now;
        PitzkiWidget.Clock = () => DateTime.Today.AddHours(2) + (DateTime.Now - began);
        PitzkiWidget.AwakeFor = 40;
        PitzkiWidget.NodOffFor = 30;
        try
        {
            var night = new WidgetWindow(Sample("pitzki"));
            night.Show();
            await Task.Delay(2000);
            var who = Find<PitzkiWidget>(night)!;
            var face = FindMascot(night)!;
            log.Add($"night: mood {face.Mood} (expect Sleepy), words '{Words(night)}'");
            Snap(night, Path.Combine(dir, "night-asleep.png"));

            var woke = DateTime.Now;
            who.Wake();
            await Task.Delay(700);
            log.Add($"night clicked: mood {face.Mood} (expect Happy), drowsy {face.Drowsy:0.00} (expect 0), words '{Words(night)}'");
            Snap(night, Path.Combine(dir, "night-woken.png"));

            int shot = 0;
            foreach (double at in new[] { 12.0, 19.0, 26.0, 33.0, 37.0 })
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, at - (DateTime.Now - woke).TotalSeconds)));
                log.Add($"night +{at}s: mood {face.Mood}, drowsy {face.Drowsy:0.00}, words '{Words(night)}'");
                Snap(night, Path.Combine(dir, $"night-nodding-{++shot}.png"));
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, 42 - (DateTime.Now - woke).TotalSeconds)));
            log.Add($"night +42s: mood {face.Mood} (expect Sleepy), drowsy {face.Drowsy:0.00} (expect 0), words '{Words(night)}'");
            Snap(night, Path.Combine(dir, "night-asleep-again.png"));
            night.Close();
        }
        finally
        {
            PitzkiWidget.Clock = () => DateTime.Now;
            PitzkiWidget.AwakeFor = 1800;
            PitzkiWidget.NodOffFor = 120;
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetCursorPos(int x, int y);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern void mouse_event(uint flags, int dx, int dy, uint data, IntPtr extra);

    /// <summary>A genuine click through Windows at that screen point; the cursor goes back where it was.</summary>
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr WindowFromPoint(System.Drawing.Point p);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetClassName(IntPtr h, System.Text.StringBuilder b, int n);
    internal static string UnderPoint = "";

    static async Task RealClick(int x, int y)
    {
        var h = WindowFromPoint(new System.Drawing.Point(x, y));
        GetWindowThreadProcessId(h, out uint pid);
        var cls = new System.Text.StringBuilder(128); GetClassName(h, cls, 128);
        UnderPoint = $"under {x},{y}: {cls} (pid {pid}, me {Environment.ProcessId})";
        // from another thread, with real pauses, so the card's own thread is free to take each message as it comes
        await Task.Run(() =>
        {
            var was = System.Windows.Forms.Cursor.Position;
            SetCursorPos(x - 8, y - 4);
            System.Threading.Thread.Sleep(150);
            SetCursorPos(x, y);
            System.Threading.Thread.Sleep(250);
            mouse_event(0x0002, 0, 0, 0, IntPtr.Zero); // left down
            System.Threading.Thread.Sleep(90);
            mouse_event(0x0004, 0, 0, 0, IntPtr.Zero); // left up
            System.Threading.Thread.Sleep(400);
            SetCursorPos(was.X, was.Y);
        });
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] static extern bool BitBlt(IntPtr dst, int x, int y, int w, int h, IntPtr src, int sx, int sy, int rop);

    /// <summary>The whole desktop (both screens), layered windows included, scaled down by half.</summary>
    static void ScreenShot(string path)
    {
        var all = System.Windows.Forms.SystemInformation.VirtualScreen;
        using var bmp = new System.Drawing.Bitmap(all.Width, all.Height);
        using (var g = System.Drawing.Graphics.FromImage(bmp))
        {
            var dst = g.GetHdc();
            var src = GetDC(IntPtr.Zero);
            BitBlt(dst, 0, 0, all.Width, all.Height, src, all.X, all.Y, 0x00CC0020 | 0x40000000); // SRCCOPY | CAPTUREBLT
            ReleaseDC(IntPtr.Zero, src);
            g.ReleaseHdc(dst);
        }
        using var half = new System.Drawing.Bitmap(bmp, all.Width / 2, all.Height / 2);
        half.Save(path, System.Drawing.Imaging.ImageFormat.Png);
    }

    static void SnapStage(FrameworkElement stage, string path)
    {
        var shot = new RenderTargetBitmap((int)stage.ActualWidth, (int)stage.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        shot.Render(stage);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(shot));
        using var file = File.Create(path);
        encoder.Save(file);
    }

    static T? Find<T>(DependencyObject root) where T : DependencyObject
    {
        if (root is T hit) return hit;
        if (root is Window w && w.Content is DependencyObject c) return Find<T>(c);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (Find<T>(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }

    static Pitzki.Controls.Mascot? FindMascot(DependencyObject root)
    {
        if (root is Pitzki.Controls.Mascot m) return m;
        if (root is Window w && w.Content is DependencyObject c) return FindMascot(c);
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (FindMascot(VisualTreeHelper.GetChild(root, i)) is { } found) return found;
        return null;
    }

    public static async void Run(App app)
    {
        var dir = Path.Combine(Path.GetTempPath(), "piztki_widgets");
        Directory.CreateDirectory(dir);
        var log = new List<string>();

        // whatever is really on the desktop, kept here and put back at the end
        var had = Board.Load();

        try
        {
            // a city, so the weather card has something to ask about. It is not kept.
            App.Board.WeatherCity = "Tel Aviv";

            // PITZKI_WIDGETS_ONLY=join runs just the joining checks, which is quicker while working on them
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "join")
            {
                try { await Together(app, dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=startmenu flicks the picker's Start menu switch like a click would,
            // on and off, and puts the real Start menu back the way it was
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "startmenu")
            {
                try { await StartMenuSwitch(log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=switches presses every card switch in the picker with mouse down/up, on and back off
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "switches")
            {
                try { await PickerSwitches(app, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=keep drags a joined frame past every edge of a made-up screen
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "keep")
            {
                try { await KeptOnScreen(app, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=stocks: the stocks card with real prices, then a price moving
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "stocks")
            {
                try { await StocksCheck(dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=discord: the Discord card against the real Discord, then a made-up channel
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "discord")
            {
                try { await DiscordCheck(dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=claude: the Claude usage card, made-up answers first, then this user's real numbers
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "claude")
            {
                try { await ClaudeCheck(dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=cs2inv: the CS2 inventory card, from this PC's Steam account, then a price moving
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "cs2inv")
            {
                try { await InventoryCheck(dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=steamitems: the Steam items card, priced for real (a few minutes), then a price moving
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "steamitems")
            {
                try { await SteamItemsCheck(dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=money: every money card with real prices, and the made-up checks
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "money")
            {
                try { await MoneyCheck(dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=skins: the skin cards (shekels, movers, all my Steam, watch list, cases)
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "skins")
            {
                try { await SkinsCheck(dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=tray runs just the tray menu checks
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "tray")
            {
                try { await TrayMenuCheck(app, dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=life films Pitzki's scenes (chocolate milk, game, TV, gum, juggling)
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "life")
            {
                try { await LifeCheck(dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // PITZKI_WIDGETS_ONLY=lastgame shows the games card with Pitzki's real last game, a made-up one, and none
            if (Environment.GetEnvironmentVariable("PITZKI_WIDGETS_ONLY") == "lastgame")
            {
                try { await LastGameCheck(dir, log); }
                catch (Exception bad) { log.Add("BROKE: " + bad); }
                had.Save();
                File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
                Application.Current.Shutdown();
                return;
            }

            // screen time counts into a file of the test's own, with a made-up week, never the real one
            ScreenTimeWidget.File = Path.Combine(dir, "screentime-test.json");
            if (File.Exists(ScreenTimeWidget.File)) File.Delete(ScreenTimeWidget.File);
            foreach (var (prog, minutes) in new[] { ("Google Chrome", 95.0), ("Fortnite", 140.0), ("Visual Studio Code", 52.0), ("Discord", 21.0), ("Spotify", 9.0) })
                ScreenTimeWidget.Counter.Add(DateTime.Today, prog, minutes * 60);
            for (int d = 1; d < 7; d++) ScreenTimeWidget.Counter.Add(DateTime.Today.AddDays(-d), "Google Chrome", (60 + d * 37 % 200) * 60);

            // a backup written out and read back holds the same cards; a file that is not a backup is refused
            {
                var sample = new Core.Board();
                sample.Widgets.Add(new Placed { Kind = "notes", X = 40, Y = 50, Text = "keep me" });
                sample.Widgets.Add(new Placed { Kind = "worldclock", X = 300, Y = 50, Items = new() { "Paris", "Delhi", "Sydney" } });
                string backup = Path.Combine(dir, "backup-test.json");
                bool wrote = sample.Export(backup);
                var readBack = Core.Board.Import(backup);
                log.Add($"backup: wrote={wrote}, read back {readBack?.Widgets.Count} card(s) (expect 2), note says '{readBack?.Widgets[0].Text}' (expect keep me), clock cities {string.Join("/", readBack?.Widgets[1].Items ?? new())} (expect Paris/Delhi/Sydney)");
                string junk = Path.Combine(dir, "not-a-backup.json");
                File.WriteAllText(junk, "{\"hello\": 1}");
                log.Add($"backup: a file that is not one -> {(Core.Board.Import(junk) == null ? "refused" : "taken")} (expect refused)");
            }

            // which programs count as a game for the Game profile
            foreach (var exe in new[] { @"D:\SteamLibrary\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe",
                                        @"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe",
                                        @"C:\Program Files (x86)\Epic Games\Launcher\Portal\Binaries\Win64\EpicGamesLauncher.exe",
                                        @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                                        @"C:\Users\x\AppData\Local\Roblox\Versions\v1\RobloxPlayerBeta.exe" })
                log.Add($"auto game: {Path.GetFileName(exe)} counts={Core.AutoGame.IsGame(exe)}");
            log.Add($"auto game: running now = '{Core.AutoGame.Find()}'");
            // a pretend game (a copy of Notepad inside a steamapps\common folder) puts the Game profile on, and closing it puts the old one back
            if (System.Diagnostics.Process.GetProcessesByName("fakegame").Length > 0)
            {
                string was = App.Board.Profile;
                App.Board.Profile = "Work";
                var auto = new Core.AutoGame(app);
                auto.Poll();
                log.Add($"auto game: with the pretend game open -> playing '{auto.Playing}', profile '{App.Board.Profile}' (expect fakegame, Game)");
                foreach (var fake in System.Diagnostics.Process.GetProcessesByName("fakegame")) { fake.Kill(); fake.WaitForExit(3000); }
                auto.Poll(); string mid = App.Board.Profile;
                auto.Poll();
                log.Add($"auto game: closed -> after one look '{mid}' (expect Game), after two '{App.Board.Profile}' (expect Work)");
                App.Board.Profile = was;
            }

            // ----- every kind, once, with something on it where it keeps something -----
            foreach (var (kind, name, _, _) in WidgetBody.Kinds)
            {
                var placed = Sample(kind);
                var window = new WidgetWindow(placed);
                window.Show();
                await Task.Delay(kind switch { "news" => 8000, "weather" or "stocks" or "chart" or "portfolio" or "indices" or "markets" or "paper" or "guess" or "marketnews" or "networth" or "skinwatch" or "cases" or "cs2inv" or "steamitems" or "music" or "clips" or "batteries" or "photos" => 3500, _ => 1400 });   // the slow ones ask the world

                log.Add($"{kind}: {Math.Round(window.ActualWidth)}x{Math.Round(window.ActualHeight)} | {Words(window)}");
                // nothing may be left half-faded or half-rolled once a card has settled
                int faded = Faded(window);
                if (faded > 0) log.Add($"  {kind}: {faded} piece(s) of writing left faded (expect 0)");
                Snap(window, Path.Combine(dir, $"{kind}.png"));

                // things only some cards can tell us
                switch (window.Body.Content)
                {
                    case NewsWidget news: log.Add($"  news: {news.Shown} patch(es) shown"); break;
                    case ClipsWidget clips: log.Add($"  clips: {clips.Found} found, picture drawn: {clips.Pictured}"); break;
                    case BatteriesWidget batteries: log.Add($"  batteries: {batteries.Shown} row(s) on a PC that reports them"); break;
                    case ScreenTimeWidget screen: log.Add($"  screentime: {screen.Listed} program(s) listed (expect 4)"); break;
                    case CalcWidget calc:
                        log.Add($"  calc: '20 usd to ils' -> '{calc.Answer}'");
                        foreach (var sum in new[] { "12*(3+4)", "5 km to mi", "100 f to c", "2^10", "hello" })
                        {
                            await calc.Work(sum);
                            await Task.Delay(500);
                            log.Add($"  calc: '{sum}' -> '{calc.Answer}'");
                        }
                        break;
                    case WorldClockWidget world:
                        log.Add($"  worldclock: {world.Shown}");
                        world.Next(0);
                        await Task.Delay(600);
                        log.Add($"  worldclock: first row moved on -> {world.Shown}");
                        break;
                    case PhotosWidget photos: log.Add($"  photos: {photos.Found} in the folder, picture drawn: {photos.Pictured}"); break;
                    case MusicWidget music: log.Add($"  music: timed words {music.HasLyrics}, line now '{music.Sung}'"); break;
                    case SoundWidget sound: log.Add($"  sound: {sound.Apps} app row(s), mic line {sound.MicShown}, discord line {sound.DiscordShown}"); break;
                    case ShortcutsWidget shortcuts: log.Add($"  shortcuts: {shortcuts.Count} on the card"); break;
                }
                window.Close();
            }

            await CardMenu(dir, log);
            await Sizes(dir, log);
            await Graph(dir, log);
            await Timers(dir, log);
            await Lists(dir, log);
            await Days(dir, log);
            await Shortcuts(dir, log);
            await Batteries(dir, log);
            News(log);
            await Pinning(app, log);
            await Skies(dir, log);
            await Glassy(app, dir, log);
            await Extras(app, dir, log);
            await Newer(dir, log);
            await Together(app, dir, log);
            await Motion(dir, log);

            // ----- the picker, which is the only window with a frame -----
            var picker = new PickerWindow { Left = Far, Top = Far };
            picker.Show();
            await Task.Delay(2600);          // long enough for the rows to finish arriving
            log.Add($"picker: {Math.Round(picker.ActualWidth)}x{Math.Round(picker.ActualHeight)} | {Words(picker)}");
            Snap(picker, Path.Combine(dir, "picker.png"));
            picker.Scroll.ScrollToVerticalOffset(picker.Scroll.ScrollableHeight * 0.5);
            await Task.Delay(300);
            Snap(picker, Path.Combine(dir, "picker-middle.png"));
            picker.Scroll.ScrollToEnd();
            await Task.Delay(300);
            Snap(picker, Path.Combine(dir, "picker-end.png"));

            // "Find my location": the log says only whether it worked, never where
            await picker.Locate();
            await Task.Delay(500);
            log.Add($"locate: worked={App.Board.WeatherCity.Length > 0 && App.Board.WeatherLat != 0}, " +
                    $"board filled in so the weather card needs no second lookup={App.Board.WeatherPlace == App.Board.WeatherCity}");
            Snap(picker, Path.Combine(dir, "picker-locate.png"));
            picker.Close();

            // ----- the board remembers what was put on it, including the new things cards keep -----
            var board = new Board { LockAll = false };
            board.Widgets.Add(new Placed
            {
                Kind = "tasks", X = 12, Y = 34, Scale = 1.25, Locked = true, Number = 45, Name = "Rome",
                Items = { "0|milk", "1|bread" }, Until = new DateTime(2027, 1, 2), Since = new DateTime(2026, 11, 13),
            });
            board.Save();
            var back = Board.Load();
            var first = back.Widgets[0];
            log.Add($"saved and read back: {back.Widgets.Count} widget(s), first {first.Kind} at {first.X},{first.Y} x{first.Scale}, " +
                    $"locked={first.Locked}, items={string.Join(";", first.Items)}, number={first.Number}, name={first.Name}, until={first.Until:yyyy-MM-dd}, since={first.Since:yyyy-MM-dd}");
            had.Save();
            log.Add($"and put back as it was: {Board.Load().Widgets.Count} widget(s)");
        }
        catch (Exception bad)
        {
            log.Add("BROKE: " + bad);
        }

        File.WriteAllText(Path.Combine(dir, "widgets.txt"), string.Join(Environment.NewLine, log));
        Application.Current.Shutdown();
    }

    /// <summary>A card of that kind, off-screen, with something sensible written on it.</summary>
    static Placed Sample(string kind)
    {
        var placed = new Placed { Kind = kind, X = Far, Y = Far };
        switch (kind)
        {
            case "notes": placed.Text = "milk\nbread\nask about the thing"; break;
            case "calc": placed.Text = "20 usd to ils"; break;
            case "tasks": placed.Items.AddRange(new[] { "1|Send the invoice", "0|Call the dentist", "0|Water the plants", "0|Finish the widgets" }); break;
            case "countdown":
                placed.Name = "Flight to Rome";
                placed.Until = DateTime.Today.AddDays(12);
                placed.Since = DateTime.Today.AddDays(-18);
                break;
            case "shortcuts":
                placed.Items.AddRange(new[]
                {
                    Path.Combine(Environment.SystemDirectory, "notepad.exe"), Path.Combine(Environment.SystemDirectory, "calc.exe"),
                    Path.Combine(Environment.SystemDirectory, "mspaint.exe"), Path.Combine(Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows", "explorer.exe"),
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + @"\Downloads",
                });
                break;
        }
        return placed;
    }

    // ----- the sizes from the menu: a bigger card must not be cut off by its own window -----

    static async Task Sizes(string dir, List<string> log)
    {
        foreach (var (kind, scale, file) in new[] { ("steam", 1.0, "size-normal.png"), ("steam", 1.6, "size-huge.png"), ("weather", 1.25, "size-big.png"), ("weather", 0.85, "size-small.png") })
        {
            var window = new WidgetWindow(new Placed { Kind = kind, Scale = scale, X = Far, Y = Far });
            window.Show();
            await Task.Delay(1400);
            var content = ((FrameworkElement)window.Body.Content);
            double zoom = ((System.Windows.Media.ScaleTransform)((FrameworkElement)window.Content).LayoutTransform).ScaleX;
            // the window has to be at least as big as the card inside it, scaled
            bool whole = window.ActualWidth + 1 >= (content.ActualWidth + 28 + 8) * zoom && window.ActualHeight + 1 >= (content.ActualHeight + 24 + 8) * zoom;
            log.Add($"size {kind} x{scale}: window {window.ActualWidth:0}x{window.ActualHeight:0}, scale {zoom:0.00}, card fits inside: {whole}");
            Snap(window, Path.Combine(dir, file));
            window.Close();
        }
    }

    // ----- two cards dropped against each other become one -----

    /// <summary>A joined frame dragged far past each side and corner of the screen stays wholly on it.</summary>
    static async Task KeptOnScreen(App app, List<string> log)
    {
        var room = new Rect(Far, Far, 1000, 800);
        WidgetWindow.RoomForTests = room;
        App.Board.Widgets.Clear();
        var lead = new Placed { Kind = "clock", X = Far + 100, Y = Far + 100 };
        var mate = new Placed { Kind = "drives", X = Far + 330, Y = Far + 100 };
        App.Board.Widgets.Add(lead);
        App.Board.Widgets.Add(mate);
        app.Redraw();
        await Task.Delay(1200);
        app.Join(mate, lead, across: true);
        await Task.Delay(1200);
        var one = app.Window(lead.Id)!;
        log.Add($"joined frame: {one.Parts.Count} cards, {one.ActualWidth:0}x{one.ActualHeight:0}");

        var spots = new (string Name, double X, double Y)[]
        {
            ("past the right edge", Far + 2000, Far + 300), ("past the left edge", Far - 900, Far + 300),
            ("past the bottom", Far + 400, Far + 2000), ("past the top", Far + 400, Far - 900),
            ("past the bottom-right corner", Far + 3000, Far + 3000), ("past the top-left corner", Far - 3000, Far - 3000),
            ("in the middle", Far + 300, Far + 300),
        };
        int bad = 0;
        foreach (var (name, x, y) in spots)
        {
            one.Carried(new Point(20, 20));
            one.Follow(new Point(x, y), lineUp: false);
            var seen = one.Seen();
            bool inside = seen.Left >= room.Left - 0.5 && seen.Top >= room.Top - 0.5 && seen.Right <= room.Right + 0.5 && seen.Bottom <= room.Bottom + 0.5;
            if (!inside) bad++;
            log.Add($"  {name}: card at {seen.Left - Far:0},{seen.Top - Far:0} to {seen.Right - Far:0},{seen.Bottom - Far:0} of the 1000x800 screen -> {(inside ? "on screen" : "OFF SCREEN")}");
            one.Let();
            await Task.Delay(50);
        }
        // and with lining up on, the way a real drag goes
        one.Carried(new Point(20, 20));
        one.Follow(new Point(Far + 5000, Far + 5000), lineUp: true);
        var lined = one.Seen();
        bool ok = lined.Right <= room.Right + 0.5 && lined.Bottom <= room.Bottom + 0.5;
        if (!ok) bad++;
        log.Add($"  past the corner, lining up: right {lined.Right - Far:0} bottom {lined.Bottom - Far:0} -> {(ok ? "on screen" : "OFF SCREEN")}");
        one.Let();
        log.Add(bad == 0 ? "kept on screen: ALL GOOD" : $"kept on screen: {bad} WRONG");
        WidgetWindow.RoomForTests = null;
    }

    static async Task Together(App app, string dir, List<string> log)
    {
        // which card is flush against which, on made-up rectangles
        var side = new Rect(310, 100, 200, 150);      // right beside it, the usual gap
        var under = new Rect(100, 260, 200, 150);     // right under it
        var away = new Rect(700, 500, 200, 150);
        var barely = new Rect(310, 240, 200, 150);    // beside it, but only its corner faces it
        var first = new List<Rect> { new(100, 100, 200, 150) };
        log.Add($"joining: beside -> {Core.Snap.Touching(side, first)} (expect 0, across True)");
        log.Add($"joining: under  -> {Core.Snap.Touching(under, first)} (expect 0, across False)");
        log.Add($"joining: far away -> {(Core.Snap.Touching(away, first) is null ? "nothing, as it should" : "WRONG")}");
        log.Add($"joining: only a corner facing -> {(Core.Snap.Touching(barely, first) is null ? "nothing, as it should" : "WRONG")}");

        // and really, on the board: a clock and a drives card joined into one frame
        var had = App.Board.Widgets.ToList();
        App.Board.Widgets.Clear();
        var lead = new Placed { Kind = "clock", X = Far, Y = Far };
        var mate = new Placed { Kind = "drives", X = Far + 230, Y = Far };
        App.Board.Widgets.Add(lead);
        App.Board.Widgets.Add(mate);
        app.Redraw();
        await Task.Delay(1200);
        log.Add($"two cards apart: {app.OpenCount} window(s) on the desktop (expect 2)");

        app.Join(mate, lead, across: true);
        await Task.Delay(1200);
        var one = app.Window(lead.Id);
        log.Add($"after joining: {app.OpenCount} window(s) (expect 1), holding {one?.Parts.Count} card(s) (expect 2), {Math.Round(one?.ActualWidth ?? 0)} wide");
        log.Add($"what it says: {(one == null ? "" : Words(one))}");
        if (one != null) Snap(one, Path.Combine(dir, "joined-across.png"));

        // the menu of a joined card offers to split the other one off again
        if (one != null)
        {
            one.ContextMenu.PlacementTarget = one;
            one.ContextMenu.IsOpen = true;
            await Task.Delay(400);
            var words = one.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().Select(i => (string)i.Header).ToList();
            var splitter = one.ContextMenu.Items.OfType<System.Windows.Controls.MenuItem>().FirstOrDefault(i => (string)i.Header == "Split off");
            log.Add($"its menu: {string.Join(" / ", words)} | split off: {string.Join(", ", splitter?.Items.OfType<System.Windows.Controls.MenuItem>().Select(i => (string)i.Header) ?? Array.Empty<string>())}");
            one.ContextMenu.IsOpen = false;
        }

        // one above the other
        app.Split(mate);
        await Task.Delay(900);
        log.Add($"after splitting: {app.OpenCount} window(s) (expect 2), the second one is on its own: {mate.Under.Length == 0}");
        app.Join(mate, lead, across: false);
        await Task.Delay(1200);
        var stacked = app.Window(lead.Id);
        log.Add($"joined one under the other: {Math.Round(stacked?.ActualWidth ?? 0)}x{Math.Round(stacked?.ActualHeight ?? 0)}, holding {stacked?.Parts.Count} card(s) [{stacked?.PartSizes}], body {Math.Round(((FrameworkElement)stacked!.Body.Content).ActualHeight)}, across={lead.Across}, mate under={mate.Under == lead.Id}, windows={app.OpenCount}");
        if (stacked != null) Snap(stacked, Path.Combine(dir, "joined-down.png"));

        // ----- 1.13: put on top joins at once, the line on top pulls it out, and nothing starts over -----
        var r = new Rect(100, 100, 200, 150);
        log.Add($"on top: middle inside -> {Core.Snap.Over(new Rect(150, 130, 200, 150), new List<Rect> { r })} (expect 0, across True)");
        log.Add($"on top: mostly below -> {Core.Snap.Over(new Rect(110, 190, 200, 150), new List<Rect> { r })} (expect 0, across False)");
        log.Add($"right beside, not on top -> {(Core.Snap.Target(new Rect(305, 100, 200, 150), new List<Rect> { r }) is null ? "nothing, as it should" : "WRONG")}");
        log.Add($"on top: only a corner -> {(Core.Snap.Over(new Rect(280, 235, 200, 150), new List<Rect> { r }) is null ? "nothing, as it should" : "WRONG")}");

        var bystander = new Placed { Kind = "clock", X = Far + 900, Y = Far };
        App.Board.Widgets.Add(bystander);
        app.Open(bystander);
        await Task.Delay(900);
        var still = app.Window(bystander.Id);
        var frame = app.Window(lead.Id)!;
        var mateBody = frame.Body.Content is System.Windows.Controls.Panel p0 ? ((System.Windows.Controls.Grid)p0.Children[2]).Children[0] : null;
        log.Add($"pull: frame has {frame.Parts.Count} card(s), lines drawn: {Lines(frame)} (expect 2)");

        // grab the drives card by its line and carry it far off
        var mouse = new Point(frame.Left + 60, frame.Top + 200);
        var alone = frame.PullOut(mate, mouse);
        await Task.Delay(300);
        log.Add($"pull: a window of its own: {alone != null}, same insides (not rebuilt): {ReferenceEquals(alone?.Body.Content, mateBody)}, frame now holds {frame.Parts.Count}, lines {Lines(frame)} (expect 0), windows {app.OpenCount} (expect 3)");
        alone?.Follow(new Point(Far + 600, Far + 300), false);
        frame.Let();
        await Task.Delay(400);
        log.Add($"pull: let go far away: on its own {mate.Under.Length == 0}, windows {app.OpenCount} (expect 3), the clock nearby was not reopened: {ReferenceEquals(app.Window(bystander.Id), still)}");

        // now put it right on top of the clock card: it joins the moment it is let go
        var clockFrame = app.Window(lead.Id)!;
        var clockBody = clockFrame.Body.Content;
        alone!.Begin(new Point(20, 20));
        alone.Follow(new Point(clockFrame.Left + 40, clockFrame.Top + 60), false);
        bool lit = LitUp(clockFrame);
        alone.Let();
        await Task.Delay(500);
        var again = app.Window(lead.Id);
        log.Add($"on top: target lit while dragging: {lit}, joined: {mate.Under == lead.Id}, same frame window (not reopened): {ReferenceEquals(again, clockFrame)}, clock insides kept: {again != null && Holds(again, clockBody)}, drives insides kept: {again != null && Holds(again, mateBody)}, windows {app.OpenCount} (expect 2), bystander untouched: {ReferenceEquals(app.Window(bystander.Id), still)}");
        if (again != null) Snap(again, Path.Combine(dir, "joined-lines.png"));

        // pulling the card the frame belongs to: the other one takes the frame over
        if (again != null)
        {
            var leadOut = again.PullOut(lead, new Point(again.Left + 40, again.Top + 30));
            leadOut?.Follow(new Point(Far + 300, Far + 500), false);
            again.Let();
            await Task.Delay(400);
            log.Add($"pull the first card: frame now belongs to {again.Placed.Kind} (expect drives), both free: {lead.Under.Length == 0 && mate.Under.Length == 0}, windows {app.OpenCount} (expect 3)");
            // and back together for the old checks below
            app.Join(mate, lead, false);
            await Task.Delay(400);
            App.Board.Widgets.Remove(bystander);
            app.Window(bystander.Id)?.Close();
        }

        // taking the card that holds the frame off the desktop leaves the other one standing
        app.Remove(lead);
        await Task.Delay(900);
        log.Add($"the one holding the frame taken off: {app.OpenCount} window(s) (expect 1), the other is free: {mate.Under.Length == 0}");

        // nothing may be joined to itself, or to a card that is itself joined
        var a = new Placed { Kind = "clock" };
        var b = new Placed { Kind = "drives", Under = a.Id };
        var c = new Placed { Kind = "tasks" };
        App.Board.Widgets.Clear();
        App.Board.Widgets.AddRange(new[] { a, b, c });
        app.Join(a, a, true);
        log.Add($"a card joined to itself: {(a.Under.Length == 0 ? "refused, as it should" : "WRONG")}");
        app.Join(c, b, true);
        log.Add($"joined to one that is itself joined: it goes to the one holding the frame instead: {c.Under == a.Id}");

        App.Board.Widgets.Clear();
        App.Board.Widgets.AddRange(had);
        App.Board.TidyJoins();
        app.Redraw();
        await Task.Delay(600);
        foreach (var window in new[] { app.Window(lead.Id), app.Window(mate.Id) }) window?.Close();
    }

    // ----- what is next in the calendar, what was copied, and the times under the song -----

    static async Task Newer(string dir, List<string> log)
    {
        // the calendar of this PC, really asked
        var card = new AgendaWidget();
        var window = new WidgetWindow(new Placed { Kind = "agenda", X = Far, Y = Far });
        window.Show();
        await Task.Delay(4000);
        var real = (AgendaWidget)window.Body.Content;
        log.Add($"calendar: {(real.Found < 0 ? "Windows would not open it" : real.Found + " thing(s) in the next week")}");
        Snap(window, Path.Combine(dir, "agenda-real.png"));
        window.Close();

        // and made-up ones, so the card is seen full whatever is in the real calendar
        var now = new DateTime(2026, 11, 14, 9, 30, 0);
        var made = new List<Agenda.Entry>
        {
            new("Stand-up", now.AddMinutes(-10), TimeSpan.FromMinutes(30), false, "Teams"),
            new("Dentist", now.AddMinutes(35), TimeSpan.FromHours(1), false, "Herzl 12"),
            new("Pick up the parcel", now.Date.AddDays(1).AddHours(17), TimeSpan.FromMinutes(30), false, ""),
            new("Mum's birthday", now.Date.AddDays(3), TimeSpan.Zero, true, ""),
        };
        var full = new WidgetWindow(new Placed { Kind = "agenda", X = Far, Y = Far });
        full.Show();
        await Task.Delay(700);
        ((AgendaWidget)full.Body.Content).Draw(made, now);
        await Task.Delay(400);
        log.Add($"calendar, made up: {Words(full)}");
        Snap(full, Path.Combine(dir, "agenda-demo.png"));
        full.Close();

        // the tidying up, without a calendar at all
        var messy = new List<Agenda.Entry>
        {
            new("Stand-up", now.AddMinutes(-10), TimeSpan.FromMinutes(30), false, ""),     // on now: kept
            new("Stand-up", now.AddMinutes(-10), TimeSpan.FromMinutes(30), false, ""),     // the same one in another calendar: dropped
            new("Over and done", now.AddHours(-3), TimeSpan.FromMinutes(30), false, ""),   // finished: dropped
            new("", now.AddHours(1), TimeSpan.FromMinutes(30), false, ""),                 // no name: dropped
            new("Next month", now.AddDays(20), TimeSpan.FromMinutes(30), false, ""),       // too far ahead: dropped
            new("Later today", now.AddHours(2), TimeSpan.FromMinutes(30), false, ""),
        };
        var tidy = Agenda.Tidy(messy, now);
        log.Add($"calendar tidied: {tidy.Count} left (expect 2) -> {string.Join(", ", tidy.Select(t => t.What))}");
        log.Add($"calendar in words: now='{Agenda.When(made[0], now)}', soon='{Agenda.When(made[1], now)}', tomorrow='{Agenda.When(made[2], now)}', all day='{Agenda.When(made[3], now)}'");

        // what was copied: nothing is saved, and a password manager's own mark is obeyed
        var clips = new WidgetWindow(new Placed { Kind = "clipboard", X = Far, Y = Far });
        clips.Show();
        await Task.Delay(700);
        var clip = (ClipboardWidget)clips.Body.Content;
        log.Add($"copied, empty: {Words(clips)}");
        Snap(clips, Path.Combine(dir, "clipboard-empty.png"));
        foreach (var said in new[] { "https://github.com/yahavpenso", "the second line\nand its other half", "0x7FF4A2", "shopping list", "shopping list" })
            clip.Took(said);
        await Task.Delay(400);
        log.Add($"copied five things, two of them the same: {clip.Count} line(s) (expect 4), {Words(clips)}");
        Snap(clips, Path.Combine(dir, "clipboard.png"));
        clip.Forget();
        await Task.Delay(200);
        log.Add($"after Forget them: {clip.Count} line(s) (expect 0)");
        log.Add($"nothing of it on disk: {!File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PitzkiWidgets", "board.json")).Contains("github.com/yahavpenso")}");
        clips.Close();

        // the times under the song
        log.Add($"song times: {MusicWidget.Clock(TimeSpan.FromSeconds(7))}, {MusicWidget.Clock(TimeSpan.FromSeconds(187))}, {MusicWidget.Clock(TimeSpan.FromSeconds(3734))} (expect 0:07, 3:07, 1:02:14)");
    }

    // ----- focus rounds, glass levels, profiles and hiding at the edge -----

    static async Task Extras(App app, string dir, List<string> log)
    {
        // focus rounds
        log.Add($"focus rounds: break after round 1 = {PomodoroWidget.BreakAfter(1)} (expect 5), after round 4 = {PomodoroWidget.BreakAfter(4)} (expect 15), after round 8 = {PomodoroWidget.BreakAfter(8)} (expect 15)");
        var placed = new Placed { Kind = "pomodoro", X = Far, Y = Far };
        var window = new WidgetWindow(placed);
        window.Show();
        await Task.Delay(900);
        var card = (PomodoroWidget)window.Body.Content;
        log.Add($"focus idle: '{Words(window)}'");
        card.Begin();
        await Task.Delay(300);
        log.Add($"focus started: phase={card.Phase}, rounds={placed.Number}, '{Words(window)}'");
        Snap(window, Path.Combine(dir, "focus-running.png"));
        card.Next();
        await Task.Delay(300);
        log.Add($"focus skipped to the break: phase={card.Phase}, rounds={placed.Number} (expect break, 1), '{Words(window)}'");
        card.Next();
        log.Add($"focus skipped again: phase={card.Phase} (expect focus)");
        card.Stop();
        log.Add($"focus stopped: phase='{card.Phase}', until={(placed.Until is null ? "none" : "set")}");
        window.Close();

        // how dark the glass is
        app.SetGlass(true);
        foreach (var level in new[] { 0, 2, 1 })
        {
            app.SetGlassLevel(level);
            log.Add($"glass level {App.Levels[level]}: body alpha {Alpha((Brush)Application.Current.Resources["SurfaceBrush"])}");
        }

        // profiles
        var work = new WidgetWindow(new Placed { Kind = "clock", Only = "Work", X = Far, Y = Far });
        var everywhere = new WidgetWindow(new Placed { Kind = "clock", X = Far, Y = Far });
        work.Show(); everywhere.Show();
        await Task.Delay(500);
        string before = App.Board.Profile;
        foreach (var profile in new[] { "Game", "Work", "" })
        {
            App.Board.Profile = profile;
            work.ShowIfFits(); everywhere.ShowIfFits();
            log.Add($"profile '{profile}': work card shown={work.IsVisible}, everywhere card shown={everywhere.IsVisible}");
        }
        App.Board.Profile = before;
        work.Close(); everywhere.Close();

        // the Start menu shortcut, made somewhere of its own so the real Start menu is untouched
        string madeUp = Path.Combine(dir, "Start menu", Core.StartMenu.AppName + ".lnk");
        bool put = Core.StartMenu.Set(true, link: madeUp);
        long size = File.Exists(madeUp) ? new FileInfo(madeUp).Length : 0;
        bool gone = Core.StartMenu.Set(false, link: madeUp);
        log.Add($"Start menu shortcut: made={put} ({size} bytes), taken off again={gone}; the real one would go to {Core.StartMenu.LinkPath}");
        log.Add($"and it is in the real Start menu now: {Core.StartMenu.There}");

        // more than one screen: the card's own screen is the one it lines up and hides against
        var screens = System.Windows.Forms.Screen.AllScreens;
        log.Add($"screens on this PC: {screens.Length} -> {string.Join(" | ", screens.Select(s => $"{s.WorkingArea.Width}x{s.WorkingArea.Height} at {s.WorkingArea.X},{s.WorkingArea.Y}{(s.Primary ? " (main)" : "")}"))}");
        foreach (var screen in screens)
        {
            var middle = new Rect(screen.WorkingArea.X + screen.WorkingArea.Width / 2.0, screen.WorkingArea.Y + 40, 200, 150);
            var asked = Core.Screens.Room(middle);
            bool right = Math.Abs(asked.Width - screen.WorkingArea.Width) < 2 && Math.Abs(asked.X - screen.WorkingArea.X) < 2;
            log.Add($"  a card in the middle of {screen.DeviceName}: room {asked.Width:0}x{asked.Height:0} at {asked.X:0},{asked.Y:0} (that screen: {right})");
        }
        log.Add($"every screen together: {Core.Screens.All.Width:0}x{Core.Screens.All.Height:0} at {Core.Screens.All.X:0},{Core.Screens.All.Y:0}");

        // hiding at the screen edge
        log.Add($"tuck: card on the left half hides at {WidgetWindow.TuckedLeft(new Rect(100, 100, 200, 150), new Rect(0, 0, 1920, 1040))} (expect -182), on the right half at {WidgetWindow.TuckedLeft(new Rect(1500, 100, 200, 150), new Rect(0, 0, 1920, 1040))} (expect 1902)");
        WidgetWindow.RoomForTests = new Rect(Far, Far, 1000, 800);
        var edge = new WidgetWindow(new Placed { Kind = "clock", Peek = true, X = Far + 700, Y = Far + 100 });
        edge.Show();
        await Task.Delay(700);
        double home = edge.Left;
        edge.Tuck();
        await Task.Delay(700);
        double hidden = edge.Left;
        log.Add($"tuck: at {home:0} -> hidden {hidden:0} (tucked={edge.Tucked}); room right edge is {Far + 1000}");
        edge.Untuck();
        await Task.Delay(600);
        log.Add($"tuck: back out at {edge.Left:0} (expect {home:0}), tucked={edge.Tucked}");
        edge.Close();
        WidgetWindow.RoomForTests = null;
    }

    // ----- liquid glass: the same cards flat and as glass, over a busy backdrop -----

    static async Task Glassy(App app, string dir, List<string> log)
    {
        bool was = App.Board.Glass;
        var kinds = new[] { "clock", "system", "tasks" };
        var shots = new List<(RenderTargetBitmap Picture, int W, int H)>[2] { new(), new() };

        for (int mode = 0; mode < 2; mode++)
        {
            app.SetGlass(mode == 1);
            foreach (var kind in kinds)
            {
                var window = new WidgetWindow(Sample(kind));
                window.Show();
                await Task.Delay(1500);
                if (mode == 1)
                {
                    // the pointer is on the card, and the sheen is halfway across
                    window.GlassTouch.Opacity = 1;
                    window.GlassTouchT.X = window.ActualWidth * 0.55 - 100;
                    window.GlassTouchT.Y = window.ActualHeight * 0.4 - 100;
                    window.GlassSheenT.X = 2;      // right over the top-left corner, where a square edge would show
                }
                int w = Math.Max((int)window.ActualWidth, 1), h = Math.Max((int)window.ActualHeight, 1);
                var content = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
                content.Render((Visual)window.Content);
                shots[mode].Add((content, w, h));
                if (kind == "clock") log.Add($"glass {(mode == 1 ? "on" : "off")}: glass layer {window.Glass.Opacity:0.00}, body brush alpha {Alpha(window.Card.Background)}");
                window.Close();
            }
        }

        // two rows (flat above, glass below) over a wallpaper-like backdrop with bright shapes
        int width = shots[0].Sum(s => s.W) + 40 * (kinds.Length + 1);
        int rowH = shots[0].Max(s => s.H);
        int height = rowH * 2 + 120;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var sky = new LinearGradientBrush(Color.FromRgb(0x2B, 0x5D, 0xC8), Color.FromRgb(0xE0, 0x7A, 0x5F), new Point(0, 0), new Point(1, 1));
            dc.DrawRectangle(sky, null, new Rect(0, 0, width, height));
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0xF6, 0xD3, 0x6B)), null, new Point(width * 0.28, height * 0.5), 90, 90);
            dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(0x6B, 0xE3, 0xA0)), null, new Point(width * 0.62, height * 0.58), 70, 70);
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)), null, new Rect(width * 0.8, 0, 26, height));
            for (int mode = 0; mode < 2; mode++)
            {
                int x = 40, y = 40 + mode * (rowH + 40);
                foreach (var shot in shots[mode])
                {
                    dc.DrawImage(shot.Picture, new Rect(x, y, shot.W, shot.H));
                    x += shot.W + 40;
                }
            }
        }
        var final = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        final.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(final));
        using (var file = File.Create(Path.Combine(dir, "glass.png"))) encoder.Save(file);

        // the switch is remembered, and the tray and menus follow it
        app.SetGlass(false);
        log.Add($"glass switched off: remembered={!App.Board.Glass}, picker/menu brush alpha {Alpha((Brush)Application.Current.Resources["SurfaceBrush"])} (expect 255)");
        app.SetGlass(true);
        log.Add($"glass switched on:  remembered={App.Board.Glass}, picker/menu brush alpha {Alpha((Brush)Application.Current.Resources["SurfaceBrush"])} (expect less than 255)");
        app.SetGlass(was);
    }

    static string Alpha(Brush brush) => brush switch
    {
        SolidColorBrush s => s.Color.A.ToString(),
        GradientBrush g => g.GradientStops.Min(x => x.Color.A) + ".." + g.GradientStops.Max(x => x.Color.A),
        _ => "?",
    };

    // ----- the right-click menu of a card, opened and pictured -----

    static async Task CardMenu(string dir, List<string> log)
    {
        var window = new WidgetWindow(new Placed { Kind = "clock", Locked = true, X = Far, Y = Far });
        window.Show();
        await Task.Delay(900);
        var menu = window.ContextMenu;
        menu.PlacementTarget = window;
        menu.IsOpen = true;
        await Task.Delay(500);

        int w = Math.Max((int)menu.ActualWidth, 1), h = Math.Max((int)menu.ActualHeight, 1);
        var picture = new RenderTargetBitmap(w + 12, h + 12, 96, 96, PixelFormats.Pbgra32);
        var back = new DrawingVisual();
        using (var dc = back.RenderOpen()) dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x6A, 0x8F, 0xB8)), null, new Rect(0, 0, w + 12, h + 12));
        picture.Render(back);
        picture.Render(menu);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(picture));
        using (var file = File.Create(Path.Combine(dir, "card-menu.png"))) encoder.Save(file);

        log.Add($"card menu: {w}x{h}, {menu.Items.Count} entries, words: {string.Join(" / ", menu.Items.OfType<System.Windows.Controls.MenuItem>().Select(i => i.Header))}");
        menu.IsOpen = false;
        window.Close();
    }

    // ----- the graph on the PC card -----

    static async Task Graph(string dir, List<string> log)
    {
        var window = new WidgetWindow(new Placed { Kind = "system", X = Far, Y = Far });
        window.Show();
        await Task.Delay(900);
        if (window.Body.Content is SystemWidget pc) pc.Demo();
        await Task.Delay(300);
        Snap(window, Path.Combine(dir, "system-graph.png"));
        log.Add("system graph: a made-up minute drawn");
        window.Close();

        // the pencil: the switches, then the card with NETWORK switched off (row and graph line gone)
        var hiding = new Placed { Kind = "system", X = Far, Y = Far };
        var edit = new WidgetWindow(hiding);
        edit.Show();
        await Task.Delay(900);
        if (edit.Body.Content is SystemWidget sys)
        {
            sys.Demo();
            sys.Editing();
            await Task.Delay(700);
            Snap(edit, Path.Combine(dir, "system-edit.png"));
            sys.Set("network", false);
            await Task.Delay(400);
            Snap(edit, Path.Combine(dir, "system-edit-off.png"));
            sys.Back();
            await Task.Delay(700);
            Snap(edit, Path.Combine(dir, "system-no-network.png"));
            log.Add($"system pencil: network off -> row shown {sys.Shows("network")}, graph line {sys.LineShows("network")}, cpu row {sys.Shows("cpu")}, items [{string.Join(",", hiding.Items)}] (expect False False True hide:network)");
            sys.Set("cpu", false);
            await Task.Delay(600);
            log.Add($"system pencil: cpu off too -> graph shown {sys.GraphShows} (expect False)");
            Snap(edit, Path.Combine(dir, "system-no-graph.png"));
            sys.Set("cpu", true);
            sys.Set("network", true);
            await Task.Delay(600);
            log.Add($"system pencil: both back -> network {sys.Shows("network")}, graph {sys.GraphShows}, items [{string.Join(",", hiding.Items)}] (expect True True, empty)");
        }
        edit.Close();
    }

    // ----- the timer, running and finished -----

    static async Task Timers(string dir, List<string> log)
    {
        // running: due a minute and a half from now
        var running = new WidgetWindow(new Placed { Kind = "timer", Number = 5, Until = DateTime.Now.AddSeconds(95), X = Far, Y = Far });
        running.Show();
        await Task.Delay(1200);
        log.Add($"timer running: {Words(running)}");
        Snap(running, Path.Combine(dir, "timer-running.png"));
        running.Close();

        // finished: due in a second, and left to run out
        var done = new WidgetWindow(new Placed { Kind = "timer", Number = 1, Until = DateTime.Now.AddSeconds(1.2), X = Far, Y = Far });
        done.Show();
        await Task.Delay(2600);
        log.Add($"timer after it ran out: {Words(done)}");
        Snap(done, Path.Combine(dir, "timer-done.png"));
        done.Close();

        // pressing start on a fresh one
        var fresh = new Placed { Kind = "timer", X = Far, Y = Far };
        var card = new WidgetWindow(fresh);
        card.Show();
        await Task.Delay(700);
        if (card.Body.Content is TimerWidget t)
        {
            t.Begin();
            await Task.Delay(600);
            log.Add($"timer after Start: until {fresh.Until:HH:mm:ss}, said '{Words(card)}'");
            t.Reset();
            log.Add($"timer after Clear: until={(fresh.Until is null ? "none" : "still set")}, said '{Words(card)}'");
        }
        card.Close();
    }

    // ----- the tasks list -----

    static async Task Lists(string dir, List<string> log)
    {
        var placed = new Placed { Kind = "tasks", X = Far, Y = Far };
        var window = new WidgetWindow(placed);
        window.Show();
        await Task.Delay(700);
        if (window.Body.Content is TasksWidget tasks)
        {
            tasks.Add("Buy milk");
            tasks.Add("   ");                                  // nothing but spaces: not a task
            tasks.Add("Call mum");
            log.Add($"tasks after three tries: {string.Join(" | ", placed.Items)}");
            for (int i = 0; i < 12; i++) tasks.Add("More " + i);
            log.Add($"tasks never more than seven: {placed.Items.Count}");
        }
        await Task.Delay(400);
        Snap(window, Path.Combine(dir, "tasks-full.png"));
        window.Close();
    }

    // ----- the countdown, and the ways a day can be written -----

    static async Task Days(string dir, List<string> log)
    {
        foreach (string said in new[] { "25/12/2027", "2027-12-25", "25.12.2027", "25 Dec 2027", "Dec 25 2027", "25/12", "31/12", "banana", "", "31/02/2027" })
        {
            bool ok = CountdownWidget.TryDay(said, out var day);
            log.Add($"day '{said}': {(ok ? day.ToString("yyyy-MM-dd") : "not a day")}");
        }

        // a card with nothing set opens on the two boxes
        var empty = new WidgetWindow(new Placed { Kind = "countdown", X = Far, Y = Far });
        empty.Show();
        await Task.Delay(800);
        log.Add($"countdown with no day: {Words(empty)}");
        Snap(empty, Path.Combine(dir, "countdown-empty.png"));
        empty.Close();

        // today, and one that is over
        foreach (var (days, file) in new[] { (0, "countdown-today.png"), (-3, "countdown-over.png") })
        {
            var card = new WidgetWindow(new Placed { Kind = "countdown", Name = "Exam", Until = DateTime.Today.AddDays(days), Since = DateTime.Today.AddDays(-10), X = Far, Y = Far });
            card.Show();
            await Task.Delay(800);
            log.Add($"countdown {days:+0;-0;0} days: {Words(card)}");
            Snap(card, Path.Combine(dir, file));
            card.Close();
        }
    }

    // ----- shortcuts -----

    static async Task Shortcuts(string dir, List<string> log)
    {
        var placed = Sample("shortcuts");
        var window = new WidgetWindow(placed);
        window.Show();
        await Task.Delay(900);
        int icons = placed.Items.Count(p => ShortcutsWidget.Icon(p) != null);
        log.Add($"shortcuts: {icons} of {placed.Items.Count} have an icon");

        if (window.Body.Content is ShortcutsWidget card)
        {
            card.Add(placed.Items[0]);                  // the same program twice
            log.Add($"shortcuts after adding one twice: {card.Count}");
            card.Add(Path.Combine(Environment.SystemDirectory, "cmd.exe"));
            card.Add(Path.Combine(Environment.SystemDirectory, "taskmgr.exe"));
            log.Add($"shortcuts never more than six: {card.Count}");
        }
        await Task.Delay(400);
        Snap(window, Path.Combine(dir, "shortcuts-full.png"));
        window.Close();
    }

    // ----- batteries, with a made-up set -----

    static async Task Batteries(string dir, List<string> log)
    {
        var window = new WidgetWindow(new Placed { Kind = "batteries", X = Far, Y = Far });
        window.Show();
        await Task.Delay(900);
        if (window.Body.Content is BatteriesWidget batteries) batteries.Demo();
        await Task.Delay(300);
        Snap(window, Path.Combine(dir, "batteries-demo.png"));
        log.Add("batteries: a made-up set drawn (this PC has no device that reports one)");
        window.Close();
    }

    // ----- reading Steam's news -----

    static void News(List<string> log)
    {
        const string sample = """
        {"appnews":{"appid":730,"newsitems":[
          {"gid":"1","title":"Not a patch","url":"https://x/1","contents":"Sale!","date":1794000000,"tags":["sale"]},
          {"gid":"2","title":"Counter-Strike 2 Update","url":"https://x/2","contents":"\\Fixed some cases of popping\\[list][*]Fixed bot pathing [url=https://x]link[/url][/list]","date":1794438200,"tags":["patchnotes"]},
          {"gid":"3","title":"Older patch","url":"https://x/3","contents":"Old","date":1700000000,"tags":["patchnotes"]}]}}
        """;
        var patch = NewsWidget.Newest("Counter-Strike 2", sample);
        log.Add($"news parser: {(patch is null ? "nothing" : $"'{patch.Title}' on {patch.When:yyyy-MM-dd} -> '{patch.Words}'")}");
        const string sale = """{"appnews":{"newsitems":[{"title":"t","tags":["sale"]}]}}""";
        bool noPatch = NewsWidget.Newest("X", sale) is null;
        log.Add($"news parser, no patch notes: {(noPatch ? "nothing, as it should" : "WRONG")}");
        bool noNews = NewsWidget.Newest("X", "{}") is null;
        log.Add($"news parser, empty answer: {(noNews ? "nothing, as it should" : "WRONG")}");
    }

    // ----- locking and lining up -----

    static async Task Pinning(App app, List<string> log)
    {
        // the arithmetic first: a card dropped 6 px from another's right edge lands a gap away from it
        var other = new Rect(100, 100, 200, 120);
        var near = PitzkiWidgets.Core.Snap.Fit(new Rect(306, 104, 200, 120), new[] { other }, new Rect(0, 0, 1920, 1040));
        log.Add($"snap: beside -> {near.X},{near.Y} (expect 310,100)");
        var under = PitzkiWidgets.Core.Snap.Fit(new Rect(97, 236, 200, 120), new[] { other }, new Rect(0, 0, 1920, 1040));
        log.Add($"snap: under  -> {under.X},{under.Y} (expect 100,230)");
        var far = PitzkiWidgets.Core.Snap.Fit(new Rect(700, 500, 200, 120), new[] { other }, new Rect(0, 0, 1920, 1040));
        log.Add($"snap: far away stays put -> {far.X},{far.Y} (expect 700,500)");
        var edge = PitzkiWidgets.Core.Snap.Fit(new Rect(4, 7, 200, 120), Array.Empty<Rect>(), new Rect(0, 0, 1920, 1040));
        log.Add($"snap: screen corner -> {edge.X},{edge.Y} (expect 10,10)");

        // then two real cards
        var a = new Placed { Kind = "clock", X = Far, Y = Far };
        var b = new Placed { Kind = "clock", X = Far + 600, Y = Far };
        app.Open(a);
        app.Open(b);
        await Task.Delay(1400);
        var one = app.Window(a.Id)!;
        var two = app.Window(b.Id)!;

        var edgeOfOne = one.Seen();
        two.Begin(new Point(0, 0));
        two.Follow(new Point(edgeOfOne.Right + 6 - 4, edgeOfOne.Top + 3 - 4), lineUp: true);
        var landed = two.Seen();
        log.Add($"two cards: second dropped 6 px off the first's side landed {landed.Left - edgeOfOne.Right:0} px from it, {landed.Top - edgeOfOne.Top:0} px off its top (expect 10 and 0)");
        two.Follow(new Point(edgeOfOne.Right + 6 - 4, edgeOfOne.Top + 3 - 4), lineUp: false);
        log.Add($"two cards: with snapping off it stays {two.Seen().Left - edgeOfOne.Right:0} px away, {two.Seen().Top - edgeOfOne.Top:0} off (expect 6 and 3)");
        two.Let();

        // locked: the mouse goes down and nothing follows
        b.Locked = true;
        double before = two.Left;
        bool took = two.Begin(new Point(0, 0));
        two.Follow(new Point(two.Left + 500, two.Top + 500), lineUp: false);
        log.Add($"locked card: grabbed={took}, moved {two.Left - before:0} px (expect False and 0)");
        b.Locked = false;

        // lock everything from the tray
        App.Board.LockAll = true;
        log.Add($"lock all: first card locked={one.IsLocked}, grabbed={one.Begin(new Point(0, 0))} (expect True and False)");
        App.Board.LockAll = false;
        log.Add($"unlocked again: grabbed={one.Begin(new Point(0, 0))} (expect True)");
        one.Let();

        app.Remove(a);
        app.Remove(b);
    }

    // ----- the CS2 inventory card -----

    static async Task InventoryCheck(string dir, List<string> log)
    {
        var done = new TaskCompletionSource();
        Pitzki.Core.Cs2Inventory.Changed += () => done.TrySetResult();
        Pitzki.Core.Cs2Inventory.Start(force: true);
        await Task.WhenAny(done.Task, Task.Delay(120000));
        log.Add($"real: problem='{Pitzki.Core.Cs2Inventory.Problem}' items={Pitzki.Core.Cs2Inventory.Count} value={Pitzki.Core.Cs2Inventory.Money(Pitzki.Core.Cs2Inventory.Value)}");

        var card = new WidgetWindow(new Placed { Kind = "cs2inv", X = Far, Y = Far });
        card.Show();
        await Task.Delay(1500);
        Snap(card, Path.Combine(dir, "cs2inv-today.png"));
        if (card.Body.Content is not InventoryWidget w) { log.Add("no inventory card!"); card.Close(); return; }
        log.Add($"card: '{w.ValueText}' '{w.MoveText}'");

        // a made-up history so the change and the line show, then a price going up while it is on screen
        var real = Pitzki.Core.Cs2Inventory.History.ToList();
        double v = Pitzki.Core.Cs2Inventory.Value;
        var hist = new List<Pitzki.Core.Cs2InvPoint>();
        for (int d = 9; d >= 1; d--) hist.Add(new() { Day = DateTime.Today.AddDays(-d), Value = Math.Round(v * (0.9 + 0.01 * (9 - d)) + (d % 3) * 0.5, 2) });
        hist.Add(new() { Day = DateTime.Today, Value = v });
        Pitzki.Core.Cs2Inventory.UseHistory(hist);
        w.Redraw();
        await Task.Delay(1200);
        Snap(card, Path.Combine(dir, "cs2inv-history.png"));
        int moves = Live.Moves;
        var bumped = Pitzki.Core.Cs2Inventory.Items.Select((i, k) => k == 0 ? i with { Price = i.Price * 1.15 } : i).ToList();
        Pitzki.Core.Cs2Inventory.Use(bumped, DateTime.UtcNow);
        w.Redraw();
        await Task.Delay(200);
        Snap(card, Path.Combine(dir, "cs2inv-moving.png"));
        await Task.Delay(1200);
        Snap(card, Path.Combine(dir, "cs2inv-after.png"));
        log.Add($"after a price change: '{w.ValueText}' '{w.MoveText}', live moves {Live.Moves - moves} (expect > 0)");
        Pitzki.Core.Cs2Inventory.UseHistory(real);
        card.Close();
    }

    // ----- the Steam items card -----

    static async Task SteamItemsCheck(string dir, List<string> log)
    {
        log.Add($"price words: '$0.05' -> {Pitzki.Core.SteamItems.ReadPrice("{\"success\":true,\"lowest_price\":\"$0.05\",\"median_price\":\"$0.07\"}")}, " +
            $"none listed -> {Pitzki.Core.SteamItems.ReadPrice("{\"success\":true,\"median_price\":\"$1,204.50\"}")}, nothing -> {Pitzki.Core.SteamItems.ReadPrice("{\"success\":false}")}");

        var card = new WidgetWindow(new Placed { Kind = "steamitems", X = Far, Y = Far });
        card.Show();
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var finished = new TaskCompletionSource();
        Pitzki.Core.SteamItems.Changed += () => { if (Pitzki.Core.SteamItems.Waiting == 0) finished.TrySetResult(); };
        Pitzki.Core.SteamItems.Start(force: true);
        await Task.Delay(25000);
        Snap(card, Path.Combine(dir, "steamitems-pricing.png"));
        if (card.Body.Content is not SteamItemsWidget w) { log.Add("no card!"); card.Close(); return; }
        log.Add($"after 25 s: '{w.ValueText}', waiting {Pitzki.Core.SteamItems.Waiting}");
        await Task.WhenAny(finished.Task, Task.Delay(TimeSpan.FromMinutes(6)));
        await Task.Delay(1500);
        Snap(card, Path.Combine(dir, "steamitems-real.png"));
        log.Add($"real: problem='{Pitzki.Core.SteamItems.Problem}' items={Pitzki.Core.SteamItems.Count} for sale={Pitzki.Core.SteamItems.ForSale} waiting={Pitzki.Core.SteamItems.Waiting} value={Pitzki.Core.Cs2Inventory.Money(Pitzki.Core.SteamItems.Value)} in {clock.Elapsed.TotalSeconds:0} s | card '{w.ValueText}' '{w.MoveText}'");
        foreach (var i in Pitzki.Core.SteamItems.Items.Take(8)) log.Add($"   {i.Shown} ({i.Kind}, {i.Game}) x{i.Count} @ {i.Price}");
        card.Close();
    }

    // ----- the skin cards: shekels, movers, all my Steam, the watch list, cases -----

    static async Task SkinsCheck(string dir, List<string> log)
    {
        bool hadShekels = Pitzki.Core.MoneyPrefs.Shekels;
        try
        {
            App.AlertsShown.Clear();
            var done = new TaskCompletionSource();
            Pitzki.Core.Cs2Inventory.Changed += () => done.TrySetResult();
            Pitzki.Core.Cs2Inventory.Start(force: true);
            await Task.WhenAny(done.Task, Task.Delay(120000));
            Pitzki.Core.SteamItems.Start();
            await Pitzki.Core.Cs2Skins.WaitForPricesAsync();
            log.Add($"inventory {Pitzki.Core.Cs2Inventory.Money(Pitzki.Core.Cs2Inventory.Value)}, steam items {Pitzki.Core.Cs2Inventory.Money(Pitzki.Core.SteamItems.Value)}");

            // movers: a made-up yesterday where the knife was cheaper and the AK dearer
            var items = Pitzki.Core.Cs2Inventory.Items;
            var yesterday = items.Where(i => i.Price > 0).ToDictionary(i => i.Name, i => i.Price);
            var knife = items.First(i => i.Price > 0);
            var second = items.Where(i => i.Price > 0).Skip(1).First();
            yesterday[knife.Name] = knife.Price / 1.12;   // up 12%: a note should come
            yesterday[second.Name] = second.Price * 1.04; // down a little: no note
            Pitzki.Core.Cs2Inventory.UseDay(DateTime.Today.AddDays(-1), yesterday);

            var (w, c) = await OpenCard<InventoryWidget>(new Placed { Kind = "cs2inv" }, 1500);
            c.Redraw();
            await Task.Delay(900);
            Snap(w, Path.Combine(dir, "skins-inventory-dollars.png"));
            log.Add($"inventory card: '{c.ValueText}' movers '{c.MoversText}'");
            log.Add($"skin alerts: {string.Join(" / ", App.AlertsShown.Where(a => !a.Contains("today")))}");
            Pitzki.Core.MoneyPrefs.Shekels = true;
            await Task.Delay(1500);
            Snap(w, Path.Combine(dir, "skins-inventory-shekels.png"));
            log.Add($"in shekels: '{c.ValueText}' (rate {Pitzki.Core.MoneyPrefs.ShekelRate:0.000})");
            Pitzki.Core.MoneyPrefs.Shekels = false;
            await Task.Delay(900);
            log.Add($"back in dollars: '{c.ValueText}'");
            w.Close();

            // all my Steam
            var (w2, n) = await OpenCard<NetWorthWidget>(new Placed { Kind = "networth" }, 1500);
            n.Redraw();
            await Task.Delay(900);
            Snap(w2, Path.Combine(dir, "skins-networth.png"));
            log.Add($"all my steam: '{n.TotalText}'");
            w2.Close();

            // the watch list
            var placed = new Placed { Kind = "skinwatch" };
            var (w3, s) = await OpenCard<SkinWatchWidget>(placed, 1200);
            var found = s.Find("redline field");
            log.Add($"search 'redline field': {string.Join(" | ", found)}");
            if (found.Count > 0) s.Watch(found[0], 1000);   // surely under: a note
            var found2 = s.Find("asiimov awp field");
            if (found2.Count > 0) s.Watch(found2[0], 1);    // surely above: no note
            await Task.Delay(1200);
            Snap(w3, Path.Combine(dir, "skins-watch.png"));
            foreach (var line in s.Shown) log.Add("  watch " + line);
            log.Add($"watch alerts: {string.Join(" / ", App.AlertsShown.Where(a => a.Contains("your price")))} | saved {string.Join(", ", placed.Items)}");
            w3.Close();

            // cases
            var (w4, k) = await OpenCard<CasesWidget>(new Placed { Kind = "cases" }, 1000);
            var ready = DateTime.Now;
            while (!Pitzki.Core.Cs2Cases.Ready && DateTime.Now - ready < TimeSpan.FromSeconds(90)) await Task.Delay(500);
            k.Redraw();
            await Task.Delay(1500);
            Snap(w4, Path.Combine(dir, "skins-cases.png"));
            foreach (var line in k.Shown) log.Add("  case " + line);
            var fever = Pitzki.Core.Cs2Cases.Rate("Fever Case");
            log.Add($"fever case: cost {fever?.Cost:0.00} expected {fever?.Expected:0.00} chance {fever?.ChanceToProfit:P1} best {fever?.BestName} {fever?.Best:0}");
            w4.Close();
        }
        finally { Pitzki.Core.MoneyPrefs.Shekels = hadShekels; }
    }

    // ----- the money cards -----

    static async Task<(WidgetWindow Window, T Card)> OpenCard<T>(Placed placed, int wait = 3500) where T : class
    {
        placed.X = Far; placed.Y = Far;
        var window = new WidgetWindow(placed);
        window.Show();
        await Task.Delay(wait);
        return (window, (window.Body.Content as T)!);
    }

    static async Task MoneyCheck(string dir, List<string> log)
    {
        // names as people write them
        foreach (string typed in new[] { "S&P 500", "s&p500", "Nasdaq, Dow Jones, ת\"א 35", "apple, nvidia", "ביטקוין", "TEVA.TA", "zzzz no such company qq" })
            log.Add($"names: '{typed}' -> {string.Join(", ", await Pitzki.Core.Market.ResolveAsync(typed))}");
        {
            var placed = new Placed { Kind = "chart" };
            var (w, c) = await OpenCard<ChartWidget>(placed, 1500);
            await c.Choose("S&P 500");
            await Task.Delay(2500);
            log.Add($"chart for 'S&P 500': saved '{placed.Text}', shows {c.PriceText} {c.MoveText}");
            Snap(w, Path.Combine(dir, "money-chart-sp500.png"));
            w.Close();
        }

        // the chart, a day then a year
        {
            var (w, c) = await OpenCard<ChartWidget>(new Placed { Kind = "chart", Text = "NVDA" });
            Snap(w, Path.Combine(dir, "money-chart-1d.png"));
            log.Add($"chart 1D: {c.PriceText} {c.MoveText} points={c.Points}");
            c.Pick("1y");
            await Task.Delay(400);
            Snap(w, Path.Combine(dir, "money-chart-drawing.png"));
            await Task.Delay(3000);
            Snap(w, Path.Combine(dir, "money-chart-1y.png"));
            log.Add($"chart 1Y: {c.PriceText} {c.MoveText} points={c.Points}");
            w.Close();
        }
        // my portfolio
        {
            var placed = new Placed { Kind = "portfolio" };
            var (w, c) = await OpenCard<PortfolioWidget>(placed, 800);
            log.Add($"portfolio bad input refused: {!await c.Add("AAPL", "ten", "150")}");
            await c.Add("Apple", "10", "150");
            await c.Add("NVDA", "5", "120");
            await c.Add("TEVA.TA", "20", "60");
            await c.Add("AAPL", "10", "250");   // more of the same: 20 at 200 on average
            await Task.Delay(4000);
            Snap(w, Path.Combine(dir, "money-portfolio.png"));
            log.Add($"portfolio: {c.TotalText} | {c.GainText} | items {string.Join(", ", placed.Items)}");
            foreach (var r in c.RowsText) log.Add("   " + r);
            w.Close();
        }
        // indexes, then the bell at 2% (one alert for a big mover, not two)
        {
            App.AlertsShown.Clear();
            var (w, c) = await OpenCard<StocksWidget>(new Placed { Kind = "indices" });
            Snap(w, Path.Combine(dir, "money-indices.png"));
            foreach (var (sym, price, move) in c.Shown) log.Add($"  index {sym}: {price} {move}");
            w.Close();
            var (w2, s2) = await OpenCard<StocksWidget>(new Placed { Kind = "stocks", Text = "AAPL, NVDA", Number = 2 });
            s2.Feed(new[] { new StockQuote("AAPL", "Apple Inc.", "USD", 380, 350, new List<double> { 350, 380 }, true) });
            s2.Feed(new[] { new StockQuote("AAPL", "Apple Inc.", "USD", 381, 350, new List<double> { 350, 381 }, true) });
            log.Add($"alerts at 2%: {App.AlertsShown.Count(a => a.Contains("AAPL"))} for AAPL (expect 1) | {string.Join(" / ", App.AlertsShown)}");
            Snap(w2, Path.Combine(dir, "money-stocks-bell.png"));
            w2.Close();
        }
        // market hours: real, then made-up times
        {
            var (w, c) = await OpenCard<MarketClockWidget>(new Placed { Kind = "markets" });
            Snap(w, Path.Combine(dir, "money-markets.png"));
            foreach (var line in c.Shown) log.Add("  market: " + line);
            var ny = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
            var friday = new DateTimeOffset(2026, 11, 13, 21, 30, 0, TimeSpan.Zero);
            var opened = new DateTimeOffset(2026, 11, 13, 14, 30, 0, TimeSpan.Zero).ToUnixTimeSeconds();
            var week = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };
            log.Add($"  next open after the Friday close: {MarketClockWidget.NextOpen(opened, friday, week, ny):ddd yyyy-MM-dd HH:mm zzz} (expect Mon 2026-11-16 09:30 -05:00)");
            c.Now = () => new DateTimeOffset(2026, 11, 13, 16, 0, 0, TimeSpan.Zero);
            c.Set(0, opened, new DateTimeOffset(2026, 11, 13, 21, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds());
            await Task.Delay(1200);
            log.Add("  market made-up open: " + c.Shown.First());
            Snap(w, Path.Combine(dir, "money-markets-open.png"));
            w.Close();
        }
        // pretend portfolio
        {
            var placed = new Placed { Kind = "paper" };
            var (w, c) = await OpenCard<PaperTradeWidget>(placed, 800);
            await c.Trade("AAPL", "10", buy: true);
            await Task.Delay(700);
            log.Add($"paper buy: {c.SaidText} cash={c.State.Cash:0.00}");
            await c.Trade("AAPL", "100", buy: true);
            await Task.Delay(700);
            log.Add($"paper too much: {c.SaidText}");
            await c.Trade("NVDA", "20", buy: true);
            await c.Trade("AAPL", "4", buy: false);
            await Task.Delay(700);
            log.Add($"paper sell: {c.SaidText}");
            await c.Trade("MSFT", "1", buy: false);
            await Task.Delay(700);
            log.Add($"paper sell none: {c.SaidText}");
            await c.Trade("TEVA.TA", "1", buy: true);
            await Task.Delay(700);
            log.Add($"paper shekels: {c.SaidText}");
            await Task.Delay(1500);
            Snap(w, Path.Combine(dir, "money-paper.png"));
            log.Add($"paper: {c.TotalText} trades={c.State.Trades} saved={placed.Text}");
            w.Close();
        }
        // higher or lower: a guess, then one made a week ago that gets settled
        {
            var placed = new Placed { Kind = "guess", Text = "MSFT" };
            var (w, c) = await OpenCard<GuessWidget>(placed);
            log.Add($"guess: {c.QuestionText}");
            c.Press(true);
            await Task.Delay(2500);
            log.Add($"guessed: open='{placed.Name}' | {c.QuestionText}");
            Snap(w, Path.Combine(dir, "money-guess-open.png"));
            placed.Name = $"{DateTime.Today.AddDays(-7):yyyy-MM-dd}|1|up";
            await c.Refresh();
            await Task.Delay(1200);
            log.Add($"settled: open='{placed.Name}' score={placed.Number} | {c.ScoreText} | record {string.Join(",", placed.Items)}");
            Snap(w, Path.Combine(dir, "money-guess-settled.png"));
            w.Close();
        }
        // crypto, dollar and euro, and the converter
        foreach (var kind in new[] { "crypto", "rates" })
        {
            var (w, c) = await OpenCard<StocksWidget>(new Placed { Kind = kind });
            Snap(w, Path.Combine(dir, $"money-{kind}.png"));
            foreach (var (sym, price, move) in c.Shown) log.Add($"  {kind} {sym}: {price} {move}");
            w.Close();
        }
        {
            var placed = new Placed { Kind = "convert" };
            var (w, c) = await OpenCard<ConverterWidget>(placed);
            log.Add($"convert 100 USD>ILS: {c.AnswerText}");
            await c.Convert("50", "EUR", "USD");
            await Task.Delay(900);
            log.Add($"convert 50 EUR>USD: {c.AnswerText} (saved '{placed.Text}')");
            await c.Convert("0.01", "BTC", "ILS");
            await Task.Delay(900);
            log.Add($"convert 0.01 BTC>ILS: {c.AnswerText}");
            Snap(w, Path.Combine(dir, "money-convert.png"));
            await c.Convert("abc", "USD", "ILS");
            await Task.Delay(900);
            log.Add($"convert abc: {c.AnswerText}");
            w.Close();
        }
        // news
        {
            var (w, c) = await OpenCard<MarketNewsWidget>(new Placed { Kind = "marketnews", Text = "AAPL, NVDA, TSLA" }, 6000);
            Snap(w, Path.Combine(dir, "money-news.png"));
            foreach (var line in c.Shown) log.Add("  news " + line);
            w.Close();
        }
    }


    // ----- the stocks card -----

    static async Task StocksCheck(string dir, List<string> log)
    {
        log.Add("parse: " + string.Join("|", StocksWidget.Parse("aapl, nvda;TEVA.TA  ^gspc, bad$one, aapl, a,b,c,d")));
        var made = StocksWidget.Read("X", "{\"chart\":{\"result\":[{\"meta\":{\"currency\":\"ILA\",\"regularMarketPrice\":5120,\"chartPreviousClose\":5000,\"shortName\":\"Teva\",\"currentTradingPeriod\":{\"regular\":{\"start\":100,\"end\":200}}}," +
            "\"indicators\":{\"quote\":[{\"close\":[5000,null,5050,5120]}]}}]}}", DateTimeOffset.FromUnixTimeSeconds(150));
        log.Add($"made-up: {made?.Name} {made?.Price} {made?.Percent:0.00}% open={made?.Open} points={made?.Day.Count}");
        log.Add($"unknown: {(StocksWidget.Read("X", "{\"chart\":{\"result\":null,\"error\":{}}}", DateTimeOffset.UtcNow) == null ? "null" : "?!")}");

        var card = new WidgetWindow(new Placed { Kind = "stocks", X = Far, Y = Far, Text = "AAPL, NVDA, TEVA.TA, ^GSPC, NOSUCHSTOCK1" });
        card.Show();
        await Task.Delay(6000);
        Snap(card, Path.Combine(dir, "stocks-real.png"));
        if (card.Body.Content is not StocksWidget w) { log.Add("no stocks card!"); card.Close(); return; }
        foreach (var (sym, price, move) in w.Shown) log.Add($"  {sym}: {price} {move}");

        int moves = Live.Moves;
        var day = new List<double> { 100, 101, 99.5, 102, 103.4 };
        w.Feed(new[] { new StockQuote("AAPL", "Apple Inc.", "USD", 412.5, 400, day, true) });
        await Task.Delay(180);
        Snap(card, Path.Combine(dir, "stocks-moving.png"));
        await Task.Delay(1200);
        Snap(card, Path.Combine(dir, "stocks-after.png"));
        log.Add($"live moves on a price change: {Live.Moves - moves} (expect > 0); AAPL now {w.Shown.First().Price} {w.Shown.First().Move}");
        card.Close();
    }

    // ----- the Discord card -----

    static async Task DiscordCheck(string dir, List<string> log)
    {
        var card = new WidgetWindow(new Placed { Kind = "discord", X = Far, Y = Far });
        card.Show();
        await Task.Delay(9000);
        Snap(card, Path.Combine(dir, "discord-real.png"));
        if (card.Body.Content is not DiscordWidget w) { log.Add("no discord card!"); card.Close(); return; }
        log.Add($"real: channel '{w.Channel}', talking '{w.Talking}', faces {w.Faces}, state '{w.State}', rpc '{SoundWidget.Voice.Rpc.Status}' connected={SoundWidget.Voice.Rpc.Connected} voice={SoundWidget.Voice.Rpc.VoiceReady}");

        int moves = Live.Moves;
        var people = new List<(string, string, string?, bool, bool)>
        {
            ("1", "Yahav", null, true, false), ("2", "Noam", null, false, true), ("3", "Itay", null, false, false),
        };
        w.Feed(people, "Gaming night", false, false);
        w.MessageFrom("Discord"); w.MessageFrom("Discord"); w.MessageFrom("Chrome");
        await Task.Delay(1500);
        Snap(card, Path.Combine(dir, "discord-made.png"));
        log.Add($"made-up: channel '{w.Channel}', talking '{w.Talking}', faces {w.Faces} (expect 3), mail '{w.Mail}' (expect 2)");
        people.RemoveAt(2);
        people[0] = ("1", "Yahav", null, false, false);
        people[1] = ("2", "Noam", null, true, false);
        w.Feed(people, "Gaming night", true, false);
        await Task.Delay(1200);
        Snap(card, Path.Combine(dir, "discord-after.png"));
        log.Add($"after: talking '{w.Talking}', faces {w.Faces} (expect 2), live moves {Live.Moves - moves} (expect > 0)");
        card.Close();
    }

    // ----- the Claude usage card -----

    static async Task ClaudeCheck(string dir, List<string> log)
    {
        var made = Pitzki.Core.ClaudeUsage.Read("{\"five_hour\":{\"utilization\":23.0,\"resets_at\":\"2026-11-15T01:40:00+00:00\"}," +
            "\"seven_day\":{\"utilization\":63,\"resets_at\":\"2026-11-16T19:00:00Z\"},\"seven_day_oauth_apps\":null," +
            "\"seven_day_fable\":{\"utilization\":85.4,\"resets_at\":\"2026-11-16T19:00:00Z\"},\"extra_usage\":{\"is_enabled\":false}}");
        foreach (var l in made) log.Add($"old fields: {l.Name} | {l.Used}");
        made = Pitzki.Core.ClaudeUsage.Read("{\"five_hour\":{\"utilization\":1},\"limits\":[{\"kind\":\"session\",\"group\":\"session\",\"percent\":23,\"severity\":\"normal\",\"resets_at\":\"2026-11-15T01:40:00.26+00:00\",\"scope\":null}," +
            "{\"kind\":\"weekly_all\",\"group\":\"weekly\",\"percent\":63,\"severity\":\"normal\",\"resets_at\":\"2026-11-16T19:00:00+00:00\"}," +
            "{\"kind\":\"weekly_scoped\",\"group\":\"weekly\",\"percent\":85,\"severity\":\"warning\",\"resets_at\":\"2026-11-16T19:00:00+00:00\",\"scope\":{\"model\":{\"id\":null,\"display_name\":\"Fable\"},\"surface\":null}}]}");
        foreach (var l in made) log.Add($"made-up: {l.Name} | {l.Used} | {(l.Resets is DateTime r ? Pitzki.Core.ClaudeUsage.When(r) : "-")}");
        log.Add($"names: {Pitzki.Core.ClaudeUsage.NameOf("seven_day_opus")} / {Pitzki.Core.ClaudeUsage.NameOf("seven_day_sonnet")} / {Pitzki.Core.ClaudeUsage.NameOf("other") ?? "null"}");
        log.Add($"no file: {Pitzki.Core.ClaudeUsage.ReadSignIn(@"C:
o\suchile.json").Problem}");
        log.Add($"sign-in file of this user: {Pitzki.Core.ClaudeUsage.CredentialsPath()} exists={File.Exists(Pitzki.Core.ClaudeUsage.CredentialsPath())}");

        var card = new WidgetWindow(new Placed { Kind = "claude", X = Far, Y = Far });
        card.Show();
        await Task.Delay(6000);
        Snap(card, Path.Combine(dir, "claude-real.png"));
        if (card.Body.Content is not ClaudeUsageWidget w) { log.Add("no claude card!"); card.Close(); return; }
        log.Add($"real: plan '{w.Plan}', state '{w.State}', answer {w.LastStatus}");
        foreach (var (name, resets, used) in w.Shown) log.Add($"  {name} | {resets} | {used}");

        // Anthropic says "wait": the numbers stay, with when they are from; once the wait is over it asks again by itself
        Pitzki.Core.ClaudeUsage.PretendWait(TimeSpan.FromSeconds(8), TimeSpan.FromMinutes(20));
        await w.Refresh();
        await Task.Delay(900);
        Snap(card, Path.Combine(dir, "claude-waiting.png"));
        log.Add($"waiting: rows {w.Shown.Count()} (expect 3), state '{w.State}', answer {w.LastStatus} (expect 429)");
        await Task.Delay(9000);
        await w.Refresh();
        await Task.Delay(900);
        var mem = Pitzki.Core.ClaudeUsage.Remembered();
        log.Add($"after the wait: rows {w.Shown.Count()}, state '{w.State}', answer {w.LastStatus} (expect 200), strikes {mem.Strikes}");
        await w.Refresh();
        log.Add($"right after: answer {w.LastStatus} (200 from the shared answer, nothing asked)");

        int moves = Live.Moves;
        w.Feed(made.Select(l => l with { Used = Math.Min(100, l.Used + 12) }));
        await Task.Delay(180);
        Snap(card, Path.Combine(dir, "claude-moving.png"));
        await Task.Delay(1200);
        Snap(card, Path.Combine(dir, "claude-after.png"));
        log.Add($"live moves on a change: {Live.Moves - moves} (expect > 0)");
        foreach (var (name, resets, used) in w.Shown) log.Add($"  after: {name} | {resets} | {used}");
        card.Close();
    }

    // ----- every sky the weather card can draw, one picture each -----

    static async Task Skies(string dir, List<string> log)
    {
        var sky = new WidgetWindow(new Placed { Kind = "weather", X = Far, Y = Far });
        sky.Show();
        await Task.Delay(900);
        if (sky.Body.Content is WeatherWidget card)
            foreach (string weather in new[] { "sun", "moon", "cloud", "fog", "rain", "storm", "snow" })
            {
                card.Weather(weather);
                // the lightning is only lit for a moment, so it gets a picture of its own
                if (weather == "storm")
                {
                    await Task.Delay(120);
                    Snap(sky, Path.Combine(dir, "sky-storm-lit.png"));
                }
                await Task.Delay(800);          // long enough for the motion to be under way
                Snap(sky, Path.Combine(dir, $"sky-{weather}.png"));
                log.Add($"sky {weather}: drawn");
            }
        sky.Close();
    }

    // ----- live changes move from the old to the new, and never jump -----

    static async Task Motion(string dir, List<string> log)
    {
        // a card opening puts its first readings straight in: nothing rolls
        int before = Live.Moves;
        var seen = new List<string>();
        Live.Watch = seen.Add;
        var clock = new WidgetWindow(new Placed { Kind = "clock", X = Far, Y = Far });
        clock.Show();
        await Task.Delay(1200);
        Live.Watch = null;
        log.Add($"live: a clock card opening made {Live.Moves - before} move(s) (expect 0) {string.Join("; ", seen)}");
        clock.Close();

        // the countdown: the number rolls, the bar slides, the colour blends
        var placed = new Placed { Kind = "countdown", Name = "Rome", Until = DateTime.Today.AddDays(12), Since = DateTime.Today.AddDays(-18), X = Far, Y = Far };
        var window = new WidgetWindow(placed);
        window.Show();
        await Task.Delay(900);
        if (window.Body.Content is CountdownWidget card)
        {
            double fromBar = card.Along.Drawn;
            placed.Until = DateTime.Today.AddDays(2);
            before = Live.Moves;
            card.Redraw();
            await Task.Delay(60);
            log.Add($"live: 12 -> 2 days, 60 ms in: says '{card.Days.Text}', faded to {card.Days.Opacity:0.00} (expect still '12', under 1)");
            Snap(window, Path.Combine(dir, "live-roll-out.png"));
            await Task.Delay(140);
            log.Add($"live: 200 ms in: says '{card.Days.Text}', coming back at {card.Days.Opacity:0.00}, bar at {card.Along.Drawn:0} px on its way from {fromBar:0} to {card.Along.Part * 166:0}");
            Snap(window, Path.Combine(dir, "live-roll-in.png"));
            await Task.Delay(800);
            log.Add($"live: settled: says '{card.Days.Text}', opacity {card.Days.Opacity:0.00}, bar {card.Along.Drawn:0} px (expect '2', 1.00, {card.Along.Part * 166:0}); {Live.Moves - before} move(s)");
            Snap(window, Path.Combine(dir, "live-roll-end.png"));

            // the day itself: the number turns into a word, smaller, and green
            placed.Until = DateTime.Today;
            card.Redraw();
            await Task.Delay(180);
            var mid = ((SolidColorBrush)card.Days.Foreground).Color;
            log.Add($"live: to Today, 180 ms in: colour {mid}, size {card.Days.FontSize:0.0} (on its way from the accent to green, and 40 to 30)");
            await Task.Delay(800);
            var end = ((SolidColorBrush)card.Days.Foreground).Color;
            var green = ((SolidColorBrush)WidgetBody.Of("GoodBrush")).Color;
            bool isGreen = Math.Abs(end.R - green.R) + Math.Abs(end.G - green.G) + Math.Abs(end.B - green.B) <= 3;
            log.Add($"live: settled on '{card.Days.Text}', colour {(isGreen ? "green" : end.ToString())}, size {card.Days.FontSize:0} (expect Today, green, 30)");
            Snap(window, Path.Combine(dir, "live-today.png"));
        }
        window.Close();

        // the graph scrolls a new reading in rather than jumping a step
        var pc = new WidgetWindow(new Placed { Kind = "system", X = Far, Y = Far });
        pc.Show();
        await Task.Delay(900);
        if (pc.Body.Content is SystemWidget system)
        {
            system.Demo();
            await Task.Delay(900);
            system.OneMore();
            await Task.Delay(150);
            log.Add($"live: graph 150 ms after a reading still sliding: {system.Graph.Slide:0.00} of a step (expect between 0 and 1)");
            Snap(pc, Path.Combine(dir, "live-graph-mid.png"));
            await Task.Delay(900);
            log.Add($"live: graph settled: {system.Graph.Slide:0.00} (expect 0.00)");
        }
        pc.Close();

        // a task slides in when added, and slides away before it is gone
        var list = new Placed { Kind = "tasks", X = Far, Y = Far };
        var tasksWindow = new WidgetWindow(list);
        tasksWindow.Show();
        await Task.Delay(800);
        if (tasksWindow.Body.Content is TasksWidget tasks)
        {
            tasks.Add("Buy milk");
            await Task.Delay(400);
            tasks.Add("Call mum");
            await Task.Delay(60);
            log.Add($"live: new task 60 ms in at opacity {tasks.LineAt(1)?.Opacity:0.00} (expect under 1)");
            Snap(tasksWindow, Path.Combine(dir, "live-task-arrive.png"));
            await Task.Delay(600);
            tasks.DropAt(0);
            await Task.Delay(80);
            log.Add($"live: dropping the first, 80 ms in: still {list.Items.Count} on the list, sliding at {tasks.LineAt(0)?.Opacity:0.00}");
            await Task.Delay(500);
            log.Add($"live: after it slid away: {string.Join(" | ", list.Items)} (expect 0|Call mum)");
        }
        tasksWindow.Close();

        // a whole row of cards, left alone for a while: whatever really changed has rolled, and nothing is stuck
        foreach (var kind in new[] { "system", "music", "timer", "pomodoro" })
        {
            var p = Sample(kind);
            if (kind == "timer") { p.Number = 1; p.Until = DateTime.Now.AddSeconds(40); }
            if (kind == "pomodoro") { p.Name = "focus"; p.Until = DateTime.Now.AddMinutes(10); p.Since = DateTime.Today; }
            var w = new WidgetWindow(p);
            w.Show();
            await Task.Delay(700);
            before = Live.Moves;
            await Task.Delay(3200);
            log.Add($"live: {kind} over 3 s: {Live.Moves - before} move(s), {Faded(w)} left faded");
            Snap(w, Path.Combine(dir, $"live-{kind}.png"));
            w.Close();
        }
        log.Add($"live: {Live.Moves} moves in the whole test, {Live.Underway} roll(s) still under way (expect 0)");
    }

    /// <summary>Writing on show that is still faded: a roll that never came back would leave some.</summary>
    static int Faded(DependencyObject root)
    {
        int faded = 0;
        void Walk(DependencyObject what)
        {
            // writing meant to be faint (the sleeping z, a cross that shows on hover) is not counted:
            // only what a roll faded and is still holding there
            if (what is System.Windows.Controls.TextBlock { IsVisible: true, Text.Length: > 0 } text && text.Opacity < 0.5 && text.HasAnimatedProperties) faded++;      // some writing is meant to be a little faded; a stuck roll sits near 0
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(what); i++) Walk(VisualTreeHelper.GetChild(what, i));
        }
        Walk(root);
        return faded;
    }

    /// <summary>Every word a window ended up showing, in one line.</summary>
    static int Lines(WidgetWindow w) => w.LineCount;
    static bool LitUp(WidgetWindow w) => w.Glowing;

    /// <summary>True when that very object is drawn somewhere inside the card.</summary>
    static bool Holds(DependencyObject root, object? what)
    {
        if (what == null) return false;
        if (ReferenceEquals(root, what)) return true;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
            if (Holds(VisualTreeHelper.GetChild(root, i), what)) return true;
        return false;
    }

    /// <summary>The tray menu: drawn like Pitzki's, each switch really flips its setting and stays in step.</summary>
    static async Task TrayMenuCheck(App app, string dir, List<string> log)
    {
        var board = App.Board;
        board.LockAll = false; board.NoSnap = false; board.NoJoin = false; board.AutoGame = true; board.Profile = "";
        app.SetGlass(true);

        var menu = app.ShowTrayMenu();
        menu.Left = Far; menu.Top = Far;
        await Task.Delay(1200);
        log.Add($"tray: {Math.Round(menu.ActualWidth)}x{Math.Round(menu.ActualHeight)} | {Words(menu)}");
        Snap(menu, Path.Combine(dir, "tray-menu.png"));

        void Press(System.Windows.Controls.Button row) => row.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        string State() => $"lock={board.LockAll}/{menu.LockSwitch.IsChecked} snap={!board.NoSnap}/{menu.SnapSwitch.IsChecked} join={!board.NoJoin}/{menu.JoinSwitch.IsChecked} " +
                          $"glass={board.Glass}/{menu.GlassSwitch.IsChecked} auto={board.AutoGame}/{menu.AutoSwitch.IsChecked} profile='{board.Profile}'/'{menu.ProfileName.Text}'";
        log.Add("  before: " + State());
        Press(menu.RowLock); Press(menu.RowSnap); Press(menu.RowJoin); Press(menu.RowAuto); Press(menu.RowProfile);
        await Task.Delay(500);
        log.Add($"  after one press each: {State()} | still open {menu.IsVisible} (expect True)");
        Press(menu.RowProfile); Press(menu.RowProfile);
        log.Add($"  profile round trip: '{board.Profile}' (expect '')");
        Press(menu.RowGlass);
        await Task.Delay(700);
        log.Add($"  glass off: {State()}");
        Snap(menu, Path.Combine(dir, "tray-menu-noglass.png"));
        Press(menu.RowGlass);
        await Task.Delay(500);
        Snap(menu, Path.Combine(dir, "tray-menu-after.png"));

        // a second right click starts a fresh one rather than stacking two
        var again = app.ShowTrayMenu();
        again.Left = Far; again.Top = Far;
        await Task.Delay(400);
        log.Add($"  second open: first still showing {menu.IsVisible} (expect False)");
        again.Close();
    }

    static string Words(DependencyObject root)
    {
        var said = new List<string>();
        void Walk(DependencyObject what)
        {
            if (what is System.Windows.Controls.TextBlock { Text.Length: > 0 } text) said.Add(text.Text);
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(what); i++) Walk(VisualTreeHelper.GetChild(what, i));
        }
        Walk(root);
        return string.Join(" / ", said);
    }

    static void Snap(Window window, string path)
    {
        int w = Math.Max((int)window.ActualWidth, 1), h = Math.Max((int)window.ActualHeight, 1);
        var content = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        content.Render((Visual)window.Content);

        // on a plain grey square, the way it would look over a wallpaper
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(0x3A, 0x3F, 0x4A)), null, new Rect(0, 0, w, h));
            dc.DrawImage(content, new Rect(0, 0, w, h));
        }
        var final = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        final.Render(visual);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(final));
        using var file = File.Create(path);
        encoder.Save(file);
    }
}
