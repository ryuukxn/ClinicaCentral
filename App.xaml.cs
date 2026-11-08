using System.Windows;

namespace ClinicaCentral;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler(WindowNavigation.TrackWindow));

        base.OnStartup(e);
    }
}

public static class WindowNavigation
{
    private static WindowState _lastState = WindowState.Normal;

    private static bool FollowsState(Window window)
    {
        return window.Owner == null
            && window.ResizeMode != ResizeMode.NoResize
            && window.SizeToContent == SizeToContent.Manual;
    }

    public static void Open(this Window window)
    {
        if (FollowsState(window))
            window.WindowState = _lastState;

        window.Show();
    }

    public static void TrackWindow(object sender, RoutedEventArgs e)
    {
        if (!(sender is Window window) || !ReferenceEquals(e.OriginalSource, window)) return;
        if (!FollowsState(window)) return;

        window.StateChanged += (s, args) =>
        {
            if (window.WindowState != WindowState.Minimized)
                _lastState = window.WindowState;
        };
    }
}
