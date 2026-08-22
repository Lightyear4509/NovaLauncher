using NovaLauncher.Application.Launching;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Steam;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Application.Achievements;
using NovaLauncher.Application.Themes;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.Library;
using NovaLauncher.Domain.SaveSync;

namespace NovaLauncher.Application.Tests;

public sealed class LibraryWorkspaceViewModelTests
{
    [Fact]
    public void NavigationExposesHonestHomeLibraryAndSavesStates()
    {
        using var fixture = new WorkspaceFixture();
        Assert.True(fixture.Workspace.IsHomePage);

        fixture.Workspace.NavigateTo("Library");
        Assert.True(fixture.Workspace.IsLibraryPage);
        fixture.Workspace.NavigateTo("Saves");
        Assert.True(fixture.Workspace.IsSavesPage);
        Assert.Contains("transfer activity", fixture.Workspace.Status, StringComparison.OrdinalIgnoreCase);
        fixture.Workspace.NavigateTo("Unknown");
        Assert.True(fixture.Workspace.IsSavesPage);
    }

    [Fact]
    public void NavigationHistoryAndCompactModeRemainDeterministic()
    {
        using var fixture = new WorkspaceFixture();
        fixture.Workspace.NavigateTo("Library");
        fixture.Workspace.NavigateTo("Saves");

        Assert.True(fixture.Workspace.CanNavigateBack);
        fixture.Workspace.NavigateBack();
        Assert.True(fixture.Workspace.IsLibraryPage);
        Assert.True(fixture.Workspace.CanNavigateForward);
        fixture.Workspace.NavigateForward();
        Assert.True(fixture.Workspace.IsSavesPage);

        fixture.Workspace.ToggleNavigation();
        Assert.True(fixture.Workspace.IsNavigationCompact);
        Assert.Equal(82, fixture.Workspace.NavigationPaneWidth);
        Assert.Equal("⌂", fixture.Workspace.HomeNavigationLabel);
    }
    private static readonly string[] PreservedArguments = ["--profile My Profile", "--fullscreen"];

