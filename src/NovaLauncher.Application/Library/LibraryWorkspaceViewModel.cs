using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NovaLauncher.Application.Launching;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Steam;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Domain.Library;
using NovaLauncher.Application.Achievements;
using NovaLauncher.Domain.Achievements;
using NovaLauncher.Application.Themes;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.SaveSync;
using NovaLauncher.Application.GameTransfer;
using NovaLauncher.Application.Lifecycle;

namespace NovaLauncher.Application.Library;

public sealed record SaveSyncActivityItem(string GameName, string Status, string SnapshotId, int FileCount);

public sealed record DuplicateReviewItem(LibraryItem Primary, LibraryItem Candidate, string Reason);

public sealed class LibraryWorkspaceViewModel(
    LibraryCoordinator library,
    CollectionCoordinator collections,
    IGameLauncher launcher,
    IBackupArchiveService backups,
    SteamImportCoordinator steamImport,
    IGameEnrichmentService enrichment,
    IAchievementService achievements,
    IThemeService themes,
    IApiKeySession? apiKeys = null,
    IManualCoverService? manualCovers = null,
    ISaveSyncService? saveSync = null,
    IGameIdentityService? identityService = null,
    IGameTransferService? gameTransfers = null,
    IUpdateService? updates = null,
    IDiagnosticExportService? diagnostics = null,
    CrashRecoveryState? crashRecovery = null,
    IUpdateRecoveryService? updateRecovery = null) : INotifyPropertyChanged
{
    private LibraryItem? _selectedGame;
    private GameCollection? _selectedCollection;
    private string _searchText = string.Empty;
    private string _name = string.Empty;
    private string _platform = "Windows";
    private string _target = string.Empty;
    private string _arguments = string.Empty;
    private string _workingDirectory = string.Empty;
    private string _collectionName = string.Empty;
    private string _backupPath = string.Empty;
    private string _steamRoot = string.Empty;
    private string _status = "Loading library…";
    private bool _favoritesOnly;
    private bool _isBusy;
    private bool _confirmRemoval;
    private bool _confirmRestore;
    private bool _confirmCollectionDelete;
    private bool _confirmDuplicateMerge;
    private bool _isEditorVisible;
    private string _selectedSort = "Name";
    private string _sourceFilter = "All sources";
    private string _platformFilter = "All platforms";
    private string _availabilityFilter = "All games";
    private string _libraryViewMode = "Grid";
    private string _libraryCardSize = "Medium";
    private GameCollection? _libraryCollectionFilter;
    private string _smartCollectionFilter = "All games";
    private bool _suppressLibraryPreferenceSave;
    private string _currentPage = "Home";
    private string _selectedThemeId = themes.CurrentThemeId;
    private bool _reduceMotion = themes.ReduceMotion;
    private string _steamGridDbApiKey = string.Empty;
    private string _tailscalePeerAddress = themes.TailscalePeerAddress ?? string.Empty;
    private string _pairingCode = string.Empty;
    private string _saveSyncLinkCode = string.Empty;
    private string _saveSyncLabel = string.Empty;
    private string _activeSaveTransfer = "No save transfer is currently running.";
    private bool _isSaveTransferActive;
    private bool _showInitialSaveUploadPrompt;
    private bool _isNavigationCompact;
    private readonly Stack<string> _backHistory = new();
    private readonly Stack<string> _forwardHistory = new();
    private string _saveSyncPairingFeedback = "Enter and save the other device's Tailscale IP before pairing.";
    private string _identitySearchText = string.Empty;
    private string _manualDescription = string.Empty;
    private string _manualGenres = string.Empty;
    private string _manualDevelopers = string.Empty;
    private string _manualPublishers = string.Empty;
    private string _manualReleaseDate = string.Empty;
    private string _selectedArtworkKind = "Cover";
    private double _artworkCropX;
    private double _artworkCropY;
    private double _artworkCropWidth = 100;
    private double _artworkCropHeight = 100;
    private readonly HashSet<GameId> _selectedLibraryGameIds = [];
    private string? _pendingDuplicateMergeKey;
    private int _libraryRenderLimit = LibraryRenderPageSize;
    private const int LibraryRenderPageSize = 200;
    private const int MaximumDuplicateReviewPairs = 100;
    private bool _duplicateReviewTruncated;
    private SaveSnapshotHistoryItem? _selectedSaveSnapshot;
    private TrustedSaveSyncPeer? _selectedTrustedPeer;
    private string _trustedPeerName = string.Empty;
    private string _gameTransferSourceFolder = string.Empty;
    private string _gameTransferDestination = string.Empty;
    private bool _gameTransferRightsAttested;
    private GameTransferPreview? _gameTransferPreview;
    private PeerGameTransferOffer? _selectedGameTransferOffer;
    private string _gameTransferStatus = "No peer game transfer is active.";
    private double _gameTransferProgress;
    private bool _isGameTransferActive;
    private CancellationTokenSource? _gameTransferCancellation;
    private string _selectedUpdateChannel = themes.UpdateChannel;
    private UpdateRelease? _availableUpdate;
    private string _updateStatus = "Updates are checked only when you select Check for updates.";
    private double _updateProgress;
    private string? _stagedUpdatePath;
    private bool _confirmUpdateInstall;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LibraryItem> Games { get; } = [];

    public ObservableCollection<LibraryItem> RenderedGames { get; } = [];

    public ObservableCollection<GameCollection> Collections { get; } = [];

    public ObservableCollection<SteamImportPreviewItem> SteamPreviewItems { get; } = [];

    public ObservableCollection<SteamImportFailure> SteamImportFailures { get; } = [];

    public ObservableCollection<Achievement> Achievements { get; } = [];

    public ObservableCollection<LibraryItem> HomeGames { get; } = [];
    public ObservableCollection<LibraryItem> ContinuePlayingGames { get; } = [];
    public ObservableCollection<LibraryItem> MostPlayedGames { get; } = [];
    public ObservableCollection<SaveSyncActivityItem> SaveSyncActivities { get; } = [];
    public ObservableCollection<DuplicateReviewItem> DuplicateCandidates { get; } = [];
    public ObservableCollection<GameIdentityCandidate> IdentityCandidates { get; } = [];
    public ObservableCollection<string> IdentitySearchFailures { get; } = [];
    public ObservableCollection<ArtworkCandidate> ArtworkVariants { get; } = [];
    public ObservableCollection<SaveSnapshotHistoryItem> SaveSnapshotHistory { get; } = [];
    public ObservableCollection<SaveRestoreHistoryItem> SaveRestoreHistory { get; } = [];
    public ObservableCollection<TrustedSaveSyncPeer> TrustedSaveSyncPeers { get; } = [];
    public ObservableCollection<PeerGameTransferOffer> PeerGameTransferOffers { get; } = [];
    public ObservableCollection<GameTransferAuditItem> GameTransferHistory { get; } = [];

    public string GameTransferSourceFolder
    {
        get => _gameTransferSourceFolder;
        set
        {
            if (!Set(ref _gameTransferSourceFolder, value)) return;
            GameTransferPreview = null;
            OnPropertyChanged(nameof(GameTransferAuthorizationHint));
        }
    }
    public string GameTransferDestination { get => _gameTransferDestination; set => Set(ref _gameTransferDestination, value); }
    public bool GameTransferRightsAttested
    {
        get => _gameTransferRightsAttested;
        set
        {
            if (!Set(ref _gameTransferRightsAttested, value)) return;
            OnPropertyChanged(nameof(CanAuthorizeGameTransfer));
            OnPropertyChanged(nameof(GameTransferAuthorizationHint));
        }
    }
    public GameTransferPreview? GameTransferPreview
    {
        get => _gameTransferPreview;
        private set
        {
            if (!Set(ref _gameTransferPreview, value)) return;
            OnPropertyChanged(nameof(HasGameTransferPreview));
            OnPropertyChanged(nameof(CanAuthorizeGameTransfer));
            OnPropertyChanged(nameof(GameTransferAuthorizationHint));
        }
    }
    public bool HasGameTransferPreview => GameTransferPreview?.Accepted == true;
    public bool CanAuthorizeGameTransfer => HasGameTransferPreview && GameTransferRightsAttested && SelectedTrustedPeer?.State == TrustedPeerState.Active;
    public string GameTransferAuthorizationHint => !HasGameTransferPreview
        ? "Choose a folder to scan every regular file in it and its subfolders."
        : SelectedTrustedPeer?.State != TrustedPeerState.Active
            ? "Select one active trusted recipient."
            : !GameTransferRightsAttested
                ? "Confirm that you are authorized to copy this DRM-free folder."
                : "Ready to authorize this reviewed folder for 24 hours.";
    public PeerGameTransferOffer? SelectedGameTransferOffer { get => _selectedGameTransferOffer; set => Set(ref _selectedGameTransferOffer, value); }
    public string GameTransferStatus { get => _gameTransferStatus; private set => Set(ref _gameTransferStatus, value); }
    public double GameTransferProgress { get => _gameTransferProgress; private set => Set(ref _gameTransferProgress, value); }
    public bool IsGameTransferActive { get => _isGameTransferActive; private set => Set(ref _isGameTransferActive, value); }
    public IReadOnlyList<string> UpdateChannelOptions { get; } = ["Stable", "Beta", "Alpha"];
    public string SelectedUpdateChannel { get => _selectedUpdateChannel; set => Set(ref _selectedUpdateChannel, value); }
    public UpdateRelease? AvailableUpdate { get => _availableUpdate; private set { if (Set(ref _availableUpdate, value)) OnPropertyChanged(nameof(HasAvailableUpdate)); } }
    public bool HasAvailableUpdate => AvailableUpdate is not null;
    public string UpdateStatus { get => _updateStatus; private set => Set(ref _updateStatus, value); }
    public double UpdateProgress { get => _updateProgress; private set => Set(ref _updateProgress, value); }
    public bool ConfirmUpdateInstall { get => _confirmUpdateInstall; set { if (Set(ref _confirmUpdateInstall, value)) OnPropertyChanged(nameof(CanLaunchStagedUpdate)); } }
    public bool CanLaunchStagedUpdate => AvailableUpdate is not null && !string.IsNullOrWhiteSpace(_stagedUpdatePath) && ConfirmUpdateInstall;
    public string CrashRecoveryStatus => crashRecovery?.Message ?? "Crash recovery state is unavailable.";
    public string UpdateRecoveryStatus => updateRecovery?.State.Message ?? "Update rollback state is unavailable.";
    public bool CanRollbackUpdate => updateRecovery?.State.RollbackAvailable == true;

    public TrustedSaveSyncPeer? SelectedTrustedPeer
    {
        get => _selectedTrustedPeer;
        set
        {
            if (Set(ref _selectedTrustedPeer, value))
            {
                TrustedPeerName = value?.DisplayName ?? string.Empty;
                OnPropertyChanged(nameof(HasSelectedTrustedPeer));
                OnPropertyChanged(nameof(CanAuthorizeGameTransfer));
                OnPropertyChanged(nameof(GameTransferAuthorizationHint));
            }
        }
    }

    public string TrustedPeerName { get => _trustedPeerName; set => Set(ref _trustedPeerName, value); }
    public bool HasSelectedTrustedPeer => SelectedTrustedPeer is not null;

    public SaveSnapshotHistoryItem? SelectedSaveSnapshot
    {
        get => _selectedSaveSnapshot;
        set { if (Set(ref _selectedSaveSnapshot, value)) OnPropertyChanged(nameof(CanRestoreSelectedSnapshot)); }
    }

    public bool CanRestoreSelectedSnapshot => SelectedGame is not null && SelectedSaveSnapshot?.IntegrityValid == true;

    public IReadOnlyList<string> SortOptions { get; } = ["Name", "Recently played", "Date added", "Playtime", "Release date", "Platform", "Recently updated"];

    public IReadOnlyList<string> SourceFilterOptions { get; } = ["All sources", "Manual", "Steam"];

    public IReadOnlyList<string> PlatformFilterOptions { get; } = ["All platforms", "Windows", "Linux", "macOS", "Other"];

    public IReadOnlyList<string> AvailabilityFilterOptions { get; } = ["All games", "Available", "Missing target"];

    public IReadOnlyList<string> LibraryCardSizeOptions { get; } = ["Small", "Medium", "Large"];

    public IReadOnlyList<string> SmartCollectionOptions { get; } = ["All games", "Favorites", "Recently played", "Manual games", "Steam games", "Missing targets"];
    public IReadOnlyList<string> ArtworkKindOptions { get; } = ["Cover", "Hero", "Logo", "Background"];

    public IReadOnlyList<ThemeOption> ThemeOptions => themes.Themes;

    public string SelectedThemeId { get => _selectedThemeId; set => Set(ref _selectedThemeId, value); }

    public bool ReduceMotion { get => _reduceMotion; set => Set(ref _reduceMotion, value); }

    public string SteamGridDbApiKey { get => _steamGridDbApiKey; set => Set(ref _steamGridDbApiKey, value); }

    public string TailscalePeerAddress
    {
        get => _tailscalePeerAddress;
        set
        {
            if (Set(ref _tailscalePeerAddress, value)) OnPropertyChanged(nameof(CanAcceptPairingInvitation));
        }
    }

    public string PairingCode
    {
        get => _pairingCode;
        set
        {
            if (Set(ref _pairingCode, value)) OnPropertyChanged(nameof(CanAcceptPairingInvitation));
        }
    }

    public string SaveSyncLinkCode { get => _saveSyncLinkCode; set => Set(ref _saveSyncLinkCode, value); }
    public string SaveSyncLabel { get => _saveSyncLabel; set => Set(ref _saveSyncLabel, value); }

    public string SelectedSaveSyncIdentity => SelectedGame?.SaveSyncId is { } id
        ? id.ToString("D")
        : "Not linked across devices";

    public string ActiveSaveTransfer { get => _activeSaveTransfer; private set => Set(ref _activeSaveTransfer, value); }
    public bool IsSaveTransferActive { get => _isSaveTransferActive; private set => Set(ref _isSaveTransferActive, value); }
    public bool ShowInitialSaveUploadPrompt { get => _showInitialSaveUploadPrompt; private set => Set(ref _showInitialSaveUploadPrompt, value); }

    public string SaveSyncIdentity => saveSync is null ? "Unavailable" : $"{saveSync.Settings.DeviceName} · {saveSync.Settings.DeviceId:N}";

    public string SaveSyncPairingStatus => saveSync?.IsPaired == true
        ? $"{saveSync.Settings.EffectiveTrustedPeers.Count(peer => peer.State == TrustedPeerState.Active)} active trusted device(s). Future authenticated connections use independent pinned credentials."
        : saveSync?.Settings.PendingInvitationExpiresAtUtc is { } expires
            ? $"A single-use invitation is pending until {expires.LocalDateTime:g}."
            : "This device is not paired.";

    public bool IsSaveSyncPaired => saveSync?.IsPaired == true;

    public bool IsSaveSyncNotPaired => !IsSaveSyncPaired;

    public bool IsSaveSyncListening => saveSync?.IsListening == true;

    public bool IsSaveSyncNotListening => !IsSaveSyncListening;

    public string SaveSyncListenerStatus => saveSync?.ListenerStatus ?? "Save-sync transport is unavailable.";

    public bool CanAcceptPairingInvitation => saveSync is not null &&
        saveSync.Settings.EffectiveTrustedPeers.Count(peer => peer.State != TrustedPeerState.Revoked) < SaveSyncSettings.MaximumTrustedPeers &&
        !string.IsNullOrWhiteSpace(TailscalePeerAddress) &&
        PairingCode.Count(static character => char.IsAsciiDigit(character)) == 6;

    public string SaveSyncPairingFeedback
    {
        get => _saveSyncPairingFeedback;
        private set => Set(ref _saveSyncPairingFeedback, value);
    }

    public bool CanConfigureSaveFolder => string.Equals(SelectedGame?.Source, "Manual", StringComparison.OrdinalIgnoreCase);

    public string SelectedSaveDirectory => SelectedGame?.SaveDirectory ?? "No save folder selected";

    public string SteamGridDbKeyStatus => apiKeys?.HasSteamGridDbKey == true
        ? "SteamGridDB artwork access is active for this session."
        : "No SteamGridDB API key is active.";

    public async Task CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        if (updates is null) { UpdateStatus = "The official update service is unavailable."; return; }
        if (!Enum.TryParse<UpdateChannel>(SelectedUpdateChannel, out var channel)) { UpdateStatus = "Choose a valid update channel."; return; }
        var saveError = await themes.ConfigureUpdateChannelAsync(SelectedUpdateChannel, cancellationToken).ConfigureAwait(false);
        if (saveError is not null) { UpdateStatus = saveError; return; }
        var result = await updates.CheckAsync(channel, cancellationToken).ConfigureAwait(false); AvailableUpdate = result.Release; UpdateStatus = result.Message;
    }

    public async Task StageAvailableUpdateAsync(CancellationToken cancellationToken)
    {
        if (updates is null || AvailableUpdate is null) { UpdateStatus = "Check for a newer official release first."; return; }
        var progress = new Progress<double>(value => UpdateProgress = Math.Clamp(value * 100, 0, 100));
        var result = await updates.StageAsync(AvailableUpdate, progress, cancellationToken).ConfigureAwait(false);
        _stagedUpdatePath = result.StagedInstallerPath; ConfirmUpdateInstall = false; OnPropertyChanged(nameof(CanLaunchStagedUpdate)); UpdateStatus = result.Message;
    }

    public async Task LaunchStagedUpdateAsync(CancellationToken cancellationToken)
    {
        if (updates is null || AvailableUpdate is null || string.IsNullOrWhiteSpace(_stagedUpdatePath) || !ConfirmUpdateInstall) { UpdateStatus = "Verify, stage, and explicitly confirm the installer first."; return; }
        var result = await updates.LaunchStagedAsync(AvailableUpdate, _stagedUpdatePath, cancellationToken).ConfigureAwait(false); UpdateStatus = result.Message;
        if (result.Success) ConfirmUpdateInstall = false;
    }

    public async Task RollbackUpdateAsync(CancellationToken cancellationToken)
    {
        if (updateRecovery is null) { UpdateStatus = "Update rollback is unavailable."; return; }
        var result = await updateRecovery.LaunchRollbackAsync(cancellationToken).ConfigureAwait(false); UpdateStatus = result.Message;
    }

    public async Task ExportDiagnosticsAsync(string destination, CancellationToken cancellationToken)
    {
        if (diagnostics is null) { Status = "Diagnostic export is unavailable."; return; }
        var result = await diagnostics.ExportAsync(destination, cancellationToken).ConfigureAwait(false); Status = result.Message;
    }

    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (Set(ref _selectedSort, value))
            {
                RefreshGames();
                PersistLibraryPreferences();
            }
        }
    }

    public string SourceFilter { get => _sourceFilter; set { if (Set(ref _sourceFilter, value)) { RefreshGames(); PersistLibraryPreferences(); } } }

    public string PlatformFilter { get => _platformFilter; set { if (Set(ref _platformFilter, value)) { RefreshGames(); PersistLibraryPreferences(); } } }

    public string AvailabilityFilter { get => _availabilityFilter; set { if (Set(ref _availabilityFilter, value)) { RefreshGames(); PersistLibraryPreferences(); } } }

    public string LibraryViewMode
    {
        get => _libraryViewMode;
        set
        {
            if (!Set(ref _libraryViewMode, value)) return;
            OnPropertyChanged(nameof(IsGridLibraryView));
            OnPropertyChanged(nameof(IsListLibraryView));
            PersistLibraryPreferences();
        }
    }

    public bool IsGridLibraryView => LibraryViewMode == "Grid";

    public bool IsListLibraryView => LibraryViewMode == "List";

    public string LibraryCardSize
    {
        get => _libraryCardSize;
        set
        {
            if (!Set(ref _libraryCardSize, value)) return;
            OnPropertyChanged(nameof(LibraryCardWidth));
            OnPropertyChanged(nameof(LibraryCardHeight));
            OnPropertyChanged(nameof(LibraryCoverHeight));
            PersistLibraryPreferences();
        }
    }

    public double LibraryCardWidth => LibraryCardSize switch { "Small" => 158, "Large" => 244, _ => 194 };

    public double LibraryCardHeight => LibraryCardSize switch { "Small" => 270, "Large" => 374, _ => 314 };

    public double LibraryCoverHeight => LibraryCardSize switch { "Small" => 190, "Large" => 286, _ => 230 };

    public GameCollection? LibraryCollectionFilter
    {
        get => _libraryCollectionFilter;
        set { if (Set(ref _libraryCollectionFilter, value)) { OnPropertyChanged(nameof(HasLibraryCollectionFilter)); RefreshGames(); } }
    }

    public bool HasLibraryCollectionFilter => LibraryCollectionFilter is not null;

    public string SmartCollectionFilter
    {
        get => _smartCollectionFilter;
        set
        {
            if (Set(ref _smartCollectionFilter, value))
            {
                OnPropertyChanged(nameof(HasSmartCollectionFilter));
                RefreshGames();
            }
        }
    }

    public bool HasSmartCollectionFilter => SmartCollectionFilter != "All games";

    public bool HasDuplicateCandidates => DuplicateCandidates.Count > 0;

    public bool DuplicateReviewTruncated { get => _duplicateReviewTruncated; private set => Set(ref _duplicateReviewTruncated, value); }

    public int SelectedLibraryGameCount => _selectedLibraryGameIds.Count;

    public bool HasLibraryMultiSelection => SelectedLibraryGameCount > 0;

    public string BulkSelectionSummary => SelectedLibraryGameCount == 0
        ? "No games selected"
        : $"{SelectedLibraryGameCount} game(s) selected";

    public bool HasMoreLibraryGames => RenderedGames.Count < Games.Count;

    public string LoadMoreLibraryGamesText => $"Show next {Math.Min(LibraryRenderPageSize, Games.Count - RenderedGames.Count)} game(s)";

    public void ClearLibraryCollectionFilter()
    {
        LibraryCollectionFilter = null;
        OnPropertyChanged(nameof(HasLibraryCollectionFilter));
    }

    public void ClearSmartCollectionFilter() => SmartCollectionFilter = "All games";

    public bool IsLibraryGameSelected(GameId gameId) => _selectedLibraryGameIds.Contains(gameId);

    public void SetLibraryGameSelected(LibraryItem game, bool selected)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (selected) _selectedLibraryGameIds.Add(game.Id);
        else _selectedLibraryGameIds.Remove(game.Id);
        NotifyLibrarySelectionChanged();
    }

    public void ClearLibrarySelection()
    {
        _selectedLibraryGameIds.Clear();
        NotifyLibrarySelectionChanged();
    }

    public void LoadMoreLibraryGames()
    {
        _libraryRenderLimit = Math.Min(Games.Count, _libraryRenderLimit + LibraryRenderPageSize);
        RefreshRenderedGames();
    }

    public bool IsHomePage => _currentPage == "Home";

    public bool IsLibraryPage => _currentPage == "Library";

    public bool IsGameDetailsPage => _currentPage == "Details";

    public bool IsSavesPage => _currentPage == "Saves";

    public bool IsSettingsPage => _currentPage == "Settings";

    public string CurrentPage => _currentPage;

    public bool CanNavigateBack => _backHistory.Count > 0;

    public bool CanNavigateForward => _forwardHistory.Count > 0;

    public bool IsNavigationCompact
    {
        get => _isNavigationCompact;
        private set
        {
            if (!Set(ref _isNavigationCompact, value)) return;
            OnPropertyChanged(nameof(NavigationPaneWidth));
            OnPropertyChanged(nameof(IsNavigationExpanded));
            OnPropertyChanged(nameof(HomeNavigationLabel));
            OnPropertyChanged(nameof(LibraryNavigationLabel));
            OnPropertyChanged(nameof(SavesNavigationLabel));
            OnPropertyChanged(nameof(SettingsNavigationLabel));
        }
    }

    public bool IsNavigationExpanded => !IsNavigationCompact;

    public double NavigationPaneWidth => IsNavigationCompact ? 82 : 224;

    public string HomeNavigationLabel => IsNavigationCompact ? "⌂" : "⌂  Home";

    public string LibraryNavigationLabel => IsNavigationCompact ? "▦" : "▦  Library";

    public string SavesNavigationLabel => IsNavigationCompact ? "⇅" : "⇅  Saves";

    public string SettingsNavigationLabel => IsNavigationCompact ? "⚙" : "⚙  Settings";

    public void NavigateTo(string page)
    {
        NavigateCore(page, true);
    }

    public void NavigateBack()
    {
        if (_backHistory.Count == 0) return;
        _forwardHistory.Push(_currentPage);
        NavigateCore(_backHistory.Pop(), false);
    }

    public void NavigateForward()
    {
        if (_forwardHistory.Count == 0) return;
        _backHistory.Push(_currentPage);
        NavigateCore(_forwardHistory.Pop(), false);
    }

    public void ToggleNavigation() => IsNavigationCompact = !IsNavigationCompact;

    private void NavigateCore(string page, bool recordHistory)
    {
        if (page is not ("Home" or "Library" or "Details" or "Saves" or "Settings") || string.Equals(_currentPage, page, StringComparison.Ordinal)) return;
        if (recordHistory)
        {
            _backHistory.Push(_currentPage);
            _forwardHistory.Clear();
        }
        Set(ref _currentPage, page, nameof(CurrentPage));
        OnPropertyChanged(nameof(IsHomePage));
        OnPropertyChanged(nameof(IsLibraryPage));
        OnPropertyChanged(nameof(IsGameDetailsPage));
        OnPropertyChanged(nameof(IsSavesPage));
        OnPropertyChanged(nameof(IsSettingsPage));
        OnPropertyChanged(nameof(CanNavigateBack));
        OnPropertyChanged(nameof(CanNavigateForward));
        if (page == "Saves") RefreshSaveSyncActivities();
        Status = page switch
        {
            "Home" => "Home shows favorites, recent additions, and locally measured playtime.",
            "Details" => SelectedGame is null ? "Choose a game from Library." : $"Viewing {SelectedGame.Name}.",
            "Saves" => "Save-transfer activity and local backups are shown here.",
            "Settings" => "Settings and local diagnostics. Nothing is uploaded.",
            _ => $"Library contains {library.Games.Count} game(s).",
        };
    }

    public LibraryItem? SelectedGame
    {
        get => _selectedGame;
        set
        {
            if (Set(ref _selectedGame, value))
            {
                _confirmRemoval = false;
                OnPropertyChanged(nameof(RemoveButtonText));
                if (value is not null)
                {
                    Name = value.Name;
                    Platform = value.Platform;
                    Target = value.LaunchTarget.Target;
                    Arguments = string.Join(Environment.NewLine, value.LaunchTarget.Arguments);
                    WorkingDirectory = value.LaunchTarget.WorkingDirectory ?? string.Empty;
                    GameTransferSourceFolder = value.LaunchTarget.WorkingDirectory ?? Path.GetDirectoryName(value.LaunchTarget.Target) ?? string.Empty;
                    SaveSyncLabel = value.SaveSyncLabel ?? value.Name;
                    IdentitySearchText = value.Name;
                    ManualDescription = value.Metadata.Description?.Value ?? string.Empty;
                    ManualGenres = JoinEditable(value.Metadata.Genres?.Value);
                    ManualDevelopers = JoinEditable(value.Metadata.Developers?.Value);
                    ManualPublishers = JoinEditable(value.Metadata.Publishers?.Value);
                    ManualReleaseDate = value.Metadata.ReleaseDate?.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                }

                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(EditorTitle));
                Achievements.Clear();
                IdentityCandidates.Clear();
                IdentitySearchFailures.Clear();
                ArtworkVariants.Clear();
                SaveSnapshotHistory.Clear();
                GameTransferPreview = null;
                SelectedSaveSnapshot = null;
                OnPropertyChanged(nameof(HasIdentityCandidates));
                OnPropertyChanged(nameof(HasArtworkVariants));
                OnPropertyChanged(nameof(AchievementSummary));
                OnPropertyChanged(nameof(SelectedGamePlayTime));
                OnPropertyChanged(nameof(SelectedGameReleaseDate));
                OnPropertyChanged(nameof(SelectedGameDevelopers));
                OnPropertyChanged(nameof(SelectedGamePublishers));
                OnPropertyChanged(nameof(SelectedGameDescription));
                OnPropertyChanged(nameof(CanRunAsAdministrator));
                OnPropertyChanged(nameof(RunSelectedAsAdministrator));
                OnPropertyChanged(nameof(CanChangeManualCover));
                OnPropertyChanged(nameof(CanConfigureSaveFolder));
                OnPropertyChanged(nameof(SelectedSaveDirectory));
                OnPropertyChanged(nameof(SelectedSaveSyncIdentity));
                OnPropertyChanged(nameof(CanMatchManualGame));
                OnPropertyChanged(nameof(HasLinkedIdentity));
                OnPropertyChanged(nameof(CanBrowseProviderArtwork));
                OnPropertyChanged(nameof(LinkedIdentitySummary));
                OnPropertyChanged(nameof(CanRestoreSelectedSnapshot));
            }
        }
    }

    public GameCollection? SelectedCollection
    {
        get => _selectedCollection;
        set
        {
            if (Set(ref _selectedCollection, value))
            {
                _confirmCollectionDelete = false;
                OnPropertyChanged(nameof(DeleteCollectionButtonText));
                if (value is not null)
                {
                    CollectionName = value.Name;
                }

                OnPropertyChanged(nameof(HasSelectedCollection));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (Set(ref _searchText, value))
            {
                RefreshGames();
            }
        }
    }

    public bool FavoritesOnly
    {
        get => _favoritesOnly;
        set
        {
            if (Set(ref _favoritesOnly, value))
            {
                RefreshGames();
                PersistLibraryPreferences();
            }
        }
    }

    public string Name { get => _name; set => Set(ref _name, value); }

    public string Platform { get => _platform; set => Set(ref _platform, value); }

    public string Target { get => _target; set => Set(ref _target, value); }

    public string Arguments { get => _arguments; set => Set(ref _arguments, value); }

    public string WorkingDirectory { get => _workingDirectory; set => Set(ref _workingDirectory, value); }

    public string CollectionName { get => _collectionName; set => Set(ref _collectionName, value); }

    public string IdentitySearchText { get => _identitySearchText; set => Set(ref _identitySearchText, value); }

    public string ManualDescription { get => _manualDescription; set => Set(ref _manualDescription, value); }
    public string ManualGenres { get => _manualGenres; set => Set(ref _manualGenres, value); }
    public string ManualDevelopers { get => _manualDevelopers; set => Set(ref _manualDevelopers, value); }
    public string ManualPublishers { get => _manualPublishers; set => Set(ref _manualPublishers, value); }
    public string ManualReleaseDate { get => _manualReleaseDate; set => Set(ref _manualReleaseDate, value); }
    public string SelectedArtworkKind { get => _selectedArtworkKind; set => Set(ref _selectedArtworkKind, value); }
    public double ArtworkCropX { get => _artworkCropX; set => Set(ref _artworkCropX, value); }
    public double ArtworkCropY { get => _artworkCropY; set => Set(ref _artworkCropY, value); }
    public double ArtworkCropWidth { get => _artworkCropWidth; set => Set(ref _artworkCropWidth, value); }
    public double ArtworkCropHeight { get => _artworkCropHeight; set => Set(ref _artworkCropHeight, value); }

    public bool CanMatchManualGame => identityService is not null && string.Equals(SelectedGame?.Source, "Manual", StringComparison.OrdinalIgnoreCase);

    public bool HasLinkedIdentity => SelectedGame?.LinkedIdentity is not null;

    public bool CanBrowseProviderArtwork => SelectedGame is { Source: "Steam" } || HasLinkedIdentity;

    public bool HasIdentityCandidates => IdentityCandidates.Count > 0;

    public bool HasArtworkVariants => ArtworkVariants.Count > 0;

    public string LinkedIdentitySummary => SelectedGame?.LinkedIdentity is { } identity
        ? $"Confirmed {identity.ProviderId}: {identity.DisplayName} ({identity.ProviderItemId})" +
          (identity.SteamAppId is null ? string.Empty : $" · Steam App {identity.SteamAppId}")
        : "No provider identity is linked. Metadata and achievements will not be inferred from the executable name.";

    public string BackupPath
    {
        get => _backupPath;
        set
        {
            if (Set(ref _backupPath, value))
            {
                _confirmRestore = false;
                OnPropertyChanged(nameof(RestoreButtonText));
            }
        }
    }

    public string SteamRoot
    {
        get => _steamRoot;
        set
        {
            if (Set(ref _steamRoot, value))
            {
                SteamPreviewItems.Clear();
                SteamImportFailures.Clear();
                OnPropertyChanged(nameof(HasSteamPreview));
            }
        }
    }

    public string Status { get => _status; private set => Set(ref _status, value); }

    public bool IsBusy { get => _isBusy; private set => Set(ref _isBusy, value); }

    public bool HasSelection => SelectedGame is not null;

    public bool HasSelectedCollection => SelectedCollection is not null;

    public bool IsEmpty => Games.Count == 0;

    public int MissingTargetCount => library.Games.Count(static game => !IsTargetAvailable(game));

    public bool HasMissingTargets => MissingTargetCount > 0;

    public LibraryItem? FeaturedGame { get; private set; }

    public bool HasFeaturedGame => FeaturedGame is not null;

    public bool HasContinuePlayingGames => ContinuePlayingGames.Count > 0;

    public bool HasMostPlayedGames => MostPlayedGames.Count > 0;

    public int LibraryGameCount => library.Games.Count;

    public int FavoriteGameCount => library.Games.Count(static game => game.IsFavorite);

    public string TotalLibraryPlayTime => FormatPlayTime(TimeSpan.FromTicks(library.Games.Sum(static game => game.TotalPlayTime.Ticks)));

    public bool HasSteamPreview => SteamPreviewItems.Count > 0 || SteamImportFailures.Count > 0;

    public bool IsEditorVisible { get => _isEditorVisible; private set => Set(ref _isEditorVisible, value); }

    public bool CanRunAsAdministrator => SelectedGame?.LaunchTarget.Kind == LaunchTargetKind.Executable;

    public bool CanChangeManualCover => string.Equals(SelectedGame?.Source, "Manual", StringComparison.OrdinalIgnoreCase);

    public bool RunSelectedAsAdministrator
    {
        get => SelectedGame?.RunAsAdministrator == true;
        set
        {
            if (SelectedGame is null || SelectedGame.RunAsAdministrator == value) return;
            _ = SetRunAsAdministratorAsync(value);
        }
    }

    public string SelectedGamePlayTime => FormatPlayTime(SelectedGame?.TotalPlayTime ?? TimeSpan.Zero);

    public string SelectedGameDescription => SelectedGame?.Metadata.Description?.Value ?? "No description is available yet. Refresh metadata to retrieve one.";

    public string SelectedGameDevelopers => JoinMetadata(SelectedGame?.Metadata.Developers?.Value, "Unknown developer");

    public string SelectedGamePublishers => JoinMetadata(SelectedGame?.Metadata.Publishers?.Value, "Unknown publisher");

    public string SelectedGameReleaseDate => SelectedGame?.Metadata.ReleaseDate?.Value.ToString("MMMM d, yyyy", System.Globalization.CultureInfo.CurrentCulture) ?? "Unknown release date";

    public string EditorTitle => SelectedGame is null ? "Add a manual game" : "Edit selected game";

    public string RemoveButtonText => _confirmRemoval ? "Confirm NovaLauncher removal" : "Remove from NovaLauncher";

    public string RestoreButtonText => _confirmRestore ? "Confirm restore" : "Validate and preview restore";

    public string DeleteCollectionButtonText => _confirmCollectionDelete ? "Confirm collection deletion" : "Delete collection";

    public string AchievementSummary => Achievements.Count == 0
        ? achievements.IsConfigured ? "Achievements have not been refreshed." : "Steam achievements are not configured."
        : $"{Achievements.Count(static item => item.IsUnlocked)} of {Achievements.Count} unlocked";

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await RunBusyAsync(async () =>
        {
            var themeWarning = await themes.InitializeAsync(cancellationToken);
            ApplyLibraryPreferences(themes.LibraryPreferences);
            var gameLoad = await library.LoadAsync(cancellationToken);
            var collectionLoad = await collections.LoadAsync(cancellationToken);
            var syncWarning = saveSync is null ? null : await saveSync.InitializeAsync(cancellationToken);
            if (saveSync?.Settings.PeerAddress is { } peer)
            {
                TailscalePeerAddress = peer;
                OnPropertyChanged(nameof(SaveSyncIdentity));
                OnPropertyChanged(nameof(SaveSyncPairingStatus));
            }
            RefreshGames();
            RefreshCollections();
            Status = gameLoad.Status switch
            {
                DocumentLoadStatus.NotFound => "Your library is empty. Add a manually installed game to begin.",
                DocumentLoadStatus.RecoveredFromBackup => gameLoad.Warning ?? "Library recovered from backup.",
                DocumentLoadStatus.Unrecoverable or DocumentLoadStatus.UnsupportedNewerSchema => gameLoad.Warning ?? "Library could not be loaded safely.",
                _ when collectionLoad.Status == DocumentLoadStatus.Unrecoverable => collectionLoad.Warning ?? "Collections could not be loaded.",
                _ when themeWarning is not null => themeWarning,
                _ when syncWarning is not null => syncWarning,
                _ => $"Loaded {library.Games.Count} game(s).",
            };
        });
    }

    public async Task RelocateSelectedManualGameAsync(string executablePath, CancellationToken cancellationToken)
    {
        if (SelectedGame is not { Source: "Manual", LaunchTarget: { Kind: LaunchTargetKind.Executable } } selected)
        {
            Status = "Locate Game is available only for manually added executable games.";
            return;
        }

        if (string.IsNullOrWhiteSpace(executablePath) ||
            !Path.IsPathFullyQualified(executablePath) ||
            !string.Equals(Path.GetExtension(executablePath), ".exe", StringComparison.OrdinalIgnoreCase) ||
            !File.Exists(executablePath))
        {
            Status = "Choose an existing local .exe file to repair this game location.";
            return;
        }

        var draft = new ManualGameDraft(
            selected.Name,
            selected.Platform,
            executablePath,
            selected.LaunchTarget.Arguments,
            Path.GetDirectoryName(executablePath),
            LaunchTargetKind.Executable);
        var result = await library.EditManualGameAsync(selected.Id, draft, cancellationToken);
        if (result.Status != LibraryMutationStatus.Saved)
        {
            Status = result.Status == LibraryMutationStatus.ValidationFailed
                ? string.Join(" ", result.Errors.Values)
                : result.Error ?? "The selected executable could not be saved.";
            return;
        }

        RefreshGames();
        SelectedGame = result.Item;
        Status = "Game location repaired. NovaLauncher changed only its local library record.";
    }

    public void BeginAdd()
    {
        SelectedGame = null;
        IsEditorVisible = true;
        Name = string.Empty;
        Platform = "Windows";
        Target = string.Empty;
        Arguments = string.Empty;
        WorkingDirectory = string.Empty;
        Status = "Enter the exact executable path or an allowlisted launcher URI.";
    }

    public void BeginAddFromExecutable(string executablePath)
    {
        BeginAdd();
        Target = executablePath;
        WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty;
        Name = identityService?.SuggestName(executablePath) is { Length: > 0 } suggestion
            ? suggestion
            : Path.GetFileNameWithoutExtension(executablePath);
        NavigateTo("Library");
        Status = "Executable selected. Review the game name and save it to your library.";
    }

    public void OpenGameDetails(LibraryItem game)
    {
        IsEditorVisible = false;
        SelectedGame = game;
        NavigateTo("Details");
    }

    public async Task AddManualCoverAsync(string path, CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanChangeManualCover || manualCovers is null) return;
        var selected = SelectedGame;
        var kind = ParseArtworkKind(SelectedArtworkKind);
        var previous = GetArtwork(selected.Artwork, kind);
        var imported = await manualCovers.ImportAsync(selected.Id, kind, path, cancellationToken);
        if (imported.Cover is null)
        {
            Status = imported.Error ?? "The cover could not be imported.";
            return;
        }

        var save = await library.SetManualArtworkAsync(selected.Id, kind, imported.Cover, cancellationToken);
        if (save.Status != LibraryMutationStatus.Saved)
        {
            await manualCovers.DeleteManagedAsync(imported.Cover.Location, CancellationToken.None);
            Status = save.Error ?? "The cover record could not be saved.";
            return;
        }

        if (previous is { IsPlaceholder: false } && previous.Provenance.IsManual && previous.Location != imported.Cover.Location)
            await manualCovers.DeleteManagedAsync(previous.Location, CancellationToken.None);
        SelectedGame = save.Item;
        RefreshGames();
        Status = $"Custom {kind.ToString().ToLowerInvariant()} added. The source image was copied into NovaLauncher's bounded managed cache.";
    }

    public async Task RemoveManualCoverAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanChangeManualCover || manualCovers is null) return;
        var kind = ParseArtworkKind(SelectedArtworkKind);
        var previous = GetArtwork(SelectedGame.Artwork, kind);
        var save = await library.SetManualArtworkAsync(SelectedGame.Id, kind, artwork: null, cancellationToken);
        if (save.Status != LibraryMutationStatus.Saved)
        {
            Status = save.Error ?? "The custom cover could not be removed.";
            return;
        }
        if (previous is { IsPlaceholder: false } && previous.Provenance.IsManual)
            await manualCovers.DeleteManagedAsync(previous.Location, CancellationToken.None);
        SelectedGame = save.Item;
        RefreshGames();
        Status = $"Custom {kind.ToString().ToLowerInvariant()} removed. Installed game files were not touched.";
    }

    public async Task CropSelectedArtworkAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanChangeManualCover || manualCovers is null) return;
        var kind = ParseArtworkKind(SelectedArtworkKind);
        var previous = GetArtwork(SelectedGame.Artwork, kind);
        if (previous is null || previous.IsPlaceholder)
        {
            Status = "Add or retrieve artwork before cropping it.";
            return;
        }
        var crop = await manualCovers.CropAsync(
            SelectedGame.Id, previous,
            ArtworkCropX / 100d, ArtworkCropY / 100d,
            ArtworkCropWidth / 100d, ArtworkCropHeight / 100d,
            cancellationToken);
        if (crop.Cover is null) { Status = crop.Error ?? "The artwork crop could not be created."; return; }
        var save = await library.SetManualArtworkAsync(SelectedGame.Id, kind, crop.Cover, cancellationToken);
        if (save.Status != LibraryMutationStatus.Saved)
        {
            await manualCovers.DeleteManagedAsync(crop.Cover.Location, CancellationToken.None);
            Status = save.Error ?? "The cropped artwork could not be saved.";
            return;
        }
        if (previous.Location != crop.Cover.Location) await manualCovers.DeleteManagedAsync(previous.Location, CancellationToken.None);
        SelectedGame = save.Item;
        RefreshGames();
        Status = "Crop applied from the displayed percentages. Provider refresh now treats this artwork as a protected manual override.";
    }

    public async Task InspectArtworkCacheAsync(CancellationToken cancellationToken)
    {
        if (manualCovers is null) return;
        var result = await manualCovers.InspectCacheAsync(cancellationToken);
        Status = $"Managed artwork cache: {result.FileCount} files, {result.TotalBytes / 1024d / 1024d:0.##} MiB." +
            (result.Error is null ? string.Empty : $" {result.Error}");
    }

    public async Task CleanupArtworkCacheAsync(CancellationToken cancellationToken)
    {
        if (manualCovers is null) return;
        var retained = library.Games
            .Where(static game => game.Artwork is not null)
            .SelectMany(static game => new[] { game.Artwork!.Cover, game.Artwork.Hero, game.Artwork.Logo, game.Artwork.Background })
            .Where(static artwork => !artwork.IsPlaceholder && artwork.Location.StartsWith("managed-artwork:///", StringComparison.OrdinalIgnoreCase))
            .Select(static artwork => artwork.Location)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = await manualCovers.CleanupCacheAsync(retained, cancellationToken);
        Status = $"Managed artwork cleanup removed {result.RemovedCount} orphan files ({result.RemovedBytes / 1024d / 1024d:0.##} MiB). " +
            $"All {retained.Count} referenced assets were retained." + (result.Error is null ? string.Empty : $" {result.Error}");
    }

    public void EditSelectedGame()
    {
        if (SelectedGame is null) return;
        IsEditorVisible = true;
        NavigateTo("Library");
        Status = "Edit the selected game record. Installed files are never modified.";
    }

    public async Task SaveDraftAsync(CancellationToken cancellationToken)
    {
        await RunBusyAsync(async () =>
        {
            var targetKind = Uri.TryCreate(Target, UriKind.Absolute, out var uri) && !uri.IsFile
                ? LaunchTargetKind.Uri
                : LaunchTargetKind.Executable;
            var draft = new ManualGameDraft(
                Name,
                Platform,
                Target,
                Arguments.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                WorkingDirectory,
                targetKind);
            var result = SelectedGame is null
                ? await library.AddManualGameAsync(draft, cancellationToken)
                : await library.EditManualGameAsync(SelectedGame.Id, draft, cancellationToken);
            if (result.Status == LibraryMutationStatus.Saved)
            {
                RefreshGames();
                SelectedGame = result.Item;
                IsEditorVisible = false;
                Status = "Game saved.";
            }
            else
            {
                Status = result.Status == LibraryMutationStatus.ValidationFailed
                    ? string.Join(" ", result.Errors.Values)
                    : result.Error ?? "The game could not be saved.";
            }
        });
    }

    public async Task RemoveSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null)
        {
            return;
        }

        if (!_confirmRemoval)
        {
            _confirmRemoval = true;
            Status = "This removes only the NovaLauncher record. Click confirm to continue.";
            OnPropertyChanged(nameof(RemoveButtonText));
            return;
        }

        var result = await library.RemoveAsync(SelectedGame.Id, cancellationToken);
        if (result.Status == LibraryMutationStatus.Saved)
        {
            SelectedGame = null;
            RefreshGames();
            BeginAdd();
            Status = "Removed from NovaLauncher. Installed game files were not touched.";
        }
        else
        {
            Status = result.Error ?? "Removal failed.";
        }
    }

    public async Task ToggleFavoriteAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null)
        {
            return;
        }

        var result = await library.ToggleFavoriteAsync(SelectedGame.Id, cancellationToken);
        if (result.Status == LibraryMutationStatus.Saved)
        {
            SelectedGame = result.Item;
            RefreshGames();
            Status = result.Item!.IsFavorite ? "Added to favorites." : "Removed from favorites.";
        }
    }

    public async Task LaunchSelectedAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null)
        {
            return;
        }

        var selected = SelectedGame;
        if (saveSync is not null && selected.SaveDirectory is not null && string.Equals(selected.Source, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            IsSaveTransferActive = true;
            ActiveSaveTransfer = $"Checking {selected.Name} for a newer save on the paired device…";
            var pull = await saveSync.PullBeforeLaunchAsync(selected, cancellationToken);
            IsSaveTransferActive = false;
            ActiveSaveTransfer = pull.Message;
            RefreshSaveSyncActivities();
            if (pull.Status is SaveSyncStatus.Conflict or SaveSyncStatus.Failed)
            {
                Status = pull.Message;
                return;
            }
            Status = pull.Message;
        }
        var startedAt = DateTimeOffset.UtcNow;
        var result = await launcher.LaunchAsync(selected.LaunchTarget, selected.RunAsAdministrator, cancellationToken);
        Status = result.Status == GameLaunchStatus.Started
            ? selected.RunAsAdministrator ? "Game started after Windows administrator approval." : "Game started."
            : result.Error ?? "Launch failed.";
        if (result.Status == GameLaunchStatus.Started && result.ProcessId is { } processId && selected.LaunchTarget.Kind == LaunchTargetKind.Executable)
            _ = TrackPlaySessionAsync(selected.Id, processId, startedAt, cancellationToken);
    }

    public Task RefreshSelectedMetadataAsync(CancellationToken cancellationToken) =>
        RefreshSelectedMetadataAsync(forceRefresh: false, cancellationToken);

    public Task ForceRefreshSelectedMetadataAsync(CancellationToken cancellationToken) =>
        RefreshSelectedMetadataAsync(forceRefresh: true, cancellationToken);

    public async Task SearchSelectedGameIdentityAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanMatchManualGame || identityService is null)
        {
            Status = "Select a manually added game before searching for identity matches.";
            return;
        }
        var selectedId = SelectedGame.Id;
        await RunBusyAsync(async () =>
        {
            Status = "Searching local Steam manifests and configured first-party providers…";
            var result = await identityService.SearchAsync(SelectedGame, IdentitySearchText, cancellationToken);
            if (SelectedGame?.Id != selectedId) return;
            IdentityCandidates.Clear();
            foreach (var candidate in result.Candidates) IdentityCandidates.Add(candidate);
            IdentitySearchFailures.Clear();
            foreach (var failure in result.Failures) IdentitySearchFailures.Add(failure);
            OnPropertyChanged(nameof(HasIdentityCandidates));
            Status = result.Candidates.Count == 0
                ? "No identity was selected or linked. Refine the title or keep this game unlinked."
                : $"Review {result.Candidates.Count} candidate(s). Nothing is linked or downloaded until you confirm one.";
        });
    }

    public async Task ConfirmSelectedGameIdentityAsync(GameIdentityCandidate candidate, CancellationToken cancellationToken)
    {
        if (SelectedGame is null || identityService is null) return;
        var result = await identityService.ConfirmAsync(SelectedGame.Id, candidate, cancellationToken);
        if (result.Status != LibraryMutationStatus.Saved)
        {
            Status = result.Error ?? "The identity could not be linked.";
            return;
        }
        SelectedGame = result.Item;
        RefreshGames();
        Status = $"Linked to {candidate.ProviderId} only after your confirmation. The manual executable remains the launch target.";
    }

    public void RejectIdentityCandidates()
    {
        IdentityCandidates.Clear();
        IdentitySearchFailures.Clear();
        OnPropertyChanged(nameof(HasIdentityCandidates));
        Status = "No match selected. The game remains unlinked and no provider data was downloaded.";
    }

    public async Task UnlinkSelectedGameIdentityAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || identityService is null || !HasLinkedIdentity) return;
        var result = await identityService.UnlinkAsync(SelectedGame.Id, cancellationToken);
        if (result.Status != LibraryMutationStatus.Saved)
        {
            Status = result.Error ?? "The identity could not be unlinked.";
            return;
        }
        SelectedGame = result.Item;
        RefreshGames();
        Status = "Provider identity unlinked. Existing manual overrides and downloaded artwork remain local.";
    }

    public async Task SaveManualMetadataAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanMatchManualGame) return;
        DateOnly? releaseDate = null;
        if (!string.IsNullOrWhiteSpace(ManualReleaseDate) &&
            !DateOnly.TryParseExact(ManualReleaseDate.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _))
        {
            Status = "Release date must use YYYY-MM-DD or be left blank.";
            return;
        }
        else if (!string.IsNullOrWhiteSpace(ManualReleaseDate))
            releaseDate = DateOnly.ParseExact(ManualReleaseDate.Trim(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var result = await library.SetManualMetadataAsync(
            SelectedGame.Id,
            ManualDescription,
            ParseEditableList(ManualGenres),
            ParseEditableList(ManualDevelopers),
            ParseEditableList(ManualPublishers),
            releaseDate,
            cancellationToken);
        if (result.Status != LibraryMutationStatus.Saved) { Status = result.Error ?? "Manual metadata could not be saved."; return; }
        SelectedGame = result.Item;
        RefreshGames();
        Status = "Manual metadata saved with protected provenance. Provider refresh will not overwrite these fields.";
    }

    public async Task RestoreProviderMetadataAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanMatchManualGame) return;
        var result = await library.ClearManualMetadataProtectionAsync(SelectedGame.Id, cancellationToken);
        if (result.Status != LibraryMutationStatus.Saved) { Status = result.Error ?? "Manual protection could not be removed."; return; }
        SelectedGame = result.Item;
        await RefreshSelectedMetadataAsync(forceRefresh: true, cancellationToken);
    }

    public async Task PreviewArtworkVariantsAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null) return;
        var kind = ParseArtworkKind(SelectedArtworkKind);
        var result = await enrichment.PreviewArtworkVariantsAsync(SelectedGame.Id, kind, cancellationToken);
        ArtworkVariants.Clear();
        foreach (var candidate in result.Candidates) ArtworkVariants.Add(candidate);
        OnPropertyChanged(nameof(HasArtworkVariants));
        Status = result.Error ?? $"Review {result.Candidates.Count} bounded {kind.ToString().ToLowerInvariant()} variant(s). Nothing is downloaded until you choose one.";
    }

    public async Task ApplyArtworkVariantAsync(ArtworkCandidate candidate, CancellationToken cancellationToken)
    {
        if (SelectedGame is null) return;
        var result = await enrichment.ApplyArtworkVariantAsync(SelectedGame.Id, candidate, cancellationToken);
        if (result.Status != ProviderResultStatus.Success || result.Item is null)
        {
            Status = result.Error ?? "The selected artwork variant could not be applied.";
            return;
        }
        SelectedGame = result.Item;
        RefreshGames();
        ArtworkVariants.Clear();
        OnPropertyChanged(nameof(HasArtworkVariants));
        Status = "Selected provider artwork was validated, copied to the managed cache, and applied.";
    }

    public async Task RefreshSelectedAchievementsAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null) return;
        await RunBusyAsync(async () =>
        {
            Status = "Refreshing read-only achievements…";
            var result = await achievements.GetAsync(SelectedGame.Id, forceRefresh: true, cancellationToken);
            Achievements.Clear();
            if (result.Achievements is { } snapshot)
            {
                foreach (var item in snapshot.Items.OrderByDescending(static item => item.IsUnlocked).ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
                    Achievements.Add(item);
                OnPropertyChanged(nameof(AchievementSummary));
                Status = result.UsedStaleCache
                    ? "Steam was unavailable; showing stale locally cached achievements."
                    : $"Achievements refreshed: {snapshot.UnlockedCount}/{snapshot.Items.Count} ({snapshot.CompletionPercent}%).";
                return;
            }

            OnPropertyChanged(nameof(AchievementSummary));
            Status = result.Error ?? "Achievements are unavailable.";
        });
    }

    public async Task ApplySelectedThemeAsync(CancellationToken cancellationToken)
    {
        var error = await themes.ApplyAsync(SelectedThemeId, cancellationToken);
        Status = error ?? $"Applied {themes.Themes.First(item => item.Id == SelectedThemeId).DisplayName}.";
    }

    public async Task ApplyMotionPreferenceAsync(CancellationToken cancellationToken)
    {
        var error = await themes.ConfigureReduceMotionAsync(ReduceMotion, cancellationToken);
        Status = error ?? (ReduceMotion ? "Reduced motion enabled." : "Standard interface motion enabled.");
    }

    public async Task ConfigureTailscalePeerAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null) { Status = "Save sync is unavailable."; return; }
        var error = await saveSync.ConfigurePeerAsync(TailscalePeerAddress, cancellationToken);
        SaveSyncPairingFeedback = error ?? "Tailscale peer saved. Generate a code on one device and accept it on the other.";
        Status = SaveSyncPairingFeedback;
    }

    public async Task GeneratePairingCodeAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null) return;
        PairingCode = await saveSync.GeneratePairingCodeAsync(cancellationToken);
        OnPropertyChanged(nameof(SaveSyncPairingStatus));
        RefreshSaveSyncPairingState();
        SaveSyncPairingFeedback = "Invitation ready. Keep NovaLauncher and Tailscale running on this device while the other device accepts it.";
        Status = SaveSyncPairingFeedback;
    }

    public async Task RetrySaveSyncListenerAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null) { SaveSyncPairingFeedback = "Save sync is unavailable."; return; }
        var error = await saveSync.RetryListenerAsync(cancellationToken);
        RefreshSaveSyncPairingState();
        SaveSyncPairingFeedback = error ?? saveSync.ListenerStatus;
        Status = SaveSyncPairingFeedback;
    }

    public async Task ApplyPairingCodeAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null) return;
        var error = await saveSync.ApplyPairingCodeAsync(PairingCode, cancellationToken);
        if (error is null) PairingCode = string.Empty;
        RefreshSaveSyncPairingState();
        SaveSyncPairingFeedback = error ?? "Pairing succeeded. This device now trusts the displayed pinned device ID.";
        Status = SaveSyncPairingFeedback;
    }

    public async Task RevokePairedDeviceAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null) return;
        var error = await saveSync.RevokePeerAsync(cancellationToken);
        RefreshSaveSyncPairingState();
        SaveSyncPairingFeedback = error ?? "The paired device was revoked. Its previous invitation and credentials can no longer authorize synchronization.";
        Status = SaveSyncPairingFeedback;
    }

    public async Task RenameSelectedTrustedPeerAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null || SelectedTrustedPeer is null) return;
        var error = await saveSync.RenamePeerAsync(SelectedTrustedPeer.DeviceId, TrustedPeerName, cancellationToken);
        SaveSyncPairingFeedback = error ?? "Trusted device renamed.";
        RefreshSaveSyncPairingState();
    }

    public async Task ToggleSelectedTrustedPeerAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null || SelectedTrustedPeer is null) return;
        var pause = SelectedTrustedPeer.State == TrustedPeerState.Active;
        var error = await saveSync.SetPeerPausedAsync(SelectedTrustedPeer.DeviceId, pause, cancellationToken);
        SaveSyncPairingFeedback = error ?? (pause ? "Trusted device paused." : "Trusted device resumed.");
        RefreshSaveSyncPairingState();
    }

    public async Task RevokeSelectedTrustedPeerAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null || SelectedTrustedPeer is null) return;
        var error = await saveSync.RevokePeerAsync(SelectedTrustedPeer.DeviceId, cancellationToken);
        SaveSyncPairingFeedback = error ?? "Trusted device independently revoked.";
        SelectedTrustedPeer = null;
        RefreshSaveSyncPairingState();
    }

    public async Task PreviewSelectedGameTransferAsync(CancellationToken cancellationToken)
    {
        if (gameTransfers is null || SelectedGame is null) { GameTransferStatus = "Select a manually added game first."; return; }
        GameTransferPreview = await gameTransfers.PreviewAsync(SelectedGame, GameTransferSourceFolder, cancellationToken);
        GameTransferStatus = GameTransferPreview.Accepted
            ? $"Preview ready: {GameTransferPreview.Files.Count:N0} files, {GameTransferPreview.TotalBytes:N0} bytes. Nothing is shared until authorization."
            : GameTransferPreview.Error ?? "The folder cannot be offered.";
    }

    public async Task SelectAndPreviewGameTransferSourceAsync(string sourceFolder, CancellationToken cancellationToken)
    {
        GameTransferSourceFolder = sourceFolder;
        GameTransferStatus = "Scanning the selected folder and every subfolder…";
        await PreviewSelectedGameTransferAsync(cancellationToken);
    }

    public async Task AuthorizeSelectedGameTransferAsync(CancellationToken cancellationToken)
    {
        if (gameTransfers is null || SelectedGame is null || GameTransferPreview is null || SelectedTrustedPeer is null)
        { GameTransferStatus = "Create a preview and select one active trusted recipient."; return; }
        var result = await gameTransfers.AuthorizeAsync(SelectedGame, GameTransferPreview, [SelectedTrustedPeer.DeviceId], GameTransferRightsAttested, cancellationToken);
        GameTransferStatus = result.Message;
        await RefreshGameTransferHistoryAsync(cancellationToken);
    }

    public async Task RefreshPeerGameTransferOffersAsync(CancellationToken cancellationToken)
    {
        if (gameTransfers is null) return;
        PeerGameTransferOffers.Clear();
        foreach (var offer in await gameTransfers.RefreshOffersAsync(cancellationToken)) PeerGameTransferOffers.Add(offer);
        GameTransferStatus = PeerGameTransferOffers.Count == 0 ? "No authorized offers are available from active trusted devices." : $"Found {PeerGameTransferOffers.Count} authorized offer(s).";
    }

    public async Task DownloadSelectedGameTransferAsync(CancellationToken cancellationToken)
    {
        if (gameTransfers is null || SelectedGameTransferOffer is null || string.IsNullOrWhiteSpace(GameTransferDestination))
        { GameTransferStatus = "Select an authorized offer and an empty destination."; return; }
        _gameTransferCancellation?.Dispose();
        _gameTransferCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        IsGameTransferActive = true;
        GameTransferProgress = 0;
        try
        {
            var progress = new Progress<GameTransferProgress>(item =>
            {
                GameTransferProgress = item.TotalBytes == 0 ? 0 : item.CompletedBytes * 100d / item.TotalBytes;
                GameTransferStatus = $"Receiving {item.RelativePath} · {item.CompletedBytes:N0}/{item.TotalBytes:N0} bytes · {item.BytesPerSecond / 1024d / 1024d:F1} MiB/s";
            });
            var result = await gameTransfers.DownloadAsync(SelectedGameTransferOffer, GameTransferDestination, progress, _gameTransferCancellation.Token);
            GameTransferStatus = result.Message;
            if (result.Success) GameTransferProgress = 100;
        }
        catch (OperationCanceledException) { GameTransferStatus = "Transfer paused or cancelled. Verified staged progress is retained for resume."; }
        finally { IsGameTransferActive = false; await RefreshGameTransferHistoryAsync(CancellationToken.None); }
    }

    public void PauseGameTransfer() => _gameTransferCancellation?.Cancel();

    public async Task RefreshGameTransferHistoryAsync(CancellationToken cancellationToken)
    {
        GameTransferHistory.Clear();
        if (gameTransfers is null) return;
        foreach (var item in (await gameTransfers.GetHistoryAsync(cancellationToken)).OrderByDescending(static item => item.TimestampUtc)) GameTransferHistory.Add(item);
    }

    public void RefreshSaveSyncPairingState()
    {
        var selectedId = SelectedTrustedPeer?.DeviceId;
        TrustedSaveSyncPeers.Clear();
        if (saveSync is not null)
            foreach (var peer in saveSync.Settings.EffectiveTrustedPeers.OrderBy(static peer => peer.DisplayName, StringComparer.OrdinalIgnoreCase))
                TrustedSaveSyncPeers.Add(peer);
        SelectedTrustedPeer = selectedId is { } id ? TrustedSaveSyncPeers.FirstOrDefault(peer => peer.DeviceId == id) : null;
        OnPropertyChanged(nameof(SaveSyncPairingStatus));
        OnPropertyChanged(nameof(IsSaveSyncPaired));
        OnPropertyChanged(nameof(IsSaveSyncNotPaired));
        OnPropertyChanged(nameof(CanAcceptPairingInvitation));
        OnPropertyChanged(nameof(IsSaveSyncListening));
        OnPropertyChanged(nameof(IsSaveSyncNotListening));
        OnPropertyChanged(nameof(SaveSyncListenerStatus));
    }

    public void ReportInvitationClipboardStatus(string message)
    {
        SaveSyncPairingFeedback = message;
        Status = message;
    }

    public async Task SetSelectedSaveDirectoryAsync(string? directory, CancellationToken cancellationToken)
    {
        if (SelectedGame is null) return;
        var result = await library.SetSaveDirectoryAsync(SelectedGame.Id, directory, cancellationToken);
        Status = result.Status == LibraryMutationStatus.Saved
            ? directory is null ? "Save-folder mapping removed." : "Save folder mapped for this manual game."
            : result.Error ?? "The save folder could not be mapped.";
        if (result.Item is not null) { SelectedGame = result.Item; RefreshGames(); }
        ShowInitialSaveUploadPrompt = result.Status == LibraryMutationStatus.Saved && directory is not null &&
            Directory.EnumerateFileSystemEntries(directory).Any();
        if (ShowInitialSaveUploadPrompt)
            Status = "Existing save files were found. Review the mapping, then choose Upload existing saves or Not now.";
    }

    public void DismissInitialSaveUploadPrompt()
    {
        ShowInitialSaveUploadPrompt = false;
        Status = "Existing saves were not uploaded. Use Sync now whenever you are ready.";
    }

    public async Task SyncSelectedGameNowAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanConfigureSaveFolder) { Status = "Select a manually added game first."; return; }
        if (SelectedGame.SaveSyncId is null) { Status = "Link this game with the paired device before uploading saves."; return; }
        if (string.IsNullOrWhiteSpace(SelectedGame.SaveDirectory) || !Directory.Exists(SelectedGame.SaveDirectory))
        { Status = "Choose an existing save folder before synchronizing."; return; }
        if (saveSync is null) { Status = "Save sync is unavailable."; return; }
        ShowInitialSaveUploadPrompt = false;
        IsSaveTransferActive = true;
        ActiveSaveTransfer = $"Scanning and uploading existing saves for {SelectedGame.Name}…";
        try
        {
            var sync = await saveSync.SnapshotAndPushAfterExitAsync(SelectedGame, cancellationToken);
            ActiveSaveTransfer = sync.Message;
            Status = sync.Message;
            RefreshSaveSyncActivities();
        }
        finally { IsSaveTransferActive = false; }
    }

    public async Task GenerateSaveSyncLinkAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanConfigureSaveFolder) { Status = "Select a manually added game first."; return; }
        var identity = SelectedGame.SaveSyncId ?? Guid.NewGuid();
        var result = await library.SetSaveSyncIdAsync(SelectedGame.Id, identity, SelectedGame.SaveSyncLabel, cancellationToken);
        if (result.Status != LibraryMutationStatus.Saved) { Status = result.Error ?? "The save link could not be created."; return; }
        SelectedGame = result.Item;
        SaveSyncLinkCode = identity.ToString("D");
        Status = "Shared save identity created. Copy this code to the matching game on the paired device.";
    }

    public async Task LinkSaveAutomaticallyAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanConfigureSaveFolder) { Status = "Select a manually added game first."; return; }
        if (saveSync is null) { Status = "Save sync is unavailable."; return; }
        var derived = saveSync.DeriveSharedSaveIdentity(SaveSyncLabel, SelectedGame.Platform);
        if (derived.Identity is not { } identity) { Status = derived.Error ?? "The shared save identity could not be derived."; return; }
        var result = await library.SetSaveSyncIdAsync(SelectedGame.Id, identity, SaveSyncLabel, cancellationToken);
        if (result.Status != LibraryMutationStatus.Saved) { Status = result.Error ?? "The game could not be linked."; return; }
        SelectedGame = result.Item;
        Status = "Automatic save link enabled. Use the same sync label and platform for the matching game on the paired device.";
    }

    public async Task ApplySaveSyncLinkAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanConfigureSaveFolder) { Status = "Select a manually added game first."; return; }
        if (!Guid.TryParse(SaveSyncLinkCode.Trim(), out var identity) || identity == Guid.Empty)
        { Status = "Paste a valid shared save identity from the matching game."; return; }
        var result = await library.SetSaveSyncIdAsync(SelectedGame.Id, identity, SaveSyncLabel, cancellationToken);
        if (result.Status != LibraryMutationStatus.Saved) { Status = result.Error ?? "The save link could not be applied."; return; }
        SelectedGame = result.Item;
        Status = "This game is now linked to the paired device's shared save identity.";
    }

    public void RefreshSaveSyncActivities()
    {
        SaveSyncActivities.Clear();
        if (saveSync is null) return;
        foreach (var state in saveSync.Settings.Games)
        {
            var game = Games.FirstOrDefault(candidate =>
                (candidate.SaveSyncId is { } shared ? new GameId(shared) : candidate.Id) == state.GameId);
            SaveSyncActivities.Add(new(
                game?.Name ?? $"Linked game {state.GameId.Value:N}",
                state.Status,
                state.HeadSnapshotId?.ToString("N") ?? "No snapshot",
                state.LastObservedFiles.Count));
        }
    }

    public async Task RetryPendingSaveUploadsAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null) { ActiveSaveTransfer = "Save sync is unavailable."; return; }
        IsSaveTransferActive = true;
        ActiveSaveTransfer = "Retrying queued save snapshots…";
        try
        {
            var count = await saveSync.RetryPendingUploadsAsync(cancellationToken);
            ActiveSaveTransfer = count == 0
                ? "No queued snapshot was acknowledged. The peer may be offline, or there may be nothing pending."
                : $"The peer acknowledged {count} queued snapshot(s).";
            RefreshSaveSyncActivities();
        }
        finally { IsSaveTransferActive = false; }
    }

    public async Task RefreshSelectedSnapshotHistoryAsync(CancellationToken cancellationToken)
    {
        SaveSnapshotHistory.Clear();
        SaveRestoreHistory.Clear();
        SelectedSaveSnapshot = null;
        if (saveSync is null || SelectedGame is null) { Status = "Select a game to inspect its save history."; return; }
        var gameId = SelectedGame.SaveSyncId is { } shared ? new GameId(shared) : SelectedGame.Id;
        var history = await saveSync.GetSnapshotHistoryAsync(gameId, cancellationToken);
        foreach (var item in history) SaveSnapshotHistory.Add(item);
        var restores = await saveSync.GetRestoreHistoryAsync(gameId, cancellationToken);
        foreach (var item in restores) SaveRestoreHistory.Add(item);
        Status = history.Count == 0 ? "No retained snapshots exist for this game." : $"Loaded {history.Count} integrity-checked save snapshots.";
    }

    public async Task RestoreSelectedSnapshotAsync(CancellationToken cancellationToken)
    {
        if (saveSync is null || SelectedGame is null || SelectedSaveSnapshot is null) return;
        if (!SelectedSaveSnapshot.IntegrityValid) { Status = "The selected snapshot failed integrity verification and cannot be restored."; return; }
        var result = await saveSync.RestoreSnapshotAsync(SelectedGame, SelectedSaveSnapshot.SnapshotId, cancellationToken);
        Status = result.Message;
        RefreshSaveSyncActivities();
        await RefreshSelectedSnapshotHistoryAsync(cancellationToken);
    }

    public async Task ResolveSaveConflictAsync(SaveConflictChoice choice, CancellationToken cancellationToken)
    {
        if (SelectedGame is null || saveSync is null) return;
        var result = await saveSync.ResolveConflictAsync(SelectedGame, choice, cancellationToken);
        Status = result.Message;
    }

    public void ApplySteamGridDbApiKey()
    {
        if (apiKeys is null)
        {
            Status = "SteamGridDB key integration is unavailable.";
            return;
        }
        apiKeys.SetSteamGridDbKey(SteamGridDbApiKey);
        SteamGridDbApiKey = string.Empty;
        OnPropertyChanged(nameof(SteamGridDbKeyStatus));
        Status = apiKeys.HasSteamGridDbKey
            ? "SteamGridDB key applied for this session. The key is not logged or stored in library data."
            : "SteamGridDB key cleared.";
    }

    public async Task CreateCollectionAsync(CancellationToken cancellationToken)
    {
        var result = await collections.CreateAsync(CollectionName, cancellationToken);
        Status = result.Status == DocumentSaveStatus.Saved ? "Collection created." : result.Error ?? "Collection failed.";
        if (result.Status == DocumentSaveStatus.Saved)
        {
            CollectionName = string.Empty;
            RefreshCollections();
        }
    }

    public async Task RenameCollectionAsync(CancellationToken cancellationToken)
    {
        if (SelectedCollection is null)
        {
            return;
        }

        var result = await collections.RenameAsync(SelectedCollection.Id, CollectionName, cancellationToken);
        Status = result.Status == DocumentSaveStatus.Saved ? "Collection renamed." : result.Error ?? "Rename failed.";
        if (result.Status == DocumentSaveStatus.Saved)
        {
            RefreshCollections();
        }
    }

    public async Task DeleteCollectionAsync(CancellationToken cancellationToken)
    {
        if (SelectedCollection is null)
        {
            return;
        }

        if (!_confirmCollectionDelete)
        {
            _confirmCollectionDelete = true;
            Status = "Deleting a collection does not remove its games. Click confirm to continue.";
            OnPropertyChanged(nameof(DeleteCollectionButtonText));
            return;
        }

        var result = await collections.DeleteAsync(SelectedCollection.Id, cancellationToken);
        Status = result.Status == DocumentSaveStatus.Saved ? "Collection deleted; games were preserved." : result.Error ?? "Delete failed.";
        _confirmCollectionDelete = false;
        OnPropertyChanged(nameof(DeleteCollectionButtonText));
        if (result.Status == DocumentSaveStatus.Saved)
        {
            RefreshCollections();
        }
    }

    public async Task ToggleCollectionMembershipAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || SelectedCollection is null)
        {
            return;
        }

        var isMember = !SelectedCollection.GameIds.Contains(SelectedGame.Id);
        var result = await collections.SetMembershipAsync(
            SelectedCollection.Id,
            SelectedGame.Id,
            isMember,
            cancellationToken);
        Status = result.Status == DocumentSaveStatus.Saved ? "Collection membership updated." : result.Error ?? "Collection update failed.";
        RefreshCollections();
    }

    public async Task ExportAsync(CancellationToken cancellationToken)
    {
        if (!TryValidateBackupPath(out var pathError))
        {
            Status = pathError;
            return;
        }

        var result = await backups.ExportAsync(BackupPath, cancellationToken);
        Status = result.Succeeded ? $"Backup exported to {result.ArchivePath}." : result.Error ?? "Export failed.";
    }

    public async Task RestoreAsync(CancellationToken cancellationToken)
    {
        if (!TryValidateBackupPath(out var pathError))
        {
            Status = pathError;
            return;
        }

        if (!_confirmRestore)
        {
            var preview = await backups.PreviewRestoreAsync(BackupPath, cancellationToken);
            if (!preview.IsValid)
            {
                Status = preview.Error ?? "Backup validation failed.";
                return;
            }

            _confirmRestore = true;
            Status = $"Validated: {string.Join(", ", preview.Documents)}. Confirm to create a pre-restore backup and replace these documents.";
            OnPropertyChanged(nameof(RestoreButtonText));
            return;
        }

        var result = await backups.RestoreAsync(BackupPath, cancellationToken);
        _confirmRestore = false;
        OnPropertyChanged(nameof(RestoreButtonText));
        Status = result.Succeeded ? "Backup restored. Reloading library…" : result.Error ?? "Restore failed.";
        if (result.Succeeded)
        {
            await InitializeAsync(cancellationToken);
        }
    }

    public void ReportUnexpectedFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        Status = $"Unexpected operation failure: {exception.Message}";
    }

    public void ReportArtworkUnavailable() =>
        Status = "The cached artwork could not be displayed safely; the game remains available.";

    private async Task RefreshSelectedMetadataAsync(bool forceRefresh, CancellationToken cancellationToken)
    {
        if (SelectedGame is null)
        {
            return;
        }

        await RunBusyAsync(async () =>
        {
            Status = forceRefresh ? "Forcing metadata and artwork refresh…" : "Refreshing metadata and artwork…";
            var result = await enrichment.RefreshAsync(SelectedGame.Id, forceRefresh, cancellationToken);
            if (result.Status == ProviderResultStatus.Success && result.Item is not null)
            {
                RefreshGames();
                SelectedGame = result.Item;
                Status = result.UsedStaleCache
                    ? "Live providers were unavailable; showing stale cached metadata and artwork."
                    : result.UsedCache
                        ? "Loaded metadata and artwork from the local cache."
                        : result.ProviderFailures.Count > 0
                            ? $"Metadata refreshed with {result.ProviderFailures.Count} provider warning(s)."
                            : "Metadata and artwork refreshed.";
                return;
            }

            Status = result.Error ?? "No metadata or artwork was available.";
        });
    }

    public async Task PreviewSteamImportAsync(CancellationToken cancellationToken)
    {
        await RunBusyAsync(async () =>
        {
            var preview = await steamImport.PreviewAsync(
                string.IsNullOrWhiteSpace(SteamRoot) ? null : SteamRoot.Trim(),
                cancellationToken);
            SteamPreviewItems.Clear();
            foreach (var item in preview.Items)
            {
                SteamPreviewItems.Add(item);
            }

            SteamImportFailures.Clear();
            foreach (var failure in preview.Failures)
            {
                SteamImportFailures.Add(failure);
            }

            OnPropertyChanged(nameof(HasSteamPreview));
            Status = $"Steam preview: {preview.Added} add, {preview.Updated} update, " +
                $"{preview.Unchanged} unchanged, {preview.Failures.Count} skipped. Review before importing.";
        });
    }

    public async Task CommitSteamImportAsync(CancellationToken cancellationToken)
    {
        await RunBusyAsync(async () =>
        {
            var result = await steamImport.CommitAsync(cancellationToken);
            if (result.Status == SteamImportCommitStatus.Saved)
            {
                RefreshGames();
                SteamPreviewItems.Clear();
                SteamImportFailures.Clear();
                OnPropertyChanged(nameof(HasSteamPreview));
                Status = $"Steam import saved atomically: {result.Imported} added or updated.";
                return;
            }

            Status = result.Error ?? "Steam import failed.";
        });
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await action();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SetRunAsAdministratorAsync(bool enabled)
    {
        if (SelectedGame is null) return;
        var result = await library.SetRunAsAdministratorAsync(SelectedGame.Id, enabled, CancellationToken.None);
        if (result.Status == LibraryMutationStatus.Saved)
        {
            SelectedGame = result.Item;
            RefreshGames();
            Status = enabled
                ? "This game will request administrator access through Windows UAC when launched."
                : "This game will launch with standard user permissions.";
        }
        else Status = result.Error ?? "Administrator preference could not be saved.";
    }

    private async Task TrackPlaySessionAsync(GameId gameId, int processId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken)
    {
        var elapsed = await launcher.WaitForExitAsync(processId, startedAtUtc, cancellationToken);
        if (elapsed is null) return;
        var result = await library.AddPlayTimeAsync(gameId, elapsed.Value, startedAtUtc, CancellationToken.None);
        if (result.Status == LibraryMutationStatus.Saved)
        {
            RefreshGames();
            if (SelectedGame?.Id == gameId) SelectedGame = result.Item;
            Status = $"Play session recorded: {FormatPlayTime(elapsed.Value)}.";
        }
        var game = library.Games.FirstOrDefault(item => item.Id == gameId);
        if (game is not null && saveSync is not null && game.SaveDirectory is not null && string.Equals(game.Source, "Manual", StringComparison.OrdinalIgnoreCase))
        {
            IsSaveTransferActive = true;
            ActiveSaveTransfer = $"Scanning and uploading changed saves for {game.Name}…";
            var sync = await saveSync.SnapshotAndPushAfterExitAsync(game, cancellationToken);
            IsSaveTransferActive = false;
            ActiveSaveTransfer = sync.Message;
            RefreshSaveSyncActivities();
            Status = sync.Message;
        }
    }

    private static string FormatPlayTime(TimeSpan value) => value.TotalHours >= 1
        ? $"{value.TotalHours:0.0} hours"
        : $"{Math.Max(0, value.TotalMinutes):0} minutes";

    private static string JoinMetadata(IReadOnlyList<string>? values, string fallback) =>
        values is { Count: > 0 } ? string.Join(", ", values) : fallback;

    private static string JoinEditable(IReadOnlyList<string>? values) => values is { Count: > 0 } ? string.Join(", ", values) : string.Empty;

    private static string[]? ParseEditableList(string value)
    {
        var items = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        return items.Length == 0 ? null : items;
    }

    private static ArtworkKind ParseArtworkKind(string value) => Enum.TryParse<ArtworkKind>(value, ignoreCase: true, out var kind) ? kind : ArtworkKind.Cover;

    private static ArtworkReference? GetArtwork(GameArtwork? artwork, ArtworkKind kind) => kind switch
    {
        ArtworkKind.Cover => artwork?.Cover,
        ArtworkKind.Hero => artwork?.Hero,
        ArtworkKind.Logo => artwork?.Logo,
        _ => artwork?.Background,
    };

    private void RefreshGames()
    {
        _selectedLibraryGameIds.RemoveWhere(id => library.Games.All(game => game.Id != id));
        _libraryRenderLimit = LibraryRenderPageSize;
        Games.Clear();
        IEnumerable<LibraryItem> query = library.Games;
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            var search = SearchText.Trim();
            query = query.Where(game => game.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || game.Platform.Contains(search, StringComparison.OrdinalIgnoreCase)
                || game.Source.Contains(search, StringComparison.OrdinalIgnoreCase));
        }
        if (FavoritesOnly) query = query.Where(static game => game.IsFavorite);
        if (SourceFilter != "All sources") query = query.Where(game => string.Equals(game.Source, SourceFilter, StringComparison.OrdinalIgnoreCase));
        if (LibraryCollectionFilter is { } collection) query = query.Where(game => collection.GameIds.Contains(game.Id));
        query = SmartCollectionFilter switch
        {
            "Favorites" => query.Where(static game => game.IsFavorite),
            "Recently played" => query.Where(static game => game.LastPlayedAtUtc is not null),
            "Manual games" => query.Where(static game => string.Equals(game.Source, "Manual", StringComparison.OrdinalIgnoreCase)),
            "Steam games" => query.Where(static game => string.Equals(game.Source, "Steam", StringComparison.OrdinalIgnoreCase)),
            "Missing targets" => query.Where(static game => !IsTargetAvailable(game)),
            _ => query,
        };
        if (PlatformFilter != "All platforms")
        {
            query = PlatformFilter == "Other"
                ? query.Where(static game => game.Platform is not ("Windows" or "Linux" or "macOS"))
                : query.Where(game => string.Equals(game.Platform, PlatformFilter, StringComparison.OrdinalIgnoreCase));
        }
        query = AvailabilityFilter switch
        {
            "Available" => query.Where(static game => IsTargetAvailable(game)),
            "Missing target" => query.Where(static game => !IsTargetAvailable(game)),
            _ => query,
        };
        query = SelectedSort switch
        {
            "Recently played" => query.OrderByDescending(static game => game.LastPlayedAtUtc).ThenBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
            "Date added" => query.OrderByDescending(static game => game.AddedAtUtc).ThenBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
            "Playtime" => query.OrderByDescending(static game => game.TotalPlayTime).ThenBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
            "Release date" => query.OrderByDescending(static game => game.Metadata.ReleaseDate?.Value).ThenBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
            "Platform" => query.OrderBy(static game => game.Platform, StringComparer.OrdinalIgnoreCase).ThenBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
            "Recently updated" => query.OrderByDescending(static game => game.UpdatedAtUtc).ThenBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
            _ => query.OrderBy(static game => game.Name, StringComparer.OrdinalIgnoreCase),
        };
        foreach (var game in query)
        {
            Games.Add(game);
        }
        RefreshRenderedGames();

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(MissingTargetCount));
        OnPropertyChanged(nameof(HasMissingTargets));
        OnPropertyChanged(nameof(LibraryGameCount));
        OnPropertyChanged(nameof(FavoriteGameCount));
        OnPropertyChanged(nameof(TotalLibraryPlayTime));
        HomeGames.Clear();
        foreach (var game in library.Query(string.Empty, false, LibrarySort.RecentlyUpdated)
                     .OrderByDescending(static item => item.IsFavorite)
                     .ThenByDescending(static item => item.UpdatedAtUtc)
                     .Take(8))
        {
            HomeGames.Add(game);
        }

        ContinuePlayingGames.Clear();
        foreach (var game in library.Games
                     .Where(static item => item.LastPlayedAtUtc is not null)
                     .OrderByDescending(static item => item.LastPlayedAtUtc)
                     .Take(6))
        {
            ContinuePlayingGames.Add(game);
        }

        MostPlayedGames.Clear();
        foreach (var game in library.Games
                     .Where(static item => item.TotalPlayTime > TimeSpan.Zero)
                     .OrderByDescending(static item => item.TotalPlayTime)
                     .ThenBy(static item => item.Name, StringComparer.OrdinalIgnoreCase)
                     .Take(6))
        {
            MostPlayedGames.Add(game);
        }

        FeaturedGame = ContinuePlayingGames.FirstOrDefault()
            ?? library.Games.Where(static item => item.IsFavorite).OrderByDescending(static item => item.UpdatedAtUtc).FirstOrDefault()
            ?? library.Games.OrderByDescending(static item => item.AddedAtUtc).FirstOrDefault();
        OnPropertyChanged(nameof(FeaturedGame));
        OnPropertyChanged(nameof(HasFeaturedGame));
        OnPropertyChanged(nameof(HasContinuePlayingGames));
        OnPropertyChanged(nameof(HasMostPlayedGames));
        RefreshDuplicateCandidates();
        NotifyLibrarySelectionChanged();
    }

    private void NotifyLibrarySelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedLibraryGameCount));
        OnPropertyChanged(nameof(HasLibraryMultiSelection));
        OnPropertyChanged(nameof(BulkSelectionSummary));
    }

    private void RefreshRenderedGames()
    {
        RenderedGames.Clear();
        foreach (var game in Games.Take(_libraryRenderLimit)) RenderedGames.Add(game);
        OnPropertyChanged(nameof(HasMoreLibraryGames));
        OnPropertyChanged(nameof(LoadMoreLibraryGamesText));
    }

    private void RefreshDuplicateCandidates()
    {
        DuplicateCandidates.Clear();
        DuplicateReviewTruncated = false;
        var seenPairs = new HashSet<string>(StringComparer.Ordinal);

        foreach (var group in library.Games
                     .Where(static game => game.LaunchTarget.Kind == LaunchTargetKind.Executable)
                     .GroupBy(static game => NormalizeExecutableTarget(game.LaunchTarget.Target), StringComparer.OrdinalIgnoreCase)
                     .Where(static group => !string.IsNullOrWhiteSpace(group.Key) && group.Count() > 1))
        {
            AddDuplicateGroup(group, "Same normalized executable target", seenPairs);
        }

        foreach (var group in library.Games
                     .Where(static game => string.Equals(game.Source, "Steam", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(game.SourceItemId))
                     .GroupBy(static game => game.SourceItemId!, StringComparer.OrdinalIgnoreCase)
                     .Where(static group => group.Count() > 1))
        {
            AddDuplicateGroup(group, "Same stable Steam identity", seenPairs);
        }

        OnPropertyChanged(nameof(HasDuplicateCandidates));
    }

    private void AddDuplicateGroup(IEnumerable<LibraryItem> items, string reason, HashSet<string> seenPairs)
    {
        var ordered = items.OrderBy(static game => game.Id.Value).ToArray();
        for (var index = 1; index < ordered.Length; index++)
        {
            if (DuplicateCandidates.Count >= MaximumDuplicateReviewPairs)
            {
                DuplicateReviewTruncated = true;
                return;
            }
            var pairKey = $"{ordered[0].Id.Value:N}:{ordered[index].Id.Value:N}";
            if (seenPairs.Add(pairKey)) DuplicateCandidates.Add(new(ordered[0], ordered[index], reason));
        }
    }

    public async Task FavoriteSelectedGamesAsync(CancellationToken cancellationToken)
    {
        var selected = library.Games.Where(game => _selectedLibraryGameIds.Contains(game.Id)).ToArray();
        if (selected.Length == 0) { Status = "Select one or more games first."; return; }
        var failures = 0;
        foreach (var game in selected.Where(static game => !game.IsFavorite))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = await library.ToggleFavoriteAsync(game.Id, cancellationToken);
            if (result.Status != LibraryMutationStatus.Saved) failures++;
        }
        RefreshGames();
        Status = failures == 0
            ? $"Marked {selected.Length} selected game(s) as favorites."
            : $"Bulk favorite completed with {failures} failure(s). No game files were changed.";
    }

    public async Task RefreshSelectedGamesMetadataAsync(CancellationToken cancellationToken)
    {
        var selected = library.Games.Where(game => _selectedLibraryGameIds.Contains(game.Id)).Take(100).ToArray();
        if (selected.Length == 0) { Status = "Select one or more games first."; return; }
        var failures = 0;
        await RunBusyAsync(async () =>
        {
            foreach (var game in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var result = await enrichment.RefreshAsync(game.Id, forceRefresh: true, cancellationToken);
                if (result.Status is not (ProviderResultStatus.Success or ProviderResultStatus.NoData)) failures++;
            }
            RefreshGames();
            Status = failures == 0
                ? $"Refreshed metadata for {selected.Length} selected game(s)."
                : $"Metadata refresh finished with {failures} per-game failure(s). Existing metadata was preserved.";
        });
    }

    public async Task MergeDuplicateAsync(DuplicateReviewItem review, bool candidateSurvives, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(review);
        var survivor = candidateSurvives ? review.Candidate : review.Primary;
        var duplicate = candidateSurvives ? review.Primary : review.Candidate;
        var preview = library.PreviewDuplicateMerge(survivor.Id, duplicate.Id);
        if (!preview.CanMerge) { Status = preview.Error ?? "These records cannot be merged safely."; return; }
        var key = $"{survivor.Id.Value:N}:{duplicate.Id.Value:N}";
        if (!_confirmDuplicateMerge || !string.Equals(_pendingDuplicateMergeKey, key, StringComparison.Ordinal))
        {
            _confirmDuplicateMerge = true;
            _pendingDuplicateMergeKey = key;
            Status = $"Confirm merge: keep '{survivor.Name}' and remove only the duplicate NovaLauncher record '{duplicate.Name}'. Game files are never deleted.";
            return;
        }

        _confirmDuplicateMerge = false;
        _pendingDuplicateMergeKey = null;
        var collectionResult = await collections.ReplaceGameReferenceAsync(duplicate.Id, survivor.Id, cancellationToken);
        if (collectionResult.Status != DocumentSaveStatus.Saved)
        {
            Status = collectionResult.Error ?? "Collection references could not be prepared; the merge was not performed.";
            return;
        }
        var result = await library.CommitDuplicateMergeAsync(preview, cancellationToken);
        if (result.Status != LibraryMutationStatus.Saved)
        {
            Status = result.Error ?? "The library changed after preview. Both game records remain available.";
            RefreshCollections();
            return;
        }
        _selectedLibraryGameIds.Remove(duplicate.Id);
        SelectedGame = result.Item;
        RefreshCollections();
        RefreshGames();
        Status = $"Merged the duplicate record into '{survivor.Name}'. No installed files were changed.";
    }

    private static string NormalizeExecutableTarget(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return string.Empty;
        try
        {
            return Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return target.Trim();
        }
    }

    private static bool IsTargetAvailable(LibraryItem game) => game.LaunchTarget.Kind == LaunchTargetKind.Uri
        || File.Exists(game.LaunchTarget.Target);

    private void RefreshCollections()
    {
        var selectedId = SelectedCollection?.Id;
        Collections.Clear();
        foreach (var collection in collections.Collections.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            Collections.Add(collection);
        }

        SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == selectedId);
        if (LibraryCollectionFilter is { } filter)
        {
            LibraryCollectionFilter = Collections.FirstOrDefault(collection => collection.Id == filter.Id);
        }
    }

    private void ApplyLibraryPreferences(LibraryViewPreferences preferences)
    {
        _suppressLibraryPreferenceSave = true;
        try
        {
            LibraryViewMode = preferences.ViewMode;
            LibraryCardSize = preferences.CardSize;
            SelectedSort = preferences.Sort;
            SourceFilter = preferences.SourceFilter;
            PlatformFilter = preferences.PlatformFilter;
            AvailabilityFilter = preferences.AvailabilityFilter;
            FavoritesOnly = preferences.FavoritesOnly;
        }
        finally
        {
            _suppressLibraryPreferenceSave = false;
        }
    }

    private async void PersistLibraryPreferences()
    {
        if (_suppressLibraryPreferenceSave) return;
        try
        {
            var error = await themes.SaveLibraryPreferencesAsync(new(
                LibraryViewMode,
                LibraryCardSize,
                SelectedSort,
                SourceFilter,
                PlatformFilter,
                AvailabilityFilter,
                FavoritesOnly), CancellationToken.None);
            if (error is not null) Status = error;
        }
        catch (Exception exception)
        {
            Status = $"Library preferences could not be saved: {exception.Message}";
        }
    }

    private bool TryValidateBackupPath(out string error)
    {
        if (string.IsNullOrWhiteSpace(BackupPath) ||
            !Path.IsPathFullyQualified(BackupPath) ||
            !string.Equals(Path.GetExtension(BackupPath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            error = "Enter an absolute backup path ending in .zip.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
