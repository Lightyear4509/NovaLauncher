using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace NovaLauncher.App.Views.Pages;

public sealed partial class SavesView : NovaPage
{
    public SavesView() => AvaloniaXamlLoader.Load(this);

    private void OnShowSettings(object? sender, RoutedEventArgs e) => Workspace.NavigateTo("Settings");
    private void OnRefreshSaveSyncActivity(object? sender, RoutedEventArgs e) => Workspace.RefreshSaveSyncActivities();
    private void OnPauseSaveTransfer(object? sender, RoutedEventArgs e) => Workspace.PauseSaveTransfer();
    private async void OnCancelSaveTransfer(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.CancelPartialSaveTransfersAsync(LifetimeToken));
    private async void OnRetryPendingSaveUploads(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RetryPendingSaveUploadsAsync(LifetimeToken));
    private async void OnExport(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ExportAsync(LifetimeToken));
    private async void OnRestore(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RestoreAsync(LifetimeToken));
    private async void OnConfigurePrivateDestination(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.ConfigurePrivateDestinationAsync(LifetimeToken));
    private async void OnPreviewPrivateDestinationPublish(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PreviewPrivateDestinationPublishAsync(LifetimeToken));
    private async void OnPublishPrivateDestination(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PublishPrivateDestinationAsync(LifetimeToken));
    private async void OnRefreshPrivateDestinationSnapshots(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.RefreshPrivateDestinationSnapshotsAsync(LifetimeToken));
    private async void OnPreviewPrivateDestinationRepair(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.PreviewPrivateDestinationRepairAsync(LifetimeToken));
    private async void OnQuarantinePrivateDestinationSnapshot(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => Workspace.QuarantinePrivateDestinationSnapshotAsync(LifetimeToken));

    private async void OnChoosePrivateDestination(object? sender, RoutedEventArgs e)
    {
        var folders = await Storage.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Choose a local private snapshot folder", AllowMultiple = false });
        var path = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) Workspace.PrivateDestinationPath = path;
    }
}
