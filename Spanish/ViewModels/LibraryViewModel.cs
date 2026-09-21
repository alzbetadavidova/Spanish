using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spanish.Core;

namespace Spanish.ViewModels;

public partial class LibraryViewModel : ObservableObject, IPage
{
    public LibraryViewModel(LibraryContext context)
    {
        Nouns = new WordListViewModel(context, WordKind.Noun);
        Verbs = new WordListViewModel(context, WordKind.Verb);
        Topics = new TopicsViewModel(context);
        // Tabs edit the same library; keep every tab (and open editors) in sync.
        context.Changed += (_, _) => OnActivated();
    }

    public WordListViewModel Nouns { get; }
    public WordListViewModel Verbs { get; }
    public TopicsViewModel Topics { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CurrentList), nameof(AddLabel), nameof(IsAddVisible))]
    private int _selectedTabIndex;

    /// <summary>The nouns or verbs list of the selected tab; null on the topics tab.</summary>
    public WordListViewModel? CurrentList => SelectedTabIndex switch
    {
        0 => Nouns,
        1 => Verbs,
        _ => null
    };

    public bool IsAddVisible => CurrentList is not null;
    public string AddLabel => CurrentList?.AddLabel ?? string.Empty;

    [RelayCommand]
    private void Add() => CurrentList?.AddCommand.Execute(null);

    public void OnActivated()
    {
        Nouns.Refresh();
        Verbs.Refresh();
        Topics.Refresh();
    }
}

/// <summary>Searchable list of nouns or verbs with an editor for the selected one.</summary>
public partial class WordListViewModel : ObservableObject
{
    private readonly LibraryContext _context;
    private readonly EditorCallbacks _callbacks;
    private bool _restoringSelection;

    public WordListViewModel(LibraryContext context, WordKind kind)
    {
        _context = context;
        Kind = kind;
        _callbacks = new EditorCallbacks(OnSaved, OnDeleted, OnCancelled);
        Refresh();
    }

    public WordKind Kind { get; }
    public string AddLabel => Kind == WordKind.Noun ? "Add noun" : "Add verb";
    public string SearchPlaceholder => Kind == WordKind.Noun ? "Search nouns" : "Search verbs";
    public ObservableCollection<LearnUnit> Items { get; } = [];

