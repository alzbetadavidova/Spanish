using Avalonia;
using Avalonia.Controls;

namespace Spanish.Views;

/// <summary>The noun chips of an editor of a word practiced with linked nouns.</summary>
public partial class NounChips : UserControl
{
    public static readonly StyledProperty<string?> HintProperty =
        AvaloniaProperty.Register<NounChips, string?>(nameof(Hint), "Pick the nouns to practice it with.");

    public NounChips()
    {
        InitializeComponent();
    }

    /// <summary>How to pick the nouns, shown below the chips.</summary>
    public string? Hint
    {
        get => GetValue(HintProperty);
        set => SetValue(HintProperty, value);
    }
}
