using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spanish.Core;

namespace Spanish.ViewModels;

public record NavItem(string Label, string Icon, IPage Page);

public partial class MainWindowViewModel(
    IFileStore<LearnLibrary> libraryStore,
    IFileStore<SessionSettings> settingsStore,
    IRandomSource random,
    IClock clock) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoading))]
    private IReadOnlyList<NavItem> _navItems = [];

    [ObservableProperty]
    private NavItem? _selectedNav;

    [ObservableProperty]
    private IPage? _currentPage;

    [ObservableProperty]
    private LibraryContext? _context;

    [ObservableProperty]
    private string? _notice;

    [ObservableProperty]
    private string? _loadError;

    public bool IsLoading => NavItems.Count == 0 && LoadError is null;

    public async Task InitializeAsync()
    {
        LoadResult<LearnLibrary> library;
        LoadResult<SessionSettings> settings;
        try
        {
            library = await libraryStore.LoadAsync();
            settings = await settingsStore.LoadAsync();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            LoadError = $"Couldn't open your library: {e.Message}";
            OnPropertyChanged(nameof(IsLoading));
            return;
        }

        if (library.CorruptFileBackupPath is not null)
        {
            Notice = $"Your library file was damaged, so it was moved to {library.CorruptFileBackupPath} and the starter library was loaded.";
        }
        else if (settings.CorruptFileBackupPath is not null)
        {
            Notice = "Your session settings were damaged and have been reset.";
        }

        Context = new LibraryContext(library.Value, libraryStore);
        NavItems =
        [
            new("Learn", "M12 3L1 9l11 6 9-4.9V17h2V9L12 3z", new LearnViewModel(Context, settingsStore, settings.Value, random, clock)),
            new("Library", "M4 6H2v14c0 1.1.9 2 2 2h14v-2H4V6zm16-4H8c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2z", new LibraryViewModel(Context)),
            new("Stats", "M5 9.2h3V19H5zM10.6 5h2.8v14h-2.8zm5.6 8H19v6h-2.8z", new StatsViewModel(Context, clock))
        ];
        SelectedNav = NavItems[0];
    }

    partial void OnSelectedNavChanged(NavItem? value)
    {
        if (value is null)
        {
            return;
        }
        value.Page.OnActivated();
        CurrentPage = value.Page;
    }

    [RelayCommand]
    private void DismissNotice() => Notice = null;
}
