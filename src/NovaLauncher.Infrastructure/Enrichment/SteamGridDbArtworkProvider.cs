using System.Globalization;
using System.Net;
using System.Text.Json;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Infrastructure.Enrichment;

public sealed class SteamGridDbArtworkProvider : IArtworkProvider
{
    private readonly IBoundedHttpClient _httpClient;
    private readonly TimeProvider _timeProvider;
    private readonly Func<string?> _getApiKey;

    public SteamGridDbArtworkProvider(IBoundedHttpClient httpClient, TimeProvider timeProvider, string? apiKey)
        : this(httpClient, timeProvider, () => apiKey) { }

    public static SteamGridDbArtworkProvider FromSession(
        IBoundedHttpClient httpClient,
        TimeProvider timeProvider,
        IApiKeySession apiKeys) => new(httpClient, timeProvider, apiKeys.GetSteamGridDbKey);

    private SteamGridDbArtworkProvider(IBoundedHttpClient httpClient, TimeProvider timeProvider, Func<string?> getApiKey)
    {
        _httpClient = httpClient;
        _timeProvider = timeProvider;
        _getApiKey = getApiKey;
    }

    public string Id => "SteamGridDB";

    public int Priority => 10;

    public bool CanHandle(MetadataRequest request) =>
        !string.IsNullOrWhiteSpace(_getApiKey()) &&
        string.Equals(request.Source, "Steam", StringComparison.OrdinalIgnoreCase) &&
        uint.TryParse(request.SourceItemId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) && appId > 0;

    public async Task<ArtworkProviderResult> GetArtworkAsync(MetadataRequest request, CancellationToken cancellationToken)
    {
        if (!CanHandle(request))
        {
            return new ArtworkProviderResult(Id, ProviderResultStatus.NoData, [], null);
        }

        var uri = new Uri($"https://www.steamgriddb.com/api/v2/grids/steam/{request.SourceItemId}?dimensions=600x900&types=static");
        var apiKey = _getApiKey();
        var response = await _httpClient.GetAsync(
            uri,
            new Dictionary<string, string> { ["Authorization"] = $"Bearer {apiKey}" },
            1024 * 1024,
            cancellationToken).ConfigureAwait(false);
        if (response.Content is null)
        {
            return new ArtworkProviderResult(
                Id,
                response.IsOffline ? ProviderResultStatus.Offline : response.StatusCode == HttpStatusCode.TooManyRequests ? ProviderResultStatus.RateLimited : ProviderResultStatus.Failed,
                [],
                response.Error);
        }

        if (response.ContentType is not null &&
            !string.Equals(response.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
        {
            return new ArtworkProviderResult(Id, ProviderResultStatus.InvalidResponse, [], "SteamGridDB returned an unexpected content type.");
        }

        try
        {
            using var document = JsonDocument.Parse(response.Content, new JsonDocumentOptions { MaxDepth = 16 });
            if (!document.RootElement.TryGetProperty("success", out var success) || !success.GetBoolean() ||
                !document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            {
                return new ArtworkProviderResult(Id, ProviderResultStatus.InvalidResponse, [], "SteamGridDB returned an invalid envelope.");
            }

            var candidates = data.EnumerateArray()
                .Take(20)
                .Where(static item => item.TryGetProperty("url", out var url) && url.ValueKind == JsonValueKind.String)
                .Select(item => item.GetProperty("url").GetString())
                .Where(static value => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
                .Select(value => new ArtworkCandidate(ArtworkKind.Cover, value!, Id, request.SourceItemId, _timeProvider.GetUtcNow()))
                .Take(1)
                .ToArray();
            return new ArtworkProviderResult(Id, candidates.Length > 0 ? ProviderResultStatus.Success : ProviderResultStatus.NoData, candidates, null);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return new ArtworkProviderResult(Id, ProviderResultStatus.InvalidResponse, [], exception.Message);
        }
    }
}
