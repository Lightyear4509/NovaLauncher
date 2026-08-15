using System.Security.Cryptography;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Domain.Library;
using NovaLauncher.Infrastructure.Persistence;
using SkiaSharp;

namespace NovaLauncher.Infrastructure.Enrichment;

public sealed class ManagedArtworkMaterializer(
    IBoundedHttpClient httpClient,
    IAtomicFileSystem fileSystem,
    string cacheRoot) : IArtworkMaterializer, IManualCoverService
{
    public const int MaximumEncodedBytes = 8 * 1024 * 1024;
    public const int MaximumDimension = 8_192;
    public const long MaximumPixels = 16_000_000;
    private readonly string _cacheRoot = Path.GetFullPath(cacheRoot);

    public async Task<ArtworkMaterializationResult> MaterializeAsync(
        GameId gameId,
        IReadOnlyList<ArtworkCandidate> candidates,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var accepted = new List<ArtworkCandidate>();
        var created = new List<string>();
        var failures = new List<string>();
        fileSystem.CreateDirectory(_cacheRoot);

        try
        {
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!Uri.TryCreate(candidate.Location, UriKind.Absolute, out var uri) ||
                    !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"{candidate.ProviderId}/{candidate.Kind}: artwork URL was not HTTPS.");
                    continue;
                }

                var response = await httpClient.GetAsync(uri, null, MaximumEncodedBytes, cancellationToken).ConfigureAwait(false);
                if (response.Content is null)
                {
                    failures.Add($"{candidate.ProviderId}/{candidate.Kind}: {response.Error ?? "artwork download failed"}");
                    continue;
                }

                if (!TryValidate(response.Content, response.ContentType, out var extension, out var error))
                {
                    failures.Add($"{candidate.ProviderId}/{candidate.Kind}: {error}");
                    continue;
                }

                var hash = Convert.ToHexString(SHA256.HashData(response.Content)).ToLowerInvariant();
                var fileName = GetManagedFileName(gameId, candidate.Kind, hash, extension);
                var destination = Path.Combine(_cacheRoot, fileName);
                if (!fileSystem.FileExists(destination))
                {
                    var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
                    try
                    {
                        await fileSystem.WriteAllBytesDurableAsync(temporary, response.Content, cancellationToken).ConfigureAwait(false);
                        fileSystem.MoveFile(temporary, destination, overwrite: false);
                        created.Add(destination);
                    }
                    finally
                    {
                        if (fileSystem.FileExists(temporary)) fileSystem.DeleteFile(temporary);
                    }
                }

                accepted.Add(candidate with { Location = $"managed-artwork:///{fileName}" });
            }
        }
        catch
        {
            await RollbackAsync(created, CancellationToken.None).ConfigureAwait(false);
            throw;
        }

        return new ArtworkMaterializationResult(accepted, created, failures);
    }

    public Task RollbackAsync(IReadOnlyList<string> createdFiles, CancellationToken cancellationToken)
    {
        foreach (var path in createdFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsManaged(path) && fileSystem.FileExists(path)) fileSystem.DeleteFile(path);
        }

        return Task.CompletedTask;
    }

    public Task CleanupObsoleteAsync(GameArtwork? previous, GameArtwork current, CancellationToken cancellationToken)
    {
        var retained = References(current).Select(static item => item.Location).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var reference in previous is null ? [] : References(previous))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!reference.Provenance.IsManual && TryResolve(reference.Location, out var path) &&
                !retained.Contains(reference.Location) && fileSystem.FileExists(path))
            {
                fileSystem.DeleteFile(path);
            }
        }

        return Task.CompletedTask;
    }

    public async Task<ManualCoverImportResult> ImportAsync(
        GameId gameId,
        string sourcePath,
        CancellationToken cancellationToken)
    {
        if (!Path.IsPathFullyQualified(sourcePath) || !File.Exists(sourcePath))
            return new(null, null, "Choose an existing local image file.");
        byte[] content;
        try
        {
            await using var stream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length is <= 0 or > MaximumEncodedBytes)
                return new(null, null, "Cover image must be between 1 byte and 8 MiB.");
            content = new byte[(int)stream.Length];
            await stream.ReadExactlyAsync(content, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(null, null, $"Cover image could not be read: {exception.Message}");
        }

        var contentType = Path.GetExtension(sourcePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            _ => null,
        };
        if (!TryValidate(content, contentType, out var extension, out var error)) return new(null, null, error);
        fileSystem.CreateDirectory(_cacheRoot);
        var hash = Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();
        var fileName = GetManagedFileName(gameId, ArtworkKind.Cover, hash, extension);
        var destination = Path.Combine(_cacheRoot, fileName);
        if (!fileSystem.FileExists(destination))
        {
            var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                await fileSystem.WriteAllBytesDurableAsync(temporary, content, cancellationToken).ConfigureAwait(false);
                fileSystem.MoveFile(temporary, destination, overwrite: false);
            }
            finally
            {
                if (fileSystem.FileExists(temporary)) fileSystem.DeleteFile(temporary);
            }
        }

        var provenance = new MetadataProvenance("Manual", null, DateTimeOffset.UtcNow, IsManual: true);
        var reference = new ArtworkReference(ArtworkKind.Cover, $"managed-artwork:///{fileName}", provenance, IsPlaceholder: false);
        return new(reference, destination, null);
    }

    public Task DeleteManagedAsync(string location, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryResolve(location, out var path) && fileSystem.FileExists(path)) fileSystem.DeleteFile(path);
        return Task.CompletedTask;
    }

    private static bool TryValidate(byte[] content, string? contentType, out string extension, out string error)
    {
        extension = string.Empty;
        error = string.Empty;
        var declared = contentType?.Split(';', 2)[0].Trim().ToLowerInvariant();
        if (declared is not ("image/jpeg" or "image/png" or "image/webp"))
        {
            error = "unsupported artwork content type";
            return false;
        }

        using var data = SKData.CreateCopy(content);
        using var codec = SKCodec.Create(data);
        if (codec is null || codec.FrameCount > 1)
        {
            error = "invalid or animated image data";
            return false;
        }

        extension = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Jpeg when declared == "image/jpeg" => ".jpg",
            SKEncodedImageFormat.Png when declared == "image/png" => ".png",
            SKEncodedImageFormat.Webp when declared == "image/webp" => ".webp",
            _ => string.Empty,
        };
        var info = codec.Info;
        if (extension.Length == 0)
        {
            error = "content type did not match the encoded image";
            return false;
        }

        if (info.Width <= 0 || info.Height <= 0 || info.Width > MaximumDimension || info.Height > MaximumDimension ||
            (long)info.Width * info.Height > MaximumPixels)
        {
            error = "decoded image dimensions exceed safety limits";
            return false;
        }

        using var bitmap = new SKBitmap(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul);
        var result = codec.GetPixels(bitmap.Info, bitmap.GetPixels());
        if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
        {
            error = "image pixels could not be decoded safely";
            return false;
        }

        if (result == SKCodecResult.IncompleteInput)
        {
            error = "image data was truncated";
            return false;
        }

        return true;
    }

    private static string GetManagedFileName(GameId gameId, ArtworkKind kind, string hash, string extension) =>
        $"{gameId}-{kind.ToString().ToLowerInvariant()}-{hash[..16]}{extension}";

    public bool TryResolve(string location, out string path)
    {
        path = string.Empty;
        if (!Uri.TryCreate(location, UriKind.Absolute, out var uri) || uri.Scheme != "managed-artwork" ||
            !string.IsNullOrEmpty(uri.Host)) return false;
        var fileName = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        if (fileName.Length == 0 || fileName != Path.GetFileName(fileName)) return false;
        var candidate = Path.Combine(_cacheRoot, fileName);
        if (!IsManaged(candidate)) return false;
        path = candidate;
        return true;
    }

    private bool IsManaged(string path)
    {
        if (!Path.IsPathFullyQualified(path)) return false;
        var fullPath = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(_cacheRoot, fullPath);
        return relative != ".." && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !Path.IsPathFullyQualified(relative);
    }

    private static IEnumerable<ArtworkReference> References(GameArtwork artwork)
    {
        yield return artwork.Cover;
        yield return artwork.Hero;
        yield return artwork.Logo;
        yield return artwork.Background;
    }
}
