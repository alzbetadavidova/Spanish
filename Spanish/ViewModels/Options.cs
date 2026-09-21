using CommunityToolkit.Mvvm.ComponentModel;

namespace Spanish.ViewModels;

/// <summary>An item for a combo box; displayed through <see cref="ToString"/>.</summary>
public record Option<T>(T Value, string Label)
{
    public override string ToString() => Label;
}

/// <summary>A selectable chip (toggle button). Non-generic so XAML templates can bind to it.</summary>
public partial class ToggleOption(string label, bool isSelected) : ObservableObject
{
    public string Label { get; } = label;

    [ObservableProperty]
    private bool _isSelected = isSelected;
}

public class ToggleOption<T>(T value, string label, bool isSelected = false) : ToggleOption(label, isSelected)
{
    public T Value { get; } = value;
}
