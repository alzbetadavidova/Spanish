using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Spanish.ViewModels;

namespace Spanish;

public partial class MainWindow : Window
{
    private const string EnterFullScreenIcon = "M7 14H5v5h5v-2H7v-3zm-2-4h2V7h3V5H5v5zm12 7h-3v2h5v-5h-2v3zM14 5v2h3v3h2V5h-5z";
    private const string ExitFullScreenIcon = "M5 16h3v3h2v-5H5v2zm3-8H5v2h5V5H8v3zm6 11h2v-3h3v-2h-5v5zm2-11V5h-2v5h5V8h-3z";

    public MainWindow()
    {
        InitializeComponent();
        SyncFullScreenToggle();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainWindowViewModel viewModel)
        {
            await viewModel.InitializeAsync();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        // F11 toggles; Escape only ever leaves full screen, so it can't close a maximised window by accident.
        if (e.Key == Key.F11)
        {
            ToggleFullScreen();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && WindowState == WindowState.FullScreen)
        {
            WindowState = WindowState.Normal;
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void OnFullScreenToggleClick(object? sender, RoutedEventArgs e) => ToggleFullScreen();

    private void ToggleFullScreen() =>
        WindowState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState.FullScreen;

    /// <summary>
    /// Keeps the button in step with the window, so F11 and the window manager's own full-screen
    /// controls leave it showing the right state.
    /// </summary>
    private void SyncFullScreenToggle()
    {
        var isFullScreen = WindowState == WindowState.FullScreen;
        FullScreenToggle.IsChecked = isFullScreen;
        FullScreenIcon.Data = Geometry.Parse(isFullScreen ? ExitFullScreenIcon : EnterFullScreenIcon);
    }

    protected override void OnPropertyChanged(Avalonia.AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == WindowStateProperty)
        {
            SyncFullScreenToggle();
        }
    }
}
