using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Spanish.ViewModels;

namespace Spanish.Views;

public partial class EndingsScenarioView : UserControl
{
    public EndingsScenarioView()
    {
        InitializeComponent();
        // Tunnel, so the answer box and the button don't act on Enter themselves.
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    // Put the caret in the first box so the user can start typing right away. The view is reused
    // when two endings scenarios follow each other, so also refocus on a new DataContext.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        FocusFirstBox();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        FocusFirstBox();
    }

    // The boxes are created when the rows are laid out, so wait for that.
    private void FocusFirstBox() => Dispatcher.UIThread.Post(() => FocusRow(0), DispatcherPriority.Loaded);

    /// <summary>Enter moves to the next empty box, and submits once the other boxes are filled.</summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers != KeyModifiers.None || DataContext is not EndingsScenarioViewModel vm)
        {
            return;
        }
        e.Handled = true;

        var index = e.Source is Control { DataContext: EndingRowViewModel row } ? vm.Rows.ToList().IndexOf(row) : -1;
        if (index >= 0 && vm.NextEmptyRow(index) is { } next)
        {
            FocusRow(next);
        }
        else if (vm.SubmitCommand.CanExecute(null))
        {
            vm.SubmitCommand.Execute(null);
        }
    }

    private void FocusRow(int index) =>
        RowList.ContainerFromIndex(index)?.GetVisualDescendants().OfType<TextBox>().FirstOrDefault()?.Focus();
}
