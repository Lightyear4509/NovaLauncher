using System.Diagnostics.CodeAnalysis;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using NovaLauncher.Application;
using NovaLauncher.Application.Library;

namespace NovaLauncher.App.Views;

[SuppressMessage(
    "Design",
    "CA1001:Types that own disposable fields should be disposable",
    Justification = "Avalonia controls have no IDisposable lifecycle; the token source is cancelled and disposed when detached.")]
public abstract class NovaPage : UserControl
{
    private readonly CancellationTokenSource _lifetimeCancellation = new();

    protected NovaPage() => Unloaded += OnUnloaded;

    protected MainWindowViewModel ViewModel =>
        DataContext as MainWindowViewModel ?? throw new InvalidOperationException("A NovaLauncher page requires MainWindowViewModel as its data context.");

    protected LibraryWorkspaceViewModel Workspace =>
        ViewModel.Workspace ?? throw new InvalidOperationException("The NovaLauncher workspace is unavailable.");

    protected CancellationToken LifetimeToken => _lifetimeCancellation.Token;

    protected IStorageProvider Storage =>
        TopLevel.GetTopLevel(this)?.StorageProvider ?? throw new InvalidOperationException("The desktop storage provider is unavailable.");

    protected async Task ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation();
        }
        catch (OperationCanceledException) when (_lifetimeCancellation.IsCancellationRequested)
        {
            // Closing the owning visual owns and cancels active UI operations.
        }
        catch (Exception exception)
        {
            Workspace.ReportUnexpectedFailure(exception);
        }
    }

    protected static FilePickerFileType WindowsExecutableFileType { get; } =
        new("Windows executable") { Patterns = ["*.exe"] };

    protected static FilePickerFileType ArtworkFileType { get; } =
        new("Supported artwork") { Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp"] };

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        _lifetimeCancellation.Cancel();
        _lifetimeCancellation.Dispose();
    }
}
