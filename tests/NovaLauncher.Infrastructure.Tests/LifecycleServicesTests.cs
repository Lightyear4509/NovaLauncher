using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using NovaLauncher.Application.Lifecycle;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Infrastructure.Lifecycle;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class LifecycleServicesTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"NovaLauncher-Lifecycle-{Guid.NewGuid():N}");

    [Fact]
    public async Task UpdateCheckAcceptsOnlyNewerOfficialChannelAsset()
    {
        var installer = new byte[] { 1, 2, 3 }; var handler = new RoutingHandler();
        handler.AddJson("api.github.com", ReleasesJson("v0.6.0-beta.1", true, installer.Length));
        var service = new GitHubUpdateService(new HttpClient(handler), new FakeAuthenticode(true), Path.Combine(_root, "stage"), new HashSet<string> { new('a', 64) });

        var stable = await service.CheckAsync(UpdateChannel.Stable, CancellationToken.None);
        var beta = await service.CheckAsync(UpdateChannel.Beta, CancellationToken.None);

        Assert.Null(stable.Release);
        Assert.Equal("v0.6.0-beta.1", Assert.IsType<UpdateRelease>(beta.Release).Tag);
    }

    [Fact]
    public async Task UnsignedBuildFailsClosedBeforeDownloadingInstaller()
    {
        var handler = new RoutingHandler(); var service = new GitHubUpdateService(new HttpClient(handler), new FakeAuthenticode(true), Path.Combine(_root, "stage"), new HashSet<string>());
        var release = Release(3);

        var result = await service.StageAsync(release, null, CancellationToken.None);

        Assert.False(result.Success); Assert.Contains("no trusted", result.Message, StringComparison.OrdinalIgnoreCase); Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task StageRequiresExactHashAndPublisherPinBeforePromotion()
    {
        var installer = new byte[] { 1, 2, 3 }; var hash = Convert.ToHexString(SHA256.HashData(installer)).ToLowerInvariant(); var handler = new RoutingHandler();
        handler.Add("github.com", "/Lightyear4509/NovaLauncher/releases/download/v0.6.0/sums", Encoding.ASCII.GetBytes($"{hash}  setup.exe\n")); handler.Add("github.com", "/Lightyear4509/NovaLauncher/releases/download/v0.6.0/setup.exe", installer);
        var service = new GitHubUpdateService(new HttpClient(handler), new FakeAuthenticode(true), Path.Combine(_root, "stage"), new HashSet<string> { new('a', 64) });

        var result = await service.StageAsync(Release(installer.Length), null, CancellationToken.None);

        Assert.True(result.Success, result.Message); Assert.Equal(installer, await File.ReadAllBytesAsync(result.StagedInstallerPath!));
    }

    [Fact]
    public async Task HashMismatchLeavesNoStagedInstaller()
    {
        var handler = new RoutingHandler(); handler.Add("github.com", "/Lightyear4509/NovaLauncher/releases/download/v0.6.0/sums", Encoding.ASCII.GetBytes($"{new string('0', 64)}  setup.exe\n")); handler.Add("github.com", "/Lightyear4509/NovaLauncher/releases/download/v0.6.0/setup.exe", [1, 2, 3]);
        var service = new GitHubUpdateService(new HttpClient(handler), new FakeAuthenticode(true), Path.Combine(_root, "stage-bad"), new HashSet<string> { new('a', 64) });

        var result = await service.StageAsync(Release(3), null, CancellationToken.None);

        Assert.False(result.Success); Assert.Empty(Directory.GetFiles(Path.Combine(_root, "stage-bad")));
    }

    [Fact]
    public async Task StageRejectsNonOfficialUrlBeforeNetworkAccess()
    {
        var handler = new RoutingHandler(); var service = new GitHubUpdateService(new HttpClient(handler), new FakeAuthenticode(true), Path.Combine(_root, "stage-url"), new HashSet<string> { new('a', 64) });
        var release = Release(3) with { InstallerUri = new Uri("https://example.com/setup.exe") };

        var result = await service.StageAsync(release, null, CancellationToken.None);

        Assert.False(result.Success); Assert.Equal(0, handler.RequestCount);
    }

    [Fact]
    public async Task PublisherPinFailureDiscardsVerifiedDownload()
    {
        var installer = new byte[] { 1, 2, 3 }; var hash = Convert.ToHexString(SHA256.HashData(installer)).ToLowerInvariant(); var handler = new RoutingHandler();
        handler.Add("github.com", "/Lightyear4509/NovaLauncher/releases/download/v0.6.0/sums", Encoding.ASCII.GetBytes($"{hash}  setup.exe\n")); handler.Add("github.com", "/Lightyear4509/NovaLauncher/releases/download/v0.6.0/setup.exe", installer);
        var service = new GitHubUpdateService(new HttpClient(handler), new FakeAuthenticode(false), Path.Combine(_root, "stage-pin"), new HashSet<string> { new('a', 64) });

        var result = await service.StageAsync(Release(3), null, CancellationToken.None);

        Assert.False(result.Success); Assert.Empty(Directory.GetFiles(Path.Combine(_root, "stage-pin")));
    }

    [Fact]
    public void CrashMarkerReportsInterruptedSessionAndClearsOnCleanExit()
    {
        var first = new CrashRecoveryService(_root, TimeProvider.System); Assert.False(first.BeginSession().PreviousSessionInterrupted);
        var second = new CrashRecoveryService(_root, TimeProvider.System); Assert.True(second.BeginSession().PreviousSessionInterrupted); second.CompleteSession();
        Assert.False(new CrashRecoveryService(_root, TimeProvider.System).BeginSession().PreviousSessionInterrupted);
    }

    [Fact]
    public async Task DiagnosticExportRedactsIdentifiersAddressesAndPaths()
    {
        var logs = Path.Combine(_root, "Logs"); Directory.CreateDirectory(logs);
        await File.WriteAllTextAsync(Path.Combine(logs, "novalauncher.jsonl"), "id=12345678-1234-1234-1234-123456789abc peer=100.64.0.2 path=C:\\Users\\Person\\save.dat");
        var destination = Path.Combine(_root, "diagnostics.zip"); var service = new SanitizedDiagnosticExportService(_root, TimeProvider.System);

        var result = await service.ExportAsync(destination, CancellationToken.None);

        Assert.True(result.Success, result.Message); using var archive = ZipFile.OpenRead(destination); using var reader = new StreamReader(archive.GetEntry("sanitized-log.jsonl")!.Open()); var text = await reader.ReadToEndAsync();
        Assert.DoesNotContain("12345678", text, StringComparison.Ordinal); Assert.DoesNotContain("100.64.0.2", text, StringComparison.Ordinal); Assert.DoesNotContain("Person", text, StringComparison.Ordinal); Assert.Contains("[redacted", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SettingsSchemaOneMigratesExplicitlyToStableChannel()
    {
        await using var fixture = PersistenceTestFixture.Create(); Directory.CreateDirectory(fixture.Root);
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "settings.json"), "{\"schemaVersion\":1,\"settings\":{\"themeId\":\"nova-dark\",\"reduceMotion\":false,\"confirmBeforeRemovingLibraryItems\":true}}");

        var load = await fixture.SettingsStore.LoadAsync(CancellationToken.None);

        Assert.Equal(DocumentLoadStatus.MigratedLegacy, load.Status); Assert.Equal(SettingsDocument.CurrentSchemaVersion, load.Document!.SchemaVersion); Assert.Equal("Stable", load.Document.Settings.UpdateChannel);
        var save = await fixture.SettingsStore.SaveAsync(load.Document, CancellationToken.None);
        Assert.Equal(DocumentSaveStatus.Saved, save.Status); Assert.True(File.Exists(Path.Combine(fixture.Root, "settings.json.bak")));
    }

    private static UpdateRelease Release(long size) => new("0.6.0", "v0.6.0", "notes", new("https://github.com/Lightyear4509/NovaLauncher/releases/download/v0.6.0/setup.exe"), new("https://github.com/Lightyear4509/NovaLauncher/releases/download/v0.6.0/sums"), size, false);
    private static string ReleasesJson(string tag, bool prerelease, long size) => $$"""[{"draft":false,"prerelease":{{prerelease.ToString().ToLowerInvariant()}},"tag_name":"{{tag}}","body":"notes","assets":[{"name":"NovaLauncher-Setup-0.6.0-win-x64.exe","size":{{size}},"browser_download_url":"https://github.com/Lightyear4509/NovaLauncher/releases/download/{{tag}}/NovaLauncher-Setup-0.6.0-win-x64.exe"},{"name":"SHA256SUMS.txt","size":100,"browser_download_url":"https://github.com/Lightyear4509/NovaLauncher/releases/download/{{tag}}/SHA256SUMS.txt"}]}]""";
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); GC.SuppressFinalize(this); }

    private sealed class FakeAuthenticode(bool trusted) : IAuthenticodeVerifier { public AuthenticodeVerification Verify(string path, IReadOnlySet<string> pins) => new(trusted, trusted ? "valid" : "invalid"); }
    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _responses = new(StringComparer.OrdinalIgnoreCase); public int RequestCount { get; private set; }
        public void Add(string host, string path, byte[] bytes) => _responses[host + path] = bytes;
        public void AddJson(string host, string json) => Add(host, "/repos/Lightyear4509/NovaLauncher/releases", Encoding.UTF8.GetBytes(json));
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) { RequestCount++; var key = request.RequestUri!.Host + request.RequestUri.AbsolutePath; if (!_responses.TryGetValue(key, out var bytes)) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)); var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }; response.Content.Headers.ContentLength = bytes.Length; return Task.FromResult(response); }
    }
}
