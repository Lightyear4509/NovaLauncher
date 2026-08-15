using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using NovaLauncher.Application.Enrichment;

namespace NovaLauncher.Infrastructure.Enrichment;

public sealed partial class SteamMetadataProvider(IBoundedHttpClient httpClient, TimeProvider timeProvider) : IMetadataProvider
{
    private const int MaximumResponseBytes = 2 * 1024 * 1024;

    public string Id => "Steam";

    public int Priority => 100;

    public bool CanHandle(MetadataRequest request) =>
        string.Equals(request.Source, "Steam", StringComparison.OrdinalIgnoreCase) &&
        uint.TryParse(request.SourceItemId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) && appId > 0;

    public async Task<MetadataProviderResult> GetMetadataAsync(MetadataRequest request, CancellationToken cancellationToken)
    {
        if (!CanHandle(request))
        {
            return new MetadataProviderResult(Id, ProviderResultStatus.NoData, null, null);
        }

        var appId = uint.Parse(request.SourceItemId!, CultureInfo.InvariantCulture);
        var uri = new Uri($"https://store.steampowered.com/api/appdetails?appids={appId}&l=english&cc=us");
        var response = await httpClient.GetAsync(uri, null, MaximumResponseBytes, cancellationToken).ConfigureAwait(false);
        if (response.Content is null)
        {
            return new MetadataProviderResult(
                Id,
                response.IsOffline ? ProviderResultStatus.Offline : ToStatus(response.StatusCode),
                null,
                response.Error);
        }

        if (response.ContentType is not null &&
            !string.Equals(response.ContentType, "application/json", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(response.ContentType, "text/javascript", StringComparison.OrdinalIgnoreCase))
        {
            return new MetadataProviderResult(Id, ProviderResultStatus.InvalidResponse, null, "Steam returned an unexpected content type.");
        }

        try
        {
            using var document = JsonDocument.Parse(response.Content, new JsonDocumentOptions { MaxDepth = 32 });
            if (!document.RootElement.TryGetProperty(appId.ToString(CultureInfo.InvariantCulture), out var envelope) ||
                !envelope.TryGetProperty("success", out var success) || !success.GetBoolean() ||
                !envelope.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
            {
                return new MetadataProviderResult(Id, ProviderResultStatus.NoData, null, null);
            }

            var snapshot = new MetadataSnapshot(
                Id,
                appId.ToString(CultureInfo.InvariantCulture),
                ReadDescription(data),
                ReadDescriptionArray(data, "genres"),
                ReadStringArray(data, "developers"),
                ReadStringArray(data, "publishers"),
                ReadReleaseDate(data),
                ReadRating(data),
                timeProvider.GetUtcNow());
            return new MetadataProviderResult(Id, ProviderResultStatus.Success, snapshot, null);
        }
        catch (JsonException exception)
        {
            return new MetadataProviderResult(Id, ProviderResultStatus.InvalidResponse, null, exception.Message);
        }
        catch (InvalidOperationException exception)
        {
            return new MetadataProviderResult(Id, ProviderResultStatus.InvalidResponse, null, exception.Message);
        }
    }

    private static string? ReadDescription(JsonElement data)
    {
        if (!data.TryGetProperty("short_description", out var value) || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var decoded = WebUtility.HtmlDecode(HtmlTagRegex().Replace(value.GetString() ?? string.Empty, " "));
        var normalized = WhitespaceRegex().Replace(decoded, " ").Trim();
        return normalized.Length switch { 0 => null, > 10_000 => normalized[..10_000], _ => normalized };
    }

    private static string[]? ReadStringArray(JsonElement data, string property)
    {
        if (!data.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return array.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString()?.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value) && value.Length <= 256)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();
    }

    private static string[]? ReadDescriptionArray(JsonElement data, string property)
    {
        if (!data.TryGetProperty(property, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        return array.EnumerateArray()
            .Where(static item => item.ValueKind == JsonValueKind.Object && item.TryGetProperty("description", out _))
            .Select(static item => item.GetProperty("description"))
            .Where(static item => item.ValueKind == JsonValueKind.String)
            .Select(static item => item.GetString()?.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value) && value.Length <= 256)
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToArray();
    }

    private static DateOnly? ReadReleaseDate(JsonElement data)
    {
        if (!data.TryGetProperty("release_date", out var release) ||
            !release.TryGetProperty("date", out var date) || date.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return DateOnly.TryParse(date.GetString(), CultureInfo.GetCultureInfo("en-US"), DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed
            : null;
    }

    private static decimal? ReadRating(JsonElement data)
    {
        if (!data.TryGetProperty("metacritic", out var metacritic) ||
            !metacritic.TryGetProperty("score", out var score) || !score.TryGetDecimal(out var rating))
        {
            return null;
        }

        return rating is >= 0 and <= 100 ? rating : null;
    }

    private static ProviderResultStatus ToStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.TooManyRequests => ProviderResultStatus.RateLimited,
        >= HttpStatusCode.InternalServerError => ProviderResultStatus.Offline,
        _ => ProviderResultStatus.Failed,
    };

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();
}
