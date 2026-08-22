using System.Text;
using System.Text.Json;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Infrastructure.Persistence;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class AtomicJsonDocumentStoreTests
{
    private static readonly string[] ConcurrentNames = ["One", "Two"];

    [Fact]
    public async Task FirstSaveAndLoadRoundTrip()
    {
        await using var fixture = PersistenceTestFixture.Create();
        var expected = PersistenceTestFixture.Games("Alpha");

        var save = await fixture.GamesStore.SaveAsync(expected, CancellationToken.None);
        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentSaveStatus.Saved, save.Status);
        Assert.Equal(DocumentLoadStatus.Loaded, load.Status);
        var reloaded = Assert.Single(Assert.IsType<GamesDocument>(load.Document).Games);
        Assert.Equal("Alpha", reloaded.Name);
        Assert.Equal(Assert.Single(expected.Games).Id, reloaded.Id);
        Assert.NotEqual(Guid.Empty, reloaded.Id.Value);
    }

    [Fact]
    public async Task MultipleDistinctGameIdsSurviveStagedValidationAndRoundTrip()
    {
        await using var fixture = PersistenceTestFixture.Create();
        var first = PersistenceTestFixture.Games("Manual").Games[0];
        var steam = first with
        {
            Id = NovaLauncher.Domain.Library.GameId.FromSteamAppId(570),
            Name = "Steam game",
            Source = "Steam",
            SourceItemId = "570",
            ImportedName = "Steam game",
            LaunchTarget = new NovaLauncher.Domain.Library.LaunchTarget(
                "steam://run/570", [], null, NovaLauncher.Domain.Library.LaunchTargetKind.Uri),
        };

        var save = await fixture.GamesStore.SaveAsync(
            new GamesDocument(GamesDocument.CurrentSchemaVersion, [first, steam]),
            CancellationToken.None);
        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentSaveStatus.Saved, save.Status);
        Assert.Equal(DocumentLoadStatus.Loaded, load.Status);
        Assert.Equal(2, Assert.IsType<GamesDocument>(load.Document).Games.Select(static game => game.Id).Distinct().Count());
    }

    [Fact]
    public async Task LegacyEmptyGameIdIsRecoveredOnceAndRewrittenAsStableString()
    {
        await using var fixture = PersistenceTestFixture.Create();
        Directory.CreateDirectory(fixture.Root);
        var document = PersistenceTestFixture.Games("Legacy empty identity");
        var json = JsonSerializer.Serialize(document).Replace(
            document.Games[0].Id.Value.ToString("D"),
            "00000000-0000-0000-0000-000000000000",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "games.json"), json, CancellationToken.None);

        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);
        var recovered = Assert.Single(Assert.IsType<GamesDocument>(load.Document).Games);
        var save = await fixture.GamesStore.SaveAsync(Assert.IsType<GamesDocument>(load.Document), CancellationToken.None);

        Assert.Equal(DocumentLoadStatus.Loaded, load.Status);
        Assert.NotEqual(Guid.Empty, recovered.Id.Value);
        Assert.Equal(DocumentSaveStatus.Saved, save.Status);
    }

    [Fact]
    public async Task MissingDocumentReturnsNotFound()
    {
        await using var fixture = PersistenceTestFixture.Create();

        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentLoadStatus.NotFound, load.Status);
        Assert.Null(load.Document);
    }

    [Fact]
    public async Task SaveRejectsWrongSchemaAndInvalidDomainWithoutWriting()
    {
        await using var fixture = PersistenceTestFixture.Create();
        var wrongSchema = PersistenceTestFixture.Games("Wrong") with { SchemaVersion = GamesDocument.CurrentSchemaVersion + 1 };
        var invalidDomain = PersistenceTestFixture.Games("Invalid") with { Games = [] };
        invalidDomain = invalidDomain with { Games = [PersistenceTestFixture.Games("Invalid").Games[0] with { Name = "" }] };

        var schemaSave = await fixture.GamesStore.SaveAsync(wrongSchema, CancellationToken.None);
        var domainSave = await fixture.GamesStore.SaveAsync(invalidDomain, CancellationToken.None);

        Assert.Equal(DocumentSaveStatus.Failed, schemaSave.Status);
        Assert.Equal(DocumentSaveStatus.Failed, domainSave.Status);
        Assert.False(File.Exists(Path.Combine(fixture.Root, "games.json")));
    }

    [Fact]
    public async Task LegacyRootArrayLoadsForExplicitMigration()
    {
        await using var fixture = PersistenceTestFixture.Create();
        await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Legacy"), CancellationToken.None);
        var path = Path.Combine(fixture.Root, "games.json");
        using var current = JsonDocument.Parse(await File.ReadAllBytesAsync(path, CancellationToken.None));
        var legacyJson = current.RootElement.GetProperty("games").GetRawText();
        await File.WriteAllTextAsync(path, legacyJson, CancellationToken.None);

        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentLoadStatus.MigratedLegacy, load.Status);
        Assert.Equal("Legacy", Assert.Single(Assert.IsType<GamesDocument>(load.Document).Games).Name);
        Assert.Contains("legacy", load.Warning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task VersionOneGamesMigrateInMemoryThenCommitAtomicallyWithBackup()
    {
        await using var fixture = PersistenceTestFixture.Create();
        await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Before 1.1"), CancellationToken.None);
        var path = Path.Combine(fixture.Root, "games.json");
        var currentJson = await File.ReadAllTextAsync(path, CancellationToken.None);
        var versionOneJson = currentJson.Replace(
            $"\"schemaVersion\": {GamesDocument.CurrentSchemaVersion}",
            "\"schemaVersion\": 1",
            StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, versionOneJson, CancellationToken.None);

        var migrated = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentLoadStatus.MigratedLegacy, migrated.Status);
        Assert.Equal(GamesDocument.CurrentSchemaVersion, migrated.Document!.SchemaVersion);
        Assert.Equal(DocumentSaveStatus.Saved, (await fixture.GamesStore.SaveAsync(migrated.Document, CancellationToken.None)).Status);
        using var committed = JsonDocument.Parse(await File.ReadAllBytesAsync(path, CancellationToken.None));
        Assert.Equal(GamesDocument.CurrentSchemaVersion, committed.RootElement.GetProperty("schemaVersion").GetInt32());
        var backup = await File.ReadAllTextAsync(path + ".bak", CancellationToken.None);
        Assert.Contains("\"schemaVersion\": 1", backup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SettingsAndCollectionsRoundTripTheirIndependentSchemas()
    {
        await using var fixture = PersistenceTestFixture.Create();

        Assert.Equal(
            DocumentSaveStatus.Saved,
            (await fixture.SettingsStore.SaveAsync(SettingsDocument.Default, CancellationToken.None)).Status);
        Assert.Equal(
            DocumentSaveStatus.Saved,
            (await fixture.CollectionsStore.SaveAsync(CollectionsDocument.Empty, CancellationToken.None)).Status);

        Assert.Equal(DocumentLoadStatus.Loaded, (await fixture.SettingsStore.LoadAsync(CancellationToken.None)).Status);
        Assert.Equal(DocumentLoadStatus.Loaded, (await fixture.CollectionsStore.LoadAsync(CancellationToken.None)).Status);
    }

    [Fact]
    public async Task CorruptPrimaryRecoversLastKnownGoodBackup()
    {
        await using var fixture = PersistenceTestFixture.Create();
        await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("First"), CancellationToken.None);
        await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Second"), CancellationToken.None);
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, "games.json"),
            "{corrupt",
            CancellationToken.None);

        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentLoadStatus.RecoveredFromBackup, load.Status);
        Assert.Equal("First", Assert.Single(Assert.IsType<GamesDocument>(load.Document).Games).Name);
        Assert.NotNull(load.Warning);
    }

    [Fact]
    public async Task CorruptPrimaryAndBackupAreUnrecoverable()
    {
        await using var fixture = PersistenceTestFixture.Create();
        Directory.CreateDirectory(fixture.Root);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "games.json"), "bad", CancellationToken.None);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "games.json.bak"), "bad", CancellationToken.None);

        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentLoadStatus.Unrecoverable, load.Status);
        Assert.Null(load.Document);
    }

    [Fact]
    public async Task NewerSchemaIsRefusedAndNotOverwritten()
    {
        await using var fixture = PersistenceTestFixture.Create();
        Directory.CreateDirectory(fixture.Root);
        var path = Path.Combine(fixture.Root, "games.json");
        var future = "{\"schemaVersion\":999,\"games\":[]}";
        await File.WriteAllTextAsync(path, future, CancellationToken.None);

        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);
        var save = await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Current"), CancellationToken.None);

        Assert.Equal(DocumentLoadStatus.UnsupportedNewerSchema, load.Status);
        Assert.Equal(DocumentSaveStatus.Failed, save.Status);
        Assert.Equal(future, await File.ReadAllTextAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task LaterSavePreservesCorruptPrimaryBeforeReplacement()
    {
        await using var fixture = PersistenceTestFixture.Create();
        Directory.CreateDirectory(fixture.Root);
        var path = Path.Combine(fixture.Root, "games.json");
        var corrupt = Encoding.UTF8.GetBytes("{corrupt");
        await File.WriteAllBytesAsync(path, corrupt, CancellationToken.None);

        var save = await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Recovered"), CancellationToken.None);

        Assert.Equal(DocumentSaveStatus.Saved, save.Status);
        var invalidPath = Assert.Single(Directory.GetFiles(fixture.Root, "games.json.invalid-*"));
        Assert.Equal(corrupt, await File.ReadAllBytesAsync(invalidPath, CancellationToken.None));
    }

    [Fact]
    public async Task CancellationBeforeCommitChangesNothing()
    {
        await using var fixture = PersistenceTestFixture.Create();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Cancelled"), cancellation.Token));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "games.json")));
    }

    [Fact]
    public async Task ConcurrentSavesRemainValidAndSerialized()
    {
        await using var fixture = PersistenceTestFixture.Create();

        var saves = await Task.WhenAll(
            fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("One"), CancellationToken.None),
            fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Two"), CancellationToken.None));
        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.All(saves, result => Assert.Equal(DocumentSaveStatus.Saved, result.Status));
        Assert.Equal(DocumentLoadStatus.Loaded, load.Status);
        Assert.Contains(
            Assert.Single(Assert.IsType<GamesDocument>(load.Document).Games).Name,
            ConcurrentNames);
        Assert.True(File.Exists(Path.Combine(fixture.Root, "games.json.bak")));
    }

    [Fact]
    public async Task WriteFailureLeavesPreviousDocumentReadable()
    {
        await using var fixture = PersistenceTestFixture.Create();
        await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Original"), CancellationToken.None);
        var failingStore = DocumentStoreFactory.CreateGamesStore(
            fixture.Root,
            new WriteFailureFileSystem(fixture.FileSystem),
            fixture.Clock);

        var save = await failingStore.SaveAsync(PersistenceTestFixture.Games("Replacement"), CancellationToken.None);
        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentSaveStatus.Failed, save.Status);
        Assert.Equal("Original", Assert.Single(Assert.IsType<GamesDocument>(load.Document).Games).Name);
    }

    [Fact]
    public async Task ReplacementFailureLeavesPreviousDocumentReadable()
    {
        await using var fixture = PersistenceTestFixture.Create();
        await fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Original"), CancellationToken.None);
        var failingStore = DocumentStoreFactory.CreateGamesStore(
            fixture.Root,
            new ReplacementFailureFileSystem(fixture.FileSystem),
            fixture.Clock);

        var save = await failingStore.SaveAsync(PersistenceTestFixture.Games("Replacement"), CancellationToken.None);
        var load = await fixture.GamesStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentSaveStatus.Failed, save.Status);
        Assert.Equal("Original", Assert.Single(Assert.IsType<GamesDocument>(load.Document).Games).Name);
    }

    [Fact]
    public async Task LockWaitHonorsCancellation()
    {
        await using var fixture = PersistenceTestFixture.Create();
        await using var heldLock = await fixture.FileSystem.AcquireExclusiveLockAsync(
            Path.Combine(fixture.Root, "games.json.lock"),
            CancellationToken.None);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => fixture.GamesStore.SaveAsync(PersistenceTestFixture.Games("Blocked"), cancellation.Token));
        Assert.False(File.Exists(Path.Combine(fixture.Root, "games.json")));
    }

    private sealed class WriteFailureFileSystem(IAtomicFileSystem inner) : DelegatingAtomicFileSystem(inner)
    {
        public override Task WriteAllBytesDurableAsync(
            string path,
            ReadOnlyMemory<byte> content,
            CancellationToken cancellationToken) =>
            throw new IOException("Injected disk-full failure.");
    }

    private sealed class ReplacementFailureFileSystem(IAtomicFileSystem inner) : DelegatingAtomicFileSystem(inner)
    {
        public override void ReplaceFile(string sourcePath, string destinationPath, string backupPath) =>
            throw new IOException("Injected replacement failure.");
    }
}
