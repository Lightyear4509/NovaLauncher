using System.Net;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Domain.Library;
using NovaLauncher.Infrastructure.Enrichment;
using NovaLauncher.Infrastructure.Persistence;
using SkiaSharp;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class ManagedArtworkMaterializerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"NovaLauncher-Artwork-{Guid.NewGuid():N}");

    [Fact]
    public async Task ValidImageIsDecodedStoredUnderGeneratedNameAndResolved()
    {
        var bytes = CreatePng();
        var materializer = new ManagedArtworkMaterializer(
            new RouteHttp(new Dictionary<string, BoundedHttpResult>
            {
                ["https://example.test/user-supplied-name.exe"] = new(HttpStatusCode.OK, bytes, false, null, "image/png"),
            }),
            new PhysicalAtomicFileSystem(),
            _root);
        var candidate = Candidate("https://example.test/user-supplied-name.exe");

        var result = await materializer.MaterializeAsync(new GameId(Guid.NewGuid()), [candidate], CancellationToken.None);

        var accepted = Assert.Single(result.Candidates);
        Assert.StartsWith("managed-artwork:///", accepted.Location, StringComparison.Ordinal);
        Assert.DoesNotContain("user-supplied-name", accepted.Location, StringComparison.Ordinal);
        Assert.True(materializer.TryResolve(accepted.Location, out var path));
        Assert.True(File.Exists(path));
        Assert.Equal(bytes, await File.ReadAllBytesAsync(path));
        Assert.Empty(result.Failures);
    }

    [Theory]
    [InlineData("text/html")]
    [InlineData("image/jpeg")]
    public async Task WrongContentTypeOrSignatureIsRejectedWithoutWriting(string contentType)
    {
        var materializer = Create(CreatePng(), contentType);

        var result = await materializer.MaterializeAsync(new GameId(Guid.NewGuid()), [Candidate()], CancellationToken.None);

        Assert.Empty(result.Candidates);
        Assert.Single(result.Failures);
        Assert.Empty(Directory.Exists(_root) ? Directory.GetFiles(_root) : []);
    }

    [Fact]
    public async Task CorruptImageAndUnsafeUrlFailPerItemWhileValidCandidateSurvives()
    {
        var http = new RouteHttp(new Dictionary<string, BoundedHttpResult>
        {
            ["https://example.test/bad"] = new(HttpStatusCode.OK, [1, 2, 3], false, null, "image/png"),
            ["https://example.test/good"] = new(HttpStatusCode.OK, CreatePng(), false, null, "image/png"),
        });
        var materializer = new ManagedArtworkMaterializer(http, new PhysicalAtomicFileSystem(), _root);

        var result = await materializer.MaterializeAsync(
            new GameId(Guid.NewGuid()),
            [Candidate("http://example.test/unsafe"), Candidate("https://example.test/bad"), Candidate("https://example.test/good")],
            CancellationToken.None);

        Assert.Single(result.Candidates);
        Assert.Equal(2, result.Failures.Count);
    }

    [Fact]
    public async Task RollbackAndCleanupDeleteOnlyManagedNonManualFiles()
    {
        var materializer = Create(CreatePng(), "image/png");
        var gameId = new GameId(Guid.NewGuid());
        var first = await materializer.MaterializeAsync(gameId, [Candidate()], CancellationToken.None);
        Assert.True(materializer.TryResolve(first.Candidates[0].Location, out var path));

        await materializer.RollbackAsync(first.CreatedFiles, CancellationToken.None);
        Assert.False(File.Exists(path));

        var second = await materializer.MaterializeAsync(gameId, [Candidate()], CancellationToken.None);
        var provenance = new MetadataProvenance("Steam", null, DateTimeOffset.UtcNow, false);
        var oldReference = new ArtworkReference(ArtworkKind.Cover, second.Candidates[0].Location, provenance, false);
        var placeholder = new ArtworkReference(ArtworkKind.Cover, "placeholder://cover", provenance, true);
        await materializer.CleanupObsoleteAsync(
            new GameArtwork(oldReference, placeholder, placeholder, placeholder),
            new GameArtwork(placeholder, placeholder, placeholder, placeholder),
            CancellationToken.None);
        Assert.True(materializer.TryResolve(oldReference.Location, out path));
        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task CancellationIsPropagatedBeforeNetworkOrDiskMutation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var materializer = Create(CreatePng(), "image/png");

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            materializer.MaterializeAsync(new GameId(Guid.NewGuid()), [Candidate()], cancellation.Token));
        Assert.Empty(Directory.Exists(_root) ? Directory.GetFiles(_root) : []);
    }

    [Fact]
    public async Task ManualCoverIsValidatedCopiedToManagedStorageAndRemovable()
    {
        Directory.CreateDirectory(_root);
        var source = Path.Combine(_root, "chosen.png");
        await File.WriteAllBytesAsync(source, CreatePng());
        var materializer = Create(CreatePng(), "image/png");

        var result = await materializer.ImportAsync(new GameId(Guid.NewGuid()), source, CancellationToken.None);

        Assert.Null(result.Error);
        Assert.NotNull(result.Cover);
        Assert.Equal("Manual", result.Cover.Provenance.Source);
        Assert.True(materializer.TryResolve(result.Cover.Location, out var managedPath));
        Assert.NotEqual(source, managedPath);
        Assert.True(File.Exists(managedPath));
        await materializer.DeleteManagedAsync(result.Cover.Location, CancellationToken.None);
        Assert.False(File.Exists(managedPath));
        Assert.True(File.Exists(source));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private ManagedArtworkMaterializer Create(byte[] bytes, string contentType) =>
        new(new RouteHttp(new Dictionary<string, BoundedHttpResult>
        {
            ["https://example.test/art"] = new(HttpStatusCode.OK, bytes, false, null, contentType),
        }), new PhysicalAtomicFileSystem(), _root);

    private static ArtworkCandidate Candidate(string location = "https://example.test/art") =>
        new(ArtworkKind.Cover, location, "Test", "1", DateTimeOffset.UtcNow);

    private static byte[] CreatePng()
    {
        using var bitmap = new SKBitmap(8, 8);
        bitmap.Erase(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private sealed class RouteHttp(IReadOnlyDictionary<string, BoundedHttpResult> responses) : IBoundedHttpClient
    {
        public Task<BoundedHttpResult> GetAsync(Uri uri, IReadOnlyDictionary<string, string>? headers, int maximumBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responses[uri.AbsoluteUri]);
        }
    }
}
