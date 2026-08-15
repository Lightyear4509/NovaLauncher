using NovaLauncher.Infrastructure.Steam;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class SteamCatalogSourceTests
{
    [Fact]
    public void ParserReadsEscapesCommentsAndNestedObjects()
    {
        var parsed = ValveDataParser.Parse("// comment\n\"root\" { \"path\" \"C:\\\\Steam\" \"nested\" { \"value\" \"yes\" } }");

        var root = Assert.IsType<ValveDataNode>(parsed.GetNode("root"));
        Assert.Equal("C:\\Steam", root.GetString("path"));
        Assert.Equal("yes", root.GetNode("nested")?.GetString("value"));
    }

    [Theory]
    [InlineData("\"root\" {")]
    [InlineData("\"root\" \"one\" \"root\" \"two\"")]
    [InlineData("}")]
    [InlineData("\"unterminated")]
    public void ParserRejectsMalformedInput(string content) =>
        Assert.Throws<ValveDataException>(() => ValveDataParser.Parse(content));

    [Fact]
    public void ParserRejectsOversizedInput() =>
        Assert.Throws<ValveDataException>(() => ValveDataParser.Parse(new string('a', ValveDataParser.MaximumCharacters + 1)));

    [Fact]
    public async Task ManualRootAndConfiguredLibrariesProduceCandidatesAndPerFileFailures()
    {
        var files = new FakeSteamFileSystem();
        files.AddDirectory(@"C:\Steam\steamapps");
        files.AddDirectory(@"D:\Library\steamapps");
        files.AddDirectory(@"D:\Library\steamapps\common\ValidGame");
        files.AddFile(@"C:\Steam\steamapps\libraryfolders.vdf", Libraries(@"D:\\Library"));
        files.AddFile(@"D:\Library\steamapps\appmanifest_10.acf", Manifest(10, "Valid", "ValidGame"));
        files.AddFile(@"D:\Library\steamapps\appmanifest_20.acf", "not valid {");
        var source = new SteamCatalogSource(new FakeRegistry(), files);

        var result = await source.ScanAsync(@"C:\Steam", CancellationToken.None);

        Assert.Equal(2, result.LibraryRoots.Count);
        Assert.Equal((uint)10, Assert.Single(result.Games).AppId);
        Assert.Single(result.Failures);
    }

    [Fact]
    public async Task RegistryDiscoveryIsReadOnlyAndInvalidRootsAreReported()
    {
        var files = new FakeSteamFileSystem();
        var registry = new FakeRegistry(@"C:\Missing");
        var source = new SteamCatalogSource(registry, files);

        var result = await source.ScanAsync(null, CancellationToken.None);

        Assert.True(registry.Called);
        Assert.Empty(result.Games);
        Assert.Single(result.Failures);
    }

    [Fact]
    public async Task RelativeManualRootIsRejectedWithoutRegistryFallback()
    {
        var registry = new FakeRegistry(@"C:\Steam");
        var result = await new SteamCatalogSource(registry, new FakeSteamFileSystem())
            .ScanAsync("relative", CancellationToken.None);

        Assert.False(registry.Called);
        Assert.Contains(result.Failures, failure => failure.Reason.Contains("absolute", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NetworkLibraryAndUnsafeInstallDirectoryAreRejected()
    {
        var files = new FakeSteamFileSystem();
        files.AddDirectory(@"C:\Steam\steamapps");
        files.AddFile(@"C:\Steam\steamapps\libraryfolders.vdf", Libraries(@"\\\\server\\share"));
        files.AddFile(@"C:\Steam\steamapps\appmanifest_42.acf", Manifest(42, "Unsafe", "..\\escape"));

        var result = await new SteamCatalogSource(new FakeRegistry(), files)
            .ScanAsync(@"C:\Steam", CancellationToken.None);

        Assert.Empty(result.Games);
        Assert.Equal(2, result.Failures.Count);
    }

    [Fact]
    public async Task TenThousandManifestsAreParsedDeterministically()
    {
        var files = new FakeSteamFileSystem();
        files.AddDirectory(@"C:\Steam\steamapps");
        for (uint appId = 1; appId <= 10_000; appId++)
        {
            files.AddDirectory($@"C:\Steam\steamapps\common\Game{appId}");
            files.AddFile($@"C:\Steam\steamapps\appmanifest_{appId}.acf", Manifest(appId, $"Game {appId}", $"Game{appId}"));
        }

        var result = await new SteamCatalogSource(new FakeRegistry(), files)
            .ScanAsync(@"C:\Steam", CancellationToken.None);

        Assert.Equal(10_000, result.Games.Count);
        Assert.Empty(result.Failures);
        Assert.Equal((uint)1, result.Games[0].AppId);
        Assert.Equal((uint)10_000, result.Games[^1].AppId);
    }

    [Fact]
    public async Task CancellationBeforeScanPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SteamCatalogSource(new FakeRegistry(), new FakeSteamFileSystem())
                .ScanAsync(null, cancellation.Token));
    }

    [Fact]
    public async Task PhysicalFileSystemScansARealTemporarySteamTree()
    {
        var root = Path.Combine(Path.GetTempPath(), $"NovaLauncher-Steam-{Guid.NewGuid():N}");
        var steamApps = Path.Combine(root, "steamapps");
        var gameDirectory = Path.Combine(steamApps, "common", "Game");
        Directory.CreateDirectory(gameDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(steamApps, "appmanifest_50.acf"),
            Manifest(50, "Physical game", "Game"),
            CancellationToken.None);
        try
        {
            var result = await new SteamCatalogSource(new FakeRegistry(), new PhysicalSteamFileSystem())
                .ScanAsync(root, CancellationToken.None);

            Assert.Equal("Physical game", Assert.Single(result.Games).Name);
            Assert.Empty(result.Failures);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WindowsRegistryDiscoveryReturnsOnlyNonBlankRoots()
    {
        var roots = new WindowsSteamRegistryReader().FindSteamRoots();

        Assert.DoesNotContain(roots, string.IsNullOrWhiteSpace);
    }

    [Fact]
    public async Task NoDetectedSteamReturnsActionableFailure()
    {
        var result = await new SteamCatalogSource(new FakeRegistry(), new FakeSteamFileSystem())
            .ScanAsync(null, CancellationToken.None);

        Assert.Contains(result.Failures, failure => failure.Reason.Contains("not found", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task LegacyLibraryEntryAndDuplicateAppIdAreHandledDeterministically()
    {
        var files = new FakeSteamFileSystem();
        files.AddDirectory(@"C:\Steam\steamapps");
        files.AddDirectory(@"D:\Library\steamapps");
        files.AddDirectory(@"C:\Steam\steamapps\common\Game");
        files.AddDirectory(@"D:\Library\steamapps\common\Game");
        files.AddFile(@"C:\Steam\steamapps\libraryfolders.vdf", "\"libraryfolders\" { \"1\" \"D:\\\\Library\" }");
        files.AddFile(@"C:\Steam\steamapps\appmanifest_60.acf", Manifest(60, "First", "Game"));
        files.AddFile(@"D:\Library\steamapps\appmanifest_60.acf", Manifest(60, "Duplicate", "Game"));

        var result = await new SteamCatalogSource(new FakeRegistry(), files)
            .ScanAsync(@"C:\Steam", CancellationToken.None);

        Assert.Equal("First", Assert.Single(result.Games).Name);
        Assert.Contains(result.Failures, failure => failure.Reason.Contains("Duplicate", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("\"AppState\" { \"appid\" \"0\" \"name\" \"Game\" \"installdir\" \"Game\" }")]
    [InlineData("\"AppState\" { \"appid\" \"70\" \"name\" \"\" \"installdir\" \"Game\" }")]
    [InlineData("\"Other\" { \"appid\" \"70\" }")]
    public async Task InvalidManifestFieldsBecomePerItemFailures(string manifest)
    {
        var files = new FakeSteamFileSystem();
        files.AddDirectory(@"C:\Steam\steamapps");
        files.AddDirectory(@"C:\Steam\steamapps\common\Game");
        files.AddFile(@"C:\Steam\steamapps\appmanifest_70.acf", manifest);

        var result = await new SteamCatalogSource(new FakeRegistry(), files)
            .ScanAsync(@"C:\Steam", CancellationToken.None);

        Assert.Empty(result.Games);
        Assert.Single(result.Failures);
    }

    [Fact]
    public async Task ManifestFilenameMismatchIsRejected()
    {
        var files = new FakeSteamFileSystem();
        files.AddDirectory(@"C:\Steam\steamapps");
        files.AddDirectory(@"C:\Steam\steamapps\common\Game");
        files.AddFile(@"C:\Steam\steamapps\appmanifest_71.acf", Manifest(72, "Game", "Game"));

        var result = await new SteamCatalogSource(new FakeRegistry(), files)
            .ScanAsync(@"C:\Steam", CancellationToken.None);

        Assert.Contains(result.Failures, failure => failure.Reason.Contains("filename", StringComparison.OrdinalIgnoreCase));
    }

    private static string Libraries(string path) =>
        $"\"libraryfolders\" {{ \"0\" {{ \"path\" \"{path}\" }} }}";

    private static string Manifest(uint appId, string name, string installDirectory) =>
        $"\"AppState\" {{ \"appid\" \"{appId}\" \"name\" \"{name}\" \"installdir\" \"{installDirectory}\" }}";

    private sealed class FakeRegistry(params string[] roots) : ISteamRegistryReader
    {
        public bool Called { get; private set; }

        public IReadOnlyList<string> FindSteamRoots()
        {
            Called = true;
            return roots;
        }
    }

    private sealed class FakeSteamFileSystem : ISteamFileSystem
    {
        private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

        public void AddDirectory(string path) => _directories.Add(path);

        public void AddFile(string path, string content) => _files[path] = content;

        public bool DirectoryExists(string path) => _directories.Contains(path);

        public bool FileExists(string path) => _files.ContainsKey(path);

        public string GetFullPath(string path) => Path.GetFullPath(path);

        public IEnumerable<string> EnumerateFiles(string path, string pattern) =>
            _files.Keys.Where(file =>
                string.Equals(Path.GetDirectoryName(file), path, StringComparison.OrdinalIgnoreCase) &&
                Path.GetFileName(file).StartsWith("appmanifest_", StringComparison.OrdinalIgnoreCase) &&
                Path.GetExtension(file).Equals(".acf", StringComparison.OrdinalIgnoreCase));

        public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_files[path]);
        }
    }
}
