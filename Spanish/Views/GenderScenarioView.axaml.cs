using Avalonia;
using Avalonia.Controls;

namespace Spanish.Views;

public partial class GenderScenarioView : UserControl
{
    public GenderScenarioView()
    {
        InitializeComponent();
    }

    // Take focus so the keyboard shortcuts work without clicking first.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focus();
    }
}
