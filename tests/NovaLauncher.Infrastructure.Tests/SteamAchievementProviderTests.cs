using System.Net;
using System.Text;
using NovaLauncher.Application.Achievements;
using NovaLauncher.Domain.Library;
using NovaLauncher.Infrastructure.Achievements;
using NovaLauncher.Infrastructure.Enrichment;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class SteamAchievementProviderTests
{
    private const string Player = """
        {"playerstats":{"success":true,"achievements":[{"apiname":"FIRST","achieved":1,"unlocktime":1700000000},{"apiname":"LOCKED","achieved":0,"unlocktime":0}]}}
        """;
    private const string Schema = """
        {"game":{"availableGameStats":{"achievements":[{"name":"FIRST","displayName":"First Win","description":"Win once"},{"name":"LOCKED","displayName":"Keep Going","description":"Try again"}]}}}
        """;

    [Fact]
    public async Task CombinesDocumentedPlayerAndSchemaResponsesReadOnly()
    {
        var http = new SequenceHttp(Json(Player), Json(Schema));
        var provider = new SteamAchievementProvider(http, TimeProvider.System, "secret-key", "76561198000000000");

        var result = await provider.GetAsync(Request(), CancellationToken.None);

        Assert.Equal(AchievementRefreshStatus.Success, result.Status);
        Assert.Equal(2, result.Achievements!.Items.Count);
        var unlocked = Assert.Single(result.Achievements.Items, static item => item.IsUnlocked);
        Assert.Equal("First Win", unlocked.Name);
        Assert.NotNull(unlocked.UnlockedAtUtc);
        Assert.All(http.Uris, static uri => Assert.Equal(Uri.UriSchemeHttps, uri.Scheme));
        Assert.All(http.Uris, static uri => Assert.Contains("partner.steam-api.com", uri.Host, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RequiresExplicitConfigurationAndSupportedStableSteamIdentity()
    {
        var provider = new SteamAchievementProvider(new SequenceHttp(), TimeProvider.System, null, null);
        Assert.False(provider.IsConfigured);
        var result = await provider.GetAsync(Request(), CancellationToken.None);
        Assert.Equal(AchievementRefreshStatus.Unavailable, result.Status);
        Assert.False(provider.CanHandle(new AchievementRequest(default, "Manual", null)));
    }

    [Theory]
    [InlineData(429, false, AchievementRefreshStatus.RateLimited)]
    [InlineData(0, true, AchievementRefreshStatus.Offline)]
    [InlineData(400, false, AchievementRefreshStatus.Failed)]
    public async Task ReturnsTypedTransportFailures(int status, bool offline, AchievementRefreshStatus expected)
    {
        var provider = new SteamAchievementProvider(
            new SequenceHttp(new BoundedHttpResult((HttpStatusCode)status, null, offline, "failure", "application/json")),
            TimeProvider.System, "key", "76561198000000000");
        var result = await provider.GetAsync(Request(), CancellationToken.None);
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public async Task RejectsMalformedWrongContentAndPrivateData()
    {
        var malformed = await Provider(Json("{"), Json(Schema)).GetAsync(Request(), CancellationToken.None);
        Assert.Equal(AchievementRefreshStatus.InvalidResponse, malformed.Status);
        var wrong = await Provider(new BoundedHttpResult(HttpStatusCode.OK, Encoding.UTF8.GetBytes(Player), false, null, "text/html")).GetAsync(Request(), CancellationToken.None);
        Assert.Equal(AchievementRefreshStatus.InvalidResponse, wrong.Status);
        var unavailable = await Provider(Json("{\"playerstats\":{\"success\":false}}"), Json(Schema)).GetAsync(Request(), CancellationToken.None);
        Assert.Equal(AchievementRefreshStatus.Unavailable, unavailable.Status);
    }

    [Fact]
    public async Task CancellationPropagatesWithoutFabricatingResults()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(() => Provider(Json(Player)).GetAsync(Request(), cancellation.Token));
    }

    private static AchievementRequest Request() => new(GameId.FromSteamAppId(570), "Steam", "570");
    private static BoundedHttpResult Json(string value) => new(HttpStatusCode.OK, Encoding.UTF8.GetBytes(value), false, null, "application/json");
    private static SteamAchievementProvider Provider(params BoundedHttpResult[] results) =>
        new(new SequenceHttp(results), TimeProvider.System, "key", "76561198000000000");

    private sealed class SequenceHttp(params BoundedHttpResult[] results) : IBoundedHttpClient
    {
        private int _index;
        public List<Uri> Uris { get; } = [];
        public Task<BoundedHttpResult> GetAsync(Uri uri, IReadOnlyDictionary<string, string>? headers, int maximumBytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Uris.Add(uri);
            return Task.FromResult(results[_index++]);
        }
    }
}
