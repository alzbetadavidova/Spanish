using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace Spanish.Views;

/// <summary>Lists the reasons a word breaks the usual rules.</summary>
public partial class ExceptionNotes : UserControl
{
    public static readonly StyledProperty<IReadOnlyList<string>?> NotesProperty =
        AvaloniaProperty.Register<ExceptionNotes, IReadOnlyList<string>?>(nameof(Notes));

    public ExceptionNotes()
    {
        InitializeComponent();
    }

    public IReadOnlyList<string>? Notes
    {
        get => GetValue(NotesProperty);
        set => SetValue(NotesProperty, value);
    }
}
