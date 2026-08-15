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

namespace NovaLauncher.Application.Library;

public sealed record SaveSyncActivityItem(string GameName, string Status, string SnapshotId, int FileCount);

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
    ISaveSyncService? saveSync = null) : INotifyPropertyChanged
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
    private bool _isEditorVisible;
    private string _selectedSort = "Name";
    private string _currentPage = "Home";
    private string _selectedThemeId = themes.CurrentThemeId;
    private string _steamGridDbApiKey = string.Empty;
    private string _tailscalePeerAddress = themes.TailscalePeerAddress ?? string.Empty;
    private string _pairingCode = string.Empty;
    private string _saveSyncLinkCode = string.Empty;
    private string _saveSyncLabel = string.Empty;
    private string _activeSaveTransfer = "No save transfer is currently running.";
    private bool _isSaveTransferActive;
    private bool _showInitialSaveUploadPrompt;
    private string _saveSyncPairingFeedback = "Enter and save the other device's Tailscale IP before pairing.";

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LibraryItem> Games { get; } = [];

    public ObservableCollection<GameCollection> Collections { get; } = [];

    public ObservableCollection<SteamImportPreviewItem> SteamPreviewItems { get; } = [];

    public ObservableCollection<SteamImportFailure> SteamImportFailures { get; } = [];

    public ObservableCollection<Achievement> Achievements { get; } = [];

    public ObservableCollection<LibraryItem> HomeGames { get; } = [];
    public ObservableCollection<SaveSyncActivityItem> SaveSyncActivities { get; } = [];

    public IReadOnlyList<string> SortOptions { get; } = ["Name", "Platform", "Recently updated"];

    public IReadOnlyList<ThemeOption> ThemeOptions => themes.Themes;

    public string SelectedThemeId { get => _selectedThemeId; set => Set(ref _selectedThemeId, value); }

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
        ? $"Paired with device {saveSync.Settings.PeerDeviceId:N}. Future authenticated connections are automatic."
        : saveSync?.Settings.PendingInvitationExpiresAtUtc is { } expires
            ? $"A single-use invitation is pending until {expires.LocalDateTime:g}."
            : "This device is not paired.";

    public bool IsSaveSyncPaired => saveSync?.IsPaired == true;

    public bool IsSaveSyncNotPaired => !IsSaveSyncPaired;

    public bool IsSaveSyncListening => saveSync?.IsListening == true;

    public bool IsSaveSyncNotListening => !IsSaveSyncListening;

    public string SaveSyncListenerStatus => saveSync?.ListenerStatus ?? "Save-sync transport is unavailable.";

    public bool CanAcceptPairingInvitation => IsSaveSyncNotPaired &&
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

    public string SelectedSort
    {
        get => _selectedSort;
        set
        {
            if (Set(ref _selectedSort, value))
            {
                RefreshGames();
            }
        }
    }

    public bool IsHomePage => _currentPage == "Home";

    public bool IsLibraryPage => _currentPage == "Library";

    public bool IsGameDetailsPage => _currentPage == "Details";

    public bool IsSavesPage => _currentPage == "Saves";

    public bool IsSettingsPage => _currentPage == "Settings";

    public string CurrentPage => _currentPage;

    public void NavigateTo(string page)
    {
        if (page is not ("Home" or "Library" or "Details" or "Saves" or "Settings") || !Set(ref _currentPage, page, nameof(CurrentPage))) return;
        OnPropertyChanged(nameof(IsHomePage));
        OnPropertyChanged(nameof(IsLibraryPage));
        OnPropertyChanged(nameof(IsGameDetailsPage));
        OnPropertyChanged(nameof(IsSavesPage));
        OnPropertyChanged(nameof(IsSettingsPage));
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
                    SaveSyncLabel = value.SaveSyncLabel ?? value.Name;
                }

                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(EditorTitle));
                Achievements.Clear();
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
            }
        }
    }

    public string Name { get => _name; set => Set(ref _name, value); }

    public string Platform { get => _platform; set => Set(ref _platform, value); }

    public string Target { get => _target; set => Set(ref _target, value); }

    public string Arguments { get => _arguments; set => Set(ref _arguments, value); }

    public string WorkingDirectory { get => _workingDirectory; set => Set(ref _workingDirectory, value); }

    public string CollectionName { get => _collectionName; set => Set(ref _collectionName, value); }

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
                _ when syncWarning is not null => syncWarning,
                _ => $"Loaded {library.Games.Count} game(s).",
            };
        });
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
        Name = Path.GetFileNameWithoutExtension(executablePath);
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
        var previous = selected.Artwork?.Cover;
        var imported = await manualCovers.ImportAsync(selected.Id, path, cancellationToken);
        if (imported.Cover is null)
        {
            Status = imported.Error ?? "The cover could not be imported.";
            return;
        }

        var save = await library.SetManualCoverAsync(selected.Id, imported.Cover, cancellationToken);
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
        Status = "Custom cover added. The source image was copied into NovaLauncher's bounded managed cache.";
    }

    public async Task RemoveManualCoverAsync(CancellationToken cancellationToken)
    {
        if (SelectedGame is null || !CanChangeManualCover || manualCovers is null) return;
        var previous = SelectedGame.Artwork?.Cover;
        var save = await library.SetManualCoverAsync(SelectedGame.Id, cover: null, cancellationToken);
        if (save.Status != LibraryMutationStatus.Saved)
        {
            Status = save.Error ?? "The custom cover could not be removed.";
            return;
        }
        if (previous is { IsPlaceholder: false } && previous.Provenance.IsManual)
            await manualCovers.DeleteManagedAsync(previous.Location, CancellationToken.None);
        SelectedGame = save.Item;
        RefreshGames();
        Status = "Custom cover removed. Installed game files were not touched.";
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

    public void RefreshSaveSyncPairingState()
    {
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

    private void RefreshGames()
    {
        Games.Clear();
        var sort = SelectedSort switch
        {
            "Platform" => LibrarySort.Platform,
            "Recently updated" => LibrarySort.RecentlyUpdated,
            _ => LibrarySort.Name,
        };
        foreach (var game in library.Query(SearchText, FavoritesOnly, sort))
        {
            Games.Add(game);
        }

        OnPropertyChanged(nameof(IsEmpty));
        HomeGames.Clear();
        foreach (var game in library.Query(string.Empty, false, LibrarySort.RecentlyUpdated)
                     .OrderByDescending(static item => item.IsFavorite)
                     .ThenByDescending(static item => item.UpdatedAtUtc)
                     .Take(8))
        {
            HomeGames.Add(game);
        }
    }

    private void RefreshCollections()
    {
        var selectedId = SelectedCollection?.Id;
        Collections.Clear();
        foreach (var collection in collections.Collections.OrderBy(static item => item.Name, StringComparer.OrdinalIgnoreCase))
        {
            Collections.Add(collection);
        }

        SelectedCollection = Collections.FirstOrDefault(collection => collection.Id == selectedId);
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
