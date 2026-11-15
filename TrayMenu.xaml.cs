using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Pitzki.Controls;
using Pitzki.Core;
using PitzkiWidgets.Core;

namespace PitzkiWidgets;

/// <summary>The menu that opens from the tray icon, drawn like Pitzki's own (Piztki\TrayMenu.xaml).
/// The on/off rows keep it open so a few can be flipped in a row; Widgets… and Quit close it.</summary>
public partial class TrayMenu : Window
{
    bool _closing;

    static App Host => (App)Application.Current;
    static Board Board => App.Board;

    public TrayMenu()
    {
        InitializeComponent();
        Refresh();

        SourceInitialized += (_, _) => Native.HideFromAltTab(new WindowInteropHelper(this).Handle);
        Loaded += (_, _) =>
        {
            if (!SelfTest.Wanted) PlaceNearCursor();
            // grows out of the corner nearest the tray icon, then the rows follow
            Fx.Appear(Root, dx: 0, dy: 10, ms: 240, fromScale: 0.9);
            int i = 0;
            foreach (UIElement row in Rows)
                Fx.Appear(row, dx: 0, dy: 8, ms: 260, delay: 60 + i++ * 30);
            Face.Wave();
        };
        Deactivated += (_, _) => CloseSmoothly(); // clicking anywhere else dismisses it
    }

    UIElement[] Rows => new UIElement[] { RowWidgets, RowLock, RowSnap, RowJoin, RowGlass, RowProfile, RowAuto, RowRestart, RowQuit };

    /// <summary>Every switch and the profile name, read fresh from the board.</summary>
    internal void Refresh()
    {
        LockSwitch.IsChecked = Board.LockAll;
        SnapSwitch.IsChecked = !Board.NoSnap;
        JoinSwitch.IsChecked = !Board.NoJoin;
        GlassSwitch.IsChecked = Board.Glass;
        AutoSwitch.IsChecked = Board.AutoGame;
        ProfileName.Text = Board.Profile == "" ? "All cards" : Board.Profile;
    }

    /// <summary>Sits just above and to the left of the pointer, kept inside the screen it is on.</summary>
    void PlaceNearCursor()
    {
        if (!Native.GetCursorPos(out var p)) return;
        var dpi = VisualTreeHelper.GetDpi(this);
        double x = p.X / dpi.DpiScaleX, y = p.Y / dpi.DpiScaleY;

        var area = Screens.RoomAtMouse();
        Left = Math.Clamp(x - ActualWidth + 20, area.Left, Math.Max(area.Left, area.Right - ActualWidth));
        Top = Math.Clamp(y - ActualHeight - 2, area.Top, Math.Max(area.Top, area.Bottom - ActualHeight));
    }

    void CloseSmoothly(Action? then = null)
    {
        if (_closing) return;
        _closing = true;
        Fx.Disappear(Root, dy: 8, ms: 120, done: () =>
        {
            Close();
            then?.Invoke();
        });
    }

    /// <summary>A switch flipped: Pitzki sparkles when something goes on and glances when it goes off.</summary>
    void Flipped(bool on)
    {
        Refresh();
        if (on) Face.Sparkle(); else Face.Glance();
    }

    void Widgets_Click(object sender, RoutedEventArgs e) => CloseSmoothly(() => Host.ShowPicker());

    void Lock_Click(object sender, RoutedEventArgs e)
    {
        Board.LockAll = !Board.LockAll;
        Board.Save();
        Flipped(Board.LockAll);
    }

    void Snap_Click(object sender, RoutedEventArgs e)
    {
        Board.NoSnap = !Board.NoSnap;
        Board.Save();
        Flipped(!Board.NoSnap);
    }

    void Join_Click(object sender, RoutedEventArgs e)
    {
        Board.NoJoin = !Board.NoJoin;
        Board.Save();
        Flipped(!Board.NoJoin);
    }

    void Glass_Click(object sender, RoutedEventArgs e)
    {
        Host.SetGlass(!Board.Glass);
        Flipped(Board.Glass);
    }

    void Profile_Click(object sender, RoutedEventArgs e)
    {
        var all = new[] { "" }.Concat(App.Profiles).ToList();
        Host.SetProfile(all[(all.IndexOf(Board.Profile) + 1) % all.Count]);
        Refresh();
        Fx.Pop(ProfileName, from: 0.8, ms: 260);
        Face.Glance();
    }

    void Auto_Click(object sender, RoutedEventArgs e)
    {
        Board.AutoGame = !Board.AutoGame;
        Board.Save();
        Flipped(Board.AutoGame);
    }

    void Quit_Click(object sender, RoutedEventArgs e) => CloseSmoothly(() => Host.Quit());

    void Restart_Click(object sender, RoutedEventArgs e) => CloseSmoothly(() => Host.Restart());
}
