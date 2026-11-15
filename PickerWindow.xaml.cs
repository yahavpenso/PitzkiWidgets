using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Pitzki.Controls;
using PitzkiWidgets.Widgets;
using Fx = Pitzki.Core.Fx;

namespace PitzkiWidgets;

/// <summary>
/// The one window with a frame: every widget there is, each on its own row with its own little
/// badge and a switch, laid out the way the Pitzki settings window is. Flicking a switch puts the
/// card on the desktop straight away, or takes it off, and Pitzki at the top answers each time.
/// </summary>
public partial class PickerWindow : Window
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string RunName = "PitzkiWidgets";

    /// <summary>A glyph for each widget, from the icon font Pitzki itself draws with.</summary>
    static readonly Dictionary<string, string> Badges = new()
    {
        ["pitzki"] = "", ["clock"] = "", ["calendar"] = "", ["weather"] = "",
        ["notes"] = "", ["tasks"] = "", ["timer"] = "", ["countdown"] = "",
        ["pomodoro"] = "",
        ["agenda"] = "", ["clipboard"] = "",
        ["shortcuts"] = "",
        ["system"] = "", ["drives"] = "", ["batteries"] = "", ["clips"] = "",
        ["music"] = "", ["steam"] = "", ["news"] = "", ["robrun"] = "",
        ["crypto"] = "", ["rates"] = "", ["convert"] = "", ["chart"] = "", ["portfolio"] = "", ["indices"] = "", ["markets"] = "", ["paper"] = "", ["guess"] = "", ["marketnews"] = "", ["stocks"] = "", ["networth"] = "", ["skinwatch"] = "", ["cases"] = "", ["steamitems"] = "", ["cs2inv"] = "", ["screentime"] = "", ["photos"] = "", ["calc"] = "", ["worldclock"] = "", ["sound"] = "", ["claude"] = "", ["discord"] = "",
    };

    readonly List<FrameworkElement> _rows = new();
    bool _filling;
    bool _leaving;

    public PickerWindow()
    {
        InitializeComponent();
        var room = Core.Screens.RoomAtMouse();
        Height = Math.Min(Height, room.Height);
        // in the middle of the screen the mouse is on, not always the main one
        Left = room.Left + (room.Width - Width) / 2;
        Top = room.Top + (room.Height - Height) / 2;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Away(); };

        Grain.Fill = Core.Grain.Paper;
        Fill();

        City.Text = App.Board.WeatherCity;
        City.LostFocus += (_, _) => Town();
        City.KeyDown += (_, e) => { if (e.Key == Key.Enter) Town(); };
        CityRow();
        BuildLocate();

        Startup.IsChecked = App.Board.StartWithWindows;
        Startup.Checked += (_, _) => Startup_(true);
        Startup.Unchecked += (_, _) => Startup_(false);

        InStart.IsChecked = Core.StartMenu.There;
        BackupKeys.Children.Add(Widgets.WidgetBody.Pill("Save…", SaveBackup, quiet: true));
        BackupKeys.Children.Add(Widgets.WidgetBody.Pill("Load…", LoadBackup, quiet: true));
        InStart.Checked += (_, _) => StartMenu_(true);
        InStart.Unchecked += (_, _) => StartMenu_(false);

        Loaded += (_, _) => Open();
    }

    // ----- coming and going -----

    /// <summary>It grows into place, a light sweeps over it, and the rows arrive one after another.</summary>
    /// <summary>
    /// The grain and the sheen are cut to the window's rounded shape. ClipToBounds would cut them to
    /// a square and leave a hard corner over the round one.
    /// </summary>
    void Skin_SizeChanged(object sender, SizeChangedEventArgs e) =>
        Skin.Clip = new RectangleGeometry(new Rect(0, 0, Skin.ActualWidth, Skin.ActualHeight), 16, 16);

    void Open()
    {
        var ease = (IEasingFunction)FindResource("EaseOut");
        Fx.Animate(Root, OpacityProperty, 1, 180, ease);
        Fx.Animate(RootScale, ScaleTransform.ScaleXProperty, 1, 340, ease);
        Fx.Animate(RootScale, ScaleTransform.ScaleYProperty, 1, 340, ease);

        Sheen.Opacity = 1;
        Fx.Animate(SheenSlide, TranslateTransform.XProperty, Width + 320, 900, ease, 120,
                   () => Sheen.Opacity = 0);

        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            var slide = (TranslateTransform)row.RenderTransform;
            int wait = 90 + i * 28;
            Fx.Animate(row, OpacityProperty, 1, 260, ease, wait);
            Fx.Animate(slide, TranslateTransform.YProperty, 0, 420, (IEasingFunction)FindResource("Spring"), wait);
        }

        // and once the rows have landed, Pitzki says hello
        Pit.Mood = MascotMood.Happy;
        var hello = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(520) };
        hello.Tick += (_, _) => { hello.Stop(); Pit.Wave(); };
        hello.Start();
    }

    /// <summary>It shrinks away before the window really goes, so it does not just blink out.</summary>
    void Away()
    {
        if (_leaving) return;
        _leaving = true;
        Fx.Animate(Root, OpacityProperty, 0, 160);
        Fx.Animate(RootScale, ScaleTransform.ScaleXProperty, 0.95, 180, null, 0, Close);
        Fx.Animate(RootScale, ScaleTransform.ScaleYProperty, 0.95, 180);
    }

    void Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    void Close_Click(object sender, RoutedEventArgs e) => Away();

    // ----- the rows -----

    /// <summary>One row per widget: a badge, its name, and the switch.</summary>
    void Fill()
    {
        _filling = true;
        var app = (App)Application.Current;
        var accent = (Brush)FindResource("AccentBrush");

        foreach (var group in WidgetBody.Kinds.GroupBy(k => k.Group))
        {
        Lists.Children.Add(new TextBlock { Text = group.Key, Style = (Style)FindResource("Group") });
        var list = new StackPanel();
        Lists.Children.Add(new Border { Style = (Style)FindResource("Card"), Child = list });

        foreach (var (kind, name, blurb, _) in group)
        {
            bool on = app.Has(kind);

            // the badge: a quiet little square that lights up in the accent colour when the card is out
            var lit = new Border
            {
                CornerRadius = new CornerRadius(10), Background = accent, Opacity = on ? 1 : 0,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = new ScaleTransform(on ? 1 : 0.5, on ? 1 : 0.5),
            };
            var glyph = new TextBlock
            {
                Text = Badges.GetValueOrDefault(kind, ""),
                FontFamily = (FontFamily)FindResource("Icons"), FontSize = 15,
                Foreground = on ? (Brush)FindResource("OnAccentBrush") : (Brush)FindResource("SubtleBrush"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            var badge = new Border
            {
                Width = 34, Height = 34, CornerRadius = new CornerRadius(10),
                Background = (Brush)FindResource("ChipBrush"),
                VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0),
                Child = new Grid { Children = { lit, glyph } },
            };

            var tick = new CheckBox
            {
                Style = (Style)FindResource("Switch"),
                IsChecked = on,
                VerticalAlignment = VerticalAlignment.Center,
                // the row below takes every click, the switch included, so a press never lands twice or nowhere
                IsHitTestVisible = false,
            };

            var words = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 12, 0) };
            words.Children.Add(new TextBlock { Text = name, Style = (Style)FindResource("RowTitle") });
            words.Children.Add(new TextBlock { Text = blurb, Style = (Style)FindResource("RowHint") });

            var line = new Grid { Style = (Style)FindResource("Row") };
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            line.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            line.Children.Add(badge);
            Grid.SetColumn(words, 1);
            line.Children.Add(words);
            Grid.SetColumn(tick, 2);
            line.Children.Add(tick);

            // the whole row answers to the mouse, and lifts a little as the pointer crosses it
            var wash = new Border
            {
                CornerRadius = new CornerRadius(10), Background = new SolidColorBrush(Color.FromArgb(0x0E, 0xFF, 0xFF, 0xFF)),
                Margin = new Thickness(6, 2, 6, 2), Opacity = 0,
            };
            var reach = new Border
            {
                Background = Brushes.Transparent, Cursor = Cursors.Hand,
                Child = new Grid { Children = { wash, line } },
            };
            reach.MouseEnter += (_, _) => Fx.Animate(wash, OpacityProperty, 1, 130);
            reach.MouseLeave += (_, _) => Fx.Animate(wash, OpacityProperty, 0, 240);
            // one flick per press: the button has to go down and come back up on this same row.
            // The switch itself lets clicks fall through to here, so it is never toggled from two places.
            bool down = false;
            reach.MouseLeftButtonDown += (_, e) => { down = true; e.Handled = true; };
            reach.MouseLeave += (_, _) => down = false;
            reach.MouseLeftButtonUp += (_, e) =>
            {
                if (!down) return;
                down = false;
                e.Handled = true;
                tick.IsChecked = !(tick.IsChecked ?? false);
            };

            tick.Checked += (_, _) =>
            {
                Light(lit, glyph, true);
                if (_filling) return;
                if (!app.Has(kind)) app.Add(kind);
                Pit.Sparkle();
                After();
            };
            tick.Unchecked += (_, _) =>
            {
                Light(lit, glyph, false);
                if (_filling) return;
                foreach (var placed in App.Board.Widgets.Where(w => w.Kind == kind).ToList()) app.Remove(placed);
                Pit.Glance();
                After();
            };

            // a whole row, divider and all, so the two arrive together
            var whole = new StackPanel { Opacity = 0, RenderTransform = new TranslateTransform(0, 14) };
            if (list.Children.Count > 0) whole.Children.Add(new Border { Style = (Style)FindResource("Divider") });
            whole.Children.Add(reach);

            list.Children.Add(whole);
            _rows.Add(whole);
        }
        }

        _filling = false;
        Tally();
    }

    /// <summary>The badge fills with colour, or lets go of it.</summary>
    void Light(Border lit, TextBlock glyph, bool on)
    {
        var spring = (IEasingFunction)FindResource("Spring");
        Fx.Animate(lit, OpacityProperty, on ? 1 : 0, on ? 140 : 200);
        var scale = (ScaleTransform)lit.RenderTransform;
        Fx.Animate(scale, ScaleTransform.ScaleXProperty, on ? 1 : 0.5, on ? 380 : 200, on ? spring : null);
        Fx.Animate(scale, ScaleTransform.ScaleYProperty, on ? 1 : 0.5, on ? 380 : 200, on ? spring : null);
        glyph.Foreground = (Brush)FindResource(on ? "OnAccentBrush" : "SubtleBrush");
    }

    /// <summary>What to do once a switch has been flicked.</summary>
    void After()
    {
        CityRow();
        Tally();
    }

    /// <summary>The line under the title counts what is out there.</summary>
    void Tally()
    {
        int many = App.Board.Widgets.Count;
        Count.Text = many == 0 ? "Nothing on the desktop yet"
                   : many == 1 ? "One card on your desktop"
                   : $"{many} cards on your desktop";
    }

    // ----- the rest -----

    /// <summary>The city row is only shown while the weather card is on the desktop.</summary>
    void CityRow() =>
        CityBox.Visibility = ((App)Application.Current).Has("weather") ? Visibility.Visible : Visibility.Collapsed;

    TextBlock _locateSaid = null!;
    bool _locating;

    /// <summary>The "Find my location" button and the word beside it, the same one Pitzki has.</summary>
    void BuildLocate()
    {
        _locateSaid = new TextBlock
        {
            FontSize = 12, Foreground = (Brush)FindResource("SubtleBrush"), VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0), TextWrapping = TextWrapping.Wrap, MaxWidth = 220,
        };
        var button = WidgetBody.Pill("Find my location", () => _ = Locate(), quiet: true);
        var words = (UIElement)button.Child;
        var pin = new TextBlock
        {
            Text = "", FontFamily = (FontFamily)FindResource("Icons"), FontSize = 12, VerticalAlignment = VerticalAlignment.Center,
            Foreground = (Brush)FindResource("AccentBrush"), Margin = new Thickness(0, 1, 7, 0),
        };
        button.Child = null;
        button.Child = new StackPanel { Orientation = Orientation.Horizontal, Children = { pin, words } };
        LocateRow.Children.Add(button);
        LocateRow.Children.Add(_locateSaid);
    }

    /// <summary>Works the city out from the connection and puts it on the card.</summary>
    internal async Task Locate()
    {
        if (_locating) return;               // one lookup at a time
        _locating = true;
        _locateSaid.Text = "Looking…";

        var found = await Core.Sky.LocateAsync();
        _locating = false;
        if (found is not { } here)
        {
            _locateSaid.Text = "Couldn't work it out. Type your city instead.";
            return;
        }

        // the coordinates come with it, so the weather card does not have to look the name up again
        App.Board.WeatherCity = here.City;
        App.Board.WeatherPlace = here.City;
        App.Board.WeatherLat = here.Lat;
        App.Board.WeatherLon = here.Lon;
        App.Board.Save();
        City.Text = here.City;
        _locateSaid.Text = "Found " + here.City;
        Fx.Appear(_locateSaid, dx: 8, dy: 0, ms: 280);
        Pit.Sparkle();
        ((App)Application.Current).Again("weather");
    }

    /// <summary>A new city: it is looked up again the moment the card next asks.</summary>
    void Town()
    {
        string said = City.Text.Trim();
        if (said == App.Board.WeatherCity) return;
        App.Board.WeatherCity = said;
        App.Board.WeatherPlace = "";          // so the place is looked up afresh
        App.Board.Save();
        ((App)Application.Current).Again("weather");
    }

    /// <summary>Puts the app in, or out of, the list Windows starts at login.</summary>
    bool _startBusy;

    /// <summary>
    /// The switch shows what the Start menu really holds. Putting it back by hand fires the switch's
    /// own events again, so those are ignored while it is being put back (that loop is what kept it off).
    /// </summary>
    void StartMenu_(bool want)
    {
        if (_startBusy) return;
        _startBusy = true;
        try
        {
            Core.StartMenu.Set(want);
            InStart.IsChecked = Core.StartMenu.There;
        }
        finally { _startBusy = false; }
    }

    void Startup_(bool want)
    {
        App.Board.StartWithWindows = want;
        App.Board.Save();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
            if (key == null) return;
            if (want) key.SetValue(RunName, $"\"{Environment.ProcessPath}\"");
            else key.DeleteValue(RunName, false);
        }
        catch { } // a locked-down machine is no reason to lose the setting
    }

    // ----- backup -----

    void SaveBackup()
    {
        var pick = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Save a backup of your cards", FileName = $"Pitzki Widgets {DateTime.Now:yyyy-MM-dd}.json",
            Filter = "Widgets backup|*.json", DefaultExt = ".json",
        };
        if (pick.ShowDialog(this) != true) return;
        bool done = App.Board.Export(pick.FileName);
        BackupHint.Text = done ? $"Saved {App.Board.Widgets.Count} card(s) to {System.IO.Path.GetFileName(pick.FileName)}" : "Couldn't write that file";
        if (done) Pit.Sparkle();
    }

    void LoadBackup()
    {
        var pick = new Microsoft.Win32.OpenFileDialog { Title = "Load a backup of your cards", Filter = "Widgets backup|*.json" };
        if (pick.ShowDialog(this) != true) return;
        var board = Core.Board.Import(pick.FileName);
        if (board == null) { BackupHint.Text = "That file isn't a widgets backup"; Pit.Glance(); return; }
        if (MessageBox.Show(this, $"Put back the {board.Widgets.Count} card(s) from this backup? The cards on the desktop now are replaced.",
                "Pitzki Widgets", MessageBoxButton.OKCancel) != MessageBoxResult.OK) return;
        ((App)Application.Current).Restore(board);
        BackupHint.Text = $"Put back {board.Widgets.Count} card(s)";
        Pit.Celebrate();
    }
}
