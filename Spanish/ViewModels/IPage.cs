namespace Spanish.ViewModels;

/// <summary>A page shown in the main window's content area.</summary>
public interface IPage
{
    /// <summary>Called when the user navigates to the page.</summary>
    void OnActivated();
}
