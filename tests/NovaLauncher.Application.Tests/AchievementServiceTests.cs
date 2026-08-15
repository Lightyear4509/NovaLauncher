using NovaLauncher.Application.Achievements;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Achievements;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Tests;

public sealed class AchievementServiceTests
{
    [Fact]
    public async Task SuccessfulRefreshPersistsWithoutMutatingLibrary()
    {
        var game = Game();
        var gameStore = new Store<GamesDocument>(new GamesDocument(1, [game]));
        using var library = new LibraryCoordinator(gameStore, new ManualGameDraftValidator(), TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        var cache = new Store<AchievementsDocument>();
        using var service = new AchievementService([new Provider(game.Id)], cache, library, TimeProvider.System, "fingerprint-123456");

        var result = await service.GetAsync(game.Id, true, CancellationToken.None);

        Assert.Equal(AchievementRefreshStatus.Success, result.Status);
        Assert.Single(result.Achievements!.Items);
        Assert.NotNull(cache.Value);
        Assert.Same(game, Assert.Single(library.Games));
        Assert.Equal(0, gameStore.SaveCalls);
    }

    [Fact]
    public async Task ProviderFailureUsesStaleCacheAndAccountMismatchDoesNotLeakCache()
    {
        var game = Game();
        var stale = Snapshot(game.Id, DateTimeOffset.UtcNow.AddDays(-2));
        var gameStore = new Store<GamesDocument>(new GamesDocument(1, [game]));
        using var library = new LibraryCoordinator(gameStore, new ManualGameDraftValidator(), TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        using var service = new AchievementService(
            [new Provider(game.Id, AchievementRefreshStatus.Offline)],
            new Store<AchievementsDocument>(new AchievementsDocument(1, "fingerprint-123456", [new(stale)])),
            library, TimeProvider.System, "fingerprint-123456");

        var result = await service.GetAsync(game.Id, false, CancellationToken.None);

        Assert.True(result.UsedStaleCache);
        Assert.True(result.Achievements!.IsStale);

        using var mismatch = new AchievementService(
            [], new Store<AchievementsDocument>(new AchievementsDocument(1, "different-account", [new(stale)])),
            library, TimeProvider.System, "fingerprint-123456");
        var unavailable = await mismatch.GetAsync(game.Id, false, CancellationToken.None);
        Assert.Null(unavailable.Achievements);
    }

    [Fact]
    public async Task FailedCacheSaveDoesNotPublishNewSnapshotAndCancellationPropagates()
    {
        var game = Game();
        using var library = new LibraryCoordinator(new Store<GamesDocument>(new GamesDocument(1, [game])), new ManualGameDraftValidator(), TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        var cache = new Store<AchievementsDocument> { FailSave = true };
        using var service = new AchievementService([new Provider(game.Id)], cache, library, TimeProvider.System, "fingerprint-123456");

        var failed = await service.GetAsync(game.Id, true, CancellationToken.None);
        Assert.Equal(AchievementRefreshStatus.Failed, failed.Status);
        Assert.Null(cache.Value);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAsync(game.Id, true, cancellation.Token));
    }

    private static LibraryItem Game()
    {
        var now = DateTimeOffset.UtcNow;
        return new LibraryItem(GameId.FromSteamAppId(570), "Game", "Windows", "Steam",
            new LaunchTarget("steam://run/570", [], null, LaunchTargetKind.Uri),
            new GameMetadata(null, null, null, null, null, null), false, now, now, "570", "Game");
    }

    private static GameAchievements Snapshot(GameId id, DateTimeOffset refreshed) =>
        new(id, "Steam", [new(new AchievementId("Steam", "FIRST"), "First", "Description", true, refreshed, "Steam")], refreshed, false);

    private sealed class Provider(GameId gameId, AchievementRefreshStatus status = AchievementRefreshStatus.Success) : IAchievementProvider
    {
        public string Id => "Steam";
        public bool IsConfigured => true;
        public bool CanHandle(AchievementRequest request) => true;
        public Task<AchievementProviderResult> GetAsync(AchievementRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(status == AchievementRefreshStatus.Success
                ? new AchievementProviderResult(status, Snapshot(gameId, DateTimeOffset.UtcNow), null)
                : new AchievementProviderResult(status, null, "offline"));
        }
    }

    private sealed class Store<T>(T? initial = null) : IDocumentStore<T> where T : class, IVersionedDocument
    {
        public T? Value { get; private set; } = initial;
        public bool FailSave { get; init; }
        public int SaveCalls { get; private set; }
        public Task<DocumentLoadResult<T>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Value is null ? new(DocumentLoadStatus.NotFound, null, null) : new DocumentLoadResult<T>(DocumentLoadStatus.Loaded, Value, null));
        public Task<DocumentSaveResult> SaveAsync(T document, CancellationToken cancellationToken)
        {
            SaveCalls++;
            if (FailSave) return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "disk failure"));
            Value = document;
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }
}
