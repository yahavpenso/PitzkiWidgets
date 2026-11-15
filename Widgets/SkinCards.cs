using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using static PitzkiWidgets.Widgets.MarketBits;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Everything the Steam account holds, in one number: the CS2 inventory and the Steam items (cards,
/// backgrounds, emoticons) together, each one under it with how it moved since yesterday.
/// </summary>
public sealed class NetWorthWidget : UserControl
{
    readonly TextBlock _total = new() { Style = Big, FontSize = 28 };
    readonly TextBlock _move = new() { FontSize = 11.5 };
    readonly (TextBlock Value, TextBlock Move, Meter Bar)[] _parts = new (TextBlock, TextBlock, Meter)[2];
    readonly TextBlock _about = Small();
    double _was;
    bool _has;

    internal string TotalText => _total.Text;
    internal void Redraw() => Show();

    public NetWorthWidget()
    {
        var box = new StackPanel { Width = 230 };
        box.Children.Add(Head("All my Steam", MoneyKey(Show), Key("", () => { Cs2Inventory.Start(force: true); SteamItems.Start(force: true); }, icon: true, size: 11, tip: "Count again")));
        box.Children.Add(_total);
        box.Children.Add(_move);
        string[] names = { "CS2 inventory", "Steam items" };
        for (int i = 0; i < 2; i++)
        {
            var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var left = new StackPanel();
            left.Children.Add(new TextBlock { Text = names[i], FontSize = 12, FontWeight = FontWeights.SemiBold });
            var move = new TextBlock { FontSize = 10, Foreground = Of("SubtleBrush") };
            left.Children.Add(move);
            var value = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(value, 1);
            row.Children.Add(left);
            row.Children.Add(value);
            var bar = new Meter(230) { Margin = new Thickness(0, 3, 0, 0) };
            box.Children.Add(row);
            box.Children.Add(bar);
            _parts[i] = (value, move, bar);
        }
        box.Children.Add(_about);
        Content = box;

        void Changed() => Dispatcher.BeginInvoke(Show);
        Loaded += (_, _) => { Cs2Inventory.Changed += Changed; SteamItems.Changed += Changed; MoneyPrefs.Changed += Changed; };
        Unloaded += (_, _) => { Cs2Inventory.Changed -= Changed; SteamItems.Changed -= Changed; MoneyPrefs.Changed -= Changed; };
        this.Every(300, () => { if (!SelfTest.Wanted) { Cs2Inventory.Start(); SteamItems.Start(); } Show(); });
    }

    static (string Text, Brush Brush) Since(double now, Cs2InvPoint? before)
    {
        if (before == null) return ("first count today", Of("SubtleBrush"));
        double diff = now - before.Value;
        if (Math.Abs(diff) < 0.005) return ("no change since yesterday", Of("SubtleBrush"));
        return ($"{(diff > 0 ? "▲" : "▼")} {Cs2Inventory.Money(Math.Abs(diff))} since {(before.Day == DateTime.Today.AddDays(-1) ? "yesterday" : before.Day.ToString("d MMM", CultureInfo.InvariantCulture))}", UpDown(diff));
    }

    void Show()
    {
        double cs2 = Cs2Inventory.Value, items = SteamItems.Value, total = cs2 + items;
        if (!Cs2Inventory.HasItems && !SteamItems.HasItems)
        {
            _total.Say("--");
            _move.Say(Cs2Inventory.Problem == "private" || SteamItems.Problem == "private" ? "Your Steam inventory is private" : "Counting...");
            return;
        }
        string text = Cs2Inventory.Money(total);
        if (_has) _total.Count(text, MoneyPrefs.Shown(total), _was); else _total.Say(text);
        _was = MoneyPrefs.Shown(total);
        _has = true;
        _total.Tint(Of("AccentBrush"));

        var b1 = Cs2Inventory.Before;
        var b2 = SteamItems.Before;
        // yesterday's total: each part's yesterday, or its value now when that part has no yesterday yet
        Cs2InvPoint? both = b1 == null && b2 == null ? null
            : new Cs2InvPoint { Day = new[] { b1?.Day, b2?.Day }.Where(d => d != null).Min()!.Value, Value = (b1?.Value ?? cs2) + (b2?.Value ?? items) };
        var (all, allBrush) = Since(total, both);
        _move.Say(all);
        _move.Tint(allBrush);

        double[] values = { cs2, items };
        Cs2InvPoint?[] befores = { b1, b2 };
        for (int i = 0; i < 2; i++)
        {
            var (value, move, bar) = _parts[i];
            value.Say(Cs2Inventory.Money(values[i]));
            var (t, br) = Since(values[i], befores[i]);
            move.Say(t);
            move.Tint(br);
            bar.Glide(total > 0 ? values[i] / total : 0, 700);
            bar.Colour = i == 0 ? Of("AccentBrush") : Of("GoodBrush");
        }
        _about.Say($"{Cs2Inventory.Count + SteamItems.Count} things on your Steam account" + (SteamItems.Waiting > 0 ? $" · pricing {SteamItems.Waiting} more..." : ""));
    }
}

/// <summary>
/// Skins you want to buy, each with the price you'd pay. It shows each one's lowest price on Skinport
/// now, marks the ones at or under your price green, and gives a note from the tray when one gets
/// there. Typing a name finds the exact market item; the list lives in <c>placed.Items</c> ("name|price").
/// </summary>
public sealed class SkinWatchWidget : UserControl
{
    readonly Placed _placed;
    readonly StackPanel _rows = new(), _show = new(), _edit = new(), _found = new(), _editRows = new();
    readonly TextBox _search = Field("Search a skin, like redline ft", 60), _target = Field("Your price, $", 10, 100);
    readonly TextBlock _picked = new() { FontSize = 11.5, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 4) };
    readonly TextBlock _state = Small();
    readonly Dictionary<string, (Grid Row, TextBlock Price, TextBlock Note)> _lines = new();
    string _choice = "";

    internal IEnumerable<string> Shown => _lines.Select(l => $"{l.Key}: {l.Value.Price.Text} {l.Value.Note.Text}");
    internal void Redraw() => Show();
    internal List<string> Find(string typed) { _search.Text = typed; return Cs2Skins.Search(typed); }
    internal void Watch(string name, double price) { _choice = name; _target.Text = price.ToString(CultureInfo.InvariantCulture); AddWatch(); }

    /// <summary>"name|price": skin names have a bar in them too, so the price is after the last one.</summary>
    List<(string Name, double Price)> Watched => _placed.Items.Where(i => i.LastIndexOf('|') > 0)
        .Select(i => (i[..i.LastIndexOf('|')], double.TryParse(i[(i.LastIndexOf('|') + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0))
        .Where(w => w.Item2 > 0).ToList();

    public SkinWatchWidget(Placed placed)
    {
        _placed = placed;
        var pencil = Key("", Editing, icon: true, size: 11.5, tip: "Add or remove skins");
        _show.Children.Add(_rows);
        _show.Children.Add(_state);

        _edit.Children.Add(Boxed(_search));
        _edit.Children.Add(_found);
        _edit.Children.Add(_picked);
        var add = new StackPanel { Orientation = Orientation.Horizontal };
        add.Children.Add(Boxed(_target, new Thickness(0, 0, 6, 0)));
        add.Children.Add(Pill("Watch", AddWatch));
        add.Children.Add(Pill("Done", Back, quiet: true));
        _edit.Children.Add(add);
        _edit.Children.Add(_editRows);
        _edit.Visibility = Visibility.Collapsed;
        _search.TextChanged += (_, _) => DrawFound();
        _target.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) AddWatch(); };
        FocusOnClick(this, _search, _target);

        var box = new StackPanel { Width = 260 };
        box.Children.Add(Head("Skin watch list", MoneyKey(Show), pencil));
        box.Children.Add(_show);
        box.Children.Add(_edit);
        Content = box;

        void Changed() => Dispatcher.BeginInvoke(Show);
        Loaded += (_, _) => { Cs2Skins.Ready += Changed; MoneyPrefs.Changed += Changed; };
        Unloaded += (_, _) => { Cs2Skins.Ready -= Changed; MoneyPrefs.Changed -= Changed; };
        Cs2Skins.Start();
        this.Every(600, Show);
    }

    void DrawFound()
    {
        _found.Children.Clear();
        foreach (string name in Cs2Skins.Search(_search.Text, 5))
        {
            var chip = new Border
            {
                CornerRadius = new CornerRadius(7), Padding = new Thickness(7, 3, 7, 3), Margin = new Thickness(0, 0, 0, 3), Background = Of("ChipBrush"),
                Child = new TextBlock { Text = $"{name}  ·  {Cs2Inventory.Money(Cs2Skins.ExactPrice(name))}", FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis },
            };
            string n = name;
            Click(chip, () => { _choice = n; _picked.Text = n; _target.Focus(); });
            _found.Children.Add(chip);
        }
    }

    void AddWatch()
    {
        string typed = _target.Text.Trim().TrimStart('$', '₪');
        if (_choice.Length == 0) { _picked.Text = "Pick a skin from the list first"; return; }
        if (!double.TryParse(typed, NumberStyles.Float, CultureInfo.InvariantCulture, out var price) || price <= 0) { _picked.Text = "Write the most you'd pay, like 12.50"; return; }
        if (MoneyPrefs.Shekels) price /= MoneyPrefs.ShekelRate;   // kept in dollars, like the prices
        var list = Watched.Where(w => w.Name != _choice).ToList();
        list.Add((_choice, price));
        _placed.Items = list.Select(w => w.Name + "|" + w.Price.ToString(CultureInfo.InvariantCulture)).ToList();
        App.Board.Save();
        _choice = "";
        _picked.Text = "";
        _search.Text = _target.Text = "";
        DrawEditRows();
        Show();
    }

    void DrawEditRows()
    {
        _editRows.Children.Clear();
        foreach (var (name, price) in Watched)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock { Text = $"{name}  ≤ {Cs2Inventory.Money(price)}", FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis });
            string n = name;
            var remove = Key("", () =>
            {
                _placed.Items = Watched.Where(w => w.Name != n).Select(w => w.Name + "|" + w.Price.ToString(CultureInfo.InvariantCulture)).ToList();
                App.Board.Save();
                DrawEditRows();
                Show();
            }, icon: true, size: 10, tip: "Stop watching");
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            _editRows.Children.Add(row);
        }
    }

    void Show()
    {
        var watched = Watched;
        foreach (var gone in _lines.Keys.Where(k => watched.All(w => w.Name != k)).ToList())
        {
            _rows.Children.Remove(_lines[gone].Row);
            _lines.Remove(gone);
        }
        int under = 0;
        foreach (var (name, target) in watched)
        {
            if (!_lines.TryGetValue(name, out var line))
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 3) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var left = new StackPanel();
                left.Children.Add(new TextBlock { Text = Cs2Inventory.Short(name).TrimStart('★', ' '), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis, ToolTip = name });
                var note = new TextBlock { FontSize = 10 };
                left.Children.Add(note);
                var price = new TextBlock { FontSize = 13, FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center };
                Grid.SetColumn(price, 1);
                row.Children.Add(left);
                row.Children.Add(price);
                line = (row, price, note);
                _lines[name] = line;
                _rows.Children.Add(row);
                Live.Arrive(row, this, _lines.Count);
            }
            double now = Cs2Skins.ExactPrice(name);
            if (now <= 0) { line.Price.Say("--"); line.Note.Say("No price yet"); line.Note.Tint(Of("SubtleBrush")); continue; }
            line.Price.Say(Cs2Inventory.Money(now));
            bool cheap = now <= target;
            if (cheap) under++;
            line.Note.Say(cheap ? $"✓ under your {Cs2Inventory.Money(target)}" : $"{Cs2Inventory.Money(now - target)} above your {Cs2Inventory.Money(target)}");
            line.Note.Tint(cheap ? Of("GoodBrush") : Of("SubtleBrush"));
            line.Price.Tint(cheap ? Of("GoodBrush") : Of("TextBrush"));
            if (cheap) App.Alert("watch|" + name, $"{Cs2Inventory.Short(name)} is at your price", $"Now {Cs2Inventory.Money(now)} on Skinport (you wanted {Cs2Inventory.Money(target)} or less)");
        }
        _state.Say(watched.Count == 0 ? "Press the pencil, find a skin and write the most you'd pay." : !Cs2Skins.HasExact ? "Getting prices..." : $"{under} of {watched.Count} at your price · Skinport lowest price");
    }

    void Editing()
    {
        DrawEditRows();
        _show.Visibility = Visibility.Collapsed;
        _edit.Visibility = Visibility.Visible;
        Live.Arrive(_edit, this);
    }

    void Back()
    {
        _edit.Visibility = Visibility.Collapsed;
        _show.Visibility = Visibility.Visible;
        Show();
    }
}

