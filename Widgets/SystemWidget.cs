using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Pitzki.Core;
using PitzkiWidgets.Core;
using static PitzkiWidgets.Widgets.WidgetBody;
using static PitzkiWidgets.Widgets.MarketBits;

namespace PitzkiWidgets.Widgets;

/// <summary>
/// Processor, memory, what is going over the network, and the battery if there is one. Under the
/// numbers a small graph keeps the last minute, so a spike that has already gone is still there to see.
/// The pencil lists the rows with a switch each: a row switched off goes from the card, and the CPU and
/// network rows take their line in the graph with them. What is off is kept as "hide:&lt;row&gt;" in
/// <c>placed.Items</c>.
/// </summary>
public sealed class SystemWidget : UserControl
{
    /// <summary>One reading every three seconds, and twenty of them: the last minute.</summary>
    const int Seconds = 3, Readings = 20;

    readonly Placed _placed;
    readonly StackPanel _show = new(), _edit = new(), _switches = new();
    readonly TextBlock _key = new() { FontSize = 9.5, Margin = new Thickness(0, 8, 0, 3), FontWeight = FontWeights.SemiBold };
    readonly Dictionary<string, Row> _rows = new();

    /// <summary>The rows the pencil can switch off, in the order they are on the card.</summary>
    internal static readonly (string Id, string Name)[] Parts =
    {
        ("cpu", "CPU"), ("graphics", "Graphics"), ("memory", "Memory"), ("network", "Network"), ("onfor", "On for"), ("battery", "Battery"),
    };

    internal bool Hidden(string id) => _placed.Items.Contains("hide:" + id);
    internal bool Shows(string id) => _rows.TryGetValue(id, out var row) && row.Visibility == Visibility.Visible;
    internal bool LineShows(string id) => !(id == "cpu" ? _cpuLine : _netLine).Hidden;
    internal bool GraphShows => _graph.Visibility == Visibility.Visible;

    readonly SystemStats _stats = new();
    readonly GpuLoad _gpu = new();
    readonly Row _cpu = new(true), _card = new(true), _ram = new(true), _net = new(false), _battery = new(true), _up = new(false);
    readonly Spark _graph = new(180, 36, Readings);
    readonly Spark.Series _cpuLine, _netLine;

    public SystemWidget(Placed placed)
    {
        _placed = placed;
        var accent = ((SolidColorBrush)Of("AccentBrush")).Color;
        var good = ((SolidColorBrush)Of("GoodBrush")).Color;
        _cpuLine = _graph.Add(accent, fill: true, top: 1);                        // processor: always out of 100%
        _netLine = _graph.Add(good, fill: false, floor: 250_000);                 // network: scaled to what was seen, at least 250 KB/s

        var box = new StackPanel { Width = 180 };
        var pencil = Key("\uE70F", Editing, icon: true, size: 11.5, tip: "Choose what this card shows");
        box.Children.Add(Head("This PC", pencil));
        box.Children.Add(_show);
        box.Children.Add(_edit);
        _edit.Visibility = Visibility.Collapsed;
        _edit.Children.Add(_switches);
        var done = Pill("Done", Back);
        done.Margin = new Thickness(0, 8, 0, 0);
        done.HorizontalAlignment = HorizontalAlignment.Left;
        _edit.Children.Add(done);
        _rows["cpu"] = _cpu; _rows["graphics"] = _card; _rows["memory"] = _ram;
        _rows["network"] = _net; _rows["onfor"] = _up; _rows["battery"] = _battery;
        _cpu.Label.Text = "CPU";
        _card.Label.Text = "GRAPHICS";
        _up.Label.Text = "ON FOR";
        _ram.Label.Text = "MEMORY";
        _net.Label.Text = "NETWORK";
        _battery.Label.Text = "BATTERY";
        _show.Children.Add(_cpu);
        _show.Children.Add(_card);
        _show.Children.Add(_ram);
        _show.Children.Add(_net);
        _show.Children.Add(_up);
        _card.Visibility = Visibility.Collapsed;   // shown once the card has been read twice
        if (Battery.HasBattery) _show.Children.Add(_battery);

        // the last minute, with a key to which line is which
        _show.Children.Add(_key);
        _show.Children.Add(_graph);
        Apply();

        Content = box;
        this.Every(Seconds, Tick);
    }

