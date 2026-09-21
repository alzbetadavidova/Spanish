using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Spanish.Core;

namespace Spanish.ViewModels;

/// <summary>The loaded library shared by all pages, with saving and change notification.</summary>
public partial class LibraryContext(LearnLibrary library, IFileStore<LearnLibrary> store) : ObservableObject
{
    public const string SaveFailedMessage = "Couldn't save your changes. They'll be saved with your next change.";

    private readonly SemaphoreSlim _saveLock = new(1, 1);

    public LearnLibrary Library { get; } = library;

    [ObservableProperty]
    private string? _saveError;

    /// <summary>Raised when the library content or progress changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Notifies listeners and saves the library. Save failures are reported through <see cref="SaveError"/>.</summary>
    public async Task SaveAsync()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        await RunSaveAsync(ct => store.SaveAsync(Library, ct));
    }

    /// <summary>Runs a save operation, reporting I/O failures through <see cref="SaveError"/>.</summary>
    public async Task RunSaveAsync(Func<CancellationToken, Task> save)
    {
        await _saveLock.WaitAsync();
        try
        {
            await save(CancellationToken.None);
            SaveError = null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SaveError = SaveFailedMessage;
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