/// <summary>
/// The cases in your CS2 inventory and whether opening them is worth it: what one opening costs (a key
/// and the case), what comes out on average at today's prices, the chance of getting more than you put
/// in, and the best thing inside. With no cases it shows a few well-known ones.
/// </summary>
public sealed class CasesWidget : UserControl
{
    static readonly string[] Known = { "Fever Case", "Dreams & Nightmares Case", "Kilowatt Case", "Revolution Case" };
    readonly StackPanel _rows = new();
    readonly TextBlock _about = Small();
    readonly Dictionary<string, (StackPanel Block, TextBlock Head, TextBlock Back, TextBlock Detail, Meter Bar)> _lines = new();

    internal IEnumerable<string> Shown => _lines.Select(l => $"{l.Value.Head.Text} | {l.Value.Back.Text} | {l.Value.Detail.Text}");
    internal void Redraw() => Show();

    public CasesWidget()
    {
        var box = new StackPanel { Width = 260 };
        box.Children.Add(Head("Is opening worth it?", MoneyKey(Show)));
        box.Children.Add(_rows);
        box.Children.Add(_about);
        Content = box;
        void Changed() => Dispatcher.BeginInvoke(Show);
        Loaded += (_, _) => { Cs2Cases.Changed += Changed; Cs2Skins.Ready += Changed; Cs2Inventory.Changed += Changed; MoneyPrefs.Changed += Changed; };
        Unloaded += (_, _) => { Cs2Cases.Changed -= Changed; Cs2Skins.Ready -= Changed; Cs2Inventory.Changed -= Changed; MoneyPrefs.Changed -= Changed; };
        Cs2Skins.Start();
        Cs2Cases.Start();
        this.Every(600, () => { if (!SelfTest.Wanted) Cs2Inventory.Start(); Show(); });
    }

