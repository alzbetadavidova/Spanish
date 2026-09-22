using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spanish.Core;

namespace Spanish.ViewModels;

public enum StatsColumn
{
    Word,
    Translation,
    Overall,
    Attempts,
    LastPracticed
}

public class StatsRowViewModel(StatsRow row, DateTime now)
{
    public StatsRow Row { get; } = row;
    public string Word => Row.Word;
    public string Kind => DisplayNames.Singular(Row.Kind);
    public string Translation => Row.Translation;
    public double OverallPercent => Math.Round((Row.OverallIndex ?? 0) * 100);
    public string OverallText => Row.OverallIndex is null ? "new" : $"{OverallPercent:0}%";
    public bool IsLow => Row.OverallIndex is < 0.5;
    public bool IsMedium => Row.OverallIndex is >= 0.5 and < 0.8;
    public bool IsHigh => Row.OverallIndex is >= 0.8;

    public string Breakdown
    {
        get
        {
            var practiced = Row.Breakdown
                .Select(b => b.Index is { } index ? $"{DisplayNames.Scenario(b.Type)} {Math.Round(index * 100):0}" : null)
                .OfType<string>()
                .ToList();
            return practiced.Count == 0 ? "not practiced" : string.Join(" · ", practiced);
        }
    }

    public int Attempts => Row.Attempts;
    public string LastPracticedText => Relative(Row.LastPracticed, now);

    public static string Relative(DateTime? at, DateTime now)
    {
        if (at is null)
        {
            return "—";
        }
        var days = (now.Date - at.Value.Date).Days;
        return days switch
        {
            <= 0 => "today",
            1 => "yesterday",
            _ => $"{days} d ago"
        };
    }
}

public partial class StatsViewModel : ObservableObject, IPage
{
    private readonly LibraryContext _context;
    private readonly IClock _clock;

    public StatsViewModel(LibraryContext context, IClock clock)
    {
        _context = context;
        _clock = clock;
        Refresh();
    }

    public static IReadOnlyList<Option<WordKind?>> KindFilters { get; } =
    [
        new(null, "All word types"), new(WordKind.Noun, "Nouns"), new(WordKind.Verb, "Verbs"),
        new(WordKind.Adjective, "Adjectives"), new(WordKind.Preposition, "Prepositions"), new(WordKind.Numeral, "Numerals")
    ];

    public ObservableCollection<Option<string?>> TopicFilters { get; } = [];
    public ObservableCollection<StatsRowViewModel> Rows { get; } = [];

    [ObservableProperty]
    private Option<WordKind?> _selectedKind = KindFilters[0];

    [ObservableProperty]
    private Option<string?>? _selectedTopic;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WordHeader), nameof(TranslationHeader), nameof(OverallHeader), nameof(AttemptsHeader), nameof(LastPracticedHeader))]
    private StatsColumn _sortColumn = StatsColumn.Overall;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WordHeader), nameof(TranslationHeader), nameof(OverallHeader), nameof(AttemptsHeader), nameof(LastPracticedHeader))]
    private bool _sortDescending;

    public bool IsEmpty => Rows.Count == 0;

    public string WordHeader => Header("Word", StatsColumn.Word);
    public string TranslationHeader => Header("English", StatsColumn.Translation);
    public string OverallHeader => Header("Overall", StatsColumn.Overall);
    public string AttemptsHeader => Header("Attempts", StatsColumn.Attempts);
    public string LastPracticedHeader => Header("Last practiced", StatsColumn.LastPracticed);

    public void OnActivated() => Refresh();

    partial void OnSelectedKindChanged(Option<WordKind?> value) => FillRows();
    partial void OnSelectedTopicChanged(Option<string?>? value) => FillRows();

    [RelayCommand]
    private void SortBy(StatsColumn column)
    {
        if (SortColumn == column)
        {
            SortDescending = !SortDescending;
        }
        else
        {
            SortColumn = column;
            SortDescending = false;
        }
        FillRows();
    }

    public void Refresh()
    {
        var topic = SelectedTopic?.Value;
        TopicFilters.Clear();
        TopicFilters.Add(new Option<string?>(null, "All topics"));
        foreach (var t in _context.Library.Topics.Order())
        {
            TopicFilters.Add(new Option<string?>(t, t));
        }
        SelectedTopic = TopicFilters.FirstOrDefault(t => t.Value == topic) ?? TopicFilters[0];
        FillRows();
    }

    private void FillRows()
    {
        var now = _clock.Now;
        var rows = LearnStats.Build(_context.Library)
            .Where(r => SelectedKind.Value is null || r.Kind == SelectedKind.Value)
            .Where(r => SelectedTopic?.Value is null || r.Unit.HasTopic(SelectedTopic.Value));

        rows = SortColumn switch
        {
            StatsColumn.Word => Order(rows, r => r.Word),
            StatsColumn.Translation => Order(rows, r => r.Translation),
            StatsColumn.Attempts => Order(rows, r => r.Attempts),
            StatsColumn.LastPracticed => Order(rows, r => r.LastPracticed ?? DateTime.MinValue),
            _ => Order(rows, r => r.OverallIndex ?? -1)
        };

        Rows.Clear();
        foreach (var row in rows)
        {
            Rows.Add(new StatsRowViewModel(row, now));
        }
        OnPropertyChanged(nameof(IsEmpty));
    }

    private IEnumerable<StatsRow> Order<TKey>(IEnumerable<StatsRow> rows, Func<StatsRow, TKey> key) =>
        SortDescending ? rows.OrderByDescending(key) : rows.OrderBy(key);

    private string Header(string label, StatsColumn column) =>
        SortColumn != column ? label : SortDescending ? $"{label} ↓" : $"{label} ↑";
}
