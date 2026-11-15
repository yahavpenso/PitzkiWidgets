using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;

namespace PitzkiWidgets.Widgets;

/// <summary>One limit of the Claude plan: how much of it is used (0 to 100) and when it starts again.</summary>

/// <summary>
/// How much of the Claude plan is used: the 5-hour limit and the weekly ones, each with a bar and when it resets,
/// the same numbers as "Plan usage limits" in the Claude app. Every Windows user sees their own account: the card
/// reads the sign-in Claude Code keeps in that user's own folder (%USERPROFILE%\.claude\.credentials.json) and asks
/// Anthropic for the numbers every 2 minutes. It never signs in or renews the sign-in itself, so it can never
/// sign Claude out; when the sign-in is old it says so, and opening Claude Code renews it.
/// </summary>
public sealed class ClaudeUsageWidget : UserControl
{
    const string SeeAllUrl = ClaudeUsage.SeeAllUrl;

    readonly StackPanel _rows = new();
    readonly TextBlock _plan = new() { Style = Caption, Margin = new Thickness(0, 0, 0, 8) };
    readonly TextBlock _state = new() { FontSize = 11, Foreground = Of("SubtleBrush"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0) };
    readonly Dictionary<string, Line> _lines = new();
    bool _busy;


    /// <summary>For the tests: what each row shows now.</summary>
    internal IEnumerable<(string Name, string Resets, string Used)> Shown =>
        _lines.Values.Select(l => (l.Name.Text, l.Resets.Text, l.Used.Text));
    internal string State => _state.Text;
    internal string Plan => _plan.Text;
    internal Task Refresh() => RefreshAsync();
    internal void Feed(IEnumerable<ClaudeLimit> limits) => Show(limits.ToList());

    public ClaudeUsageWidget()
    {
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        _plan.Text = "PLAN USAGE LIMITS";
        head.Children.Add(_plan);
        var all = Key("See all →", () => Open(SeeAllUrl), size: 11, tip: "Open the usage page on claude.ai");
        all.Margin = new Thickness(0, -4, -4, 0);
        Grid.SetColumn(all, 1);
        head.Children.Add(all);

        var box = new StackPanel { Width = 250 };
        box.Children.Add(head);
        box.Children.Add(_rows);
        box.Children.Add(_state);
        Content = box;

        _state.Say("Looking...");
        this.Every(60, () => _ = RefreshAsync());
    }

    static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    // ----- the rows -----

    /// <summary>One limit: its name and when it resets on top, the bar and the percent under it.</summary>
    sealed class Line
    {
        public readonly StackPanel Row = new() { Margin = new Thickness(0, 2, 0, 8) };
        public readonly TextBlock Name = new() { FontSize = 12.5, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        public readonly TextBlock Resets = new() { FontSize = 10.5, Foreground = Of("SubtleBrush"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
        public readonly TextBlock Used = new() { FontSize = 10.5, Foreground = Of("SubtleBrush"), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 0, 0) };
        public readonly Meter Bar = new(250) { Margin = new Thickness(0, 5, 0, 0), HorizontalAlignment = HorizontalAlignment.Left };
        public double Was = -1;

        public Line()
        {
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.Children.Add(Name);
            Grid.SetColumn(Resets, 1);
            top.Children.Add(Resets);
            Grid.SetColumn(Used, 2);
            top.Children.Add(Used);
            Row.Children.Add(top);
            Row.Children.Add(Bar);
        }
    }

    void Show(List<ClaudeLimit> limits)
    {
        foreach (var gone in _lines.Keys.Where(k => limits.All(l => l.Key != k)).ToList())
        {
            _rows.Children.Remove(_lines[gone].Row);
            _lines.Remove(gone);
        }
        int order = 0;
        foreach (var limit in limits)
        {
            if (!_lines.TryGetValue(limit.Key, out var line))
            {
                line = _lines[limit.Key] = new Line();
                _rows.Children.Add(line.Row);
                Live.Arrive(line.Row, this, order++);
            }
            else
            {
                _rows.Children.Remove(line.Row);
                _rows.Children.Add(line.Row);
            }
            line.Name.Say(limit.Name);
            line.Resets.Say(limit.Resets is DateTime at ? "Resets " + ClaudeUsage.When(at) : "");
            int now = (int)Math.Round(limit.Used);
            line.Used.Count(now + "%", now, line.Was < 0 ? now : line.Was);
            if (now >= 90 && line.Was is >= 0 and < 90) Core.Buzz.Tell("usage", this, now); // just crossed 90%
            line.Was = now;
            line.Bar.Part = limit.Used / 100;
            bool bad = limit.Used >= 95 || limit.Severity is "critical" or "error" or "exceeded";
            bool warn = limit.Used >= 80 || limit.Severity == "warning";
            line.Bar.Colour = Of(bad ? "BadBrush" : warn ? "WarnBrush" : "AccentBrush");
        }
    }

    /// <summary>For the README clips: nothing is asked, only what Feed puts on the card.</summary>
    internal static bool Demo;

    async Task RefreshAsync()
    {
        if (_busy || Demo) return;
        _busy = true;
        try
        {
            var got = await ClaudeUsage.FetchAsync("PitzkiWidgets");
            LastStatus = got.Status;
            _plan.Say("PLAN USAGE LIMITS" + (string.IsNullOrEmpty(got.Plan) ? "" : " · " + got.Plan.ToUpperInvariant()));
            if (got.Limits is not { } limits)
            {
                _state.Say(got.Problem ?? "");
                return;
            }
            Show(limits);
            var at = (got.At ?? DateTime.UtcNow).ToLocalTime();
            _shownAt = at.ToString("h:mm tt", CultureInfo.GetCultureInfo("en-US"));
            // numbers from a while ago say so; fresh ones need no words
            _state.Say(limits.Count == 0 ? "No limits on this plan"
                : DateTime.Now - at > TimeSpan.FromMinutes(6) ? "Updated " + _shownAt + (got.Status == 429 ? " · Claude asked to wait" : "")
                : "");
        }
        catch
        {
            _state.Say(_lines.Count == 0 ? "No connection, trying again soon" : "Offline, last updated " + _shownAt);
        }
        finally { _busy = false; }
    }

    string _shownAt = "";

    /// <summary>For the tests: what Anthropic answered last time (0 when it was not asked).</summary>
    internal int LastStatus;
}
