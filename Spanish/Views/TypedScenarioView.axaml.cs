using Avalonia;
using Avalonia.Controls;

namespace Spanish.Views;

public partial class TypedScenarioView : UserControl
{
    public TypedScenarioView()
    {
        InitializeComponent();
    }

    // Put the caret in the answer box so the user can start typing right away.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AnswerBox.Focus();
    }
}
