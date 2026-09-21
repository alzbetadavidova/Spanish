using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spanish.Core;

public record LoadResult<T>(T Value, string? CorruptFileBackupPath);

public interface IFileStore<T>
{
    Task<LoadResult<T>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(T value, CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores a value as JSON. A missing file falls back to the seed file (or <c>createEmpty</c>);
/// a corrupt file is moved aside to a timestamped backup. Saves replace the file atomically.
/// </summary>
public class JsonFileStore<T>(string path, string? seedPath, Func<T> createEmpty, IClock clock) : IFileStore<T>
    where T : class
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public string Path { get; } = path;

    public async Task<LoadResult<T>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(Path))
        {
            return new LoadResult<T>(await LoadSeedAsync(cancellationToken), null);
        }

        try
        {
            return new LoadResult<T>(await ReadAsync(Path, cancellationToken), null);
        }
        catch (JsonException)
        {
            var backup = $"{Path}.bak-{clock.Now:yyyyMMdd-HHmmss}";
            File.Move(Path, backup, overwrite: true);
            return new LoadResult<T>(await LoadSeedAsync(cancellationToken), backup);
        }
    }

    public async Task SaveAsync(T value, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        // Serialize before the first await so the caller can keep mutating the value afterwards.
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value, Options);

        var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(Path));
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        var temp = Path + ".tmp";
        await File.WriteAllBytesAsync(temp, bytes, cancellationToken);
        File.Move(temp, Path, overwrite: true);
    }

    private async Task<T> LoadSeedAsync(CancellationToken cancellationToken) =>
        seedPath is not null && File.Exists(seedPath)
            ? await ReadAsync(seedPath, cancellationToken)
            : createEmpty();

    private static async Task<T> ReadAsync(string file, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(file);
        return await JsonSerializer.DeserializeAsync<T>(stream, Options, cancellationToken)
               ?? throw new JsonException($"{file} contains no value.");
    }
}
