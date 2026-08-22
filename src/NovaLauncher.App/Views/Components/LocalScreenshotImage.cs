using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using NovaLauncher.Application.Library;

namespace NovaLauncher.App.Views.Components;

public sealed class LocalScreenshotImage : Image
{
    private const long MaximumScreenshotBytes = 25 * 1024 * 1024;
    private Bitmap? _bitmap;

    public LocalScreenshotImage()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e) => LoadScreenshot();

    private void OnUnloaded(object? sender, RoutedEventArgs e) => ClearScreenshot();

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (IsLoaded) LoadScreenshot();
    }

    private void LoadScreenshot()
    {
        ClearScreenshot();
        if (DataContext is not ScreenshotGalleryItem item) return;
        try
        {
            using var stream = new FileStream(item.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length is <= 0 or > MaximumScreenshotBytes) return;
            _bitmap = Bitmap.DecodeToWidth(stream, 480, BitmapInterpolationMode.HighQuality);
            Source = _bitmap;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            // A user-owned screenshot may disappear or change while the gallery is open.
        }
    }

    private void ClearScreenshot()
    {
        Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
