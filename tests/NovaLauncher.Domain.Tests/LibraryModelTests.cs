using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.Settings;

namespace NovaLauncher.Domain.Tests;

public sealed class LibraryModelTests
{
    [Fact]
    public void EmptyCollectionIdIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new GameCollectionId(Guid.Empty));
    }

    [Fact]
    public void LibraryModelsPreserveIdentityLaunchAndMetadataProvenance()
    {
        var gameId = GameId.New();
        var collectionId = GameCollectionId.New();
        var now = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
        var provenance = new MetadataProvenance("Manual", null, now, IsManual: true);
        var metadata = new GameMetadata(
            new MetadataValue<string>("Description", provenance),
            new MetadataValue<IReadOnlyList<string>>(["Action"], provenance),
            new MetadataValue<IReadOnlyList<string>>(["Studio"], provenance),
            new MetadataValue<IReadOnlyList<string>>(["Publisher"], provenance),
            new MetadataValue<DateOnly>(new DateOnly(2026, 8, 13), provenance),
            new MetadataValue<decimal>(90m, provenance));
        var target = new LaunchTarget("steam://run/1", ["argument"], null, LaunchTargetKind.Uri);
        var game = new LibraryItem(gameId, "Game", "Windows", "Manual", target, metadata, true, now, now);
        var collection = new GameCollection(collectionId, "Favorites", [gameId], now, now);

        Assert.Equal(gameId, game.Id);
        Assert.Equal("steam://run/1", game.LaunchTarget.Target);
        Assert.Equal(LaunchTargetKind.Uri, game.LaunchTarget.Kind);
        Assert.True(game.IsFavorite);
        Assert.True(game.Metadata.Description!.Provenance.IsManual);
        Assert.Equal("Action", Assert.Single(game.Metadata.Genres!.Value));
        Assert.Equal(collectionId, collection.Id);
        Assert.Equal(gameId, Assert.Single(collection.GameIds));
        Assert.NotEqual(GameCollectionId.New(), collection.Id);
    }

    [Fact]
    public void DefaultSettingsArePrivacyPreservingAndConfirmRemoval()
    {
        var settings = LauncherSettings.Default;

        Assert.Equal("nova-dark", settings.ThemeId);
        Assert.False(settings.ReduceMotion);
        Assert.True(settings.ConfirmBeforeRemovingLibraryItems);
    }
}
