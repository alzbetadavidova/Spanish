using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Spanish.Views;

public partial class CardScenarioView : UserControl
{
    public CardScenarioView()
    {
        InitializeComponent();
    }

    // Take focus so the keyboard shortcuts work without clicking first. The view is reused
    // when two scenarios of the same kind follow each other, so also refocus on a new DataContext.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Dispatcher.UIThread.Post(() => Focus());
    }
}
