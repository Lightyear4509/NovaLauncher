using NovaLauncher.Application.Enrichment;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Steam;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Tests;

public sealed class GameIdentityServiceTests : IDisposable
{
    private readonly LibraryCoordinator _library = new(new Store(), new ManualGameDraftValidator(), TimeProvider.System);

    [Theory]
    [InlineData("C:\\Games\\The_Witcher3.exe", "The Witcher3")]
    [InlineData("C:\\Games\\NieR-Automata.exe", "Nie R Automata")]
    public void ExecutableSuggestionIsDeterministic(string path, string expected) =>
        Assert.Equal(expected, CreateService().SuggestName(path));

    [Theory]
    [InlineData("Pokémon™  Café", "pokemoncafe")]
    [InlineData("  HALF-LIFE 2 ", "halflife2")]
    public void NormalizationHandlesUnicodeSpacingAndPunctuation(string value, string expected) =>
        Assert.Equal(expected, GameIdentityService.Normalize(value));

    [Fact]
    public async Task SearchReturnsReviewCandidatesWithoutLinkingThenConfirmationAndUndoAreExplicit()
    {
        var game = Assert.IsType<LibraryItem>((await _library.AddManualGameAsync(
            new("Half-Life 2", "Windows", "C:\\Games\\hl2.exe", [], "C:\\Games", LaunchTargetKind.Executable), CancellationToken.None)).Item);
        var provider = new FakeProvider([new("SteamGridDB", "10", "Half-Life 2", 2004, null, "candidate")]);
        var service = CreateService(provider);

        var result = await service.SearchAsync(game, "HALF LIFE 2", CancellationToken.None);

        Assert.Equal(2, result.Candidates.Count);
        Assert.Null(Assert.Single(_library.Games).LinkedIdentity);
        var steam = result.Candidates.Single(candidate => candidate.ProviderId == "Steam");
        var linked = await service.ConfirmAsync(game.Id, steam, CancellationToken.None);
        Assert.Equal("220", linked.Item!.LinkedIdentity!.SteamAppId);
        Assert.Equal("C:\\Games\\hl2.exe", linked.Item.LaunchTarget.Target);
        await service.UnlinkAsync(game.Id, CancellationToken.None);
        Assert.Null(Assert.Single(_library.Games).LinkedIdentity);
    }

    [Fact]
    public async Task IdentityCollisionAndAutomaticFirstResultSelectionAreRefused()
    {
        var first = Assert.IsType<LibraryItem>((await _library.AddManualGameAsync(Draft("First", "one.exe"), CancellationToken.None)).Item);
        var second = Assert.IsType<LibraryItem>((await _library.AddManualGameAsync(Draft("Second", "two.exe"), CancellationToken.None)).Item);
        var service = CreateService();
        var candidate = new GameIdentityCandidate("Steam", "220", "Half-Life 2", 2004, "220", "test");
        Assert.Equal(LibraryMutationStatus.Saved, (await service.ConfirmAsync(first.Id, candidate, CancellationToken.None)).Status);
        Assert.Equal(LibraryMutationStatus.PersistenceFailed, (await service.ConfirmAsync(second.Id, candidate, CancellationToken.None)).Status);
        Assert.Null(_library.Games.Single(game => game.Id == second.Id).LinkedIdentity);
    }

    private GameIdentityService CreateService(params IGameIdentitySearchProvider[] providers) =>
        new(new SteamSource(), providers, _library, TimeProvider.System);

    private static ManualGameDraft Draft(string name, string executable) =>
        new(name, "Windows", $"C:\\Games\\{executable}", [], "C:\\Games", LaunchTargetKind.Executable);

    public void Dispose() => _library.Dispose();

    private sealed class SteamSource : ISteamCatalogSource
    {
        public Task<SteamCatalogScanResult> ScanAsync(string? manualSteamRoot, CancellationToken cancellationToken) =>
            Task.FromResult(new SteamCatalogScanResult([new(220, "Half-Life 2", "Half-Life 2", "manifest", "C:\\Steam")], [], ["C:\\Steam"]));
    }

    private sealed class FakeProvider(IReadOnlyList<GameIdentityCandidate> candidates) : IGameIdentitySearchProvider
    {
        public string Id => "SteamGridDB";
        public Task<(IReadOnlyList<GameIdentityCandidate> Candidates, string? Error)> SearchAsync(string normalizedQuery, string displayQuery, CancellationToken cancellationToken) =>
            Task.FromResult((candidates, (string?)null));
    }

    private sealed class Store : IDocumentStore<GamesDocument>
    {
        public Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.NotFound, null, null));
        public Task<DocumentSaveResult> SaveAsync(GamesDocument document, CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
    }
}
