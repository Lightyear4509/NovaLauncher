using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using NovaLauncher.Domain.Library;
using System.ComponentModel;
using Avalonia.Threading;
using NovaLauncher.Application;
using NovaLauncher.Application.Input;
using Avalonia.VisualTree;
using Avalonia.Controls.ApplicationLifetimes;

namespace NovaLauncher.App;

[SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "Avalonia Window has no IDisposable lifecycle; the token source is cancelled and disposed in Closed.")]
public sealed partial class MainWindow : Window
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();
    private readonly DispatcherTimer _controllerPollTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private IControllerInputService? _controllerInput;
    private readonly ControllerNavigationState _controllerNavigation = new();
    private bool _controllerWasConnected;
    private TrayIcon? _trayIcon;
    private bool _forceClose;

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnWindowKeyDown;
        Closing += OnClosing;
        _controllerPollTimer.Tick += OnControllerPoll;
    }

    public void AttachControllerInput(IControllerInputService controllerInput) =>
        _controllerInput = controllerInput ?? throw new ArgumentNullException(nameof(controllerInput));

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext!;

    private async void OnOpened(object? sender, EventArgs e)
    {
        ViewModel.Workspace!.PropertyChanged += OnWorkspacePropertyChanged;
        await ExecuteAsync(() => ViewModel.Workspace.InitializeAsync(_lifetimeCancellation.Token));
        UpdateControllerWindowState();
        UpdateTrayBehavior();
        if (ViewModel.Workspace.MinimizeToTray &&
            Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { Args: { } args } &&
            args.Contains("--background", StringComparer.Ordinal))
            Hide();
    }

    private void OnClosed(object? sender, EventArgs e) { _controllerPollTimer.Stop(); _trayIcon?.Dispose(); if (ViewModel.Workspace is not null) ViewModel.Workspace.PropertyChanged -= OnWorkspacePropertyChanged; _lifetimeCancellation.Cancel(); _lifetimeCancellation.Dispose(); }
    private void OnShowHome(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Home");
    private void OnShowLibrary(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Library");
    private void OnShowSaves(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Saves");
    private void OnShowDownloads(object? sender, RoutedEventArgs e) => ViewModel.Workspace!.NavigateTo("Downloads");
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
            e.Handled = MoveControllerFocus(e.Key switch
            {
                Key.Left => NavigationDirection.Left,
                Key.Up => NavigationDirection.Up,
                Key.Right => NavigationDirection.Right,
                _ => NavigationDirection.Down,
            });
        }
    }

    private void OnWorkspacePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewModel.Workspace.IsControllerMode)) UpdateControllerWindowState();
        else if (e.PropertyName == nameof(ViewModel.Workspace.MinimizeToTray)) UpdateTrayBehavior();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_forceClose || ViewModel.Workspace?.MinimizeToTray != true) return;
        e.Cancel = true;
        Hide();
    }

    private void UpdateTrayBehavior()
    {
        if (ViewModel.Workspace?.MinimizeToTray == true)
        {
            if (_trayIcon is not null) return;
            var open = new NativeMenuItem("Open NovaLauncher");
            open.Click += (_, _) => { Show(); WindowState = WindowState.Normal; Activate(); };
            var exit = new NativeMenuItem("Exit");
            exit.Click += (_, _) =>
            {
                _forceClose = true;
                if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) desktop.Shutdown();
            };
            var menu = new NativeMenu();
            menu.Items.Add(open);
            menu.Items.Add(exit);
            _trayIcon = new TrayIcon { Icon = Icon, ToolTipText = "NovaLauncher", Menu = menu, IsVisible = true };
            TrayIcon.SetIcons(Avalonia.Application.Current!, new TrayIcons { _trayIcon });
        }
        else if (_trayIcon is not null)
        {
            TrayIcon.SetIcons(Avalonia.Application.Current!, new TrayIcons());
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }

    private void UpdateControllerWindowState()
    {
        var enabled = ViewModel.Workspace?.IsControllerMode == true;
        WindowState = enabled ? WindowState.FullScreen : WindowState.Normal;
        _controllerNavigation.Reset();
        if (enabled)
        {
            PollController();
            _controllerPollTimer.Start();
            Dispatcher.UIThread.Post(() => this.FindControl<Button>("ControllerHomeButton")?.Focus());
        }
        else
        {
            _controllerPollTimer.Stop();
            _controllerWasConnected = false;
        }
    }

    private void OnControllerPoll(object? sender, EventArgs e) => PollController();

    private async void PollController()
    {
        if (ViewModel.Workspace?.IsControllerMode != true || _controllerInput is null) return;
        if (!_controllerInput.TryGetState(out var state))
        {
            if (_controllerWasConnected || ViewModel.Workspace.ControllerConnectionStatus.StartsWith("Controller input", StringComparison.Ordinal))
                ViewModel.Workspace.ReportControllerConnection(false, 0, _controllerInput.BackendName);
            _controllerWasConnected = false;
            _controllerNavigation.Reset();
            return;
        }

        if (!_controllerWasConnected)
            ViewModel.Workspace.ReportControllerConnection(true, state.ControllerIndex, _controllerInput.BackendName);
        _controllerWasConnected = true;
        var pressed = _controllerNavigation.Update(state.Buttons, DateTimeOffset.UtcNow);

        if ((pressed & ControllerButtons.Back) != 0)
        {
            if (ViewModel.Workspace.CanNavigateBack)
            {
                ViewModel.Workspace.NavigateBack();
                FocusCurrentControllerPageButton();
            }
            else await ExecuteAsync(() => ViewModel.Workspace.ToggleControllerModeAsync(_lifetimeCancellation.Token));
            return;
        }
        if ((pressed & ControllerButtons.Previous) != 0 && ViewModel.Workspace.CanNavigateBack)
        {
            ViewModel.Workspace.NavigateBack();
            FocusCurrentControllerPageButton();
        }
        if ((pressed & ControllerButtons.Next) != 0 && ViewModel.Workspace.CanNavigateForward)
        {
            ViewModel.Workspace.NavigateForward();
            FocusCurrentControllerPageButton();
        }

        var direction = (pressed & (ControllerButtons.Left | ControllerButtons.Up | ControllerButtons.Right | ControllerButtons.Down)) switch
        {
            var value when (value & ControllerButtons.Left) != 0 => NavigationDirection.Left,
            var value when (value & ControllerButtons.Up) != 0 => NavigationDirection.Up,
            var value when (value & ControllerButtons.Right) != 0 => NavigationDirection.Right,
            var value when (value & ControllerButtons.Down) != 0 => NavigationDirection.Down,
            _ => (NavigationDirection?)null,
        };
        if (direction is { } navigation) MoveControllerFocus(navigation);
        if ((pressed & ControllerButtons.Primary) != 0 && FocusManager?.GetFocusedElement() is Button button)
            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        else if ((pressed & ControllerButtons.Primary) != 0 && FocusManager?.GetFocusedElement() is TextBox)
            ViewModel.Workspace.ReportControllerTextEntryHandoff();
        if ((pressed & ControllerButtons.Context) != 0 && FocusManager?.GetFocusedElement() is Control { ContextMenu: { } menu } control)
            menu.Open(control);
    }

    private bool MoveControllerFocus(NavigationDirection direction)
    {
        if (FocusManager is null) return false;
        if (FocusManager.GetFocusedElement() is not Control { IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
        {
            FocusCurrentControllerPageButton();
            return true;
        }

        var moved = FocusManager.TryMoveFocus(direction);
        if (moved && FocusManager.GetFocusedElement() is Control focused) focused.BringIntoView();
        return moved;
    }

    private void FocusCurrentControllerPageButton() => Dispatcher.UIThread.Post(() =>
    {
        var buttonName = ViewModel.Workspace?.CurrentPage switch
        {
            "Library" => "ControllerLibraryButton",
            "Saves" => "ControllerSavesButton",
            _ => "ControllerHomeButton",
        };
        this.FindControl<Button>(buttonName)?.Focus();
    });

    private async Task ExecuteAsync(Func<Task> operation)
    {
        try { await operation(); }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested) { }
        catch (Exception exception) { ViewModel.Workspace!.ReportUnexpectedFailure(exception); }
    }
}
