using System.Net;
using System.Text;
using NovaLauncher.Application.Enrichment;
using NovaLauncher.Infrastructure.Enrichment;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class EnrichmentProviderTests
{
    [Fact]
    public async Task BoundedClientRetriesTransientAndReturnsSuccessfulBytes()
    {
        var handler = new SequenceHandler(
            new HttpResponseMessage(HttpStatusCode.TooManyRequests),
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("ok") });
        var client = new BoundedHttpClient(new HttpClient(handler), TimeProvider.System);

        var result = await client.GetAsync(new Uri("https://example.test/data"), null, 100, CancellationToken.None);

        Assert.Equal("ok", Encoding.UTF8.GetString(result.Content!));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task BoundedClientRejectsHttpAndOversizedContent()
    {
        var client = new BoundedHttpClient(
            new HttpClient(new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(new byte[101]),
            })),
            TimeProvider.System);

        var insecure = await client.GetAsync(new Uri("http://example.test"), null, 100, CancellationToken.None);
        var oversized = await client.GetAsync(new Uri("https://example.test"), null, 100, CancellationToken.None);

        Assert.Null(insecure.Content);
        Assert.Contains("HTTPS", insecure.Error, StringComparison.Ordinal);
        Assert.Null(oversized.Content);
        Assert.Contains("size", oversized.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SteamMetadataNormalizesBoundedFieldsAndRejectsMalformedJson()
    {
        const string json = """
            {"570":{"success":true,"data":{"short_description":"<b> Great </b> game","genres":[{"description":"Action"}],"developers":["Valve"],"publishers":["Valve"],"release_date":{"date":"Jul 9, 2013"},"metacritic":{"score":90}}}}
            """;
        var provider = new SteamMetadataProvider(new FakeBoundedHttp(Encoding.UTF8.GetBytes(json)), TimeProvider.System);

        var result = await provider.GetMetadataAsync(new MetadataRequest(default, "Steam", "570"), CancellationToken.None);

        Assert.Equal(ProviderResultStatus.Success, result.Status);
        Assert.Equal("Great game", result.Snapshot?.Description);
        Assert.Equal("Action", Assert.Single(result.Snapshot!.Genres!));
        Assert.Equal(90, result.Snapshot.Rating);

        var malformed = await new SteamMetadataProvider(new FakeBoundedHttp("{"u8.ToArray()), TimeProvider.System)
            .GetMetadataAsync(new MetadataRequest(default, "Steam", "570"), CancellationToken.None);
        Assert.Equal(ProviderResultStatus.InvalidResponse, malformed.Status);
    }

    [Fact]
    public async Task SteamArtworkIsDeterministicAndSteamGridDbRequiresKeyAndHttpsUrl()
    {
        var request = new MetadataRequest(default, "Steam", "570");
        var steam = await new SteamArtworkProvider(TimeProvider.System).GetArtworkAsync(request, CancellationToken.None);
        Assert.Equal(4, steam.Candidates.Count);
        Assert.All(steam.Candidates, item => Assert.StartsWith("https://", item.Location, StringComparison.Ordinal));

        var disabled = await new SteamGridDbArtworkProvider(new FakeBoundedHttp([]), TimeProvider.System, null)
            .GetArtworkAsync(request, CancellationToken.None);
        Assert.Equal(ProviderResultStatus.NoData, disabled.Status);

        var maliciousJson = Encoding.UTF8.GetBytes("{\"success\":true,\"data\":[{\"url\":\"file:///secret\"}]}");
        var malicious = await new SteamGridDbArtworkProvider(new FakeBoundedHttp(maliciousJson), TimeProvider.System, "secret")
            .GetArtworkAsync(request, CancellationToken.None);
        Assert.Equal(ProviderResultStatus.NoData, malicious.Status);
        Assert.Empty(malicious.Candidates);
    }

    [Fact]
    public async Task SteamMetadataReturnsTypedNoDataOfflineRateLimitAndContentTypeFailures()
    {
        var request = new MetadataRequest(default, "Steam", "570");
        var noData = await new SteamMetadataProvider(
            new FakeBoundedHttp(new BoundedHttpResult(HttpStatusCode.OK, "{\"570\":{\"success\":false}}"u8.ToArray(), false, null, "application/json")),
            TimeProvider.System).GetMetadataAsync(request, CancellationToken.None);
        var offline = await new SteamMetadataProvider(
            new FakeBoundedHttp(new BoundedHttpResult(0, null, true, "offline")),
            TimeProvider.System).GetMetadataAsync(request, CancellationToken.None);
        var limited = await new SteamMetadataProvider(
            new FakeBoundedHttp(new BoundedHttpResult(HttpStatusCode.TooManyRequests, null, false, "limited")),
            TimeProvider.System).GetMetadataAsync(request, CancellationToken.None);
        var wrongType = await new SteamMetadataProvider(
            new FakeBoundedHttp(new BoundedHttpResult(HttpStatusCode.OK, "{}"u8.ToArray(), false, null, "text/html")),
            TimeProvider.System).GetMetadataAsync(request, CancellationToken.None);

        Assert.Equal(ProviderResultStatus.NoData, noData.Status);
        Assert.Equal(ProviderResultStatus.Offline, offline.Status);
        Assert.Equal(ProviderResultStatus.RateLimited, limited.Status);
        Assert.Equal(ProviderResultStatus.InvalidResponse, wrongType.Status);
        Assert.False(new SteamMetadataProvider(new FakeBoundedHttp([]), TimeProvider.System)
            .CanHandle(new MetadataRequest(default, "Manual", null)));
    }

    [Fact]
    public async Task SteamGridDbAcceptsOnlyBoundedHttpsResultsAndTypedFailures()
    {
        var request = new MetadataRequest(default, "Steam", "570");
        var validJson = "{\"success\":true,\"data\":[{\"url\":\"https://cdn.example/cover.png\"}]}"u8.ToArray();
        var success = await new SteamGridDbArtworkProvider(
            new FakeBoundedHttp(new BoundedHttpResult(HttpStatusCode.OK, validJson, false, null, "application/json")),
            TimeProvider.System,
            "key").GetArtworkAsync(request, CancellationToken.None);
        var invalid = await new SteamGridDbArtworkProvider(
            new FakeBoundedHttp(new BoundedHttpResult(HttpStatusCode.OK, "{"u8.ToArray(), false, null, "application/json")),
            TimeProvider.System,
            "key").GetArtworkAsync(request, CancellationToken.None);
        var wrongType = await new SteamGridDbArtworkProvider(
            new FakeBoundedHttp(new BoundedHttpResult(HttpStatusCode.OK, validJson, false, null, "text/html")),
            TimeProvider.System,
            "key").GetArtworkAsync(request, CancellationToken.None);

        Assert.Equal("https://cdn.example/cover.png", Assert.Single(success.Candidates).Location);
        Assert.Equal(ProviderResultStatus.InvalidResponse, invalid.Status);
        Assert.Equal(ProviderResultStatus.InvalidResponse, wrongType.Status);

        var limited = await new SteamGridDbArtworkProvider(
            new FakeBoundedHttp(new BoundedHttpResult(HttpStatusCode.TooManyRequests, null, false, "limited")),
            TimeProvider.System,
            "key").GetArtworkAsync(request, CancellationToken.None);
        var offline = await new SteamGridDbArtworkProvider(
            new FakeBoundedHttp(new BoundedHttpResult(0, null, true, "offline")),
            TimeProvider.System,
            "key").GetArtworkAsync(request, CancellationToken.None);
        Assert.Equal(ProviderResultStatus.RateLimited, limited.Status);
        Assert.Equal(ProviderResultStatus.Offline, offline.Status);
        Assert.False(new SteamGridDbArtworkProvider(new FakeBoundedHttp([]), TimeProvider.System, "key")
            .CanHandle(new MetadataRequest(default, "Manual", null)));
    }

    [Fact]
    public async Task SteamProvidersHandleSparseAndIneligibleRequests()
    {
        const string sparse = "{\"570\":{\"success\":true,\"data\":{}}}";
        var metadata = await new SteamMetadataProvider(new FakeBoundedHttp(Encoding.UTF8.GetBytes(sparse)), TimeProvider.System)
            .GetMetadataAsync(new MetadataRequest(default, "Steam", "570"), CancellationToken.None);
        Assert.Equal(ProviderResultStatus.Success, metadata.Status);
        Assert.Null(metadata.Snapshot?.Description);
        Assert.Null(metadata.Snapshot?.Genres);

        var metadataNoData = await new SteamMetadataProvider(new FakeBoundedHttp([]), TimeProvider.System)
            .GetMetadataAsync(new MetadataRequest(default, "Manual", null), CancellationToken.None);
        var artworkNoData = await new SteamArtworkProvider(TimeProvider.System)
            .GetArtworkAsync(new MetadataRequest(default, "Manual", null), CancellationToken.None);
        Assert.Equal(ProviderResultStatus.NoData, metadataNoData.Status);
        Assert.Equal(ProviderResultStatus.NoData, artworkNoData.Status);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SteamArtworkProvider(TimeProvider.System).GetArtworkAsync(
                new MetadataRequest(default, "Steam", "570"), cancellation.Token));
    }

    [Fact]
    public async Task SteamGridDbIdentitySearchIsBoundedFilteredAndRequiresSessionKey()
    {
        var json = """
            {"success":true,"data":[
              {"id":10,"name":"Safe Game","release_date":946684800},
              {"id":11,"name":"Adult Game","adult":true},
              {"id":12,"name":"Flash Game","epilepsy":true},
              {"id":13,"name":"Joke Game","humor":true},
              {"id":14,"name":"Missing date"}
            ]}
            """u8.ToArray();
        var disabled = new SteamGridDbIdentitySearchProvider(new FakeBoundedHttp(json), new ApiKeySession(null));
        Assert.Empty((await disabled.SearchAsync("safegame", "Safe Game", CancellationToken.None)).Candidates);

        var provider = new SteamGridDbIdentitySearchProvider(
            new FakeBoundedHttp(new BoundedHttpResult(HttpStatusCode.OK, json, false, null, "application/json")),
            new ApiKeySession("secret"));
        var result = await provider.SearchAsync("safegame", "Safe Game", CancellationToken.None);

        Assert.Equal(2, result.Candidates.Count);
        Assert.Equal(2000, result.Candidates[0].ReleaseYear);
        Assert.DoesNotContain(result.Candidates, candidate => candidate.DisplayName.Contains("Adult", StringComparison.Ordinal));
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task BoundedClientHandlesPermanentFailureNetworkFailureAndCancellation()
    {
        var permanent = new BoundedHttpClient(
            new HttpClient(new SequenceHandler(new HttpResponseMessage(HttpStatusCode.NotFound))),
            TimeProvider.System);
        var missing = await permanent.GetAsync(new Uri("https://example.test"), null, 100, CancellationToken.None);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var network = new BoundedHttpClient(new HttpClient(new ThrowingHandler()), TimeProvider.System);
        var failed = await network.GetAsync(new Uri("https://example.test"), null, 100, CancellationToken.None);
        Assert.True(failed.IsOffline);

        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            permanent.GetAsync(new Uri("https://example.test"), null, 100, cancellation.Token));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            permanent.GetAsync(new Uri("https://example.test"), null, 0, CancellationToken.None));
    }

    private sealed class SequenceHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _index;
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(responses[_index++]);
        }
    }

    private sealed class FakeBoundedHttp
        : IBoundedHttpClient
    {
        private readonly BoundedHttpResult _result;

        public FakeBoundedHttp(byte[] content)
            : this(new BoundedHttpResult(HttpStatusCode.OK, content, false, null))
        {
        }

        public FakeBoundedHttp(BoundedHttpResult result) => _result = result;

        public Task<BoundedHttpResult> GetAsync(Uri uri, IReadOnlyDictionary<string, string>? headers, int maximumBytes, CancellationToken cancellationToken) =>
            Task.FromResult(_result);
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("offline");
    }
}
