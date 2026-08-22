using System.Diagnostics;
using NovaLauncher.Application.Library;
using NovaLauncher.Application.Persistence;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Tests;

public sealed class LibraryPerformanceTests
{
    [Fact]
    public async Task WarmSearchP95StaysUnderOneHundredMillisecondsForTenThousandGames()
    {
        var games = Enumerable.Range(1, 10_000).Select(CreateGame).ToArray();
        using var library = new LibraryCoordinator(new Store(new GamesDocument(GamesDocument.CurrentSchemaVersion, games)), new ManualGameDraftValidator(), TimeProvider.System);
        await library.LoadAsync(CancellationToken.None);
        _ = library.Query("game 99", false, LibrarySort.Name);
        var samples = new List<double>();
        for (var iteration = 0; iteration < 25; iteration++)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = library.Query("game 99", false, LibrarySort.Name);
            stopwatch.Stop();
            Assert.NotEmpty(result);
            samples.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        samples.Sort();
        var p95 = samples[(int)Math.Ceiling(samples.Count * 0.95) - 1];
        Assert.True(p95 < 100, $"Warm search p95 was {p95:F2} ms on this runner.");
    }

    [Fact]
    public async Task TwentyThousandGamesAcrossProfilesStayIsolatedAndWithinStartupSearchMemoryBudgets()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        var baselineBytes = GC.GetTotalMemory(forceFullCollection: true);
        var secondProfile = Guid.NewGuid();
        var games = Enumerable.Range(1, 20_000)
            .Select(value => CreateGame(value) with { ProfileId = value <= 10_000 ? NovaLauncher.Domain.Profiles.LocalProfileDefaults.DefaultProfileId : secondProfile })
            .ToArray();
        using var library = new LibraryCoordinator(new Store(new GamesDocument(GamesDocument.CurrentSchemaVersion, games)), new ManualGameDraftValidator(), TimeProvider.System);
        var stopwatch = Stopwatch.StartNew();

        await library.LoadAsync(CancellationToken.None);
        library.SetActiveProfile(secondProfile);
        var result = library.Query("game 199", false, LibrarySort.Name);
        stopwatch.Stop();

        Assert.Equal(10_000, library.Games.Count);
        Assert.NotEmpty(result);
        Assert.All(result, game => Assert.Equal(secondProfile, game.ProfileId));
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(1), $"Multi-profile load, switch, and search took {stopwatch.Elapsed.TotalMilliseconds:F2} ms.");
        var retainedBytes = GC.GetTotalMemory(forceFullCollection: true) - baselineBytes;
        Assert.True(retainedBytes < 128L * 1024 * 1024, $"20,000-game multi-profile fixture retained {retainedBytes / (1024d * 1024):F2} MiB.");
    }

    private static LibraryItem CreateGame(int value)
    {
        var now = DateTimeOffset.UnixEpoch.AddSeconds(value);
        return new LibraryItem(new GameId(GuidFromInt(value)), $"Game {value:D5}", "Windows", "Manual",
            new LaunchTarget($"C:\\Games\\{value}\\game.exe", [], null, LaunchTargetKind.Executable),
            new GameMetadata(null, null, null, null, null, null), value % 10 == 0, now, now);
    }

    private static Guid GuidFromInt(int value)
    {
        Span<byte> bytes = stackalloc byte[16];
        BitConverter.TryWriteBytes(bytes, value);
        return new Guid(bytes);
    }

    private sealed class Store(GamesDocument document) : IDocumentStore<GamesDocument>
    {
        public Task<DocumentLoadResult<GamesDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentLoadResult<GamesDocument>(DocumentLoadStatus.Loaded, document, null));
        public Task<DocumentSaveResult> SaveAsync(GamesDocument value, CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
    }
}
