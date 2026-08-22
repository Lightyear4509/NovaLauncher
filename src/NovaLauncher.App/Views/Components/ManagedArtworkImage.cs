using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Microsoft.Extensions.DependencyInjection;
using NovaLauncher.Domain.Library;
using NovaLauncher.Infrastructure.Enrichment;

namespace NovaLauncher.App.Views.Components;

/// <summary>Loads bounded artwork from NovaLauncher's existing managed cache.</summary>
public sealed class ManagedArtworkImage : Image
{
    public static readonly StyledProperty<ArtworkKind> ArtworkKindProperty =
        AvaloniaProperty.Register<ManagedArtworkImage, ArtworkKind>(nameof(ArtworkKind), ArtworkKind.Cover);

    public static readonly StyledProperty<int> DecodeWidthProperty =
        AvaloniaProperty.Register<ManagedArtworkImage, int>(nameof(DecodeWidth), 420);

    private Bitmap? _bitmap;

    public ManagedArtworkImage()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    public ArtworkKind ArtworkKind
    {
        get => GetValue(ArtworkKindProperty);
        set => SetValue(ArtworkKindProperty, value);
    }

    public int DecodeWidth
    {
        get => GetValue(DecodeWidthProperty);
        set => SetValue(DecodeWidthProperty, value);
    }

    private void OnLoaded(object? sender, RoutedEventArgs e) => LoadArtwork();

    private void OnUnloaded(object? sender, RoutedEventArgs e) => ClearArtwork();

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (IsLoaded) LoadArtwork();
    }

    private void LoadArtwork()
    {
        ClearArtwork();
        if (DataContext is not LibraryItem game || DecodeWidth is < 32 or > 2048) return;
        var reference = SelectReference(game);
        var resolver = Program.Services.GetRequiredService<ManagedArtworkMaterializer>();
        if (reference?.IsPlaceholder != false || !resolver.TryResolve(reference.Location, out var path)) return;

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > ManagedArtworkMaterializer.MaximumEncodedBytes) return;
            _bitmap = Bitmap.DecodeToWidth(stream, DecodeWidth, BitmapInterpolationMode.HighQuality);
            Source = _bitmap;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Program.Services.GetRequiredService<Application.MainWindowViewModel>().Workspace?.ReportArtworkUnavailable();
        }
    }

    private ArtworkReference? SelectReference(LibraryItem game) => ArtworkKind switch
    {
        ArtworkKind.Hero when game.Artwork?.Hero.IsPlaceholder == false => game.Artwork.Hero,
        ArtworkKind.Hero => game.Artwork?.Cover,
        ArtworkKind.Logo => game.Artwork?.Logo,
        ArtworkKind.Background => game.Artwork?.Background,
        _ => game.Artwork?.Cover,
    };

    private void ClearArtwork()
    {
        Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
