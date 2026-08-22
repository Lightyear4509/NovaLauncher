using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using NovaLauncher.Domain.Library;
using System.ComponentModel;
using Avalonia.Threading;
using NovaLauncher.Application;

namespace NovaLauncher.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Avalonia Window has no IDisposable lifecycle; the token source is cancelled and disposed in Closed.")]
public sealed partial class MainWindow : Window
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnWindowKeyDown;
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OnOpened(object? sender, EventArgs e)
    {
        ViewModel.Workspace!.PropertyChanged += OnWorkspacePropertyChanged;
        await ExecuteAsync(() => ViewModel.Workspace.InitializeAsync(_lifetimeCancellation.Token));
        UpdateControllerWindowState();
    }

    private void OnClosed(object? sender, EventArgs e) { if (ViewModel.Workspace is not null) ViewModel.Workspace.PropertyChanged -= OnWorkspacePropertyChanged; _lifetimeCancellation.Cancel(); _lifetimeCancellation.Dispose(); }
    private void OnShowHome(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Home");
    private void OnShowLibrary(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Library");
    private void OnShowSaves(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Saves");
    private void OnShowSettings(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Settings");
    private void OnNavigateBack(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateBack();
    private void OnNavigateForward(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateForward();
    private void OnToggleNavigation(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.ToggleNavigation();
    private void OnClearSearch(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.SearchText = string.Empty;
    private void OnToggleActivityCenter(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.ToggleActivityCenter();
    private void OnClearActivityCenter(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.ClearActivityCenter();
    private async void OnToggleControllerMode(object? sender, RoutedEventArgs e) => await ExecuteAsync(() => ViewModel.Workspace!.ToggleControllerModeAsync(_lifetimeCancellation.Token));
    private async void OnControllerLaunch(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: LibraryItem game }) return;
        ViewModel.Workspace!.SelectedGame = game;
        await ExecuteAsync(() => ViewModel.Workspace.LaunchSelectedAsync(_lifetimeCancellation.Token));
    }

    private async void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11 || (ViewModel.Workspace?.IsControllerMode == true && e.Key == Key.Escape))
        {
            e.Handled = true;
            await ExecuteAsync(() => ViewModel.Workspace!.ToggleControllerModeAsync(_lifetimeCancellation.Token));
        }
        else if (ViewModel.Workspace?.IsControllerMode == true && e.Key is Key.Left or Key.Up or Key.Right or Key.Down)
        {
            e.Handled = FocusManager?.TryMoveFocus(
                e.Key is Key.Left or Key.Up ? NavigationDirection.Previous : NavigationDirection.Next) == true;
        }
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(ViewModel.Workspace.IsControllerMode)) UpdateControllerWindowState(); }

    private void UpdateControllerWindowState()
    {
        var enabled = ViewModel.Workspace?.IsControllerMode == true;
        WindowState = enabled ? WindowState.FullScreen : WindowState.Normal;
        if (enabled) Dispatcher.UIThread.Post(() => this.FindControl<Button>("ExitControllerModeButton")?.Focus());
    }

    private async Task ExecuteAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception) { ViewModel.Workspace!.ReportUnexpectedFailure(exception); }
    }
}