    /// <summary>Whether a row belongs on the card right now: switched on, and (for the graphics card) read.</summary>
    bool Wanted(string id) => !Hidden(id) && (id != "graphics" || _gpuBusy != null);

    /// <summary>Puts the card in line with the switches: rows fade in or out, and the graph drops a hidden line.</summary>
    void Apply()
    {
        foreach (var (id, _) in Parts) Live.Show(_rows[id], Wanted(id), this);
        _cpuLine.Hidden = Hidden("cpu");
        _netLine.Hidden = Hidden("network");
        _graph.InvalidateVisual();

        _key.Inlines.Clear();
        if (!_cpuLine.Hidden) _key.Inlines.Add(new System.Windows.Documents.Run("● CPU   ") { Foreground = Of("AccentBrush") });
        if (!_netLine.Hidden) _key.Inlines.Add(new System.Windows.Documents.Run("● NETWORK   ") { Foreground = Of("GoodBrush") });
        _key.Inlines.Add(new System.Windows.Documents.Run("LAST MINUTE") { Foreground = Of("SubtleBrush") });
        bool graph = !(_cpuLine.Hidden && _netLine.Hidden);
        Live.Show(_key, graph, this);
        Live.Show(_graph, graph, this);
    }

    /// <summary>Switches one row on or off, saves it, and shows the change straight away.</summary>
    internal void Set(string id, bool show)
    {
        _placed.Items.RemoveAll(i => i == "hide:" + id);
        if (!show) _placed.Items.Add("hide:" + id);
        App.Board.Save();
        Apply();
    }

    internal void Editing()
    {
        _switches.Children.Clear();
        int order = 0;
        foreach (var (id, name) in Parts)
        {
            if (id == "battery" && !Battery.HasBattery) continue;
            var line = Switch(id, name);
            _switches.Children.Add(line);
            Live.Arrive(line, this, order++);
        }
        _show.Visibility = Visibility.Collapsed;
        _edit.Visibility = Visibility.Visible;
        Live.Arrive(_edit, this);
    }

    internal bool IsEditing => _edit.Visibility == Visibility.Visible;

    internal void Back()
    {
        _edit.Visibility = Visibility.Collapsed;
        _show.Visibility = Visibility.Visible;
        Live.Arrive(_show, this);
    }

