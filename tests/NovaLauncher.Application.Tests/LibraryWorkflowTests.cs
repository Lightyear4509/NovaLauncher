using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Tests;

public sealed class LibraryWorkflowTests
{
    private static readonly string[] NameSortedNames = ["Alpha", "Zeta"];
    private static readonly string[] PlatformSortedNames = ["Alpha", "Zeta"];

    [Fact]
    public async Task EditFavoriteRemoveAndQueryRemainPersistBeforePublish()
    {
        var store = new MemoryGamesStore();
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        var added = await coordinator.AddManualGameAsync(Draft("Zeta"), CancellationToken.None);
        var id = Assert.IsType<LibraryItem>(added.Item).Id;
        await coordinator.AddManualGameAsync(Draft("Alpha"), CancellationToken.None);

        Assert.Equal(LibraryMutationStatus.Saved, (await coordinator.ToggleFavoriteAsync(id, CancellationToken.None)).Status);
        Assert.Equal("Zeta", Assert.Single(coordinator.Query("zEt", favoritesOnly: true)).Name);
        Assert.Equal(
            NameSortedNames,
            coordinator.Query(null, favoritesOnly: false).Select(static game => game.Name));
        Assert.Equal(
            PlatformSortedNames,
            coordinator.Query(null, favoritesOnly: false, LibrarySort.Platform).Select(static game => game.Name));
        Assert.Equal(
            LibraryMutationStatus.Saved,
            (await coordinator.EditManualGameAsync(id, Draft("Edited"), CancellationToken.None)).Status);
        Assert.Equal("Edited", coordinator.Games.Single(game => game.Id == id).Name);
        Assert.Equal(LibraryMutationStatus.Saved, (await coordinator.RemoveAsync(id, CancellationToken.None)).Status);
        Assert.DoesNotContain(coordinator.Games, game => game.Id == id);
    }

    [Fact]
    public async Task FailedEditDoesNotPublishReplacement()
    {
        var store = new MemoryGamesStore();
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        var id = Assert.IsType<LibraryItem>(
            (await coordinator.AddManualGameAsync(Draft("Original"), CancellationToken.None)).Item).Id;
        store.FailSaves = true;

        var result = await coordinator.EditManualGameAsync(id, Draft("Changed"), CancellationToken.None);

        Assert.Equal(LibraryMutationStatus.PersistenceFailed, result.Status);
        Assert.Equal("Original", Assert.Single(coordinator.Games).Name);
    }

    [Fact]
    public async Task PlaytimeAndPerGameAdministratorPreferencePersistAtomically()
    {
        var store = new MemoryGamesStore();
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        var game = Assert.IsType<LibraryItem>(
            (await coordinator.AddManualGameAsync(Draft("Tracked"), CancellationToken.None)).Item);
        var launchedAt = DateTimeOffset.UtcNow.AddMinutes(-42);

        var elevation = await coordinator.SetRunAsAdministratorAsync(game.Id, true, CancellationToken.None);
        var playtime = await coordinator.AddPlayTimeAsync(game.Id, TimeSpan.FromMinutes(42), launchedAt, CancellationToken.None);

        Assert.Equal(LibraryMutationStatus.Saved, elevation.Status);
        Assert.Equal(LibraryMutationStatus.Saved, playtime.Status);
        var saved = Assert.Single(coordinator.Games);
        Assert.True(saved.RunAsAdministrator);
        Assert.Equal(TimeSpan.FromMinutes(42), saved.TotalPlayTime);
        Assert.Equal(launchedAt, saved.LastPlayedAtUtc);
        Assert.Equal(saved, Assert.Single(Assert.IsType<GamesDocument>(store.LastSaved).Games));
    }

    [Fact]
    public async Task CollectionMutationsPersistAndMissingCollectionFails()
    {
        var store = new MemoryCollectionsStore();
        using var coordinator = new CollectionCoordinator(store, TimeProvider.System);
        Assert.Equal(DocumentSaveStatus.Saved, (await coordinator.CreateAsync("RPG", CancellationToken.None)).Status);
        var collection = Assert.Single(coordinator.Collections);
        var gameId = GameId.New();
        Assert.Equal(
            DocumentSaveStatus.Saved,
            (await coordinator.SetMembershipAsync(collection.Id, gameId, true, CancellationToken.None)).Status);
        Assert.Equal(gameId, Assert.Single(Assert.Single(coordinator.Collections).GameIds));
        Assert.Equal(
            DocumentSaveStatus.Saved,
            (await coordinator.RenameAsync(collection.Id, "Favorites", CancellationToken.None)).Status);
        Assert.Equal(DocumentSaveStatus.Saved, (await coordinator.DeleteAsync(collection.Id, CancellationToken.None)).Status);
        Assert.Empty(coordinator.Collections);
        Assert.Equal(
            DocumentSaveStatus.Failed,
            (await coordinator.DeleteAsync(GameCollectionId.New(), CancellationToken.None)).Status);
    }

