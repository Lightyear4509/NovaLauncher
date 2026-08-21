using System.Globalization;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Tests;

public sealed class EnrichmentTests
{
    [Fact]
    public void MergerPreservesManualValuesAndUsesFirstValidProviderPerField()
    {
        var manual = new MetadataProvenance("Manual", null, DateTimeOffset.UnixEpoch, true);
        var current = new GameMetadata(new MetadataValue<string>("My description", manual), null, null, null, null, null);
        var now = DateTimeOffset.UtcNow;
        var snapshots = new[]
        {
            Snapshot("First", "Provider description", [" Action ", "action"], null, 120, now),
            Snapshot("Second", "Later", ["RPG"], ["Studio"], 90, now),
        };

        var merged = MetadataMerger.Merge(current, snapshots);

        Assert.Equal("My description", merged.Description?.Value);
        Assert.Equal("Action", Assert.Single(merged.Genres!.Value));
        Assert.Equal("Studio", Assert.Single(merged.Developers!.Value));
        Assert.Equal(90, merged.Rating?.Value);
        Assert.Equal("Second", merged.Rating?.Provenance.Source);
    }

    [Fact]
    public void CacheDistinguishesFreshStaleExpiredAndEvictsLeastRecentlyUsed()
    {
        var clock = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var cache = new ProviderCache<string>(clock, TimeSpan.FromMinutes(10), TimeSpan.FromHours(1), 2);
        cache.Set("a", "A");
        clock.Advance(TimeSpan.FromMinutes(1));
        cache.Set("b", "B");
        Assert.Equal(CacheLookupStatus.Fresh, cache.Get("a").Status);
        clock.Advance(TimeSpan.FromMinutes(10));
        Assert.Equal(CacheLookupStatus.Stale, cache.Get("a").Status);
        cache.Set("c", "C");
        Assert.Equal(CacheLookupStatus.Miss, cache.Get("b").Status);
        clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(CacheLookupStatus.Miss, cache.Get("a").Status);
    }

    [Fact]
    public async Task RefreshOrdersProvidersMergesArtworkAndPersistsOnce()
    {
        var game = SteamGame(570);
        var store = new LoadedStore(game);
        using var library = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        var providers = new IMetadataProvider[]
        {
            new FakeMetadataProvider("Later", 20, Snapshot("Later", "later", null, null, 80, DateTimeOffset.UtcNow)),
            new FakeMetadataProvider("First", 10, Snapshot("First", "first", null, null, null, DateTimeOffset.UtcNow)),
        };
        var artwork = new IArtworkProvider[] { new FakeArtworkProvider() };
        var service = new GameEnrichmentService(
            providers,
            artwork,
            new ProviderCache<MetadataSnapshot[]>(TimeProvider.System, TimeSpan.FromDays(1), TimeSpan.FromDays(2), 10),
            new ProviderCache<ArtworkCandidate[]>(TimeProvider.System, TimeSpan.FromDays(1), TimeSpan.FromDays(2), 10),
            new PassthroughMaterializer(),
            library);

        var result = await service.RefreshAsync(game.Id, false, CancellationToken.None);

        Assert.Equal(ProviderResultStatus.Success, result.Status);
        Assert.Equal("first", result.Item?.Metadata.Description?.Value);
        Assert.Equal(80, result.Item?.Metadata.Rating?.Value);
        Assert.Equal("https://example.test/cover.jpg", result.Item?.Artwork?.Cover.Location);
        Assert.True(result.Item?.Artwork?.Hero.IsPlaceholder);
        Assert.Equal(1, store.SaveCalls);

        var cached = await service.RefreshAsync(game.Id, false, CancellationToken.None);
        Assert.True(cached.UsedCache);
        Assert.Equal(2, store.SaveCalls);
    }