    void Show()
    {
        if (!Cs2Cases.Ready || !Cs2Skins.HasPrices) { _about.Say("Getting what's inside each case..."); return; }
        var mine = Cs2Inventory.Items.Where(i => Cs2Cases.Rate(i.Name) != null).Select(i => (i.Name, i.Count)).ToList();
        bool own = mine.Count > 0;
        var list = own ? mine : Known.Select(k => (k, 0)).ToList();
        var ratings = list.Select(c => Cs2Cases.Rate(c.Item1, c.Item2)).Where(r => r != null).Select(r => r!).Take(5).ToList();
        foreach (var gone in _lines.Keys.Where(k => ratings.All(r => r.Name != k)).ToList())
        {
            _rows.Children.Remove(_lines[gone].Block);
            _lines.Remove(gone);
        }
        foreach (var r in ratings)
        {
            if (!_lines.TryGetValue(r.Name, out var line))
            {
                var block = new StackPanel { Margin = new Thickness(0, 4, 0, 6) };
                var top = new Grid();
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var head = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
                var back = new TextBlock { FontSize = 12, FontWeight = FontWeights.SemiBold };
                Grid.SetColumn(back, 1);
                top.Children.Add(head);
                top.Children.Add(back);
                var bar = new Meter(260) { Margin = new Thickness(0, 3, 0, 2) };
                var detail = new TextBlock { FontSize = 10, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap };
                block.Children.Add(top);
                block.Children.Add(bar);
                block.Children.Add(detail);
                line = (block, head, back, detail, bar);
                _lines[r.Name] = line;
                _rows.Children.Add(block);
                Live.Arrive(block, this, _lines.Count);
            }
            line.Head.Say(r.Name + (r.Owned > 1 ? $" ×{r.Owned}" : ""));
            line.Back.Say($"{Math.Round(100 * r.Back)}¢ per $1");
            line.Back.Tint(r.Back >= 1 ? Of("GoodBrush") : r.Back >= 0.6 ? Of("WarnBrush") : Of("BadBrush"));
            line.Bar.Glide(Math.Min(1, r.Back), 700);
            line.Bar.Colour = r.Back >= 1 ? Of("GoodBrush") : r.Back >= 0.6 ? Of("WarnBrush") : Of("BadBrush");
            line.Detail.Say($"Costs {Cs2Inventory.Money(r.Cost)} (key + case), gives {Cs2Inventory.Money(r.Expected)} on average · " +
                $"{100 * r.ChanceToProfit:0.#}% chance to win · best: {Cs2Inventory.Short(r.BestName).TrimStart('★', ' ')} {Cs2Inventory.Money(r.Best)}");
        }
        _about.Say(own
            ? $"Selling the case keeps {Cs2Inventory.Money(ratings.Sum(r => r.CasePrice * r.Owned))} for sure. Valve's odds, Skinport prices."
            : "No cases in your inventory, so here are some well-known ones. Valve's odds, Skinport prices.");
    }
}