    [Fact]
    public async Task ConcurrentCollectionCreatesDoNotLoseUpdates()
    {
        var store = new MemoryCollectionsStore();
        using var coordinator = new CollectionCoordinator(store, TimeProvider.System);

        var results = await Task.WhenAll(
            coordinator.CreateAsync("One", CancellationToken.None),
            coordinator.CreateAsync("Two", CancellationToken.None));

        Assert.All(results, result => Assert.Equal(DocumentSaveStatus.Saved, result.Status));
        Assert.Equal(2, coordinator.Collections.Count);
        Assert.Equal(2, Assert.IsType<CollectionsDocument>(store.LastSaved).Collections.Count);
    }

    [Fact]
    public async Task InvalidAndMissingMutationsAreTypedAndNeverPublished()
    {
        var store = new MemoryGamesStore();
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);

        Assert.Equal(
            LibraryMutationStatus.ValidationFailed,
            (await coordinator.AddManualGameAsync(Draft(string.Empty), CancellationToken.None)).Status);
        Assert.Equal(
            LibraryMutationStatus.ValidationFailed,
            (await coordinator.EditManualGameAsync(GameId.New(), Draft(string.Empty), CancellationToken.None)).Status);
        Assert.Equal(
            LibraryMutationStatus.PersistenceFailed,
            (await coordinator.EditManualGameAsync(GameId.New(), Draft("Missing"), CancellationToken.None)).Status);
        Assert.Equal(
            LibraryMutationStatus.PersistenceFailed,
            (await coordinator.RemoveAsync(GameId.New(), CancellationToken.None)).Status);
        Assert.Equal(
            LibraryMutationStatus.PersistenceFailed,
            (await coordinator.ToggleFavoriteAsync(GameId.New(), CancellationToken.None)).Status);
        Assert.Empty(coordinator.Games);
    }

    [Fact]
    public async Task FailedAddAndRemovePreservePublishedLibrary()
    {
        var store = new MemoryGamesStore();
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        var id = Assert.IsType<LibraryItem>(
            (await coordinator.AddManualGameAsync(Draft("Original"), CancellationToken.None)).Item).Id;
        store.FailSaves = true;

        Assert.Equal(
            LibraryMutationStatus.PersistenceFailed,
            (await coordinator.AddManualGameAsync(Draft("Not added"), CancellationToken.None)).Status);
        Assert.Equal(
            LibraryMutationStatus.PersistenceFailed,
            (await coordinator.RemoveAsync(id, CancellationToken.None)).Status);
        Assert.Equal("Original", Assert.Single(coordinator.Games).Name);
    }

    [Fact]
    public async Task DuplicateMergeUsesExplicitSurvivorAndDoesNotInflatePlaytime()
    {
        var store = new MemoryGamesStore();
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        var survivor = Assert.IsType<LibraryItem>((await coordinator.AddManualGameAsync(Draft("Keep"), CancellationToken.None)).Item);
        var duplicate = Assert.IsType<LibraryItem>((await coordinator.AddManualGameAsync(Draft("Remove record"), CancellationToken.None)).Item);
        await coordinator.ToggleFavoriteAsync(duplicate.Id, CancellationToken.None);
        await coordinator.AddPlayTimeAsync(survivor.Id, TimeSpan.FromMinutes(20), DateTimeOffset.UtcNow.AddDays(-2), CancellationToken.None);
        await coordinator.AddPlayTimeAsync(duplicate.Id, TimeSpan.FromMinutes(40), DateTimeOffset.UtcNow.AddDays(-1), CancellationToken.None);

        var preview = coordinator.PreviewDuplicateMerge(survivor.Id, duplicate.Id);
        var result = await coordinator.CommitDuplicateMergeAsync(preview, CancellationToken.None);

        Assert.True(preview.CanMerge);
        Assert.Equal(LibraryMutationStatus.Saved, result.Status);
        var merged = Assert.Single(coordinator.Games);
        Assert.Equal(survivor.Id, merged.Id);
        Assert.True(merged.IsFavorite);
        Assert.Equal(TimeSpan.FromMinutes(40), merged.TotalPlayTime);
        Assert.DoesNotContain(coordinator.Games, game => game.Id == duplicate.Id);
    }

    [Fact]
    public async Task DuplicateMergeRefusesConflictingSaveIdentityAndStalePreview()
    {
        var store = new MemoryGamesStore();
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        var first = Assert.IsType<LibraryItem>((await coordinator.AddManualGameAsync(Draft("First"), CancellationToken.None)).Item);
        var second = Assert.IsType<LibraryItem>((await coordinator.AddManualGameAsync(Draft("Second"), CancellationToken.None)).Item);
        await coordinator.SetSaveSyncIdAsync(first.Id, Guid.NewGuid(), "First", CancellationToken.None);
        await coordinator.SetSaveSyncIdAsync(second.Id, Guid.NewGuid(), "Second", CancellationToken.None);
        Assert.False(coordinator.PreviewDuplicateMerge(first.Id, second.Id).CanMerge);

        await coordinator.SetSaveSyncIdAsync(second.Id, null, null, CancellationToken.None);
        var preview = coordinator.PreviewDuplicateMerge(first.Id, second.Id);
        await coordinator.ToggleFavoriteAsync(first.Id, CancellationToken.None);
        var stale = await coordinator.CommitDuplicateMergeAsync(preview, CancellationToken.None);
        Assert.Equal(LibraryMutationStatus.PersistenceFailed, stale.Status);
        Assert.Equal(2, coordinator.Games.Count);
    }

    [Fact]
    public async Task CollectionReferenceReplacementIsAtomicAndDeduplicated()
    {
        var store = new MemoryCollectionsStore();
        using var coordinator = new CollectionCoordinator(store, TimeProvider.System);
        await coordinator.CreateAsync("RPG", CancellationToken.None);
        var collection = Assert.Single(coordinator.Collections);
        var survivor = GameId.New();
        var duplicate = GameId.New();
        await coordinator.SetMembershipAsync(collection.Id, survivor, true, CancellationToken.None);
        await coordinator.SetMembershipAsync(collection.Id, duplicate, true, CancellationToken.None);

        var result = await coordinator.ReplaceGameReferenceAsync(duplicate, survivor, CancellationToken.None);

        Assert.Equal(DocumentSaveStatus.Saved, result.Status);
        Assert.Equal(survivor, Assert.Single(Assert.Single(coordinator.Collections).GameIds));
    }

    [Fact]
    public async Task ManualMetadataIsProtectedAndCanBeExplicitlyRestored()
    {
        var store = new MemoryGamesStore();
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        var game = Assert.IsType<LibraryItem>((await coordinator.AddManualGameAsync(Draft("Manual"), CancellationToken.None)).Item);

        var saved = await coordinator.SetManualMetadataAsync(
            game.Id, "My description", ["RPG"], ["Studio"], ["Publisher"], new DateOnly(2025, 1, 2), CancellationToken.None);
        var manual = Assert.IsType<LibraryItem>(saved.Item);
        Assert.True(manual.Metadata.Description!.Provenance.IsManual);
        Assert.Equal("My description", manual.Metadata.Description.Value);

        var provider = new MetadataProvenance("Provider", "1", DateTimeOffset.UtcNow, false);
        var enriched = await coordinator.ApplyEnrichmentAsync(
            game.Id,
            manual.Metadata with { Rating = new MetadataValue<decimal>(90, provider) },
            new GameArtwork(
                Placeholder(ArtworkKind.Cover), Placeholder(ArtworkKind.Hero), Placeholder(ArtworkKind.Logo), Placeholder(ArtworkKind.Background)),
            CancellationToken.None);
        Assert.Equal("My description", enriched.Item!.Metadata.Description!.Value);
        await coordinator.ClearManualMetadataProtectionAsync(game.Id, CancellationToken.None);
        var lastKnown = Assert.Single(coordinator.Games).Metadata.Description;
        Assert.NotNull(lastKnown);
        Assert.False(lastKnown.Provenance.IsManual);
        Assert.Equal("My description", lastKnown.Value);
        Assert.Equal("NovaLauncherLastKnown", lastKnown.Provenance.Source);
    }

    [Fact]
    public async Task DuplicateMergePreservesOneConfirmedIdentityAndRefusesConflicts()
    {
        var store = new MemoryGamesStore();
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        var first = Assert.IsType<LibraryItem>((await coordinator.AddManualGameAsync(Draft("First"), CancellationToken.None)).Item);
        var second = Assert.IsType<LibraryItem>((await coordinator.AddManualGameAsync(Draft("Second"), CancellationToken.None)).Item);
        var steam = new LinkedGameIdentity("Steam", "70", "Half-Life", 1998, "70", DateTimeOffset.UtcNow);
        await coordinator.SetLinkedIdentityAsync(second.Id, steam, CancellationToken.None);

        var transferable = coordinator.PreviewDuplicateMerge(first.Id, second.Id);
        Assert.True(transferable.CanMerge);
        Assert.Equal(steam, transferable.Merged!.LinkedIdentity);

        await coordinator.SetLinkedIdentityAsync(first.Id, new LinkedGameIdentity("Steam", "220", "Half-Life 2", 2004, "220", DateTimeOffset.UtcNow), CancellationToken.None);
        var conflict = coordinator.PreviewDuplicateMerge(first.Id, second.Id);
        Assert.False(conflict.CanMerge);
        Assert.Contains("different confirmed provider identities", conflict.Error, StringComparison.Ordinal);
    }

    private static ArtworkReference Placeholder(ArtworkKind kind) =>
        new(kind, $"placeholder://{kind.ToString().ToLowerInvariant()}", new MetadataProvenance("NovaLauncher", null, DateTimeOffset.UnixEpoch, false), true);

    [Theory]
    [InlineData(DocumentLoadStatus.NotFound, LibraryLoadState.Empty)]
    [InlineData(DocumentLoadStatus.Loaded, LibraryLoadState.Ready)]
    [InlineData(DocumentLoadStatus.MigratedLegacy, LibraryLoadState.Ready)]
    [InlineData(DocumentLoadStatus.RecoveredFromBackup, LibraryLoadState.Recovered)]
    [InlineData(DocumentLoadStatus.Unrecoverable, LibraryLoadState.Failed)]
    public async Task LoadMapsDocumentStatusToExplicitLibraryState(
        DocumentLoadStatus documentStatus,
        LibraryLoadState expectedState)
    {
        var game = new LibraryItem(
            GameId.New(), "Game", "Windows", "Manual", new LaunchTarget("C:\\Game.exe", [], null, LaunchTargetKind.Executable),
            new GameMetadata(null, null, null, null, null, null), false, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        var store = new MemoryGamesStore
        {
            LoadResult = new DocumentLoadResult<GamesDocument>(
                documentStatus,
                documentStatus == DocumentLoadStatus.NotFound ? null : new GamesDocument(GamesDocument.CurrentSchemaVersion, [game]),
                "warning"),
        };
        using var coordinator = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);

        await coordinator.LoadAsync(CancellationToken.None);

        Assert.Equal(expectedState, coordinator.LoadState);
        Assert.Equal("warning", coordinator.LoadWarning);
    }

    private static ManualGameDraft Draft(string name) =>
        new(name, "Windows", "C:\\Games\\Game.exe", [], "C:\\Games", LaunchTargetKind.Executable);

    private sealed class MemoryGamesStore : IDocumentStore<GamesDocument>
    {
        public bool FailSaves { get; set; }

        public GamesDocument? LastSaved { get; private set; }

        public DocumentLoadResult<GamesDocument>? LoadResult { get; init; }

        public Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(LoadResult ?? new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.NotFound, null, null));

        public Task<DocumentSaveResult> SaveAsync(GamesDocument document, CancellationToken cancellationToken)
        {
            if (!FailSaves) LastSaved = document;
            return Task.FromResult(FailSaves
                ? new DocumentSaveResult(DocumentSaveStatus.Failed, "Injected")
                : new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }

    private sealed class MemoryCollectionsStore : IDocumentStore<CollectionsDocument>
    {
        public CollectionsDocument? LastSaved { get; private set; }

        public Task<DocumentLoadResult<CollectionsDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentLoadResult<CollectionsDocument>(DocumentLoadStatus.NotFound, null, null));

        public Task<DocumentSaveResult> SaveAsync(CollectionsDocument document, CancellationToken cancellationToken)
        {
            LastSaved = document;
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }
}
