using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.App.Views.Pages;

public sealed partial class HomeView : NovaPage
{
    public HomeView() => AvaloniaXamlLoader.Load(this);

    private void OnShowLibrary(object? sender, RoutedEventArgs e) => Workspace.NavigateTo("Library");
    private void OnShowSaves(object? sender, RoutedEventArgs e) => Workspace.NavigateTo("Saves");
    private void OnToggleHomeSection(object? sender, RoutedEventArgs e) => Workspace.ToggleSelectedHomeSection();
    private void OnMoveHomeSectionUp(object? sender, RoutedEventArgs e) => Workspace.MoveSelectedHomeSection(-1);
    private void OnMoveHomeSectionDown(object? sender, RoutedEventArgs e) => Workspace.MoveSelectedHomeSection(1);

    private void OnOpenGameDetails(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: LibraryItem game }) Workspace.OpenGameDetails(game);
    }

    private async void OnLaunchHomeGame(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: LibraryItem game }) return;
        Workspace.SelectedGame = game;
        await ExecuteAsync(() => Workspace.LaunchSelectedAsync(LifetimeToken));
    }
}
