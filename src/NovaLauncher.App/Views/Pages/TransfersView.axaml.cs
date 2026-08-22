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
}
