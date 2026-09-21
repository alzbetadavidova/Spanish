using System;
using System.IO;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Spanish.Core;
using Spanish.ViewModels;

namespace Spanish;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // User data lives outside the build output so builds never overwrite it.
            var dataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Spanish");
            var clock = new SystemClock();
            var libraryStore = new JsonFileStore<LearnLibrary>(
                Path.Combine(dataDirectory, "library.json"),
                Path.Combine(AppContext.BaseDirectory, "library.json"),
                () => new LearnLibrary(),
                clock);
            var settingsStore = new JsonFileStore<SessionSettings>(
                Path.Combine(dataDirectory, "settings.json"),
                seedPath: null,
                () => new SessionSettings(),
                clock);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(libraryStore, settingsStore, new SystemRandomSource(), clock)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
