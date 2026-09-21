using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Spanish.Core;

namespace Spanish;

public partial class MainWindowViewModel(LearnLibrary library) : ObservableObject
{
    private LearnUnit _currentUnit = library.GetNextUnit();

    public LearnUnit CurrentUnit
    {
        get => _currentUnit;
        set => SetProperty(ref _currentUnit, value);
    }

    [RelayCommand]
    public void KnowUnit()
    {
        _currentUnit.IncreaseCoefficient();
        NextUnit();
    }
    
    [RelayCommand]
    public void NotKnowUnit()
    {
        _currentUnit.DecreaseCoefficient();
        NextUnit();
    }

    private void NextUnit()
    {
        CurrentUnit = library.GetNextUnit();
    }
}