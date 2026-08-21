using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Microsoft.Extensions.DependencyInjection;
using NovaLauncher.Application;
using NovaLauncher.Infrastructure.Enrichment;
using NovaLauncher.Application.SaveSync;
using Avalonia.Input.Platform;

namespace NovaLauncher.App;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Avalonia Window has no IDisposable lifecycle; the token source is cancelled and disposed in Closed.")]
public sealed partial class MainWindow : Window
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private Bitmap? _selectedArtwork;

    public MainWindow() => InitializeComponent();

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OnOpened(object? sender, EventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.InitializeAsync(_lifetimeCancellation.Token));

    private void OnClosed(object? sender, EventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _selectedArtwork?.Dispose();
        _lifetimeCancellation.Dispose();
    }

    private void OnBeginAdd(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Workspace!.BeginAdd();

    private async void OnPickGameExecutable(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Choose an installed game executable",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Windows executable") { Patterns = ["*.exe"] },
            ],
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) ViewModel.Workspace!.BeginAddFromExecutable(path);
    }

    private void OnOpenGameDetails(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button { DataContext: NovaLauncher.Domain.Library.LibraryItem game })
        {
            ViewModel.Workspace!.OpenGameDetails(game);
            OnGameSelectionChanged(null, null!);
        }
    }

    private void OnEditSelectedGame(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ViewModel.Workspace!.EditSelectedGame();

    private void OnClearSearch(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Workspace!.SearchText = string.Empty;

    private void OnShowHome(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Home");

    private void OnShowLibrary(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Library");

    private void OnShowSaves(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Saves");

    private void OnShowSettings(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Settings");

    private void OnNavigateBack(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateBack();

    private void OnNavigateForward(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateForward();

    private void OnToggleNavigation(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.ToggleNavigation();

    private void OnUseLibraryGrid(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.LibraryViewMode = "Grid";

    private void OnUseLibraryList(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.LibraryViewMode = "List";

    private void OnClearLibraryCollectionFilter(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.ClearLibraryCollectionFilter();

    private void OnClearSmartCollectionFilter(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.ClearSmartCollectionFilter();

    private void OnClearLibrarySelection(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.ClearLibrarySelection();

    private void OnLoadMoreLibraryGames(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.LoadMoreLibraryGames();

    private async void OnFavoriteSelectedGames(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.FavoriteSelectedGamesAsync(_lifetimeCancellation.Token));

    private async void OnRefreshSelectedGames(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RefreshSelectedGamesMetadataAsync(_lifetimeCancellation.Token));

    private void OnToggleLibraryGameSelection(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: NovaLauncher.Domain.Library.LibraryItem game }) return;
        var workspace = ViewModel.Workspace!;
        workspace.SetLibraryGameSelected(game, !workspace.IsLibraryGameSelected(game.Id));
    }

    private async void OnContextToggleFavorite(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: NovaLauncher.Domain.Library.LibraryItem game }) return;
        ViewModel.Workspace!.SelectedGame = game;
        await ExecuteAsync(() => ViewModel.Workspace.ToggleFavoriteAsync(_lifetimeCancellation.Token));
    }

    private async void OnContextRefreshMetadata(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: NovaLauncher.Domain.Library.LibraryItem game }) return;
        ViewModel.Workspace!.SelectedGame = game;
        await ExecuteAsync(() => ViewModel.Workspace.RefreshSelectedMetadataAsync(_lifetimeCancellation.Token));
    }

    private async void OnSearchGameIdentity(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.SearchSelectedGameIdentityAsync(_lifetimeCancellation.Token));

    private void OnRejectGameIdentityMatches(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.RejectIdentityCandidates();

    private async void OnUnlinkGameIdentity(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.UnlinkSelectedGameIdentityAsync(_lifetimeCancellation.Token));

    private async void OnConfirmGameIdentity(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: NovaLauncher.Application.Enrichment.GameIdentityCandidate candidate }) return;
        await ExecuteAsync(() => ViewModel.Workspace!.ConfirmSelectedGameIdentityAsync(candidate, _lifetimeCancellation.Token));
    }

    private async void OnSaveManualMetadata(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.SaveManualMetadataAsync(_lifetimeCancellation.Token));

    private async void OnRestoreProviderMetadata(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RestoreProviderMetadataAsync(_lifetimeCancellation.Token));

    private async void OnPreviewArtworkVariants(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.PreviewArtworkVariantsAsync(_lifetimeCancellation.Token));

    private async void OnApplyArtworkVariant(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: NovaLauncher.Application.Enrichment.ArtworkCandidate candidate }) return;
        await ExecuteAsync(() => ViewModel.Workspace!.ApplyArtworkVariantAsync(candidate, _lifetimeCancellation.Token));
        OnGameSelectionChanged(null, null!);
    }

    private async void OnCropSelectedArtwork(object? sender, RoutedEventArgs e)
    {
        await ExecuteAsync(() => ViewModel.Workspace!.CropSelectedArtworkAsync(_lifetimeCancellation.Token));
        OnGameSelectionChanged(null, null!);
    }

    private async void OnInspectArtworkCache(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.InspectArtworkCacheAsync(_lifetimeCancellation.Token));

    private async void OnCleanupArtworkCache(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.CleanupArtworkCacheAsync(_lifetimeCancellation.Token));

    private void OnReviewDuplicate(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: NovaLauncher.Application.Library.DuplicateReviewItem review } button) return;
        ViewModel.Workspace!.OpenGameDetails(button.Tag as string == "Candidate" ? review.Candidate : review.Primary);
        OnGameSelectionChanged(null, null!);
    }

    private async void OnMergeDuplicate(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: NovaLauncher.Application.Library.DuplicateReviewItem review } button) return;
        await ExecuteAsync(() => ViewModel.Workspace!.MergeDuplicateAsync(
            review,
            candidateSurvives: button.Tag as string == "Candidate",
            _lifetimeCancellation.Token));
    }

    private async void OnLocateSelectedGame(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Locate the installed game executable",
            AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("Windows executable") { Patterns = ["*.exe"] }],
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path))
            await ExecuteAsync(() => ViewModel.Workspace!.RelocateSelectedManualGameAsync(path, _lifetimeCancellation.Token));
    }

    private async void OnApplyTheme(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ApplySelectedThemeAsync(_lifetimeCancellation.Token));

    private async void OnApplyMotionPreference(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ApplyMotionPreferenceAsync(_lifetimeCancellation.Token));

    private void OnApplySteamGridDbKey(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        ViewModel.Workspace!.ApplySteamGridDbApiKey();

    private async void OnCheckForUpdates(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.CheckForUpdatesAsync(_lifetimeCancellation.Token));

    private async void OnStageUpdate(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.StageAvailableUpdateAsync(_lifetimeCancellation.Token));

    private async void OnLaunchStagedUpdate(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.LaunchStagedUpdateAsync(_lifetimeCancellation.Token));

    private async void OnRollbackUpdate(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RollbackUpdateAsync(_lifetimeCancellation.Token));

    private async void OnExportDiagnostics(object? sender, RoutedEventArgs e)
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Export sanitized NovaLauncher diagnostics",
            SuggestedFileName = $"NovaLauncher-Diagnostics-{DateTime.UtcNow:yyyyMMdd}.zip",
            DefaultExtension = "zip",
            FileTypeChoices = [new FilePickerFileType("ZIP archive") { Patterns = ["*.zip"] }],
        });
        var path = file?.TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) await ExecuteAsync(() => ViewModel.Workspace!.ExportDiagnosticsAsync(path, _lifetimeCancellation.Token));
    }

    private async void OnSaveTailscalePeer(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ConfigureTailscalePeerAsync(_lifetimeCancellation.Token));

    private async void OnGeneratePairingCode(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.GeneratePairingCodeAsync(_lifetimeCancellation.Token));

    private async void OnApplyPairingCode(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ApplyPairingCodeAsync(_lifetimeCancellation.Token));

    private async void OnRevokePairedDevice(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RevokePairedDeviceAsync(_lifetimeCancellation.Token));

    private async void OnRenameTrustedPeer(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RenameSelectedTrustedPeerAsync(_lifetimeCancellation.Token));

    private async void OnToggleTrustedPeer(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ToggleSelectedTrustedPeerAsync(_lifetimeCancellation.Token));

    private async void OnRevokeTrustedPeer(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RevokeSelectedTrustedPeerAsync(_lifetimeCancellation.Token));

    private void OnRefreshPairingStatus(object? sender, RoutedEventArgs e)
    {
        ViewModel.Workspace!.RefreshSaveSyncPairingState();
        ViewModel.Workspace.ReportInvitationClipboardStatus("Pairing status refreshed from local trusted-device state.");
    }

    private async void OnRetrySaveSyncListener(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RetrySaveSyncListenerAsync(_lifetimeCancellation.Token));

    private async void OnCopyInvitationCode(object? sender, RoutedEventArgs e)
    {
        var code = ViewModel.Workspace?.PairingCode;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || string.IsNullOrWhiteSpace(code))
        {
            ViewModel.Workspace!.ReportInvitationClipboardStatus("Generate an invitation code before copying it.");
            return;
        }
        await clipboard.SetTextAsync(code);
        ViewModel.Workspace!.ReportInvitationClipboardStatus("Invitation code copied. The clipboard will be cleared after 60 seconds if it still contains this code.");
        _ = ClearInvitationClipboardAsync(clipboard, code, _lifetimeCancellation.Token);
    }

    private async void OnPasteInvitationCode(object? sender, RoutedEventArgs e)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;
        var value = await clipboard.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(value))
        {
            ViewModel.Workspace!.ReportInvitationClipboardStatus("The clipboard does not contain an invitation code.");
            return;
        }
        ViewModel.Workspace!.PairingCode = value;
        ViewModel.Workspace.ReportInvitationClipboardStatus("Invitation code pasted. Choose Accept invitation to validate it.");
    }

    private static async Task ClearInvitationClipboardAsync(IClipboard clipboard, string expected, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);
            if (string.Equals(await clipboard.TryGetTextAsync(), expected, StringComparison.Ordinal)) await clipboard.ClearAsync();
        }
        catch (OperationCanceledException) { }
    }

    private async void OnChooseSaveFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose this game's save folder",
            AllowMultiple = false,
        });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path))
            await ExecuteAsync(() => ViewModel.Workspace!.SetSelectedSaveDirectoryAsync(path, _lifetimeCancellation.Token));
    }

    private async void OnClearSaveFolder(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.SetSelectedSaveDirectoryAsync(null, _lifetimeCancellation.Token));

    private async void OnSyncSelectedGameNow(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.SyncSelectedGameNowAsync(_lifetimeCancellation.Token));

    private async void OnChooseGameTransferSource(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose the authorized manual game folder", AllowMultiple = false });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) ViewModel.Workspace!.GameTransferSourceFolder = path;
    }

    private async void OnChooseGameTransferDestination(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose an empty destination folder", AllowMultiple = false });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) ViewModel.Workspace!.GameTransferDestination = path;
    }

    private async void OnPreviewGameTransfer(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.PreviewSelectedGameTransferAsync(_lifetimeCancellation.Token));

    private async void OnAuthorizeGameTransfer(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.AuthorizeSelectedGameTransferAsync(_lifetimeCancellation.Token));

    private async void OnRefreshGameTransferOffers(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RefreshPeerGameTransferOffersAsync(_lifetimeCancellation.Token));

    private async void OnDownloadGameTransfer(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.DownloadSelectedGameTransferAsync(_lifetimeCancellation.Token));

    private void OnPauseGameTransfer(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.PauseGameTransfer();

    private void OnDismissInitialSaveUpload(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.DismissInitialSaveUploadPrompt();

    private async void OnGenerateSaveSyncLink(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.GenerateSaveSyncLinkAsync(_lifetimeCancellation.Token));

    private async void OnLinkSaveAutomatically(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.LinkSaveAutomaticallyAsync(_lifetimeCancellation.Token));

    private async void OnApplySaveSyncLink(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ApplySaveSyncLinkAsync(_lifetimeCancellation.Token));

    private async void OnCopySaveSyncLink(object? sender, RoutedEventArgs e)
    {
        var workspace = ViewModel.Workspace!;
        var value = workspace.SelectedGame?.SaveSyncId?.ToString("D") ?? workspace.SaveSyncLinkCode;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || string.IsNullOrWhiteSpace(value)) { workspace.ReportInvitationClipboardStatus("Create a shared save identity before copying it."); return; }
        await clipboard.SetTextAsync(value);
        workspace.ReportInvitationClipboardStatus("Shared save identity copied.");
    }

    private async void OnPasteSaveSyncLink(object? sender, RoutedEventArgs e)
    {
        var value = await (TopLevel.GetTopLevel(this)?.Clipboard?.TryGetTextAsync() ?? Task.FromResult<string?>(null));
        if (!string.IsNullOrWhiteSpace(value)) ViewModel.Workspace!.SaveSyncLinkCode = value;
    }

    private void OnRefreshSaveSyncActivity(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.RefreshSaveSyncActivities();

    private async void OnRetryPendingSaveUploads(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RetryPendingSaveUploadsAsync(_lifetimeCancellation.Token));

    private async void OnKeepLocalSave(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ResolveSaveConflictAsync(SaveConflictChoice.KeepLocal, _lifetimeCancellation.Token));

    private async void OnKeepRemoteSave(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ResolveSaveConflictAsync(SaveConflictChoice.KeepRemote, _lifetimeCancellation.Token));

    private async void OnKeepBothSaves(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ResolveSaveConflictAsync(SaveConflictChoice.KeepBoth, _lifetimeCancellation.Token));

    private async void OnRefreshSnapshotHistory(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RefreshSelectedSnapshotHistoryAsync(_lifetimeCancellation.Token));

    private async void OnRestoreSelectedSnapshot(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RestoreSelectedSnapshotAsync(_lifetimeCancellation.Token));

    private void OnGameSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedArtwork?.Dispose();
        _selectedArtwork = null;
        SelectedCover.Source = null;
        var artwork = ViewModel.Workspace?.SelectedGame?.Artwork;
        var location = artwork?.Hero.IsPlaceholder == false ? artwork.Hero.Location : artwork?.Cover.Location;
        var resolver = Program.Services.GetRequiredService<ManagedArtworkMaterializer>();
        if (location is null || !resolver.TryResolve(location, out var path)) return;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > ManagedArtworkMaterializer.MaximumEncodedBytes) return;
            _selectedArtwork = Bitmap.DecodeToWidth(stream, 900, BitmapInterpolationMode.HighQuality);
            SelectedCover.Source = _selectedArtwork;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ViewModel.Workspace!.ReportArtworkUnavailable();
        }
    }

    private void OnCardArtworkLoaded(object? sender, RoutedEventArgs e)
    {
        if (sender is not Image { DataContext: NovaLauncher.Domain.Library.LibraryItem game } image) return;
        DisposeImageSource(image);
        var location = game.Artwork?.Cover.Location;
        var resolver = Program.Services.GetRequiredService<ManagedArtworkMaterializer>();
        if (location is null || !resolver.TryResolve(location, out var path)) return;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > ManagedArtworkMaterializer.MaximumEncodedBytes) return;
            image.Source = Bitmap.DecodeToWidth(stream, 420, BitmapInterpolationMode.HighQuality);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            ViewModel.Workspace!.ReportArtworkUnavailable();
        }
    }

    private static void OnCardArtworkUnloaded(object? sender, RoutedEventArgs e)
    {
        if (sender is Image image) DisposeImageSource(image);
    }

    private static void DisposeImageSource(Image image)
    {
        if (image.Source is Bitmap bitmap) bitmap.Dispose();
        image.Source = null;
    }

    private async void OnAddManualCover(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = $"Choose game {ViewModel.Workspace!.SelectedArtworkKind.ToLowerInvariant()} artwork",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Supported artwork") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp"] },
            ],
        });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(path)) return;
        await ExecuteAsync(() => ViewModel.Workspace!.AddManualCoverAsync(path, _lifetimeCancellation.Token));
        OnGameSelectionChanged(null, null!);
    }

    private async void OnRemoveManualCover(object? sender, RoutedEventArgs e)
    {
        await ExecuteAsync(() => ViewModel.Workspace!.RemoveManualCoverAsync(_lifetimeCancellation.Token));
        OnGameSelectionChanged(null, null!);
    }

    private async void OnSaveGame(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.SaveDraftAsync(_lifetimeCancellation.Token));

    private async void OnRemove(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RemoveSelectedAsync(_lifetimeCancellation.Token));

    private async void OnToggleFavorite(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ToggleFavoriteAsync(_lifetimeCancellation.Token));

    private async void OnLaunch(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.LaunchSelectedAsync(_lifetimeCancellation.Token));

    private async void OnLaunchHomeGame(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: NovaLauncher.Domain.Library.LibraryItem game }) return;
        ViewModel.Workspace!.SelectedGame = game;
        await ExecuteAsync(() => ViewModel.Workspace.LaunchSelectedAsync(_lifetimeCancellation.Token));
    }

    private async void OnRefreshMetadata(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RefreshSelectedMetadataAsync(_lifetimeCancellation.Token));

    private async void OnForceRefreshMetadata(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ForceRefreshSelectedMetadataAsync(_lifetimeCancellation.Token));

    private async void OnRefreshAchievements(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RefreshSelectedAchievementsAsync(_lifetimeCancellation.Token));

    private async void OnCreateCollection(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.CreateCollectionAsync(_lifetimeCancellation.Token));

    private async void OnRenameCollection(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RenameCollectionAsync(_lifetimeCancellation.Token));

    private async void OnDeleteCollection(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.DeleteCollectionAsync(_lifetimeCancellation.Token));

    private async void OnToggleMembership(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ToggleCollectionMembershipAsync(_lifetimeCancellation.Token));

    private async void OnExport(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.ExportAsync(_lifetimeCancellation.Token));

    private async void OnRestore(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.RestoreAsync(_lifetimeCancellation.Token));

    private async void OnPreviewSteamImport(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.PreviewSteamImportAsync(_lifetimeCancellation.Token));

    private async void OnCommitSteamImport(object? sender, Avalonia.Interactivity.RoutedEventArgs e) =>
        await ExecuteAsync(() => ViewModel.Workspace!.CommitSteamImportAsync(_lifetimeCancellation.Token));

    private async Task ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Closing the window owns and cancels all active UI operations.
        }
        catch (Exception exception)
        {
            ViewModel.Workspace!.ReportUnexpectedFailure(exception);
        }
    }
}