    /// <summary>A row's name with a small switch on the right. The knob slides across when pressed.</summary>
    FrameworkElement Switch(string id, string name)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4), Background = Brushes.Transparent };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock { Text = name.ToUpperInvariant(), Style = Caption, VerticalAlignment = VerticalAlignment.Center };
        grid.Children.Add(label);

        var slide = new TranslateTransform();
        var knob = new Border { Width = 12, Height = 12, CornerRadius = new CornerRadius(6), Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(2, 0, 0, 0), RenderTransform = slide };
        var track = new Border { Width = 30, Height = 16, CornerRadius = new CornerRadius(8), Child = knob, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(track, 1);
        grid.Children.Add(track);

        void Draw(bool on, bool now)
        {
            if (now) { slide.X = on ? 14 : 0; track.Background = Of(on ? "AccentBrush" : "ChipBrush"); }
            else
            {
                Fx.Animate(slide, TranslateTransform.XProperty, on ? 14 : 0, 220, Fx.EaseOut);
                track.Background = Live.Blend(track.Background, Of(on ? "AccentBrush" : "ChipBrush"), this);
            }
            label.Foreground = Of(on ? "TextBrush" : "SubtleBrush");
        }
        Draw(!Hidden(id), true);
        Click(grid, () => { bool on = Hidden(id); Set(id, on); Draw(on, false); });
        grid.ToolTip = "Show or hide " + name;
        return grid;
    }

    /// <summary>How long the PC has been on, in words: "3 h 20 m", or days once it has been days.</summary>
    static string Spell(TimeSpan on) =>
        on.TotalDays >= 1 ? $"{(int)on.TotalDays} d {on.Hours} h" :
        on.TotalHours >= 1 ? $"{(int)on.TotalHours} h {on.Minutes} m" :
        $"{(int)on.TotalMinutes} m";

    /// <summary>
    /// Fills the graph with a made-up minute, so the test can picture what a busy one looks like.
    /// The real readings carry on from where it leaves off.
    /// </summary>
    internal Spark Graph => _graph;

    /// <summary>One more made-up reading, for the test to watch the graph scroll.</summary>
    internal void OneMore()
    {
        _graph.Push(_cpuLine, 0.8);
        _graph.Push(_netLine, 900_000);
    }

    internal void Demo()
    {
        for (int i = 0; i < Readings; i++)
        {
            _graph.Push(_cpuLine, 0.25 + 0.2 * Math.Sin(i / 3.0) + (i > 20 ? 0.35 : 0) * Math.Sin(i * 1.7) * Math.Sin(i * 1.7));
            _graph.Push(_netLine, 400_000 + 350_000 * Math.Sin(i / 2.2 + 1) + (i > 8 && i < 14 ? 1_800_000 : 0));
        }
    }

    double _wasCpu, _wasRam, _wasCard, _wasNet;
    const int GpuEvery = 4;     // about every twelve seconds
    int _gpuTicks;
    bool _gpuReading;
    double? _gpuBusy;

    void Tick()
    {
        _stats.Update();

        _cpu.Value.Count($"{Math.Round(_stats.Cpu * 100)}%", _stats.Cpu, _wasCpu);
        _wasCpu = _stats.Cpu;
        if (_cpu.Bar != null) { _cpu.Bar.Part = _stats.Cpu; _cpu.Bar.Colour = Of(_stats.Cpu > 0.9 ? "BadBrush" : _stats.Cpu > 0.7 ? "WarnBrush" : "AccentBrush"); }

        _ram.Value.Count($"{_stats.MemoryUsedGb:0.0} / {_stats.MemoryTotalGb:0.0} GB", _stats.Memory, _wasRam);
        _wasRam = _stats.Memory;
        if (_ram.Bar != null) { _ram.Bar.Part = _stats.Memory; _ram.Bar.Colour = Of(_stats.Memory > 0.9 ? "BadBrush" : _stats.Memory > 0.75 ? "WarnBrush" : "AccentBrush"); }

        // the graphics counters are Windows' most expensive ones to read (every engine of every app is
        // listed each time), so the card is asked every ten seconds, away from the screen's thread
        if ((_gpuBusy == null || _gpuTicks++ % GpuEvery == 0) && !_gpuReading)
        {
            _gpuReading = true;
            _ = Task.Run(() =>
            {
                double? read = _gpu.Read();
                Dispatcher.BeginInvoke(() => { _gpuReading = false; _gpuBusy = read ?? _gpuBusy; });
            });
        }
        if (_gpuBusy is { } busy)
        {
            Live.Show(_card, Wanted("graphics"), this);
            _card.Value.Count($"{Math.Round(busy * 100)}%", busy, _wasCard);
            _wasCard = busy;
            if (_card.Bar != null) { _card.Bar.Part = busy; _card.Bar.Colour = Of(busy > 0.9 ? "BadBrush" : busy > 0.7 ? "WarnBrush" : "AccentBrush"); }
        }

        double net = _stats.DownBytesPerSec + _stats.UpBytesPerSec;
        _net.Value.Count($"↓ {SystemStats.Speed(_stats.DownBytesPerSec)}   ↑ {SystemStats.Speed(_stats.UpBytesPerSec)}", net, _wasNet);
        _wasNet = net;
        _up.Value.Say(Spell(TimeSpan.FromMilliseconds(Environment.TickCount64)));

        _graph.Push(_cpuLine, _stats.Cpu);
        _graph.Push(_netLine, _stats.DownBytesPerSec + _stats.UpBytesPerSec);

        if (!Battery.HasBattery) return;
        int percent = Battery.Percent;
        _battery.Value.Say(percent + "%" + (Battery.PluggedIn ? " ⚡" : ""));
        if (_battery.Bar != null)
        {
            _battery.Bar.Part = percent / 100.0;
            _battery.Bar.Colour = Of(percent <= 15 && !Battery.PluggedIn ? "BadBrush" : percent <= 30 ? "WarnBrush" : "GoodBrush");
        }
    }
}
