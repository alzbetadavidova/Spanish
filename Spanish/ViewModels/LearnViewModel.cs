using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    private bool _settingsNeedSaving;

    public LearnViewModel(LibraryContext context, IFileStore<SessionSettings> settingsStore, SessionSettings settings,
        IRandomSource random, IClock clock)
    {
        _context = context;
        _settingsStore = settingsStore;
        _random = random;
        _clock = clock;
        _factory = new ScenarioFactory(random);

        // Saved settings may name topics that were deleted since.
        var valid = settings.WithTopicsFrom(context.Library.Topics);
        _settingsNeedSaving = !ReferenceEquals(valid, settings);
        Settings = new SessionSettingsViewModel(context.Library, valid);
        Settings.PropertyChanged += OnSettingsPropertyChanged;
        _session = CreateSession(valid);

        _context.Changed += OnLibraryChanged;
        _context.TopicRenamed += OnTopicRenamed;
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

    /// <summary>Opens the pane showing the running settings; unapplied edits from last time are discarded.</summary>
    [RelayCommand]
    private void OpenSettings()
    {
        Settings.Load(_session.Settings);
        IsSettingsOpen = true;
    }

    [RelayCommand]
    private void CloseSettings() => IsSettingsOpen = false;

    [RelayCommand(CanExecute = nameof(CanStartSession))]
    private async Task StartSession()
    {
        var settings = Settings.ToSettings();
        _session = CreateSession(settings);
        IsSettingsOpen = false;
        OnPropertyChanged(nameof(SummaryChips));
        OnPropertyChanged(nameof(ProgressText));
        ShowNext();
        _settingsNeedSaving = true;
        await SaveSettingsIfNeededAsync();
    }

    private bool CanStartSession() => Settings.MatchCount > 0;

    private LearnSession CreateSession(SessionSettings settings) =>
        new(_context.Library, settings, _factory, _random, _clock);

    private void ShowNext()
    {
        var scenario = _session.Next();
        CurrentScenario = scenario is null ? null : ScenarioViewModel.Create(scenario, OnCompletedAsync);
    }

    private async Task OnCompletedAsync(Scenario scenario, bool correct)
    {
        // Ignore answers from a scenario that is no longer shown (e.g. replaced by a new session).
        if (!ReferenceEquals(CurrentScenario?.Scenario, scenario))
        {
            return;
        }
        _session.Record(scenario, correct);
        OnPropertyChanged(nameof(ProgressText));
        ShowNext();
        await _context.SaveAsync();
        await SaveSettingsIfNeededAsync();
    }

    private async Task SaveSettingsIfNeededAsync()
    {
        if (!_settingsNeedSaving)
        {
            return;
        }
        var settings = _session.Settings;
        // A failed save stays pending and is retried at the next save point.
        _settingsNeedSaving = !await _context.RunSaveAsync(ct => _settingsStore.SaveAsync(settings, ct));
    }

    // The renamed topic still matches the same words, so the current scenario stays.
    private void OnTopicRenamed(object? sender, TopicRenamedEventArgs e) =>
        ApplySettings(_session.Settings.WithTopicRenamed(e.OldName, e.NewName));

    private void OnLibraryChanged(object? sender, EventArgs e)
    {
        Settings.RefreshTopics();
        var topicsChanged = ApplySettings(_session.Settings.WithTopicsFrom(_context.Library.Topics));

        var currentUnit = CurrentScenario?.Scenario.Unit;
        if (topicsChanged || currentUnit is null || !_context.Library.Units.Contains(currentUnit))
        {
            ShowNext();
        }
    }

    /// <summary>Updates the running session when library changes affect its settings.</summary>
    /// <returns>Whether the settings changed.</returns>
    private bool ApplySettings(SessionSettings settings)
    {
        if (ReferenceEquals(settings, _session.Settings))
        {
            return false;
        }
        _session.Settings = settings;
        _settingsNeedSaving = true;
        OnPropertyChanged(nameof(SummaryChips));
        return true;
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SessionSettingsViewModel.MatchCount))
        {
            StartSessionCommand.NotifyCanExecuteChanged();
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
