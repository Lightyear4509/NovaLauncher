using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Steam;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Tests;

public sealed class SteamImportCoordinatorTests
{
    [Fact]
    public async Task PreviewDoesNotPersistAndCommitAddsWithStableIdentity()
    {
        var store = new MemoryGamesStore();
        using var library = CreateLibrary(store);
        var source = new FakeSource(Candidate(570, "Dota 2"));
        var coordinator = new SteamImportCoordinator(source, library);

        var preview = await coordinator.PreviewAsync(null, CancellationToken.None);

        Assert.Equal(1, preview.Added);
        Assert.Equal(0, store.SaveCalls);
        Assert.Empty(library.Games);

        var result = await coordinator.CommitAsync(CancellationToken.None);

        Assert.Equal(SteamImportCommitStatus.Saved, result.Status);
        Assert.Equal(1, store.SaveCalls);
        var game = Assert.Single(library.Games);
        Assert.Equal(GameId.FromSteamAppId(570), game.Id);
        Assert.Equal("570", game.SourceItemId);
        Assert.Equal("steam://run/570", game.LaunchTarget.Target);

        var secondPreview = await coordinator.PreviewAsync(null, CancellationToken.None);
        Assert.Equal(1, secondPreview.Unchanged);
        Assert.Equal(0, secondPreview.Added);
    }

    [Fact]
    public async Task ReimportPreservesManualNameFavoriteMetadataAndIdentity()
    {
        var store = new MemoryGamesStore();
        using var library = CreateLibrary(store);
        var source = new FakeSource(Candidate(10, "Original provider name"));
        var coordinator = new SteamImportCoordinator(source, library);
        await coordinator.PreviewAsync(null, CancellationToken.None);
        await coordinator.CommitAsync(CancellationToken.None);
        var id = Assert.Single(library.Games).Id;
        await library.EditManualGameAsync(
            id,
            new ManualGameDraft("My custom name", "Windows", "steam://run/10", [], null, LaunchTargetKind.Uri),
            CancellationToken.None);
        await library.ToggleFavoriteAsync(id, CancellationToken.None);
        source.Game = Candidate(10, "Renamed by provider");

        var preview = await coordinator.PreviewAsync(null, CancellationToken.None);
        var result = await coordinator.CommitAsync(CancellationToken.None);

        Assert.Equal(1, preview.Updated);
        Assert.Equal(SteamImportCommitStatus.Saved, result.Status);
        var game = Assert.Single(library.Games);
        Assert.Equal(id, game.Id);
        Assert.Equal("My custom name", game.Name);
        Assert.Equal("Renamed by provider", game.ImportedName);
        Assert.True(game.IsFavorite);
    }

    [Fact]
    public async Task PersistenceFailureAndStalePreviewNeverPublishImport()
    {
        var store = new MemoryGamesStore();
        using var library = CreateLibrary(store);
        var coordinator = new SteamImportCoordinator(new FakeSource(Candidate(20, "Game")), library);
        await coordinator.PreviewAsync(null, CancellationToken.None);
        await library.AddManualGameAsync(
            new ManualGameDraft("Manual", "Windows", "C:\\Games\\Manual.exe", [], null, LaunchTargetKind.Executable),
            CancellationToken.None);

        var stale = await coordinator.CommitAsync(CancellationToken.None);
        Assert.Equal(SteamImportCommitStatus.PreviewStale, stale.Status);
        Assert.DoesNotContain(library.Games, game => game.Source == "Steam");

        await coordinator.PreviewAsync(null, CancellationToken.None);
        store.FailSaves = true;
        var failed = await coordinator.CommitAsync(CancellationToken.None);
        Assert.Equal(SteamImportCommitStatus.PersistenceFailed, failed.Status);
        Assert.DoesNotContain(library.Games, game => game.Source == "Steam");
    }

    [Fact]
    public async Task PerItemFailuresReachPreviewAndCancellationPropagates()
    {
        using var library = CreateLibrary(new MemoryGamesStore());
        var source = new FakeSource(Candidate(30, "Game"))
        {
            Failure = new SteamImportFailure("bad.acf", "Malformed"),
        };
        var coordinator = new SteamImportCoordinator(source, library);

        var preview = await coordinator.PreviewAsync("C:\\Steam", CancellationToken.None);
        Assert.Single(preview.Failures);
        Assert.Equal("C:\\Steam", source.LastManualRoot);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            coordinator.PreviewAsync(null, cancellation.Token));
    }

    [Fact]
    public async Task TenThousandGamePreviewAndAtomicCommitRemainDeterministic()
    {
        var store = new MemoryGamesStore();
        using var library = CreateLibrary(store);
        var games = Enumerable.Range(1, 10_000)
            .Select(index => Candidate((uint)index, $"Game {index}"))
            .ToArray();
        var coordinator = new SteamImportCoordinator(new ManyGamesSource(games), library);

        var preview = await coordinator.PreviewAsync(null, CancellationToken.None);
        var result = await coordinator.CommitAsync(CancellationToken.None);

        Assert.Equal(10_000, preview.Added);
        Assert.Equal(SteamImportCommitStatus.Saved, result.Status);
        Assert.Equal(10_000, library.Games.Count);
        Assert.Equal(1, store.SaveCalls);
        Assert.Equal(GameId.FromSteamAppId(1), library.Games[0].Id);
    }

    [Fact]
    public async Task CancellationBeforeCommitPublishesNothing()
    {
        var store = new MemoryGamesStore();
        using var library = CreateLibrary(store);
        var coordinator = new SteamImportCoordinator(new FakeSource(Candidate(40, "Game")), library);
        await coordinator.PreviewAsync(null, CancellationToken.None);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => coordinator.CommitAsync(cancellation.Token));
        Assert.Empty(library.Games);
        Assert.Equal(0, store.SaveCalls);
    }

    private static LibraryCoordinator CreateLibrary(MemoryGamesStore store) =>
        new(store, new ManualGameDraftValidator(), TimeProvider.System);

    private static SteamGameCandidate Candidate(uint appId, string name) =>
        new(appId, name, name, $"C:\\Steam\\steamapps\\appmanifest_{appId}.acf", "C:\\Steam");

    private sealed class FakeSource(SteamGameCandidate game) : ISteamCatalogSource
    {
        public SteamGameCandidate Game { get; set; } = game;

        public SteamImportFailure? Failure { get; init; }

        public string? LastManualRoot { get; private set; }

        public Task<SteamCatalogScanResult> ScanAsync(string? manualSteamRoot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastManualRoot = manualSteamRoot;
            return Task.FromResult(new SteamCatalogScanResult(
                [Game],
                Failure is null ? [] : [Failure],
                [Game.LibraryRoot]));
        }
    }

    private sealed class ManyGamesSource(IReadOnlyList<SteamGameCandidate> games) : ISteamCatalogSource
    {
        public Task<SteamCatalogScanResult> ScanAsync(string? manualSteamRoot, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new SteamCatalogScanResult(games, [], ["C:\\Steam"]));
        }
    }

    private sealed class MemoryGamesStore : IDocumentStore<GamesDocument>
    {
        public int SaveCalls { get; private set; }

        public bool FailSaves { get; set; }

        public Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.NotFound, null, null));

        public Task<DocumentSaveResult> SaveAsync(GamesDocument document, CancellationToken cancellationToken)
        {
            SaveCalls++;
            return Task.FromResult(FailSaves
                ? new DocumentSaveResult(DocumentSaveStatus.Failed, "Injected failure")
                : new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }
}
