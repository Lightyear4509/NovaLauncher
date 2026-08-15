using System.Globalization;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Infrastructure.Enrichment;

public sealed class SteamArtworkProvider(TimeProvider timeProvider) : IArtworkProvider
{
    public string Id => "Steam CDN";

    public int Priority => 100;

    public bool CanHandle(MetadataRequest request) =>
        string.Equals(request.Source, "Steam", StringComparison.OrdinalIgnoreCase) &&
        uint.TryParse(request.SourceItemId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) && appId > 0;

    public Task<ArtworkProviderResult> GetArtworkAsync(MetadataRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!CanHandle(request))
        {
            return Task.FromResult(new ArtworkProviderResult(Id, ProviderResultStatus.NoData, [], null));
        }

        var appId = request.SourceItemId!;
        var root = $"https://cdn.cloudflare.steamstatic.com/steam/apps/{appId}";
        var now = timeProvider.GetUtcNow();
        IReadOnlyList<ArtworkCandidate> candidates =
        [
            new(ArtworkKind.Cover, $"{root}/library_600x900.jpg", Id, appId, now),
            new(ArtworkKind.Hero, $"{root}/library_hero.jpg", Id, appId, now),
            new(ArtworkKind.Logo, $"{root}/logo.png", Id, appId, now),
            new(ArtworkKind.Background, $"{root}/page_bg_generated_v6b.jpg", Id, appId, now),
        ];
        return Task.FromResult(new ArtworkProviderResult(Id, ProviderResultStatus.Success, candidates, null));
    }
}
