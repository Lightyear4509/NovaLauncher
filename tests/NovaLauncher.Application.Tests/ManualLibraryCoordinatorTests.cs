using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Tests;

public sealed class ManualLibraryCoordinatorTests
{
    [Fact]
    public async Task ValidAddPublishesOnlyAfterSuccessfulSave()
    {
        var store = new RecordingStore(DocumentSaveStatus.Saved);
        using var coordinator = CreateCoordinator(store);

        var result = await coordinator.AddManualGameAsync(ValidDraft(), CancellationToken.None);

        Assert.Equal(LibraryMutationStatus.Saved, result.Status);
        Assert.Equal("Game", Assert.Single(coordinator.Games).Name);
        Assert.Equal("Game", Assert.Single(Assert.IsType<GamesDocument>(store.LastSaved).Games).Name);
    }

    [Fact]
    public async Task PersistenceFailureLeavesLiveLibraryUnchanged()
    {
        var store = new RecordingStore(DocumentSaveStatus.Failed);
        using var coordinator = CreateCoordinator(store);

        var result = await coordinator.AddManualGameAsync(ValidDraft(), CancellationToken.None);

        Assert.Equal(LibraryMutationStatus.PersistenceFailed, result.Status);
        Assert.Empty(coordinator.Games);
    }

    [Fact]
    public async Task InvalidDraftDoesNotCallPersistence()
    {
        var store = new RecordingStore(DocumentSaveStatus.Saved);
        using var coordinator = CreateCoordinator(store);

        var result = await coordinator.AddManualGameAsync(ValidDraft() with { Target = "relative.exe" }, CancellationToken.None);

        Assert.Equal(LibraryMutationStatus.ValidationFailed, result.Status);
        Assert.Contains("Target", result.Errors.Keys);
        Assert.Null(store.LastSaved);
    }

    [Theory]
    [InlineData("https://example.com/game")]
    [InlineData("file:///C:/Game.exe")]
    public void ValidatorRejectsUnsafeUriSchemes(string target)
    {
        var result = new ManualGameDraftValidator().Validate(
            ValidDraft() with { Target = target, TargetKind = LaunchTargetKind.Uri });

        Assert.False(result.IsValid);
        Assert.Contains("Target", result.Errors.Keys);
    }

    [Fact]
    public void ValidatorAcceptsAllowlistedUriAndRejectsArgumentAndPathBounds()
    {
        var validator = new ManualGameDraftValidator();

        Assert.True(validator.Validate(ValidDraft() with
        {
            Target = "steam://run/123",
            TargetKind = LaunchTargetKind.Uri,
            WorkingDirectory = null,
        }).IsValid);
        Assert.False(validator.Validate(ValidDraft() with { Name = "" }).IsValid);
        Assert.False(validator.Validate(ValidDraft() with { Platform = "" }).IsValid);
        Assert.False(validator.Validate(ValidDraft() with { Target = "C:\\Games\\script.cmd" }).IsValid);
        Assert.False(validator.Validate(ValidDraft() with { WorkingDirectory = "relative" }).IsValid);
        Assert.False(validator.Validate(ValidDraft() with { Arguments = null! }).IsValid);
        Assert.False(validator.Validate(ValidDraft() with { Arguments = Enumerable.Repeat("x", 101).ToArray() }).IsValid);
        Assert.False(validator.Validate(ValidDraft() with { Arguments = [new string('x', 4_097)] }).IsValid);
    }

    [Fact]
    public async Task ManualCoverMutationIsAtomicAndSteamItemsAreRejected()
    {
        var store = new RecordingStore(DocumentSaveStatus.Saved);
        using var coordinator = CreateCoordinator(store);
        var added = await coordinator.AddManualGameAsync(ValidDraft(), CancellationToken.None);
        var cover = new ArtworkReference(
            ArtworkKind.Cover,
            "managed-artwork:///manual-cover.png",
            new MetadataProvenance("Manual", null, DateTimeOffset.UtcNow, true),
            false);

        var saved = await coordinator.SetManualCoverAsync(added.Item!.Id, cover, CancellationToken.None);

        Assert.Equal(LibraryMutationStatus.Saved, saved.Status);
        Assert.Equal(cover, saved.Item!.Artwork!.Cover);
        var steam = saved.Item with { Source = "Steam" };
        var steamStore = new SeedStore(new GamesDocument(1, [steam]));
        using var steamCoordinator = CreateCoordinator(steamStore);
        await steamCoordinator.LoadAsync(CancellationToken.None);
        var rejected = await steamCoordinator.SetManualCoverAsync(steam.Id, cover, CancellationToken.None);
        Assert.Equal(LibraryMutationStatus.PersistenceFailed, rejected.Status);
        Assert.Single(steamCoordinator.Games);
    }

    [Fact]
    public async Task AdditionalLaunchActionsAreValidatedPersistedAndRemoved()
    {
        var store = new RecordingStore(DocumentSaveStatus.Saved);
        using var coordinator = CreateCoordinator(store);
        var game = (await coordinator.AddManualGameAsync(ValidDraft(), CancellationToken.None)).Item!;
        var action = new GameLaunchAction(Guid.NewGuid(), "Configure", new("C:\\Games\\Config.exe", ["--safe mode"], "C:\\Games", LaunchTargetKind.Executable));

        var saved = await coordinator.SaveLaunchActionAsync(game.Id, action, CancellationToken.None);

        Assert.Equal(LibraryMutationStatus.Saved, saved.Status);
        Assert.Equal(action, Assert.Single(saved.Item!.LaunchActions!));
        var invalid = await coordinator.SaveLaunchActionAsync(game.Id, action with { Target = action.Target with { Target = "relative.exe" } }, CancellationToken.None);
        Assert.Equal(LibraryMutationStatus.ValidationFailed, invalid.Status);
        var removed = await coordinator.RemoveLaunchActionAsync(game.Id, action.Id, CancellationToken.None);
        Assert.Equal(LibraryMutationStatus.Saved, removed.Status);
        Assert.Empty(removed.Item!.LaunchActions!);
    }

    private static LibraryCoordinator CreateCoordinator(IDocumentStore<GamesDocument> store) =>
        new(store, new ManualGameDraftValidator(), new FixedTimeProvider());

    private static ManualGameDraft ValidDraft() => new(
        "Game",
        "Windows",
        "C:\\Games\\Game.exe",
        ["--safe"],
        "C:\\Games",
        LaunchTargetKind.Executable);

    private sealed class RecordingStore(DocumentSaveStatus saveStatus) : IDocumentStore<GamesDocument>
    {
        public GamesDocument? LastSaved { get; private set; }

        public Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.NotFound, null, null));

        public Task<DocumentSaveResult> SaveAsync(GamesDocument document, CancellationToken cancellationToken)
        {
            LastSaved = document;
            return Task.FromResult(new DocumentSaveResult(saveStatus, saveStatus == DocumentSaveStatus.Saved ? null : "Injected"));
        }
    }

    private sealed class SeedStore(GamesDocument document) : IDocumentStore<GamesDocument>
    {
        public Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.Loaded, document, null));

        public Task<DocumentSaveResult> SaveAsync(GamesDocument value, CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
    }
}
