using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using NovaLauncher.Application.Library;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.App.Views.Pages;

public sealed partial class LibraryView : NovaPage
{
    public LibraryView() => AvaloniaXamlLoader.Load(this);

    private async void OnPickGameExecutable(object? sender, RoutedEventArgs e)
    {
        var files = await Storage.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "Choose an installed game executable", AllowMultiple = false, FileTypeFilter = [WindowsExecutableFileType] });
        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (!string.IsNullOrWhiteSpace(path)) Workspace.BeginAddFromExecutable(path);
    }

    private void OnOpenGameDetails(object? sender, RoutedEventArgs e)
    { if (sender is Button { DataContext: LibraryItem game }) Workspace.OpenGameDetails(game); }

    private void OnUseLibraryGrid(object? sender, RoutedEventArgs e) => Workspace.LibraryViewMode = "Grid";
    private void OnUseLibraryList(object? sender, RoutedEventArgs e) => Workspace.LibraryViewMode = "List";
    private void OnClearLibraryCollectionFilter(object? sender, RoutedEventArgs e) => Workspace.ClearLibraryCollectionFilter();
    private void OnClearSmartCollectionFilter(object? sender, RoutedEventArgs e) => Workspace.ClearSmartCollectionFilter();
    private void OnClearLibrarySelection(object? sender, RoutedEventArgs e) => Workspace.ClearLibrarySelection();
    private void OnLoadMoreLibraryGames(object? sender, RoutedEventArgs e) => Workspace.LoadMoreLibraryGames();

    private async void OnFavoriteSelectedGames(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => Workspace.FavoriteSelectedGamesAsync(LifetimeToken));
    private async void OnRefreshSelectedGames(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => Workspace.RefreshSelectedGamesMetadataAsync(LifetimeToken));

    private void OnToggleLibraryGameSelection(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: LibraryItem game }) return;
        Workspace.SetLibraryGameSelected(game, !Workspace.IsLibraryGameSelected(game.Id));
    }

    private async void OnContextToggleFavorite(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: LibraryItem game }) return;
        Workspace.SelectedGame = game;
        await ExecuteAsync(() => Workspace.ToggleFavoriteAsync(LifetimeToken));
    }

    private async void OnContextRefreshMetadata(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: LibraryItem game }) return;
        Workspace.SelectedGame = game;
        await ExecuteAsync(() => Workspace.RefreshSelectedMetadataAsync(LifetimeToken));
    }

    private async void OnContextPlay(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: LibraryItem game }) return;
        Workspace.SelectedGame = game;
        await ExecuteAsync(() => Workspace.LaunchSelectedAsync(LifetimeToken));
    }

    private void OnContextEdit(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem { DataContext: LibraryItem game }) return;
        Workspace.SelectedGame = game;
        Workspace.EditSelectedGame();
    }

    private async void OnSaveGame(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => Workspace.SaveDraftAsync(LifetimeToken));

    private async void OnPreviewSteamImport(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => Workspace.PreviewSteamImportAsync(LifetimeToken));

    private async void OnCommitSteamImport(object? sender, RoutedEventArgs e) =>
        await ExecuteAsync(() => Workspace.CommitSteamImportAsync(LifetimeToken));

    private void OnReviewDuplicate(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DuplicateReviewItem review } button) return;
        Workspace.OpenGameDetails(button.Tag as string == "Candidate" ? review.Candidate : review.Primary);
    }

    private async void OnMergeDuplicate(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: DuplicateReviewItem review } button) return;
        await ExecuteAsync(() => Workspace.MergeDuplicateAsync(review, button.Tag as string == "Candidate", LifetimeToken));
    }
}
