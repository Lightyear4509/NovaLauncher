using System.Globalization;
using System.Runtime.Versioning;
using Microsoft.Win32;
using NovaLauncher.Application.Steam;

namespace NovaLauncher.Infrastructure.Steam;

public interface ISteamRegistryReader
{
    IReadOnlyList<string> FindSteamRoots();
}

public interface ISteamFileSystem
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    string GetFullPath(string path);

    IEnumerable<string> EnumerateFiles(string path, string pattern);

    Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken);
}

public sealed class PhysicalSteamFileSystem : ISteamFileSystem
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool FileExists(string path) => File.Exists(path);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public IEnumerable<string> EnumerateFiles(string path, string pattern) =>
        Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly);

    public Task<string> ReadAllTextAsync(string path, CancellationToken cancellationToken) =>
        File.ReadAllTextAsync(path, cancellationToken);
}

public sealed class WindowsSteamRegistryReader : ISteamRegistryReader
{
    public IReadOnlyList<string> FindSteamRoots()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var candidates = new List<string?>
        {
            ReadValue(RegistryHive.CurrentUser, RegistryView.Default, @"Software\Valve\Steam", "SteamPath"),
            ReadValue(RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Valve\Steam", "InstallPath"),
            ReadValue(RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Valve\Steam", "InstallPath"),
        };
        return candidates
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(TryCanonicalize)
            .Where(static value => value is not null)
            .Select(static value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string? TryCanonicalize(string? value)
    {
        try { return Path.GetFullPath(value!); }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException) { return null; }
    }

    [SupportedOSPlatform("windows")]
    private static string? ReadValue(RegistryHive hive, RegistryView view, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            return key?.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
        }
        catch (System.Security.SecurityException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }
}

public sealed class SteamCatalogSource(
    ISteamRegistryReader registry,
    ISteamFileSystem fileSystem) : ISteamCatalogSource
{
    private const int MaximumLibraries = 128;
    private const int MaximumManifests = 20_000;
    private const int MaximumManifestCharacters = 512 * 1024;

    public async Task<SteamCatalogScanResult> ScanAsync(
        string? manualSteamRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var failures = new List<SteamImportFailure>();
        var roots = GetCandidateRoots(manualSteamRoot, failures);
        if (roots.Count == 0 && failures.Count == 0)
        {
            failures.Add(new SteamImportFailure("Steam", "Steam was not found. Enter an absolute Steam installation folder."));
        }
        var libraries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in roots)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddRoot(root, libraries, failures);
            await AddConfiguredLibrariesAsync(root, libraries, failures, cancellationToken).ConfigureAwait(false);
            if (libraries.Count >= MaximumLibraries)
            {
                failures.Add(new SteamImportFailure(root, "Steam library count exceeded the safety limit."));
                break;
            }
        }

        var games = new Dictionary<uint, SteamGameCandidate>();
        var manifestCount = 0;
        foreach (var library in libraries.Order(StringComparer.OrdinalIgnoreCase))
        {
            var steamApps = Path.Combine(library, "steamapps");
            IEnumerable<string> manifests;
            try
            {
                manifests = fileSystem.EnumerateFiles(steamApps, "appmanifest_*.acf").ToArray();
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                failures.Add(new SteamImportFailure(steamApps, "Could not enumerate manifests."));
                continue;
            }

            foreach (var manifest in manifests.Order(StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++manifestCount > MaximumManifests)
                {
                    failures.Add(new SteamImportFailure(steamApps, "Manifest count exceeded the safety limit."));
                    return Result(games, failures, libraries);
                }

                var candidate = await ReadManifestAsync(manifest, library, failures, cancellationToken).ConfigureAwait(false);
                if (candidate is null)
                {
                    continue;
                }

                if (!games.TryAdd(candidate.AppId, candidate))
                {
                    failures.Add(new SteamImportFailure(manifest, $"Duplicate Steam App ID {candidate.AppId}."));
                }
            }
        }

        return Result(games, failures, libraries);
    }

    private IReadOnlyList<string> GetCandidateRoots(string? manualRoot, List<SteamImportFailure> failures)
    {
        if (!string.IsNullOrWhiteSpace(manualRoot))
        {
            if (!Path.IsPathFullyQualified(manualRoot))
            {
                failures.Add(new SteamImportFailure(manualRoot, "Manual Steam root must be an absolute path."));
                return [];
            }

            return [manualRoot];
        }

        return registry.FindSteamRoots();
    }

