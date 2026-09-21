using System.Text.Json;
using Spanish.Core;

namespace Spanish.Tests;

public class JsonFileStoreTests
{
    private string _directory = null!;
    private readonly FakeClock _clock = new() { Now = new DateTime(2026, 9, 21, 14, 30, 5) };

    [SetUp]
    public void Setup()
    {
        _directory = Path.Combine(Path.GetTempPath(), "spanish-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private JsonFileStore<LearnLibrary> Store(string file = "library.json", string? seed = null) =>
        new(Path.Combine(_directory, file), seed, () => new LearnLibrary { Topics = ["empty"] }, _clock);

    [Test]
    public async Task Load_MissingFileWithoutSeed_CreatesEmpty()
    {
        var result = await Store().LoadAsync();

        Assert.That(result.Value.Topics, Is.EqualTo(new[] { "empty" }));
        Assert.That(result.CorruptFileBackupPath, Is.Null);
    }

    [Test]
    public async Task Load_MissingFileWithMissingSeed_CreatesEmpty()
    {
        var result = await Store(seed: Path.Combine(_directory, "nope.json")).LoadAsync();

        Assert.That(result.Value.Topics, Is.EqualTo(new[] { "empty" }));
    }

    [Test]
    public async Task Load_MissingFile_UsesSeed()
    {
        var seed = Path.Combine(_directory, "seed.json");
        await Store("seed.json").SaveAsync(TestData.Library());

        var result = await Store(seed: seed).LoadAsync();

        Assert.That(result.Value.Units.Count(), Is.EqualTo(3));
    }

    [Test]
    public async Task SaveThenLoad_RoundTripsLibraryWithProgress()
    {
        var library = TestData.Library();
        library.Nouns[0].RecordAnswer(ScenarioType.Gender, true, _clock.Now);
        var store = Store("nested/dir/library.json");

        await store.SaveAsync(library);
        var loaded = (await store.LoadAsync()).Value;

        Assert.Multiple(() =>
        {
            Assert.That(JsonSerializer.Serialize(loaded, JsonFileStore<LearnLibrary>.Options),
                Is.EqualTo(JsonSerializer.Serialize(library, JsonFileStore<LearnLibrary>.Options)));
            Assert.That(loaded.Nouns[0].GetProgress(ScenarioType.Gender)!.Index, Is.EqualTo(1));
            Assert.That(File.Exists(store.Path + ".tmp"), Is.False);
        });
    }

    [Test]
    public async Task Save_ShorterContent_ReplacesWholeFile()
    {
        var store = Store();
        await store.SaveAsync(TestData.Library());

        await store.SaveAsync(new LearnLibrary());

        Assert.That(() => JsonDocument.Parse(File.ReadAllText(store.Path)), Throws.Nothing);
        Assert.That((await store.LoadAsync()).Value.Units, Is.Empty);
    }

    [Test]
    public void Save_Null_Throws()
    {
        Assert.That(() => Store().SaveAsync(null!), Throws.ArgumentNullException);
    }

    [Test]
    public async Task Load_CorruptFile_BacksUpAndFallsBack()
    {
        var store = Store();
        await File.WriteAllTextAsync(store.Path, "{ not json");

        var result = await store.LoadAsync();

        var expectedBackup = store.Path + ".bak-20260921-143005";
        Assert.Multiple(() =>
        {
            Assert.That(result.CorruptFileBackupPath, Is.EqualTo(expectedBackup));
            Assert.That(File.Exists(expectedBackup), Is.True);
            Assert.That(File.Exists(store.Path), Is.False);
            Assert.That(result.Value.Topics, Is.EqualTo(new[] { "empty" }));
        });
    }

    [Test]
    public async Task Load_CorruptFileWithSeed_BacksUpAndLoadsSeed()
    {
        var seed = Path.Combine(_directory, "seed.json");
        await Store("seed.json").SaveAsync(TestData.Library());
        var store = Store(seed: seed);
        await File.WriteAllTextAsync(store.Path, "{ not json");

        var result = await store.LoadAsync();

        Assert.Multiple(() =>
        {
            Assert.That(result.CorruptFileBackupPath, Is.Not.Null);
            Assert.That(File.Exists(result.CorruptFileBackupPath), Is.True);
            Assert.That(result.Value.Units.Count(), Is.EqualTo(3));
        });
    }

    [Test]
    public async Task Load_NullJson_TreatedAsCorrupt()
    {
        var store = Store();
        await File.WriteAllTextAsync(store.Path, "null");

        var result = await store.LoadAsync();

        var expectedBackup = store.Path + ".bak-20260921-143005";
        Assert.Multiple(() =>
        {
            Assert.That(result.CorruptFileBackupPath, Is.EqualTo(expectedBackup));
            Assert.That(File.Exists(expectedBackup), Is.True);
            Assert.That(File.Exists(store.Path), Is.False);
            Assert.That(result.Value.Topics, Is.EqualTo(new[] { "empty" }));
        });
    }

    [Test]
    public async Task Load_NullValues_BecomeEmpty()
    {
        var store = Store();
        await File.WriteAllTextAsync(store.Path,
            """{ "Nouns": [ { "BaseValue": "mesa", "Translation": null, "Topics": null, "Progress": null } ], "Verbs": null, "Topics": null }""");

        var library = (await store.LoadAsync()).Value;

        Assert.Multiple(() =>
        {
            Assert.That(library.Verbs, Is.Empty);
            Assert.That(library.Topics, Is.Empty);
            Assert.That(library.Nouns[0].TranslationAlternatives, Is.Empty);
            Assert.That(library.Nouns[0].Topics, Is.Empty);
            Assert.That(LearnStats.Build(library), Has.Count.EqualTo(1));
        });
    }

    [Test]
    public async Task Load_LegacyFile_IgnoresOldFieldsAndReadsGender()
    {
        var path = Path.Combine(TestContext.CurrentContext.TestDirectory, "test.json");
        var store = new JsonFileStore<LearnLibrary>(path, null, () => new LearnLibrary(), _clock);

        var noun = (await store.LoadAsync()).Value.Nouns.Single();

        Assert.Multiple(() =>
        {
            Assert.That(noun.BaseValue, Is.EqualTo("ciudad"));
            Assert.That(noun.Gender, Is.EqualTo(Gender.Feminine));
            Assert.That(noun.PluralValue, Is.EqualTo("ciudades"));
            Assert.That(noun.Progress, Is.Empty);
        });
    }

    [Test]
    public async Task SaveThenLoad_SessionSettings_RoundTrips()
    {
        var store = new JsonFileStore<SessionSettings>(Path.Combine(_directory, "settings.json"), null, () => new SessionSettings(), _clock);
        var settings = new SessionSettings
        {
            IncludeVerbs = false,
            NounScenarioTypes = [ScenarioType.Plural],
            Topics = ["city"],
            Order = SessionOrder.Random,
            Direction = Direction.SpanishToEnglish
        };

        await store.SaveAsync(settings);
        var loaded = (await store.LoadAsync()).Value;

        Assert.Multiple(() =>
        {
            Assert.That(loaded.IncludeVerbs, Is.False);
            Assert.That(loaded.NounScenarioTypes, Is.EqualTo(new[] { ScenarioType.Plural }));
            Assert.That(loaded.Topics, Is.EqualTo(new[] { "city" }));
            Assert.That(loaded.Order, Is.EqualTo(SessionOrder.Random));
            Assert.That(loaded.Direction, Is.EqualTo(Direction.SpanishToEnglish));
        });
    }

    [Test]
    public void Abstractions_SystemImplementationsWork()
    {
        Assert.Multiple(() =>
        {
            Assert.That(new SystemClock().Now, Is.EqualTo(DateTime.Now).Within(TimeSpan.FromMinutes(1)));
            Assert.That(new SystemRandomSource().Next(3), Is.InRange(0, 2));
        });
    }
}
