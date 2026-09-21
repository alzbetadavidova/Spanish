using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spanish.Core;

namespace Spanish.ViewModels;

public partial class SessionSettingsViewModel : ObservableObject
{
    private readonly LearnLibrary _library;

    public SessionSettingsViewModel(LearnLibrary library, SessionSettings settings)
    {
        _library = library;
        Load(settings);
    }

    public static IReadOnlyList<Option<SessionOrder>> Orders { get; } =
    [
        new(SessionOrder.LeastLearned, "Least learned first"),
        new(SessionOrder.LeastRecentlyPracticed, "Least recently practiced"),
        new(SessionOrder.Random, "Random")
    ];

    public static IReadOnlyList<Option<Direction>> Directions { get; } =
    [
        new(Direction.Mixed, "Mixed"),
        new(Direction.EnglishToSpanish, "English to Spanish"),
        new(Direction.SpanishToEnglish, "Spanish to English")
    ];

    public ObservableCollection<ToggleOption<ScenarioType>> NounScenarios { get; } = [];
    public ObservableCollection<ToggleOption<ScenarioType>> VerbScenarios { get; } = [];
    public ObservableCollection<ToggleOption<string>> Topics { get; } = [];

    [ObservableProperty]
    private bool _includeNouns;

    [ObservableProperty]
    private bool _includeVerbs;

    [ObservableProperty]
    private Option<SessionOrder> _selectedOrder = Orders[0];

    [ObservableProperty]
    private Option<Direction> _selectedDirection = Directions[0];

    [ObservableProperty]
    private int _matchCount;

    public string MatchText => MatchCount == 1 ? "1 exercise matches" : $"{MatchCount} exercises match";

    public SessionSettings ToSettings() => new()
    {
        IncludeNouns = IncludeNouns,
        IncludeVerbs = IncludeVerbs,
        NounScenarioTypes = Selected(NounScenarios),
        VerbScenarioTypes = Selected(VerbScenarios),
        Topics = Selected(Topics),
        Order = SelectedOrder.Value,
        Direction = SelectedDirection.Value
    };

    [RelayCommand]
    private void Reset() => Load(new SessionSettings());

    /// <summary>Rebuilds the topic chips after the library changed, keeping the selection.</summary>
    public void RefreshTopics()
    {
        var selected = Selected(Topics);
        FillTopics(selected);
        UpdateMatchCount();
    }

    /// <summary>Shows <paramref name="settings"/>, discarding unapplied edits.</summary>
    public void Load(SessionSettings settings)
    {
        IncludeNouns = settings.IncludeNouns;
        IncludeVerbs = settings.IncludeVerbs;
        SelectedOrder = Orders.First(o => o.Value == settings.Order);
        SelectedDirection = Directions.First(d => d.Value == settings.Direction);
        Fill(NounScenarios, new Noun().SupportedScenarios, settings.NounScenarioTypes);
        Fill(VerbScenarios, new Verb().SupportedScenarios, settings.VerbScenarioTypes);
        FillTopics(settings.Topics);
        UpdateMatchCount();
    }

    private void Fill(ObservableCollection<ToggleOption<ScenarioType>> target,
        IReadOnlyList<ScenarioType> all, IReadOnlyList<ScenarioType> selected)
    {
        target.Clear();
        foreach (var type in all)
        {
            target.Add(Watch(new ToggleOption<ScenarioType>(type, type.ToString(), selected.Contains(type))));
        }
    }

    private void FillTopics(IReadOnlyList<string> selected)
    {
        Topics.Clear();
        foreach (var topic in _library.Topics.Order())
        {
            Topics.Add(Watch(new ToggleOption<string>(topic, topic, selected.Contains(topic, StringComparer.OrdinalIgnoreCase))));
        }
    }

    private ToggleOption<T> Watch<T>(ToggleOption<T> option)
    {
        option.PropertyChanged += OnOptionChanged;
        return option;
    }

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e) => UpdateMatchCount();

    partial void OnIncludeNounsChanged(bool value) => UpdateMatchCount();
    partial void OnIncludeVerbsChanged(bool value) => UpdateMatchCount();
    partial void OnMatchCountChanged(int value) => OnPropertyChanged(nameof(MatchText));

    private void UpdateMatchCount() => MatchCount = LearnSession.GetExercises(_library, ToSettings()).Count;

    private static List<T> Selected<T>(IEnumerable<ToggleOption<T>> options) =>
        options.Where(o => o.IsSelected).Select(o => o.Value).ToList();
}
