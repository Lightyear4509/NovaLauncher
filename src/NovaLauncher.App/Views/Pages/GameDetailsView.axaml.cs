using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.App.Views.Pages;

public sealed partial class GameDetailsView : NovaPage
{
    public GameDetailsView() => AvaloniaXamlLoader.Load(this);

    private void OnShowLibrary(object? sender, RoutedEventArgs e) => Workspace.NavigateTo("Library");
    private void OnEditSelectedGame(object? sender, RoutedEventArgs e) => Workspace.EditSelectedGame();
    private void OnRejectGameIdentityMatches(object? sender, RoutedEventArgs e) => Workspace.RejectIdentityCandidates();
    private void OnDismissInitialSaveUpload(object? sender, RoutedEventArgs e) => Workspace.DismissInitialSaveUploadPrompt();

    private async void OnLaunch(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.LaunchSelectedAsync(LifetimeToken));
    private async void OnRemove(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RemoveSelectedAsync(LifetimeToken));
    private async void OnToggleFavorite(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ToggleFavoriteAsync(LifetimeToken));
    private async void OnRefreshMetadata(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RefreshSelectedMetadataAsync(LifetimeToken));
    private async void OnRefreshAchievements(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RefreshSelectedAchievementsAsync(LifetimeToken));
    private async void OnSearchGameIdentity(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.SearchSelectedGameIdentityAsync(LifetimeToken));
    private async void OnUnlinkGameIdentity(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.UnlinkSelectedGameIdentityAsync(LifetimeToken));
    private async void OnSaveManualMetadata(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.SaveManualMetadataAsync(LifetimeToken));
    private async void OnRestoreProviderMetadata(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RestoreProviderMetadataAsync(LifetimeToken));
    private async void OnPreviewArtworkVariants(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PreviewArtworkVariantsAsync(LifetimeToken));
    private async void OnCropSelectedArtwork(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.CropSelectedArtworkAsync(LifetimeToken));
    private async void OnInspectArtworkCache(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.InspectArtworkCacheAsync(LifetimeToken));
    private async void OnCleanupArtworkCache(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.CleanupArtworkCacheAsync(LifetimeToken));
    private async void OnClearSaveFolder(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.SetSelectedSaveDirectoryAsync(null, LifetimeToken));
    private async void OnSyncSelectedGameNow(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.SyncSelectedGameNowAsync(LifetimeToken));
    private async void OnGenerateSaveSyncLink(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.GenerateSaveSyncLinkAsync(LifetimeToken));
    private async void OnLinkSaveAutomatically(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.LinkSaveAutomaticallyAsync(LifetimeToken));
    private async void OnAddSaveDestination(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.AddSelectedSaveDestinationAsync(LifetimeToken));
    private async void OnRemoveSaveDestination(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RemoveSelectedSaveDestinationAsync(LifetimeToken));
    private async void OnUseAllSaveDestinations(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.UseAllSaveDestinationsAsync(LifetimeToken));
    private async void OnApplySaveSyncLink(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ApplySaveSyncLinkAsync(LifetimeToken));
    private async void OnRefreshSnapshotHistory(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RefreshSelectedSnapshotHistoryAsync(LifetimeToken));
    private async void OnVerifySnapshotIntegrity(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.VerifySelectedSnapshotsAsync(LifetimeToken));
    private async void OnRestoreSelectedSnapshot(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RestoreSelectedSnapshotAsync(LifetimeToken));
    private async void OnKeepLocalSave(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ResolveSaveConflictAsync(SaveConflictChoice.KeepLocal, LifetimeToken));
    private async void OnKeepRemoteSave(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ResolveSaveConflictAsync(SaveConflictChoice.KeepRemote, LifetimeToken));
    private async void OnKeepBothSaves(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ResolveSaveConflictAsync(SaveConflictChoice.KeepBoth, LifetimeToken));
    private async void OnCompareSaveConflict(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RefreshSaveConflictComparisonAsync(LifetimeToken));
    private void OnNewLaunchAction(object? sender, RoutedEventArgs e) => Workspace.BeginNewLaunchAction();
    private async void OnSaveLaunchAction(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.SaveLaunchActionAsync(LifetimeToken));
    private async void OnRemoveLaunchAction(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RemoveSelectedLaunchActionAsync(LifetimeToken));

    private async void OnLaunchAction(object? sender, RoutedEventArgs e)
    { if (sender is Button { DataContext: GameLaunchAction action }) await ExecuteAsync(() => Workspace.LaunchSelectedActionAsync(action, LifetimeToken)); }

    private async void OnChooseLaunchActionTarget(object? sender, RoutedEventArgs e)
    {
        var files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose launch action executable", AllowMultiple = false, FileTypeFilter = [WindowsExecutableFileType] });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrWhiteSpace(path)) return;
        Workspace.LaunchActionTarget = path;
        if (string.IsNullOrWhiteSpace(Workspace.LaunchActionWorkingDirectory)) Workspace.LaunchActionWorkingDirectory = Path.GetDirectoryName(path) ?? string.Empty;
    }

    private async void OnConfirmGameIdentity(object? sender, RoutedEventArgs e)
    { if (sender is Button { DataContext: GameIdentityCandidate candidate }) await ExecuteAsync(() => Workspace.ConfirmSelectedGameIdentityAsync(candidate, LifetimeToken)); }

    private async void OnApplyArtworkVariant(object? sender, RoutedEventArgs e)
    { if (sender is Button { DataContext: ArtworkCandidate candidate }) await ExecuteAsync(() => Workspace.ApplyArtworkVariantAsync(candidate, LifetimeToken)); }

    private async void OnLocateSelectedGame(object? sender, RoutedEventArgs e)
    {
        var files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Locate the installed game executable", AllowMultiple = false, FileTypeFilter = [WindowsExecutableFileType] });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) await ExecuteAsync(() => Workspace.RelocateSelectedManualGameAsync(path, LifetimeToken));
    }

    private async void OnAddManualCover(object? sender, RoutedEventArgs e)
    {
        var files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = $"Choose game {Workspace.SelectedArtworkKind.ToLowerInvariant()} artwork", AllowMultiple = false, FileTypeFilter = [ArtworkFileType] });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) await ExecuteAsync(() => Workspace.AddManualCoverAsync(path, LifetimeToken));
    }

    private async void OnRemoveManualCover(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RemoveManualCoverAsync(LifetimeToken));

    private async void OnChooseSaveFolder(object? sender, RoutedEventArgs e)
    {
        var folders = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose this game's save folder", AllowMultiple = false });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) await ExecuteAsync(() => Workspace.SetSelectedSaveDirectoryAsync(path, LifetimeToken));
    }

    private async void OnCopySaveSyncLink(object? sender, RoutedEventArgs e)
    {
        var value = Workspace.SelectedGame?.SaveSyncId?.ToString("D") ?? Workspace.SaveSyncLinkCode;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null || string.IsNullOrWhiteSpace(value)) { Workspace.ReportInvitationClipboardStatus("Create a shared save identity before copying it."); return; }
        await clipboard.SetTextAsync(value);
        Workspace.ReportInvitationClipboardStatus("Shared save identity copied.");
    }

    private async void OnPasteSaveSyncLink(object? sender, RoutedEventArgs e)
    {
        var value = await (TopLevel.GetTopLevel(this)?.Clipboard?.TryGetTextAsync() ?? Task.FromResult<string?>(null));
        if (!string.IsNullOrWhiteSpace(value)) Workspace.SaveSyncLinkCode = value;
    }
}
