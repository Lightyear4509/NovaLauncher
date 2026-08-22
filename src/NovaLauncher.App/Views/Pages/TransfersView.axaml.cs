using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace NovaLauncher.App.Views.Pages;

public sealed partial class TransfersView : NovaPage
{
    public TransfersView() => AvaloniaXamlLoader.Load(this);

    private void OnShowSettings(object? sender, RoutedEventArgs e) => Workspace.NavigateTo("Settings");
    private void OnRefreshSaveSyncActivity(object? sender, RoutedEventArgs e) => Workspace.RefreshSaveSyncActivities();
    private void OnPauseGameTransfer(object? sender, RoutedEventArgs e) => Workspace.PauseGameTransfer();
    private void OnPauseSaveTransfer(object? sender, RoutedEventArgs e) => Workspace.PauseSaveTransfer();
    private async void OnCancelSaveTransfer(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.CancelPartialSaveTransfersAsync(LifetimeToken));
    private async void OnRetryPendingSaveUploads(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RetryPendingSaveUploadsAsync(LifetimeToken));
    private async void OnPreviewGameTransfer(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PreviewSelectedGameTransferAsync(LifetimeToken));
    private async void OnAuthorizeGameTransfer(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.AuthorizeSelectedGameTransferAsync(LifetimeToken));
    private async void OnRefreshGameTransferOffers(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RefreshPeerGameTransferOffersAsync(LifetimeToken));
    private async void OnDownloadGameTransfer(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.DownloadSelectedGameTransferAsync(LifetimeToken));
    private async void OnExport(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ExportAsync(LifetimeToken));
    private async void OnRestore(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RestoreAsync(LifetimeToken));
    private async void OnConfigurePrivateDestination(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ConfigurePrivateDestinationAsync(LifetimeToken));
    private async void OnPreviewPrivateDestinationPublish(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PreviewPrivateDestinationPublishAsync(LifetimeToken));
    private async void OnPublishPrivateDestination(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PublishPrivateDestinationAsync(LifetimeToken));
    private async void OnRefreshPrivateDestinationSnapshots(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RefreshPrivateDestinationSnapshotsAsync(LifetimeToken));
    private async void OnPreviewPrivateDestinationRepair(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PreviewPrivateDestinationRepairAsync(LifetimeToken));
    private async void OnQuarantinePrivateDestinationSnapshot(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.QuarantinePrivateDestinationSnapshotAsync(LifetimeToken));
    private async void OnRefreshPrivateDestinationHealth(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RefreshPrivateDestinationHealthAsync(LifetimeToken));
    private async void OnPreviewPrivateMetadataPush(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PreviewPrivateMetadataPushAsync(LifetimeToken));
    private async void OnPreviewPrivateMetadataPull(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PreviewPrivateMetadataPullAsync(LifetimeToken));
    private async void OnCommitPrivateMetadata(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.CommitPrivateMetadataAsync(LifetimeToken));

    private async void OnChooseGameTransferSource(object? sender, RoutedEventArgs e)
    {
        var folders = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose the authorized manual game folder", AllowMultiple = false });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) await ExecuteAsync(() => Workspace.SelectAndPreviewGameTransferSourceAsync(path, LifetimeToken));
    }

    private async void OnChooseGameTransferDestination(object? sender, RoutedEventArgs e)
    {
        var folders = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose an empty destination folder", AllowMultiple = false });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) Workspace.GameTransferDestination = path;
    }

    private async void OnChoosePrivateDestination(object? sender, RoutedEventArgs e)
    {
        var folders = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a local folder or Windows-authenticated network share", AllowMultiple = false });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) Workspace.PrivateDestinationPath = path;
    }
}
