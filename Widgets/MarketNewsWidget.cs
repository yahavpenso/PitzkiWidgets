using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using static PitzkiWidgets.Widgets.MarketBits;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// One fresh headline for each of your stocks, from Yahoo Finance's public search, every fifteen minutes.
/// A click opens the story in the browser. The stocks are the ones written with the pencil
/// (<c>placed.Text</c>), the same list the Stocks card uses by default.
/// </summary>
public sealed class MarketNewsWidget : UserControl
{
    readonly Placed _placed;
    readonly StackPanel _rows = new(), _show = new(), _edit = new();
    readonly TextBox _box = Field("Names or symbols: Apple, Nvidia, TEVA.TA", 120);
    readonly TextBlock _state = Small("Looking...");
    readonly Dictionary<string, (TextBlock Title, TextBlock About, Border Row)> _shown = new();

    internal IEnumerable<string> Shown => _shown.Select(s => s.Key + ": " + s.Value.Title.Text + " (" + s.Value.About.Text + ")");
    internal Task Refresh() => RefreshAsync();

    List<string> Symbols => StocksWidget.Parse(_placed.Text.Length > 0 ? _placed.Text : "AAPL, NVDA, TSLA, MSFT").Take(5).ToList();

    public MarketNewsWidget(Placed placed)
    {
        _placed = placed;
        var pencil = Key("", Editing, icon: true, size: 11.5, tip: "Choose the stocks");
        _show.Children.Add(_rows);
        _show.Children.Add(_state);
        _edit.Children.Add(Boxed(_box));
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(Pill("Save", Save));
        buttons.Children.Add(Pill("Cancel", Back, quiet: true));
        _edit.Children.Add(buttons);
        _edit.Visibility = Visibility.Collapsed;
        _box.KeyDown += (_, e) => { if (e.Key == System.Windows.Input.Key.Enter) Save(); else if (e.Key == System.Windows.Input.Key.Escape) Back(); };
        FocusOnClick(this, _box);

        var box = new StackPanel { Width = 260 };
        box.Children.Add(Head("Market news", pencil));
        box.Children.Add(_show);
        box.Children.Add(_edit);
        Content = box;
        this.Every(900, () => _ = RefreshAsync());
    }

    async Task RefreshAsync()
    {
        var symbols = Symbols;
        foreach (var gone in _shown.Keys.Where(k => !symbols.Contains(k)).ToList())
        {
            _rows.Children.Remove(_shown[gone].Row);
            _shown.Remove(gone);
        }
        int found = 0, order = 0;
        foreach (string symbol in symbols)
        {
            var news = await Market.NewsAsync(symbol, 1);
            if (news.Count == 0) continue;
            found++;
            var n = news[0];
            if (!_shown.TryGetValue(symbol, out var row))
            {
                var title = new TextBlock { FontSize = 12, TextWrapping = TextWrapping.Wrap, MaxHeight = 34, TextTrimming = TextTrimming.CharacterEllipsis };
                var about = new TextBlock { FontSize = 10, Foreground = Of("SubtleBrush") };
                var stack = new StackPanel();
                stack.Children.Add(about);
                stack.Children.Add(title);
                var border = new Border { Padding = new Thickness(0, 3, 0, 5), Child = stack, Background = System.Windows.Media.Brushes.Transparent };
                row = (title, about, border);
                _shown[symbol] = row;
                _rows.Children.Add(border);
                Live.Arrive(border, this, order++);
            }
            row.Title.Say(n.Title);
            row.About.Say($"{symbol} · {n.Publisher} · {Market.Ago(n.When)}");
            row.Row.ToolTip = n.Title;
            string link = n.Link;
            row.Row.Tag = link;
            if (row.Row.Cursor == null) Click(row.Row, () => Open((string)row.Row.Tag));
        }
        _state.Say(found == 0 ? "No news found, or offline" : $"Yahoo Finance · {DateTime.Now:HH:mm} · click to read");
    }

    void Editing()
    {
        _box.Text = string.Join(", ", Symbols);
        _show.Visibility = Visibility.Collapsed;
        _edit.Visibility = Visibility.Visible;
        Live.Arrive(_edit, this);
    }

    void Back()
    {
        _edit.Visibility = Visibility.Collapsed;
        _show.Visibility = Visibility.Visible;
    }

    void Save() => _ = SaveAsync();

    async Task SaveAsync()
    {
        _placed.Text = string.Join(", ", await Market.ResolveAsync(_box.Text, 5));
        App.Board.Save();
        Back();
        _ = RefreshAsync();
    }
}
