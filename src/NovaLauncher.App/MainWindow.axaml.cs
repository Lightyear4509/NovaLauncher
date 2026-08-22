using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;
using NovaLauncher.Application;

namespace NovaLauncher.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Avalonia Window has no IDisposable lifecycle; the token source is cancelled and disposed in Closed.")]
public sealed partial class MainWindow : Window
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    public MainWindow() => InitializeComponent();

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OnOpened(object? sender, EventArgs e) => await ExecuteAsync(() => ViewModel.Workspace!.InitializeAsync(_lifetimeCancellation.Token));

    private void OnClosed(object? sender, EventArgs e) { _lifetimeCancellation.Cancel(); _lifetimeCancellation.Dispose(); }
    private void OnShowHome(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Home");
    private void OnShowLibrary(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Library");
    private void OnShowSaves(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Saves");
    private void OnShowSettings(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Settings");
    private void OnNavigateBack(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateBack();
    private void OnNavigateForward(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateForward();
    private void OnToggleNavigation(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.ToggleNavigation();
    private void OnClearSearch(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.SearchText = string.Empty;

    private async Task ExecuteAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception) { ViewModel.Workspace!.ReportUnexpectedFailure(exception); }
    }
}