    private void AddRoot(string root, HashSet<string> libraries, List<SteamImportFailure> failures)
    {
        try
        {
            if (!Path.IsPathFullyQualified(root) || root.StartsWith(@"\\", StringComparison.Ordinal))
            {
                failures.Add(new SteamImportFailure(root, "Steam library root must be an absolute local path."));
                return;
            }

            var canonical = fileSystem.GetFullPath(root);
            if (!fileSystem.DirectoryExists(Path.Combine(canonical, "steamapps")))
            {
                failures.Add(new SteamImportFailure(
                    canonical,
                    "This configured Steam library currently has no steamapps directory and was skipped. Other detected libraries remain available."));
                return;
            }

            libraries.Add(canonical);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            failures.Add(new SteamImportFailure(root, "Steam root could not be validated."));
        }
    }

    private async Task AddConfiguredLibrariesAsync(
        string root,
        HashSet<string> libraries,
        List<SteamImportFailure> failures,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (!fileSystem.FileExists(path))
        {
            return;
        }

        try
        {
            var text = await fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            var container = ValveDataParser.Parse(text).GetNode("libraryfolders");
            if (container is null)
            {
                failures.Add(new SteamImportFailure(path, "libraryfolders.vdf has no libraryfolders object."));
                return;
            }

            foreach (var entry in container.Values.OrderBy(static item => item.Key, StringComparer.OrdinalIgnoreCase))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var configuredPath = entry.Value switch
                {
                    ValveDataNode node => node.GetString("path"),
                    string legacyPath => legacyPath,
                    _ => null,
                };
                if (configuredPath is null)
                {
                    continue;
                }

                AddRoot(configuredPath, libraries, failures);
            }
        }
        catch (ValveDataException exception)
        {
            failures.Add(new SteamImportFailure(path, exception.Message));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failures.Add(new SteamImportFailure(path, "Could not read libraryfolders.vdf."));
        }
    }

    private async Task<SteamGameCandidate?> ReadManifestAsync(
        string path,
        string libraryRoot,
        List<SteamImportFailure> failures,
        CancellationToken cancellationToken)
    {
        try
        {
            var text = await fileSystem.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
            if (text.Length > MaximumManifestCharacters)
            {
                throw new ValveDataException("Manifest exceeds the size limit.");
            }

            var state = ValveDataParser.Parse(text).GetNode("AppState")
                ?? throw new ValveDataException("Manifest has no AppState object.");
            if (!uint.TryParse(state.GetString("appid"), NumberStyles.None, CultureInfo.InvariantCulture, out var appId) || appId == 0)
            {
                throw new ValveDataException("Manifest has an invalid App ID.");
            }

            var expectedName = $"appmanifest_{appId.ToString(CultureInfo.InvariantCulture)}.acf";
            if (!string.Equals(Path.GetFileName(path), expectedName, StringComparison.OrdinalIgnoreCase))
            {
                throw new ValveDataException("Manifest filename does not match its App ID.");
            }

            var name = state.GetString("name")?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 512)
            {
                throw new ValveDataException("Manifest has an invalid game name.");
            }

            var installDirectory = state.GetString("installdir")?.Trim();
            if (string.IsNullOrWhiteSpace(installDirectory) || installDirectory.Length > 255 ||
                installDirectory is "." or ".." ||
                installDirectory.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0 ||
                Path.IsPathFullyQualified(installDirectory))
            {
                throw new ValveDataException("Manifest has an unsafe install directory.");
            }

            var installedPath = Path.Combine(libraryRoot, "steamapps", "common", installDirectory);
            if (!fileSystem.DirectoryExists(installedPath))
            {
                throw new ValveDataException("Installed game directory is missing.");
            }

            return new SteamGameCandidate(appId, name, installDirectory, path, libraryRoot);
        }
        catch (ValveDataException exception)
        {
            failures.Add(new SteamImportFailure(path, exception.Message));
            return null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            failures.Add(new SteamImportFailure(path, "Could not read manifest."));
            return null;
        }
    }

    private static SteamCatalogScanResult Result(
        IReadOnlyDictionary<uint, SteamGameCandidate> games,
        IReadOnlyList<SteamImportFailure> failures,
        IReadOnlyCollection<string> libraries) =>
        new(
            games.Values.OrderBy(static game => game.AppId).ToArray(),
            failures.ToArray(),
            libraries.Order(StringComparer.OrdinalIgnoreCase).ToArray());
}
