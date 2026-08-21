using System.Net;
using System.Text.Json;
using NovaLauncher.Application.Enrichment;

namespace NovaLauncher.Infrastructure.Enrichment;

public sealed class SteamGridDbIdentitySearchProvider(
    IBoundedHttpClient httpClient,
    IApiKeySession apiKeys) : IGameIdentitySearchProvider
{
    public string Id => "SteamGridDB";

    public async Task<(IReadOnlyList<GameIdentityCandidate> Candidates, string? Error)> SearchAsync(
        string normalizedQuery,
        string displayQuery,
        CancellationToken cancellationToken)
    {
        var key = apiKeys.GetSteamGridDbKey();
        if (string.IsNullOrWhiteSpace(key)) return ([], null);
        var escaped = Uri.EscapeDataString(displayQuery);
        var response = await httpClient.GetAsync(
            new Uri($"https://www.steamgriddb.com/api/v2/search/autocomplete/{escaped}"),
            new Dictionary<string, string> { ["Authorization"] = $"Bearer {key}" },
            1024 * 1024,
            cancellationToken).ConfigureAwait(false);
        if (response.Content is null)
            return ([], response.IsOffline ? "Search is offline." : response.StatusCode == HttpStatusCode.TooManyRequests ? "Search is rate limited." : response.Error ?? "Search failed.");
        if (response.ContentType is not null && !string.Equals(response.ContentType, "application/json", StringComparison.OrdinalIgnoreCase))
            return ([], "Search returned an unexpected content type.");
        try
        {
            using var document = JsonDocument.Parse(response.Content, new JsonDocumentOptions { MaxDepth = 16 });
            if (!document.RootElement.TryGetProperty("success", out var success) || success.ValueKind != JsonValueKind.True ||
                !document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
                return ([], "Search returned an invalid envelope.");
            var results = new List<GameIdentityCandidate>();
            foreach (var item in data.EnumerateArray().Take(20))
            {
                if (IsFiltered(item) || !item.TryGetProperty("id", out var id) || !id.TryGetInt64(out var providerId) || providerId <= 0 ||
                    !item.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String) continue;
                var name = nameElement.GetString()?.Trim();
                if (string.IsNullOrWhiteSpace(name) || name.Length > 500) continue;
                int? year = null;
                if (item.TryGetProperty("release_date", out var release) && release.ValueKind == JsonValueKind.Number && release.TryGetInt64(out var seconds))
                {
                    try { year = DateTimeOffset.FromUnixTimeSeconds(seconds).Year; } catch (ArgumentOutOfRangeException) { }
                }
                results.Add(new("SteamGridDB", providerId.ToString(System.Globalization.CultureInfo.InvariantCulture), name, year, null,
                    GameIdentityService.Normalize(name) == normalizedQuery ? "Exact normalized SteamGridDB title" : "SteamGridDB title candidate"));
            }
            return (results, null);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return ([], $"Search returned malformed data: {exception.Message}");
        }
    }

    private static bool IsFiltered(JsonElement item) =>
        IsTrue(item, "adult") || IsTrue(item, "epilepsy") || IsTrue(item, "humor");

    private static bool IsTrue(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;
}
