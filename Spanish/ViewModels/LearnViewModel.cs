using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spanish.Core;

namespace Spanish.ViewModels;

public partial class LearnViewModel : ObservableObject, IPage
{
    private readonly LibraryContext _context;
    private readonly IFileStore<SessionSettings> _settingsStore;
    private readonly ScenarioFactory _factory;
    private readonly IRandomSource _random;
    private readonly IClock _clock;
    private LearnSession _session;

    public LearnViewModel(LibraryContext context, IFileStore<SessionSettings> settingsStore, SessionSettings settings,
        IRandomSource random, IClock clock)
    {
        _context = context;
        _settingsStore = settingsStore;
        _random = random;
        _clock = clock;
        _factory = new ScenarioFactory(random);
        Settings = new SessionSettingsViewModel(context.Library, settings);
        _session = CreateSession(settings);
        _context.Changed += OnLibraryChanged;
        ShowNext();
    }

    public SessionSettingsViewModel Settings { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private ScenarioViewModel? _currentScenario;

    [ObservableProperty]
    private bool _isSettingsOpen;

    public bool IsEmpty => CurrentScenario is null;

    public string ProgressText => $"{_session.Answered} answered · {_session.CorrectCount} correct";

    public IReadOnlyList<string> SummaryChips => BuildSummary(_session.Settings);

    public void OnActivated() => Settings.RefreshTopics();

    [RelayCommand]
    private void OpenSettings() => IsSettingsOpen = true;

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand]
    private async Task StartSession()
    {
        var settings = Settings.ToSettings();
        _session = CreateSession(settings);
        IsSettingsOpen = false;
        OnPropertyChanged(nameof(SummaryChips));
        OnPropertyChanged(nameof(ProgressText));
        ShowNext();
        await _context.RunSaveAsync(ct => _settingsStore.SaveAsync(settings, ct));
    }

    private LearnSession CreateSession(SessionSettings settings) =>
        new(_context.Library, settings, _factory, _random, _clock);

    private void ShowNext()
    {
        var scenario = _session.Next();
        CurrentScenario = scenario is null ? null : ScenarioViewModel.Create(scenario, OnCompletedAsync);
    }

    private async Task OnCompletedAsync(bool correct)
    {
        if (CurrentScenario is null)
        {
            return;
        }
        _session.Record(CurrentScenario.Scenario, correct);
        OnPropertyChanged(nameof(ProgressText));
        ShowNext();
        await _context.SaveAsync();
    }

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Settings.RefreshTopics();
        if (CurrentScenario is null)
        {
            ShowNext();
        }
    }

    private static List<string> BuildSummary(SessionSettings settings)
    {
        var kinds = (settings.IncludeNouns, settings.IncludeVerbs) switch
        {
            (true, true) => "Nouns, verbs",
            (true, false) => "Nouns",
            (false, true) => "Verbs",
            _ => "No word types"
        };
        var topics = settings.Topics.Count == 0 ? "All topics" : $"Topics: {string.Join(", ", settings.Topics)}";
        var order = SessionSettingsViewModel.Orders.First(o => o.Value == settings.Order).Label;
        return [kinds, topics, order];
    }
}
