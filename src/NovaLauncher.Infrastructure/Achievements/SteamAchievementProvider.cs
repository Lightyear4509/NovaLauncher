using System.Globalization;
using System.Net;
using System.Text.Json;
using NovaLauncher.Application.Achievements;
using NovaLauncher.Domain.Achievements;
using NovaLauncher.Infrastructure.Enrichment;

namespace NovaLauncher.Infrastructure.Achievements;

public sealed class SteamAchievementProvider(
    IBoundedHttpClient httpClient,
    TimeProvider timeProvider,
    string? apiKey,
    string? steamId) : IAchievementProvider
{
    private const int MaximumResponseBytes = 2 * 1024 * 1024;
    private const int MaximumAchievements = 2_000;

    public string Id => "Steam";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(apiKey) && ulong.TryParse(steamId, NumberStyles.None, CultureInfo.InvariantCulture, out _);

    public bool CanHandle(AchievementRequest request) =>
        string.Equals(request.Source, "Steam", StringComparison.OrdinalIgnoreCase) &&
        uint.TryParse(request.SourceItemId, NumberStyles.None, CultureInfo.InvariantCulture, out var appId) && appId > 0;

    public async Task<AchievementProviderResult> GetAsync(AchievementRequest request, CancellationToken cancellationToken)
    {
        if (!IsConfigured || !CanHandle(request))
        {
            return new(AchievementRefreshStatus.Unavailable, null, "Steam achievements are not configured for this game.");
        }

        var appId = request.SourceItemId!;
        var escapedKey = Uri.EscapeDataString(apiKey!);
        var escapedSteamId = Uri.EscapeDataString(steamId!);
        var playerUri = new Uri($"https://partner.steam-api.com/ISteamUserStats/GetPlayerAchievements/v1/?key={escapedKey}&steamid={escapedSteamId}&appid={appId}&l=english");
        var schemaUri = new Uri($"https://partner.steam-api.com/ISteamUserStats/GetSchemaForGame/v2/?key={escapedKey}&appid={appId}&l=english");
        var player = await httpClient.GetAsync(playerUri, null, MaximumResponseBytes, cancellationToken).ConfigureAwait(false);
        var playerFailure = Classify(player);
        if (playerFailure is not null) return playerFailure;
        var schema = await httpClient.GetAsync(schemaUri, null, MaximumResponseBytes, cancellationToken).ConfigureAwait(false);
        var schemaFailure = Classify(schema);
        if (schemaFailure is not null) return schemaFailure;

        try
        {
            var options = new JsonDocumentOptions { MaxDepth = 32, CommentHandling = JsonCommentHandling.Disallow };
            using var playerJson = JsonDocument.Parse(player.Content!, options);
            using var schemaJson = JsonDocument.Parse(schema.Content!, options);
            var playerStats = playerJson.RootElement.GetProperty("playerstats");
            if (playerStats.TryGetProperty("success", out var success) && !success.GetBoolean())
            {
                return new(AchievementRefreshStatus.Unavailable, null, "Steam achievement data is unavailable or private.");
            }

            if (!playerStats.TryGetProperty("achievements", out var unlockedArray) || unlockedArray.ValueKind != JsonValueKind.Array)
            {
                return new(AchievementRefreshStatus.Unavailable, null, "This game exposes no Steam achievements.");
            }

            var names = ReadSchema(schemaJson.RootElement);
            var items = new List<Achievement>();
            foreach (var item in unlockedArray.EnumerateArray().Take(MaximumAchievements + 1))
            {
                if (items.Count == MaximumAchievements)
                {
                    return new(AchievementRefreshStatus.InvalidResponse, null, "Steam returned too many achievements.");
                }

                var apiName = ReadBounded(item, "apiname", 256);
                if (apiName is null) continue;
                var isUnlocked = item.TryGetProperty("achieved", out var achieved) && achieved.TryGetInt32(out var achievedValue) && achievedValue == 1;
                DateTimeOffset? unlockedAt = null;
                if (isUnlocked && item.TryGetProperty("unlocktime", out var time) && time.TryGetInt64(out var seconds) && seconds > 0)
                {
                    try { unlockedAt = DateTimeOffset.FromUnixTimeSeconds(seconds); }
                    catch (ArgumentOutOfRangeException) { return new(AchievementRefreshStatus.InvalidResponse, null, "Steam returned an invalid achievement time."); }
                }

                names.TryGetValue(apiName, out var definition);
                items.Add(new Achievement(
                    new AchievementId(Id, apiName),
                    definition?.Name ?? apiName,
                    definition?.Description ?? string.Empty,
                    isUnlocked,
                    unlockedAt,
                    Id));
            }

            var snapshot = new GameAchievements(request.GameId, Id, items, timeProvider.GetUtcNow(), false);
            return new(AchievementRefreshStatus.Success, snapshot, null);
        }
        catch (Exception exception) when (exception is JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return new(AchievementRefreshStatus.InvalidResponse, null, "Steam returned malformed achievement data.");
        }
    }

    private static AchievementProviderResult? Classify(BoundedHttpResult response)
    {
        if (response.Content is not null && string.Equals(response.ContentType, "application/json", StringComparison.OrdinalIgnoreCase)) return null;
        if (response.IsOffline) return new(AchievementRefreshStatus.Offline, null, "Steam achievements are offline.");
        if (response.StatusCode == HttpStatusCode.TooManyRequests) return new(AchievementRefreshStatus.RateLimited, null, "Steam achievement requests are rate limited.");
        if (response.Content is not null) return new(AchievementRefreshStatus.InvalidResponse, null, "Steam returned an unexpected achievement content type.");
        return new(AchievementRefreshStatus.Failed, null, "Steam achievement request failed.");
    }

    private static Dictionary<string, Definition> ReadSchema(JsonElement root)
    {
        var result = new Dictionary<string, Definition>(StringComparer.Ordinal);
        if (!root.TryGetProperty("game", out var game) || !game.TryGetProperty("availableGameStats", out var stats) ||
            !stats.TryGetProperty("achievements", out var array) || array.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in array.EnumerateArray().Take(MaximumAchievements))
        {
            var apiName = ReadBounded(item, "name", 256);
            if (apiName is null) continue;
            result[apiName] = new Definition(ReadBounded(item, "displayName", 500) ?? apiName, ReadBounded(item, "description", 2_000) ?? string.Empty);
        }

        return result;
    }

    private static string? ReadBounded(JsonElement element, string property, int maximumLength)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString()?.Trim();
        return string.IsNullOrEmpty(text) || text.Length > maximumLength ? null : text;
    }

    private sealed record Definition(string Name, string Description);
}