    [Fact]
    public async Task FirstRunShowsEmptyStateAndValidDraftAppearsAfterSave()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);

        Assert.True(fixture.Workspace.IsEmpty);
        Assert.Contains("empty", fixture.Workspace.Status, StringComparison.OrdinalIgnoreCase);
        fixture.Workspace.Name = "Game";
        fixture.Workspace.Platform = "Windows";
        fixture.Workspace.Target = "C:\\Games\\Game.exe";
        await fixture.Workspace.SaveDraftAsync(CancellationToken.None);

        Assert.False(fixture.Workspace.IsEmpty);
        Assert.Equal("Game", Assert.Single(fixture.Workspace.Games).Name);
    }

    [Fact]
    public async Task StartupHydratesPersistedTrustedPeersForGameTransferSelection()
    {
        var peers = new[]
        {
            new TrustedSaveSyncPeer(Guid.NewGuid(), "Desktop", "100.64.0.2", "test/desktop", TrustedPeerState.Active, DateTimeOffset.UtcNow),
            new TrustedSaveSyncPeer(Guid.NewGuid(), "Laptop", "100.64.0.3", "test/laptop", TrustedPeerState.Active, DateTimeOffset.UtcNow),
        };
        using var fixture = new WorkspaceFixture(saveSync: new FakeSaveSync(peers));

        await fixture.Workspace.InitializeAsync(CancellationToken.None);

        Assert.Equal(2, fixture.Workspace.TrustedSaveSyncPeers.Count);
        Assert.All(fixture.Workspace.TrustedSaveSyncPeers, peer => Assert.Equal(TrustedPeerState.Active, peer.State));
    }

    [Fact]
    public async Task ArgumentEditorPreservesSpacesWithinEachLine()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        fixture.Workspace.Name = "Game";
        fixture.Workspace.Platform = "Windows";
        fixture.Workspace.Target = "C:\\Games\\Game.exe";
        fixture.Workspace.Arguments = "--profile My Profile\n--fullscreen";

        await fixture.Workspace.SaveDraftAsync(CancellationToken.None);

        Assert.Equal(
            PreservedArguments,
            Assert.Single(fixture.Workspace.Games).LaunchTarget.Arguments);
    }

    [Fact]
    public async Task RemovalRequiresConfirmationAndDoesNotClaimFileDeletion()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        fixture.Workspace.Name = "Game";
        fixture.Workspace.Platform = "Windows";
        fixture.Workspace.Target = "C:\\Games\\Game.exe";
        await fixture.Workspace.SaveDraftAsync(CancellationToken.None);

        await fixture.Workspace.RemoveSelectedAsync(CancellationToken.None);
        Assert.Single(fixture.Workspace.Games);
        Assert.Contains("Confirm", fixture.Workspace.RemoveButtonText, StringComparison.Ordinal);

        await fixture.Workspace.RemoveSelectedAsync(CancellationToken.None);
        Assert.Empty(fixture.Workspace.Games);
        Assert.Contains("not touched", fixture.Workspace.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RestoreRequiresValidPreviewThenExplicitConfirmation()
    {
        using var fixture = new WorkspaceFixture();
        fixture.Workspace.BackupPath = "C:\\Backups\\Nova.zip";

        await fixture.Workspace.RestoreAsync(CancellationToken.None);
        Assert.Equal(0, fixture.Backups.RestoreCalls);
        Assert.Contains("Confirm", fixture.Workspace.RestoreButtonText, StringComparison.Ordinal);

        await fixture.Workspace.RestoreAsync(CancellationToken.None);
        Assert.Equal(1, fixture.Backups.RestoreCalls);
    }

    [Fact]
    public async Task InvalidBackupPathIsReportedWithoutCallingService()
    {
        using var fixture = new WorkspaceFixture();
        fixture.Workspace.BackupPath = "relative.txt";

        await fixture.Workspace.ExportAsync(CancellationToken.None);
        await fixture.Workspace.RestoreAsync(CancellationToken.None);

        Assert.Equal(0, fixture.Backups.PreviewCalls);
        Assert.Contains("absolute", fixture.Workspace.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task FavoriteLaunchAndCollectionWorkflowsUpdateVisibleState()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        fixture.Workspace.Name = "Game";
        fixture.Workspace.Platform = "Windows";
        fixture.Workspace.Target = "C:\\Games\\Game.exe";
        await fixture.Workspace.SaveDraftAsync(CancellationToken.None);

        await fixture.Workspace.ToggleFavoriteAsync(CancellationToken.None);
        Assert.True(Assert.Single(fixture.Workspace.Games).IsFavorite);
        await fixture.Workspace.LaunchSelectedAsync(CancellationToken.None);
        Assert.Equal(1, fixture.Launcher.LaunchCalls);

        fixture.Workspace.CollectionName = "RPG";
        await fixture.Workspace.CreateCollectionAsync(CancellationToken.None);
        fixture.Workspace.SelectedCollection = Assert.Single(fixture.Workspace.Collections);
        await fixture.Workspace.ToggleCollectionMembershipAsync(CancellationToken.None);
        Assert.Single(Assert.Single(fixture.Workspace.Collections).GameIds);
        fixture.Workspace.CollectionName = "Role playing";
        await fixture.Workspace.RenameCollectionAsync(CancellationToken.None);
        Assert.Equal("Role playing", Assert.Single(fixture.Workspace.Collections).Name);
        await fixture.Workspace.DeleteCollectionAsync(CancellationToken.None);
        Assert.Single(fixture.Workspace.Collections);
        await fixture.Workspace.DeleteCollectionAsync(CancellationToken.None);
        Assert.Empty(fixture.Workspace.Collections);
    }

    [Fact]
    public async Task ExportReportsSuccessAndUnexpectedFailureBecomesStatus()
    {
        using var fixture = new WorkspaceFixture();
        fixture.Workspace.BackupPath = "C:\\Backups\\Nova.zip";

        await fixture.Workspace.ExportAsync(CancellationToken.None);
        fixture.Workspace.ReportUnexpectedFailure(new InvalidOperationException("Injected"));

        Assert.Equal(1, fixture.Backups.ExportCalls);
        Assert.Contains("Injected", fixture.Workspace.Status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommandsWithoutSelectionsAreSafeNoOps()
    {
        using var fixture = new WorkspaceFixture();

        await fixture.Workspace.RemoveSelectedAsync(CancellationToken.None);
        await fixture.Workspace.ToggleFavoriteAsync(CancellationToken.None);
        await fixture.Workspace.LaunchSelectedAsync(CancellationToken.None);
        await fixture.Workspace.RenameCollectionAsync(CancellationToken.None);
        await fixture.Workspace.DeleteCollectionAsync(CancellationToken.None);
        await fixture.Workspace.ToggleCollectionMembershipAsync(CancellationToken.None);

        Assert.Equal(0, fixture.Launcher.LaunchCalls);
        Assert.Empty(fixture.Workspace.Games);
        Assert.Empty(fixture.Workspace.Collections);
    }

    [Fact]
    public async Task InvalidRestorePreviewDoesNotReachRestore()
    {
        using var fixture = new WorkspaceFixture();
        fixture.Backups.PreviewIsValid = false;
        fixture.Workspace.BackupPath = "C:\\Backups\\Nova.zip";

        await fixture.Workspace.RestoreAsync(CancellationToken.None);

        Assert.Equal(1, fixture.Backups.PreviewCalls);
        Assert.Equal(0, fixture.Backups.RestoreCalls);
        Assert.Contains("invalid", fixture.Workspace.Status, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SearchFavoriteAndSortControlsRefreshVisibleLibrary()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        fixture.Workspace.Name = "Zeta";
        fixture.Workspace.Platform = "Windows";
        fixture.Workspace.Target = "C:\\Games\\Zeta.exe";
        await fixture.Workspace.SaveDraftAsync(CancellationToken.None);
        await fixture.Workspace.ToggleFavoriteAsync(CancellationToken.None);
        fixture.Workspace.BeginAdd();
        fixture.Workspace.Name = "Alpha";
        fixture.Workspace.Platform = "Linux";
        fixture.Workspace.Target = "C:\\Games\\Alpha.exe";
        await fixture.Workspace.SaveDraftAsync(CancellationToken.None);

        fixture.Workspace.SelectedSort = "Platform";
        Assert.Equal("Alpha", fixture.Workspace.Games[0].Name);
        fixture.Workspace.SelectedSort = "Recently updated";
        fixture.Workspace.SearchText = "Zeta";
        Assert.Equal("Zeta", Assert.Single(fixture.Workspace.Games).Name);
        fixture.Workspace.FavoritesOnly = true;
        Assert.Single(fixture.Workspace.Games);
        fixture.Workspace.SearchText = "Alpha";
        Assert.Empty(fixture.Workspace.Games);
    }

    [Fact]
    public async Task HomeDashboardExposesLocalStatisticsAndDeterministicFeaturedGame()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        fixture.Workspace.Name = "Nova One";
        fixture.Workspace.Platform = "Windows";
        fixture.Workspace.Target = "C:\\Games\\NovaOne.exe";
        await fixture.Workspace.SaveDraftAsync(CancellationToken.None);
        await fixture.Workspace.ToggleFavoriteAsync(CancellationToken.None);

        Assert.Equal(1, fixture.Workspace.LibraryGameCount);
        Assert.Equal(1, fixture.Workspace.FavoriteGameCount);
        Assert.Equal("0 minutes", fixture.Workspace.TotalLibraryPlayTime);
        Assert.True(fixture.Workspace.HasFeaturedGame);
        Assert.Equal("Nova One", fixture.Workspace.FeaturedGame!.Name);
        Assert.Empty(fixture.Workspace.ContinuePlayingGames);
        Assert.Empty(fixture.Workspace.MostPlayedGames);
    }

    [Fact]
    public async Task LibraryFiltersViewModesAndCardSizesAreDeterministic()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        fixture.Workspace.Name = "Manual Game";
        fixture.Workspace.Platform = "Windows";
        fixture.Workspace.Target = "C:\\Games\\Missing.exe";
        await fixture.Workspace.SaveDraftAsync(CancellationToken.None);
        fixture.SteamSource.Result = new SteamCatalogScanResult(
            [new SteamGameCandidate(42, "Steam Game", "steam-game", "manifest", "C:\\Steam")], [], ["C:\\Steam"]);
        await fixture.Workspace.PreviewSteamImportAsync(CancellationToken.None);
        await fixture.Workspace.CommitSteamImportAsync(CancellationToken.None);

        fixture.Workspace.SourceFilter = "Steam";
        Assert.Equal("Steam Game", Assert.Single(fixture.Workspace.Games).Name);
        fixture.Workspace.SourceFilter = "All sources";
        fixture.Workspace.AvailabilityFilter = "Missing target";
        Assert.Equal("Manual Game", Assert.Single(fixture.Workspace.Games).Name);
        Assert.True(fixture.Workspace.HasMissingTargets);

        fixture.Workspace.LibraryViewMode = "List";
        Assert.True(fixture.Workspace.IsListLibraryView);
        Assert.False(fixture.Workspace.IsGridLibraryView);
        fixture.Workspace.LibraryCardSize = "Large";
        Assert.Equal(244, fixture.Workspace.LibraryCardWidth);
        Assert.Equal(374, fixture.Workspace.LibraryCardHeight);
        Assert.Equal(286, fixture.Workspace.LibraryCoverHeight);
    }

    [Fact]
    public async Task CollectionFilterShowsOnlyMembersAndCanBeCleared()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        foreach (var name in new[] { "Member", "Outside" })
        {
            fixture.Workspace.BeginAdd();
            fixture.Workspace.Name = name;
            fixture.Workspace.Platform = "Windows";
            fixture.Workspace.Target = $"C:\\Games\\{name}.exe";
            await fixture.Workspace.SaveDraftAsync(CancellationToken.None);
        }
        fixture.Workspace.CollectionName = "Favorites shelf";
        await fixture.Workspace.CreateCollectionAsync(CancellationToken.None);
        fixture.Workspace.SelectedCollection = Assert.Single(fixture.Workspace.Collections);
        fixture.Workspace.SelectedGame = fixture.Workspace.Games.Single(game => game.Name == "Member");
        await fixture.Workspace.ToggleCollectionMembershipAsync(CancellationToken.None);

        fixture.Workspace.LibraryCollectionFilter = Assert.Single(fixture.Workspace.Collections);
        Assert.Equal("Member", Assert.Single(fixture.Workspace.Games).Name);
        fixture.Workspace.ClearLibraryCollectionFilter();
        Assert.Equal(2, fixture.Workspace.Games.Count);
    }

    [Fact]
    public async Task SmartCollectionsAreDeterministicAndIntersectWithExistingFilters()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        fixture.Workspace.Name = "Manual Favorite";
        fixture.Workspace.Platform = "Windows";
        fixture.Workspace.Target = "C:\\Games\\Favorite.exe";
        await fixture.Workspace.SaveDraftAsync(CancellationToken.None);
        await fixture.Workspace.ToggleFavoriteAsync(CancellationToken.None);
        fixture.SteamSource.Result = new SteamCatalogScanResult(
            [new SteamGameCandidate(99, "Steam Game", "steam-game", "manifest", "C:\\Steam")], [], ["C:\\Steam"]);
        await fixture.Workspace.PreviewSteamImportAsync(CancellationToken.None);
        await fixture.Workspace.CommitSteamImportAsync(CancellationToken.None);

        fixture.Workspace.SmartCollectionFilter = "Manual games";
        Assert.Equal("Manual Favorite", Assert.Single(fixture.Workspace.Games).Name);
        fixture.Workspace.SourceFilter = "Steam";
        Assert.Empty(fixture.Workspace.Games);
        fixture.Workspace.SourceFilter = "All sources";
        fixture.Workspace.SmartCollectionFilter = "Favorites";
        Assert.Equal("Manual Favorite", Assert.Single(fixture.Workspace.Games).Name);
        fixture.Workspace.ClearSmartCollectionFilter();
        Assert.Equal(2, fixture.Workspace.Games.Count);
    }

    [Fact]
    public async Task DuplicateReviewFindsSameExecutableWithoutMutatingLibrary()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        foreach (var name in new[] { "First Record", "Second Record" })
        {
            fixture.Workspace.BeginAdd();
            fixture.Workspace.Name = name;
            fixture.Workspace.Platform = "Windows";
            fixture.Workspace.Target = "C:\\Games\\Shared.exe";
            await fixture.Workspace.SaveDraftAsync(CancellationToken.None);
        }

        var duplicate = Assert.Single(fixture.Workspace.DuplicateCandidates);
        Assert.Equal("Same normalized executable target", duplicate.Reason);
        Assert.Equal(2, fixture.Workspace.LibraryGameCount);
        Assert.Equal(2, fixture.Workspace.Games.Count);
    }

    [Fact]
    public async Task BulkSelectionFavoritesAndRefreshAreBoundedExplicitActions()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        foreach (var name in new[] { "One", "Two" })
        {
            fixture.Workspace.BeginAdd();
            fixture.Workspace.Name = name;
            fixture.Workspace.Platform = "Windows";
            fixture.Workspace.Target = $"C:\\Games\\{name}.exe";
            await fixture.Workspace.SaveDraftAsync(CancellationToken.None);
        }
        foreach (var game in fixture.Workspace.Games) fixture.Workspace.SetLibraryGameSelected(game, selected: true);

        await fixture.Workspace.FavoriteSelectedGamesAsync(CancellationToken.None);
        await fixture.Workspace.RefreshSelectedGamesMetadataAsync(CancellationToken.None);

        Assert.Equal(2, fixture.Workspace.SelectedLibraryGameCount);
        Assert.All(fixture.Workspace.Games, static game => Assert.True(game.IsFavorite));
        Assert.Equal(2, fixture.Enrichment.RefreshCalls);
        fixture.Workspace.ClearLibrarySelection();
        Assert.False(fixture.Workspace.HasLibraryMultiSelection);
    }

    [Fact]
    public async Task LargeLibraryRenderingAndDuplicateReviewRemainBounded()
    {
        var now = DateTimeOffset.UtcNow;
        var games = Enumerable.Range(0, 1_000).Select(index => new LibraryItem(
            GameId.New(),
            $"Game {index:D4}",
            "Windows",
            "Manual",
            new LaunchTarget("C:\\Games\\Shared.exe", [], null, LaunchTargetKind.Executable),
            new GameMetadata(null, null, null, null, null, null),
            false,
            now,
            now)).ToArray();
        using var fixture = new WorkspaceFixture(games);

        await fixture.Workspace.InitializeAsync(CancellationToken.None);

        Assert.Equal(1_000, fixture.Workspace.Games.Count);
        Assert.Equal(200, fixture.Workspace.RenderedGames.Count);
        Assert.True(fixture.Workspace.HasMoreLibraryGames);
        fixture.Workspace.LoadMoreLibraryGames();
        Assert.Equal(400, fixture.Workspace.RenderedGames.Count);
        Assert.Equal(100, fixture.Workspace.DuplicateCandidates.Count);
        Assert.True(fixture.Workspace.DuplicateReviewTruncated);
    }

    [Fact]
    public async Task LocateGameChangesOnlyManualLibraryTargetAndRejectsSteamEntries()
    {
        using var fixture = new WorkspaceFixture();
        var replacement = Path.Combine(Path.GetTempPath(), $"NovaLauncher-Relocate-{Guid.NewGuid():N}.exe");
        await File.WriteAllBytesAsync(replacement, [0x4D, 0x5A]);
        try
        {
            await fixture.Workspace.InitializeAsync(CancellationToken.None);
            fixture.Workspace.Name = "Repair Me";
            fixture.Workspace.Platform = "Windows";
            fixture.Workspace.Target = "C:\\Games\\Old.exe";
            await fixture.Workspace.SaveDraftAsync(CancellationToken.None);

            await fixture.Workspace.RelocateSelectedManualGameAsync(replacement, CancellationToken.None);
            Assert.Equal(replacement, fixture.Workspace.SelectedGame!.LaunchTarget.Target);
            Assert.Contains("local library record", fixture.Workspace.Status, StringComparison.OrdinalIgnoreCase);

            fixture.SteamSource.Result = new SteamCatalogScanResult(
                [new SteamGameCandidate(7, "Steam Only", "steam-only", "manifest", "C:\\Steam")], [], ["C:\\Steam"]);
            await fixture.Workspace.PreviewSteamImportAsync(CancellationToken.None);
            await fixture.Workspace.CommitSteamImportAsync(CancellationToken.None);
            fixture.Workspace.SelectedGame = fixture.Workspace.Games.Single(game => game.Source == "Steam");
            await fixture.Workspace.RelocateSelectedManualGameAsync("D:\\Wrong.exe", CancellationToken.None);
            Assert.Contains("only for manually added", fixture.Workspace.Status, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(replacement);
        }
    }

    [Fact]
    public async Task SteamImportRequiresPreviewAndRefreshesLibraryOnlyAfterCommit()
    {
        using var fixture = new WorkspaceFixture();
        await fixture.Workspace.InitializeAsync(CancellationToken.None);
        fixture.SteamSource.Result = new SteamCatalogScanResult(
            [new SteamGameCandidate(570, "Dota 2", "dota 2 beta", "manifest", "C:\\Steam")],
            [new SteamImportFailure("bad.acf", "Skipped malformed manifest")],
            ["C:\\Steam"]);
        fixture.Workspace.SteamRoot = "C:\\Steam";

        await fixture.Workspace.CommitSteamImportAsync(CancellationToken.None);
        Assert.Empty(fixture.Workspace.Games);
        Assert.Contains("Preview", fixture.Workspace.Status, StringComparison.OrdinalIgnoreCase);

        await fixture.Workspace.PreviewSteamImportAsync(CancellationToken.None);
        Assert.Single(fixture.Workspace.SteamPreviewItems);
        Assert.Single(fixture.Workspace.SteamImportFailures);
        Assert.Empty(fixture.Workspace.Games);

        await fixture.Workspace.CommitSteamImportAsync(CancellationToken.None);
        Assert.Equal("Dota 2", Assert.Single(fixture.Workspace.Games).Name);
        Assert.Empty(fixture.Workspace.SteamPreviewItems);
    }

    private sealed class WorkspaceFixture : IDisposable
    {
        private readonly LibraryCoordinator _library;
        private readonly CollectionCoordinator _collections;

        public WorkspaceFixture(IReadOnlyList<LibraryItem>? initialGames = null, ISaveSyncService? saveSync = null)
        {
            _library = new LibraryCoordinator(
                new MemoryStore<GamesDocument>(initialGames is null ? null : new GamesDocument(GamesDocument.CurrentSchemaVersion, initialGames)),
                new ManualGameDraftValidator(),
                TimeProvider.System);
            _collections = new CollectionCoordinator(new MemoryStore<CollectionsDocument>(), TimeProvider.System);
            Backups = new FakeBackups();
            Launcher = new FakeLauncher();
            SteamSource = new ConfigurableSteamCatalogSource();
            Workspace = new LibraryWorkspaceViewModel(
                _library,
                _collections,
                Launcher,
                Backups,
                new SteamImportCoordinator(SteamSource, _library),
                Enrichment = new FakeEnrichment(),
                new FakeAchievements(),
                new FakeThemes(),
                saveSync: saveSync);
        }

        public LibraryWorkspaceViewModel Workspace { get; }

        public FakeBackups Backups { get; }

        public FakeLauncher Launcher { get; }

        public ConfigurableSteamCatalogSource SteamSource { get; }

        public FakeEnrichment Enrichment { get; }

        public void Dispose()
        {
            _library.Dispose();
            _collections.Dispose();
        }
    }

    public sealed class ConfigurableSteamCatalogSource : ISteamCatalogSource
    {
        public SteamCatalogScanResult Result { get; set; } = new([], [], []);

        public Task<SteamCatalogScanResult> ScanAsync(string? manualSteamRoot, CancellationToken cancellationToken) =>
            Task.FromResult(Result);
    }

    private sealed class FakeEnrichment : IGameEnrichmentService
    {
        public int RefreshCalls { get; private set; }

        public Task<ProviderRefreshResult> RefreshAsync(GameId gameId, bool forceRefresh, CancellationToken cancellationToken) =>
            Task.FromResult(Refresh(gameId));

        public Task<ArtworkVariantResult> PreviewArtworkVariantsAsync(GameId gameId, ArtworkKind kind, CancellationToken cancellationToken) =>
            Task.FromResult(new ArtworkVariantResult(ProviderResultStatus.NoData, [], [], "No fake variants."));

        public Task<ProviderRefreshResult> ApplyArtworkVariantAsync(GameId gameId, ArtworkCandidate candidate, CancellationToken cancellationToken) =>
            Task.FromResult(new ProviderRefreshResult(ProviderResultStatus.NoData, null, false, false, [], "No fake variant."));

        private ProviderRefreshResult Refresh(GameId gameId)
        {
            RefreshCalls++;
            return new ProviderRefreshResult(ProviderResultStatus.NoData, null, false, false, [], "No fake metadata.");
        }
    }

    private sealed class FakeAchievements : IAchievementService
    {
        public bool IsConfigured => false;

        public Task<AchievementRefreshResult> GetAsync(GameId gameId, bool forceRefresh, CancellationToken cancellationToken) =>
            Task.FromResult(new AchievementRefreshResult(AchievementRefreshStatus.Unavailable, null, false, "Not configured."));
    }

    private sealed class FakeThemes : IThemeService
    {
        public IReadOnlyList<ThemeOption> Themes { get; } = [new("nova-dark", "Nova Dark")];
        public string CurrentThemeId => "nova-dark";
        public bool ReduceMotion { get; private set; }
        public bool ControllerMode { get; private set; }
        public LibraryViewPreferences LibraryPreferences { get; private set; } = new("Grid", "Medium", "Name", "All sources", "All platforms", "All games", false);
        public HomeViewPreferences HomePreferences { get; private set; } = new(["Highlights", "RecentlyPlayed", "MostPlayed"], new HashSet<string>(StringComparer.Ordinal));
        public string? TailscalePeerAddress => null;
        public string UpdateChannel { get; private set; } = "Stable";
        public Task<string?> InitializeAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<string?> ApplyAsync(string themeId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<string?> ConfigureReduceMotionAsync(bool reduceMotion, CancellationToken cancellationToken)
        {
            ReduceMotion = reduceMotion;
            return Task.FromResult<string?>(null);
        }
        public Task<string?> ConfigureTailscalePeerAsync(string address, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<string?> ConfigureUpdateChannelAsync(string channel, CancellationToken cancellationToken) { UpdateChannel = channel; return Task.FromResult<string?>(null); }
        public Task<string?> SaveLibraryPreferencesAsync(LibraryViewPreferences preferences, CancellationToken cancellationToken)
        {
            LibraryPreferences = preferences;
            return Task.FromResult<string?>(null);
        }
        public Task<string?> ConfigureControllerModeAsync(bool enabled, CancellationToken cancellationToken) { ControllerMode = enabled; return Task.FromResult<string?>(null); }
        public Task<string?> SaveHomePreferencesAsync(HomeViewPreferences preferences, CancellationToken cancellationToken)
        {
            HomePreferences = preferences;
            return Task.FromResult<string?>(null);
        }
    }

    private sealed class FakeSaveSync(IReadOnlyList<TrustedSaveSyncPeer> peers) : ISaveSyncService
    {
        public event Action<SaveTransferProgress>? TransferProgressChanged { add { } remove { } }
        public SaveSyncSettings Settings { get; } = new(Guid.NewGuid(), "Local", null, null, SaveSyncSettings.DefaultPort, [], TrustedPeers: peers);
        public bool IsPaired => Settings.EffectiveTrustedPeers.Any(static peer => peer.State == TrustedPeerState.Active);
        public bool IsListening => true;
        public string ListenerStatus => "Listening";
        public Task<string?> InitializeAsync(CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string> GeneratePairingCodeAsync(CancellationToken token) => Task.FromResult("123 456");
        public Task<string?> ApplyPairingCodeAsync(string code, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RevokePeerAsync(CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RenamePeerAsync(Guid id, string name, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> SetPeerPausedAsync(Guid id, bool paused, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RevokePeerAsync(Guid id, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RotatePeerCredentialAsync(Guid id, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> ConfigurePeerAsync(string address, CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> RetryListenerAsync(CancellationToken token) => Task.FromResult<string?>(null);
        public Task<string?> CancelPartialTransfersAsync(CancellationToken token) => Task.FromResult<string?>(null);
        public (Guid? Identity, string? Error) DeriveSharedSaveIdentity(string label, string platform) => (null, null);
        public Task<int> RetryPendingUploadsAsync(CancellationToken token) => Task.FromResult(0);
        public Task<SaveSyncResult> PullBeforeLaunchAsync(LibraryItem game, CancellationToken token) => Task.FromResult(new SaveSyncResult(SaveSyncStatus.Unchanged, string.Empty));
        public Task<SaveSyncResult> SnapshotAndPushAfterExitAsync(LibraryItem game, CancellationToken token) => Task.FromResult(new SaveSyncResult(SaveSyncStatus.Unchanged, string.Empty));
        public Task<SaveSyncResult> ResolveConflictAsync(LibraryItem game, SaveConflictChoice choice, CancellationToken token) => Task.FromResult(new SaveSyncResult(SaveSyncStatus.Unchanged, string.Empty));
        public Task<IReadOnlyList<SaveConflictComparisonItem>> GetConflictComparisonAsync(LibraryItem game, CancellationToken token) => Task.FromResult<IReadOnlyList<SaveConflictComparisonItem>>([]);
        public Task<IReadOnlyList<SaveSnapshotHistoryItem>> GetSnapshotHistoryAsync(GameId id, CancellationToken token) => Task.FromResult<IReadOnlyList<SaveSnapshotHistoryItem>>([]);
        public Task<SaveSyncResult> VerifySnapshotsAsync(GameId id, CancellationToken token) => Task.FromResult(new SaveSyncResult(SaveSyncStatus.Unchanged, "Verified."));
        public Task<SaveSyncResult> RestoreSnapshotAsync(LibraryItem game, Guid id, CancellationToken token) => Task.FromResult(new SaveSyncResult(SaveSyncStatus.Unchanged, string.Empty));
        public Task<IReadOnlyList<SaveRestoreHistoryItem>> GetRestoreHistoryAsync(GameId id, CancellationToken token) => Task.FromResult<IReadOnlyList<SaveRestoreHistoryItem>>([]);
    }

    private sealed class MemoryStore<TDocument> : IDocumentStore<TDocument>
        where TDocument : class, IVersionedDocument
    {
        private TDocument? _document;

        public MemoryStore(TDocument? document = null) => _document = document;

        public Task<DocumentLoadResult<TDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(_document is null
                ? new DocumentLoadResult<TDocument>(DocumentLoadStatus.NotFound, null, null)
                : new DocumentLoadResult<TDocument>(DocumentLoadStatus.Loaded, _document, null));

        public Task<DocumentSaveResult> SaveAsync(TDocument document, CancellationToken cancellationToken)
        {
            _document = document;
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }

    public sealed class FakeLauncher : IGameLauncher
    {
        public int LaunchCalls { get; private set; }

        public Task<GameLaunchResult> LaunchAsync(LaunchTarget target, CancellationToken cancellationToken)
        {
            LaunchCalls++;
            return Task.FromResult(new GameLaunchResult(GameLaunchStatus.Started, 1, null));
        }
    }

    public sealed class FakeBackups : IBackupArchiveService
    {
        public bool PreviewIsValid { get; set; } = true;

        public int RestoreCalls { get; private set; }

        public int PreviewCalls { get; private set; }

        public int ExportCalls { get; private set; }

        public Task<BackupExportResult> ExportAsync(string destinationPath, CancellationToken cancellationToken)
        {
            ExportCalls++;
            return Task.FromResult(new BackupExportResult(true, destinationPath, null));
        }

        public Task<BackupRestorePreview> PreviewRestoreAsync(string archivePath, CancellationToken cancellationToken)
        {
            PreviewCalls++;
            return Task.FromResult(PreviewIsValid
                ? new BackupRestorePreview(true, ["games.json"], null)
                : new BackupRestorePreview(false, [], "Invalid backup."));
        }

        public Task<BackupRestoreResult> RestoreAsync(string archivePath, CancellationToken cancellationToken)
        {
            RestoreCalls++;
            return Task.FromResult(new BackupRestoreResult(true, "pre.zip", null));
        }
    }
}