    [Fact]
    public async Task ProviderFailureWithoutCacheDoesNotMutateLibrary()
    {
        var game = SteamGame(10);
        var store = new LoadedStore(game);
        using var library = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        var service = new GameEnrichmentService(
            [new FakeMetadataProvider("Offline", 1, null)],
            [],
            new ProviderCache<MetadataSnapshot[]>(TimeProvider.System, TimeSpan.Zero, TimeSpan.Zero, 1),
            new ProviderCache<ArtworkCandidate[]>(TimeProvider.System, TimeSpan.Zero, TimeSpan.Zero, 1),
            new PassthroughMaterializer(),
            library);

        var result = await service.RefreshAsync(game.Id, false, CancellationToken.None);

        Assert.Equal(ProviderResultStatus.Offline, result.Status);
        Assert.Equal(0, store.SaveCalls);
        Assert.Same(game, Assert.Single(library.Games));
    }

    [Fact]
    public async Task OfflineProviderUsesStaleCacheButForceRefreshDoesNot()
    {
        var game = SteamGame(20);
        var store = new LoadedStore(game);
        using var library = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        var clock = new MutableTimeProvider(DateTimeOffset.UnixEpoch);
        var metadataCache = new ProviderCache<MetadataSnapshot[]>(clock, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), 10);
        metadataCache.Set("STEAM:20", [Snapshot("Cached", "stale value", null, null, null, clock.GetUtcNow())]);
        clock.Advance(TimeSpan.FromMinutes(2));
        var service = new GameEnrichmentService(
            [new FakeMetadataProvider("Offline", 1, null)],
            [],
            metadataCache,
            new ProviderCache<ArtworkCandidate[]>(clock, TimeSpan.FromMinutes(1), TimeSpan.FromHours(1), 10),
            new PassthroughMaterializer(),
            library);

        var stale = await service.RefreshAsync(game.Id, false, CancellationToken.None);
        var forced = await service.RefreshAsync(game.Id, true, CancellationToken.None);

