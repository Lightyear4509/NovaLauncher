using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.Settings;
using NovaLauncher.Domain.SaveSync;
using NovaLauncher.Infrastructure.Persistence;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class DocumentPolicyTests
{
    [Fact]
    public void SteamIdentityMustMatchStableGameId()
    {
        var now = DateTimeOffset.UtcNow;
        var invalid = new LibraryItem(
            GameId.New(),
            "Steam game",
            "Windows",
            "Steam",
            new LaunchTarget("steam://run/570", [], null, LaunchTargetKind.Uri),
            new GameMetadata(null, null, null, null, null, null),
            false,
            now,
            now,
            "570",
            "Steam game");

        var error = new GamesDocumentPolicy().Validate(
            new GamesDocument(GamesDocument.CurrentSchemaVersion, [invalid]));

        Assert.Contains("Steam source identity", error, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsafeArtworkLocationIsRejected()
    {
        var now = DateTimeOffset.UtcNow;
        var provenance = new MetadataProvenance("Provider", "1", now, false);
        var unsafeReference = new ArtworkReference(ArtworkKind.Cover, "file:///secret", provenance, false);
        var game = new LibraryItem(
            GameId.New(), "Game", "Windows", "Manual",
            new LaunchTarget("C:\\Game.exe", [], null, LaunchTargetKind.Executable),
            new GameMetadata(null, null, null, null, null, null), false, now, now,
            Artwork: new GameArtwork(unsafeReference, unsafeReference, unsafeReference, unsafeReference));

        var error = new GamesDocumentPolicy().Validate(new GamesDocument(1, [game]));

        Assert.Contains("Artwork locations", error, StringComparison.Ordinal);
    }

    [Fact]
    public void ManagedArtworkLocationIsAccepted()
    {
        var document = new GamesDocument(1, [BoundedGame(new GameMetadata(null, null, null, null, null, null), "managed-artwork:///generated-cover.png")]);

        Assert.Null(new GamesDocumentPolicy().Validate(document));
    }

    [Theory]
    [InlineData("managed-artwork://host/cover.png")]
    [InlineData("managed-artwork:///../cover.png")]
    [InlineData("managed-artwork:///cover.exe")]
    public void MalformedManagedArtworkLocationIsRejected(string location)
    {
        var document = new GamesDocument(1, [BoundedGame(new GameMetadata(null, null, null, null, null, null), location)]);

        Assert.NotNull(new GamesDocumentPolicy().Validate(document));
    }

    [Fact]
    public void BoundedMetadataAndArtworkAreAccepted()
    {
        var now = DateTimeOffset.UtcNow;
        var provenance = new MetadataProvenance("Provider", "1", now, false);
        var game = BoundedGame(
            new GameMetadata(
                new MetadataValue<string>("Description", provenance),
                new MetadataValue<IReadOnlyList<string>>(["Action"], provenance),
                new MetadataValue<IReadOnlyList<string>>(["Studio"], provenance),
                new MetadataValue<IReadOnlyList<string>>(["Publisher"], provenance),
                null,
                new MetadataValue<decimal>(90, provenance)),
            "https://example.test/cover.jpg");

        Assert.Null(new GamesDocumentPolicy().Validate(new GamesDocument(1, [game])));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void OutOfRangeMetadataRatingIsRejected(int rating)
    {
        var provenance = new MetadataProvenance("Provider", null, DateTimeOffset.UtcNow, false);
        var metadata = new GameMetadata(null, null, null, null, null, new MetadataValue<decimal>(rating, provenance));

        var error = new GamesDocumentPolicy().Validate(new GamesDocument(1, [BoundedGame(metadata, "placeholder://cover")]));

        Assert.Contains("Metadata description or rating", error, StringComparison.Ordinal);
    }

    [Fact]
    public void OversizedDescriptionAndInvalidMetadataListsAreRejected()
    {
        var provenance = new MetadataProvenance("Provider", null, DateTimeOffset.UtcNow, false);
        var oversized = new GameMetadata(new MetadataValue<string>(new string('x', 10_001), provenance), null, null, null, null, null);
        var invalidList = new GameMetadata(null, new MetadataValue<IReadOnlyList<string>>([string.Empty], provenance), null, null, null, null);

        Assert.Contains(
            "Metadata description",
            new GamesDocumentPolicy().Validate(new GamesDocument(1, [BoundedGame(oversized, "placeholder://cover")])),
            StringComparison.Ordinal);
        Assert.Contains(
            "Metadata list",
            new GamesDocumentPolicy().Validate(new GamesDocument(1, [BoundedGame(invalidList, "placeholder://cover")])),
            StringComparison.Ordinal);
    }

    private static LibraryItem BoundedGame(GameMetadata metadata, string artworkLocation)
    {
        var now = DateTimeOffset.UtcNow;
        var provenance = new MetadataProvenance("Provider", null, now, false);
        var reference = new ArtworkReference(ArtworkKind.Cover, artworkLocation, provenance, artworkLocation.StartsWith("placeholder", StringComparison.Ordinal));
        return new LibraryItem(
            GameId.New(), "Game", "Windows", "Manual",
            new LaunchTarget("C:\\Game.exe", [], null, LaunchTargetKind.Executable),
            metadata, false, now, now, Artwork: new GameArtwork(reference, reference, reference, reference));
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void GamesPolicyRejectsMissingAndDuplicateGames()
    {
        var policy = new GamesDocumentPolicy();
        var game = ValidGame();

        Assert.Contains("required", policy.Validate(new GamesDocument(1, null!)), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Duplicate", policy.Validate(new GamesDocument(1, [game, game])), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "Windows", "Manual")]
    [InlineData("Name", "", "Manual")]
    [InlineData("Name", "Windows", "")]
    public void GamesPolicyRejectsMissingIdentityFields(string name, string platform, string source)
    {
        var game = ValidGame() with { Name = name, Platform = platform, Source = source };

        Assert.NotNull(new GamesDocumentPolicy().Validate(new GamesDocument(1, [game])));
    }

    [Fact]
    public void GamesPolicyRejectsInvalidLaunchAndTime()
    {
        var policy = new GamesDocumentPolicy();
        var game = ValidGame();
        var noTarget = game with { LaunchTarget = new LaunchTarget("", [], null, LaunchTargetKind.Executable) };
        var reversedTime = game with { UpdatedAtUtc = game.AddedAtUtc.AddMinutes(-1) };

        Assert.Contains("launch", policy.Validate(new GamesDocument(1, [noTarget])), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("precede", policy.Validate(new GamesDocument(1, [reversedTime])), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("playtime", policy.Validate(new GamesDocument(1, [game with { TotalPlayTime = TimeSpan.FromMinutes(-1) }])), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Administrator", policy.Validate(new GamesDocument(1, [game with
        {
            LaunchTarget = new LaunchTarget("steam://run/570", [], null, LaunchTargetKind.Uri),
            RunAsAdministrator = true,
        }])), StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionsPolicyRejectsMissingDuplicateAndInvalidMembership()
    {
        var policy = new CollectionsDocumentPolicy();
        var gameId = GameId.New();
        var collection = new GameCollection(GameCollectionId.New(), "Group", [gameId], Now, Now);

        Assert.Contains("required", policy.Validate(new CollectionsDocument(1, null!)), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Duplicate", policy.Validate(new CollectionsDocument(1, [collection, collection])), StringComparison.Ordinal);
        Assert.Contains(
            "unique",
            policy.Validate(new CollectionsDocument(1, [collection with { GameIds = [gameId, gameId] }])),
            StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(policy.Validate(new CollectionsDocument(1, [collection with { Name = "" }])));
        Assert.Null(policy.Validate(new CollectionsDocument(1, [collection])));
    }

    [Fact]
    public void SettingsPolicyRejectsMissingBlankAndOversizedTheme()
    {
        var policy = new SettingsDocumentPolicy();

        Assert.Contains("require", policy.Validate(new SettingsDocument(1, null!)), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "require",
            policy.Validate(new SettingsDocument(1, LauncherSettings.Default with { ThemeId = "" })),
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "100",
            policy.Validate(new SettingsDocument(1, LauncherSettings.Default with { ThemeId = new string('x', 101) })),
            StringComparison.Ordinal);
        Assert.Contains(
            "Library view",
            policy.Validate(new SettingsDocument(1, LauncherSettings.Default with { LibraryViewMode = "downloaded-view" })),
            StringComparison.OrdinalIgnoreCase);
        Assert.Null(policy.Validate(SettingsDocument.Default));
    }

    [Fact]
    public void SaveSyncPolicyRequiresIdentityTailscalePeerAndSafeManifests()
    {
        var policy = new SaveSyncDocumentPolicy();
        var valid = SaveSyncDocument.CreateDefault();
        Assert.Null(policy.Validate(valid));
        Assert.NotNull(policy.Validate(valid with { Settings = valid.Settings with { DeviceId = Guid.Empty } }));
        Assert.NotNull(policy.Validate(valid with { Settings = valid.Settings with { PeerAddress = "192.168.1.2" } }));
        var unsafeState = new SaveSyncGameState(GameId.New(), null, [new("../escape", 1, new string('0', 64))], "Pending upload");
        Assert.NotNull(policy.Validate(valid with { Settings = valid.Settings with { Games = [unsafeState] } }));
    }

    private static LibraryItem ValidGame() => new(
        GameId.New(),
        "Game",
        "Windows",
        "Manual",
        new LaunchTarget("C:\\Game.exe", [], null, LaunchTargetKind.Executable),
        new GameMetadata(null, null, null, null, null, null),
        IsFavorite: false,
        Now,
        Now);
}