    public string CountText
    {
        get
        {
            var noun = Kind == WordKind.Noun ? "noun" : "verb";
            return Items.Count == 1 ? $"1 {noun}" : $"{Items.Count} {noun}s";
        }
    }

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private LearnUnit? _selected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasEditor))]
    private WordEditorViewModel? _editor;

    public bool HasEditor => Editor is not null;

    partial void OnSearchTextChanged(string value) => Refresh();

    partial void OnSelectedChanged(LearnUnit? value)
    {
        if (value is not null && !_restoringSelection)
        {
            Editor = CreateEditor(value);
        }
    }

    [RelayCommand]
    private void Add()
    {
        Selected = null;
        Editor = CreateEditor(null);
    }

    public void Refresh()
    {
        var selected = Selected;
        var units = Kind == WordKind.Noun ? _context.Library.Nouns.Cast<LearnUnit>() : _context.Library.Verbs;
        var search = SearchText.Trim();
        Items.Clear();
        foreach (var unit in units
                     .Where(u => search.Length == 0
                                 || u.BaseValue.Contains(search, StringComparison.OrdinalIgnoreCase)
                                 || u.Translation.Contains(search, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(u => u.BaseValue, StringComparer.CurrentCultureIgnoreCase))
        {
            Items.Add(unit);
        }
        OnPropertyChanged(nameof(CountText));
        // Clearing the list resets the selection; restore it without recreating the editor.
        // A selection hidden by the search is cleared, but its editor stays open.
        _restoringSelection = true;
        Selected = selected is not null && Items.Contains(selected) ? selected : null;
        _restoringSelection = false;
        Editor?.RefreshTopics();
    }

    private WordEditorViewModel CreateEditor(LearnUnit? unit) => Kind == WordKind.Noun
        ? new NounEditorViewModel(_context, unit as Noun, _callbacks)
        : new VerbEditorViewModel(_context, unit as Verb, _callbacks);

    private void OnSaved(LearnUnit unit)
    {
        Refresh();
        Selected = null;
        Selected = unit;
    }

    private void OnDeleted()
    {
        Selected = null;
        Editor = null;
        Refresh();
    }

    private void OnCancelled() => Editor = Selected is null ? null : CreateEditor(Selected);
}

/// <summary>Adds, renames and deletes topics and assigns words to them.</summary>
public partial class TopicsViewModel : ObservableObject
{
    private readonly LibraryContext _context;

    public TopicsViewModel(LibraryContext context)
    {
        _context = context;
        Refresh();
    }

    public ObservableCollection<string> Topics { get; } = [];
    public ObservableCollection<ToggleOption<LearnUnit>> Words { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private string? _selectedTopic;

    [ObservableProperty]
    private string _newTopicName = string.Empty;

    [ObservableProperty]
    private string _renameText = string.Empty;

    [ObservableProperty]
    private string? _topicError;

    [ObservableProperty]
    private bool _isConfirmingDelete;

    public bool HasSelection => SelectedTopic is not null;
    public string DeleteLabel => IsConfirmingDelete ? "Confirm delete" : "Delete topic";

    partial void OnIsConfirmingDeleteChanged(bool value) => OnPropertyChanged(nameof(DeleteLabel));
    partial void OnNewTopicNameChanged(string value) => TopicError = null;
    partial void OnRenameTextChanged(string value) => TopicError = null;

    partial void OnSelectedTopicChanged(string? value)
    {
        RenameText = value ?? string.Empty;
        IsConfirmingDelete = false;
        TopicError = null;
        FillWords();
    }

    public void Refresh()
    {
        var selected = SelectedTopic;
        Topics.Clear();
        foreach (var topic in _context.Library.Topics.Order())
        {
            Topics.Add(topic);
        }
        SelectedTopic = selected is not null && Topics.Contains(selected) ? selected : null;
        FillWords();
    }

    [RelayCommand]
    private Task AddTopic() => Modify(() =>
    {
        _context.Library.AddTopic(NewTopicName);
        var added = NewTopicName.Trim();
        NewTopicName = string.Empty;
        return added;
    });

    [RelayCommand]
    private Task RenameTopic() => SelectedTopic is null
        ? Task.CompletedTask
        : Modify(() =>
        {
            _context.RenameTopic(SelectedTopic, RenameText);
            return RenameText.Trim();
        });

    [RelayCommand]
    private async Task DeleteTopic()
    {
        if (SelectedTopic is null)
        {
            return;
        }
        if (!IsConfirmingDelete)
        {
            IsConfirmingDelete = true;
            return;
        }
        _context.Library.RemoveTopic(SelectedTopic);
        SelectedTopic = null;
        Refresh();
        await _context.SaveAsync();
    }

    /// <summary>Called by a word chip after it was toggled.</summary>
    [RelayCommand]
    private async Task ToggleWord(ToggleOption<LearnUnit> option)
    {
        if (SelectedTopic is null)
        {
            return;
        }
        _context.Library.SetTopicMembership(SelectedTopic, option.Value, option.IsSelected);
        await _context.SaveAsync();
    }

    private async Task Modify(Func<string> change)
    {
        string topic;
        try
        {
            topic = change();
        }
        catch (LibraryValidationException e)
        {
            TopicError = e.Errors[0].Message;
            return;
        }
        Refresh();
        SelectedTopic = topic;
        await _context.SaveAsync();
    }

    private void FillWords()
    {
        Words.Clear();
        if (SelectedTopic is null)
        {
            return;
        }
        foreach (var unit in _context.Library.Units.OrderBy(u => u.BaseValue, StringComparer.CurrentCultureIgnoreCase))
        {
            var label = $"{unit.BaseValue} · {(unit.Kind == WordKind.Noun ? "noun" : "verb")}";
            Words.Add(new ToggleOption<LearnUnit>(unit, label, unit.HasTopic(SelectedTopic)));
        }
    }
}