        Assert.True(stale.UsedStaleCache);
        Assert.Equal("stale value", stale.Item?.Metadata.Description?.Value);
        Assert.Equal(ProviderResultStatus.Offline, forced.Status);
        Assert.False(forced.UsedStaleCache);
    }

    [Fact]
    public async Task FailedLibraryCommitRollsBackNewManagedArtwork()
    {
        var game = SteamGame(30);
        var store = new LoadedStore(game) { FailSaves = true };
        using var library = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        var materializer = new TrackingMaterializer();
        var service = new GameEnrichmentService(
            [new FakeMetadataProvider("Steam", 1, Snapshot("Steam", "value", null, null, null, DateTimeOffset.UtcNow))],
            [new FakeArtworkProvider()],
            new ProviderCache<MetadataSnapshot[]>(TimeProvider.System, TimeSpan.FromDays(1), TimeSpan.FromDays(2), 10),
            new ProviderCache<ArtworkCandidate[]>(TimeProvider.System, TimeSpan.FromDays(1), TimeSpan.FromDays(2), 10),
            materializer,
            library);

        var result = await service.RefreshAsync(game.Id, false, CancellationToken.None);

        Assert.Equal(ProviderResultStatus.Failed, result.Status);
        Assert.True(materializer.RolledBack);
        Assert.Equal("Game", Assert.Single(library.Games).Name);
    }

    [Fact]
    public async Task ArtworkVariantPreviewIsBoundedAndApplyPersistsOnlyChosenCandidate()
    {
        var game = SteamGame(40);
        var store = new LoadedStore(game);
        using var library = new LibraryCoordinator(store, new ManualGameDraftValidator(), TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        var service = new GameEnrichmentService(
            [], [new ManyArtworkProvider()],
            new ProviderCache<MetadataSnapshot[]>(TimeProvider.System, TimeSpan.FromDays(1), TimeSpan.FromDays(2), 10),
            new ProviderCache<ArtworkCandidate[]>(TimeProvider.System, TimeSpan.FromDays(1), TimeSpan.FromDays(2), 10),
            new PassthroughMaterializer(), library);

        var preview = await service.PreviewArtworkVariantsAsync(game.Id, ArtworkKind.Cover, CancellationToken.None);
        Assert.Equal(20, preview.Candidates.Count);
        Assert.Equal(0, store.SaveCalls);
        var chosen = preview.Candidates[5];
        var applied = await service.ApplyArtworkVariantAsync(game.Id, chosen, CancellationToken.None);
        Assert.Equal(ProviderResultStatus.Success, applied.Status);
        Assert.Equal(chosen.Location, applied.Item!.Artwork!.Cover.Location);
        Assert.Equal(1, store.SaveCalls);
    }

    private static MetadataSnapshot Snapshot(
        string provider,
        string? description,
        IReadOnlyList<string>? genres,
        IReadOnlyList<string>? developers,
        decimal? rating,
        DateTimeOffset now) =>
        new(provider, "570", description, genres, developers, null, null, rating, now);

    private static LibraryItem SteamGame(uint appId)
    {
        var now = DateTimeOffset.UtcNow;
        return new LibraryItem(
            GameId.FromSteamAppId(appId), "Game", "Windows", "Steam",
            new LaunchTarget($"steam://run/{appId}", [], null, LaunchTargetKind.Uri),
            new GameMetadata(null, null, null, null, null, null), false, now, now,
            appId.ToString(System.Globalization.CultureInfo.InvariantCulture), "Game");
    }

    private sealed class LoadedStore(LibraryItem game) : IDocumentStore<GamesDocument>
    {
        public int SaveCalls { get; private set; }
        public bool FailSaves { get; init; }

        public Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.Loaded, new GamesDocument(1, [game]), null));

        public Task<DocumentSaveResult> SaveAsync(GamesDocument document, CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(FailSaves
                ? new DocumentSaveResult(DocumentSaveStatus.Failed, "disk failure")
                : new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }

    private sealed class FakeMetadataProvider(string id, int priority, MetadataSnapshot? snapshot) : IMetadataProvider
    {
        public string Id => id;
        public int Priority => priority;
        public bool CanHandle(MetadataRequest request) => true;
        public Task<MetadataProviderResult> GetMetadataAsync(MetadataRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(snapshot is null
                ? new MetadataProviderResult(Id, ProviderResultStatus.Offline, null, "offline")
                : new MetadataProviderResult(Id, ProviderResultStatus.Success, snapshot, null));
    }

    private sealed class FakeArtworkProvider : IArtworkProvider
    {
        public string Id => "Art";
        public int Priority => 1;
        public bool CanHandle(MetadataRequest request) => true;
        public Task<ArtworkProviderResult> GetArtworkAsync(MetadataRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtworkProviderResult(Id, ProviderResultStatus.Success,
                [new ArtworkCandidate(ArtworkKind.Cover, "https://example.test/cover.jpg", Id, null, DateTimeOffset.UtcNow)], null));
    }

    private sealed class ManyArtworkProvider : IArtworkProvider
    {
        public string Id => "Variants";
        public int Priority => 1;
        public bool CanHandle(MetadataRequest request) => true;
        public Task<ArtworkProviderResult> GetArtworkAsync(MetadataRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtworkProviderResult(Id, ProviderResultStatus.Success,
                Enumerable.Range(0, 25).Select(index => new ArtworkCandidate(ArtworkKind.Cover, $"https://example.test/{index}.png", Id, index.ToString(CultureInfo.InvariantCulture), DateTimeOffset.UtcNow)).ToArray(), null));
    }

    private sealed class PassthroughMaterializer : IArtworkMaterializer
    {
        public Task<ArtworkMaterializationResult> MaterializeAsync(GameId gameId, IReadOnlyList<ArtworkCandidate> candidates, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtworkMaterializationResult(candidates, [], []));

        public Task RollbackAsync(IReadOnlyList<string> createdFiles, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task CleanupObsoleteAsync(GameArtwork? previous, GameArtwork current, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TrackingMaterializer : IArtworkMaterializer
    {
        public bool RolledBack { get; private set; }

        public Task<ArtworkMaterializationResult> MaterializeAsync(GameId gameId, IReadOnlyList<ArtworkCandidate> candidates, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtworkMaterializationResult(
                candidates.Select(static item => item with { Location = "managed-artwork:///new.png" }).ToArray(),
                ["new.png"], []));

        public Task RollbackAsync(IReadOnlyList<string> createdFiles, CancellationToken cancellationToken)
        {
            RolledBack = true;
            return Task.CompletedTask;
        }

        public Task CleanupObsoleteAsync(GameArtwork? previous, GameArtwork current, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class MutableTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan amount) => _now += amount;
    }
}
