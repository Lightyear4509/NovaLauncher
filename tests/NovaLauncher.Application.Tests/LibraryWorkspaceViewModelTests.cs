using NovaLauncher.Application.Launching;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Steam;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Application.Achievements;
using NovaLauncher.Application.Themes;
using NovaLauncher.Domain.Library;

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

        public WorkspaceFixture()
        {
            _library = new LibraryCoordinator(new MemoryStore<GamesDocument>(), new ManualGameDraftValidator(), TimeProvider.System);
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
                new FakeEnrichment(),
                new FakeAchievements(),
                new FakeThemes());
        }

        public LibraryWorkspaceViewModel Workspace { get; }

        public FakeBackups Backups { get; }

        public FakeLauncher Launcher { get; }

        public ConfigurableSteamCatalogSource SteamSource { get; }

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
        public Task<ProviderRefreshResult> RefreshAsync(GameId gameId, bool forceRefresh, CancellationToken cancellationToken) =>
            Task.FromResult(new ProviderRefreshResult(ProviderResultStatus.NoData, null, false, false, [], "No fake metadata."));
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
        public string? TailscalePeerAddress => null;
        public Task<string?> InitializeAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<string?> ApplyAsync(string themeId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<string?> ConfigureTailscalePeerAsync(string address, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    }

    private sealed class MemoryStore<TDocument> : IDocumentStore<TDocument>
        where TDocument : class, IVersionedDocument
    {
        private TDocument? _document;

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
