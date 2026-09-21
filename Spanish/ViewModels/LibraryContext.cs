using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Spanish.Core;

namespace Spanish.ViewModels;

public class TopicRenamedEventArgs(string oldName, string newName) : EventArgs
{
    public string OldName { get; } = oldName;
    public string NewName { get; } = newName;
}

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

    /// <summary>Raised after a topic was renamed, before <see cref="Changed"/>.</summary>
    public event EventHandler<TopicRenamedEventArgs>? TopicRenamed;

    /// <summary>Renames a topic and tells listeners that keep topic names (e.g. session settings).</summary>
    /// <exception cref="LibraryValidationException">The new name is empty or already used.</exception>
    public void RenameTopic(string oldName, string newName)
    {
        Library.RenameTopic(oldName, newName);
        TopicRenamed?.Invoke(this, new TopicRenamedEventArgs(oldName, newName.Trim()));
    }

    /// <summary>Notifies listeners and saves the library. Save failures are reported through <see cref="SaveError"/>.</summary>
    public async Task SaveAsync()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        await RunSaveAsync(ct => store.SaveAsync(Library, ct));
    }

    /// <summary>Runs a save operation, reporting I/O failures through <see cref="SaveError"/>.</summary>
    /// <returns>Whether the save succeeded.</returns>
    public async Task<bool> RunSaveAsync(Func<CancellationToken, Task> save)
    {
        await _saveLock.WaitAsync();
        try
        {
            await save(CancellationToken.None);
            SaveError = null;
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            SaveError = SaveFailedMessage;
            return false;
        }
        finally
        {
            _saveLock.Release();
        }
    }
}
