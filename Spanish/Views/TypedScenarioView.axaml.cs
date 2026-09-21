using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Spanish.Views;

public partial class TypedScenarioView : UserControl
{
    public TypedScenarioView()
    {
        InitializeComponent();
    }

    // Put the caret in the answer box so the user can start typing right away. The view is reused
    // when two typed scenarios follow each other, so also refocus on a new DataContext.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        AnswerBox.Focus();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        Dispatcher.UIThread.Post(() => AnswerBox.Focus());
    }
}
