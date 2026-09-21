using System;
using System.Collections.Generic;
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
    public const string LibraryTarget = "library";

    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly HashSet<string> _failedTargets = [];
    private readonly List<Func<Task>> _saveParticipants = [];

    public LearnLibrary Library { get; } = library;

    /// <summary>Set while the last save of any target (library, settings) failed.</summary>
    [ObservableProperty]
    private string? _saveError;

    /// <summary>Raised when the library content or progress changed.</summary>
    public event EventHandler? Changed;

    /// <summary>Raised after a topic was renamed, before <see cref="Changed"/>.</summary>
    public event EventHandler<TopicRenamedEventArgs>? TopicRenamed;

    /// <summary>
    /// Registers a save that runs after every library save, e.g. settings that follow library changes.
    /// It should report failures through <see cref="RunSaveAsync"/>.
    /// </summary>
    public void AddSaveParticipant(Func<Task> save) => _saveParticipants.Add(save);

    /// <summary>Renames a topic and tells listeners that keep topic names (e.g. session settings).</summary>
    /// <exception cref="LibraryValidationException">The new name is empty or already used.</exception>
    public void RenameTopic(string oldName, string newName)
    {
        Library.RenameTopic(oldName, newName);
        TopicRenamed?.Invoke(this, new TopicRenamedEventArgs(oldName, newName.Trim()));
    }

    /// <summary>
    /// Notifies listeners, saves the library, then runs the save participants.
    /// Save failures are reported through <see cref="SaveError"/>.
    /// </summary>
    public async Task SaveAsync()
    {
        Changed?.Invoke(this, EventArgs.Empty);
        await RunSaveAsync(LibraryTarget, ct => store.SaveAsync(Library, ct));
        foreach (var participant in _saveParticipants)
        {
            await participant();
        }
    }

    /// <summary>
    /// Runs a save for <paramref name="target"/>, reporting I/O failures through <see cref="SaveError"/>.
    /// A success clears only that target's failure.
    /// </summary>
    /// <returns>Whether the save succeeded.</returns>
    public async Task<bool> RunSaveAsync(string target, Func<CancellationToken, Task> save)
    {
        await _saveLock.WaitAsync();
        try
        {
            await save(CancellationToken.None);
            _failedTargets.Remove(target);
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _failedTargets.Add(target);
            return false;
        }
        finally
        {
            SaveError = _failedTargets.Count > 0 ? SaveFailedMessage : null;
            _saveLock.Release();
        }
    }
}
