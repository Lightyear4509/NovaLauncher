using System.Security.Cryptography;
using System.Text.Json;
using NovaLauncher.Application.Lifecycle;
using NovaLauncher.Domain;

namespace NovaLauncher.Infrastructure.Lifecycle;

public sealed class GitHubUpdateService(HttpClient http, IAuthenticodeVerifier authenticode, IUpdateInstallerLauncher installerLauncher, IUpdateRecoveryService recovery, string stagingRoot, IReadOnlySet<string> trustedCertificateSha256) : IUpdateService
{
    public const long MaximumMetadataBytes = 1024 * 1024;
    public const long MaximumInstallerBytes = 256L * 1024 * 1024;
    private static readonly Uri ReleasesEndpoint = new("https://api.github.com/repos/Lightyear4509/NovaLauncher/releases?per_page=20");

    public async Task<UpdateCheckResult> CheckAsync(UpdateChannel channel, CancellationToken cancellationToken)
    {
        try
        {
            var bytes = await GetBoundedAsync(ReleasesEndpoint, MaximumMetadataBytes, null, cancellationToken).ConfigureAwait(false);
            using var json = JsonDocument.Parse(bytes);
            foreach (var item in json.RootElement.EnumerateArray())
            {
                if (item.GetProperty("draft").GetBoolean()) continue;
                var prerelease = item.GetProperty("prerelease").GetBoolean();
                var tag = item.GetProperty("tag_name").GetString() ?? string.Empty;
                if (!ChannelAccepts(channel, tag, prerelease) || !TryParseVersion(tag, out var candidate) || !TryParseVersion(ProductIdentity.Version, out var current) || candidate.CompareTo(current) <= 0) continue;
                var assets = item.GetProperty("assets").EnumerateArray().ToArray();
                var installer = assets.FirstOrDefault(static asset => (asset.GetProperty("name").GetString() ?? string.Empty) is { } name && name.StartsWith("NovaLauncher-Setup-", StringComparison.Ordinal) && name.EndsWith("-win-x64.exe", StringComparison.Ordinal));
                var sums = assets.FirstOrDefault(static asset => string.Equals(asset.GetProperty("name").GetString(), "SHA256SUMS.txt", StringComparison.Ordinal));
                if (installer.ValueKind == JsonValueKind.Undefined || sums.ValueKind == JsonValueKind.Undefined) continue;
                var size = installer.GetProperty("size").GetInt64(); if (size is <= 0 or > MaximumInstallerBytes) continue;
                var notes = (item.GetProperty("body").GetString() ?? "No release notes supplied.").Trim();
                var release = new UpdateRelease(tag.TrimStart('v', 'V'), tag, notes[..Math.Min(notes.Length, 20_000)], RequireOfficialUri(installer), RequireOfficialUri(sums), size, prerelease);
                return new(true, release, $"NovaLauncher {release.Version} is available on the {channel.ToString().ToLowerInvariant()} channel.");
            }
            return new(true, null, "No newer release is available on the selected channel.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or JsonException or InvalidDataException or FormatException)
        { return new(false, null, $"Official release check failed safely: {exception.Message}"); }
    }

    public async Task<UpdateStageResult> StageAsync(UpdateRelease release, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        if (trustedCertificateSha256.Count == 0) return new(false, null, "Updates are disabled because this build has no trusted NovaLauncher signing-certificate pin.");
        if (!IsOfficialAssetUri(release.InstallerUri) || !IsOfficialAssetUri(release.ChecksumsUri)) return new(false, null, "Update staging refused a non-official asset URL.");
        Directory.CreateDirectory(stagingRoot);
        var temporary = Path.Combine(stagingRoot, $".{Guid.NewGuid():N}.download");
        try
        {
            var sums = System.Text.Encoding.ASCII.GetString(await GetBoundedAsync(release.ChecksumsUri, MaximumMetadataBytes, null, cancellationToken).ConfigureAwait(false));
            var name = Path.GetFileName(release.InstallerUri.LocalPath); var expectedHash = ParseChecksum(sums, name);
            var actualHash = await DownloadFileAsync(release.InstallerUri, temporary, release.InstallerBytes, progress, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, Convert.FromHexString(expectedHash))) throw new CryptographicException("Installer SHA-256 does not match the official checksum manifest.");
            var signature = authenticode.Verify(temporary, trustedCertificateSha256); if (!signature.Trusted) throw new CryptographicException(signature.Message);
            var staged = Path.Combine(stagingRoot, name); File.Move(temporary, staged, true);
            return new(true, staged, "The signed update was verified and staged. Installation still requires explicit user confirmation.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or CryptographicException or FormatException)
        { return new(false, null, $"Update staging failed safely: {exception.Message}"); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<UpdateLaunchResult> LaunchStagedAsync(UpdateRelease release, string stagedInstallerPath, CancellationToken cancellationToken)
    {
        if (trustedCertificateSha256.Count == 0) return new(false, "Installer launch is disabled because this build has no trusted NovaLauncher signing-certificate pin.");
        if (!IsOfficialAssetUri(release.InstallerUri) || !IsOfficialAssetUri(release.ChecksumsUri)) return new(false, "Installer launch refused a non-official release.");
        try
        {
            var expectedPath = Path.GetFullPath(Path.Combine(stagingRoot, Path.GetFileName(release.InstallerUri.LocalPath)));
            var actualPath = Path.GetFullPath(stagedInstallerPath);
            if (!string.Equals(expectedPath, actualPath, StringComparison.OrdinalIgnoreCase) || !File.Exists(actualPath)) return new(false, "The selected installer is not the expected launcher-owned staged file.");
            var info = new FileInfo(actualPath); if (info.Length != release.InstallerBytes) return new(false, "The staged installer size changed after verification.");
            var sums = System.Text.Encoding.ASCII.GetString(await GetBoundedAsync(release.ChecksumsUri, MaximumMetadataBytes, null, cancellationToken).ConfigureAwait(false));
            var expectedHash = ParseChecksum(sums, info.Name); var actualHash = await HashFileAsync(actualPath, cancellationToken).ConfigureAwait(false);
            if (!CryptographicOperations.FixedTimeEquals(actualHash, Convert.FromHexString(expectedHash))) return new(false, "The staged installer hash changed after verification.");
            var signature = authenticode.Verify(actualPath, trustedCertificateSha256); if (!signature.Trusted) return new(false, signature.Message);
            await recovery.RecordPendingAsync(release.Version, cancellationToken).ConfigureAwait(false);
            return installerLauncher.Launch(actualPath)
                ? new(true, "The verified installer was opened after explicit confirmation. Complete or cancel it in Windows Setup.")
                : new(false, "Windows did not open the verified installer.");
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or CryptographicException or FormatException)
        { return new(false, $"Installer handoff failed safely: {exception.Message}"); }
    }

    private async Task<byte[]> GetBoundedAsync(Uri uri, long maximum, long? exact, CancellationToken token, IProgress<double>? progress = null)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } length && (length > maximum || exact is { } expected && length != expected)) throw new InvalidDataException("Response size does not match trusted release metadata.");
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false); using var output = new MemoryStream(); var buffer = new byte[1024 * 1024];
        while (true) { var read = await input.ReadAsync(buffer, token).ConfigureAwait(false); if (read == 0) break; if (output.Length + read > maximum) throw new InvalidDataException("Response exceeded the safety limit."); await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false); if (exact is { } total) progress?.Report(output.Length / (double)total); }
        if (exact is { } required && output.Length != required) throw new InvalidDataException("Response ended before the declared size."); return output.ToArray();
    }
    private async Task<byte[]> DownloadFileAsync(Uri uri, string path, long exactBytes, IProgress<double>? progress, CancellationToken token)
    {
        using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false); response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is { } length && length != exactBytes) throw new InvalidDataException("Installer size does not match trusted release metadata.");
        await using var input = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
        await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[1024 * 1024]; long total = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, token).ConfigureAwait(false); if (read == 0) break;
            total = checked(total + read); if (total > exactBytes || total > MaximumInstallerBytes) throw new InvalidDataException("Installer exceeded the declared size.");
            hash.AppendData(buffer, 0, read); await output.WriteAsync(buffer.AsMemory(0, read), token).ConfigureAwait(false); progress?.Report(total / (double)exactBytes);
        }
        await output.FlushAsync(token).ConfigureAwait(false); if (total != exactBytes) throw new InvalidDataException("Installer download ended before the declared size."); return hash.GetHashAndReset();
    }
    private static async Task<byte[]> HashFileAsync(string path, CancellationToken token) { await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan); return await SHA256.HashDataAsync(stream, token).ConfigureAwait(false); }
    private static string ParseChecksum(string text, string fileName) => text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(static line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Where(static parts => parts.Length == 2).FirstOrDefault(parts => string.Equals(parts[1], fileName, StringComparison.Ordinal) && parts[0].Length == 64 && parts[0].All(Uri.IsHexDigit))?[0].ToLowerInvariant() ?? throw new InvalidDataException("The official checksum manifest does not contain the installer.");
    private static Uri RequireOfficialUri(JsonElement asset) { var value = asset.GetProperty("browser_download_url").GetString(); if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || !IsOfficialAssetUri(uri)) throw new InvalidDataException("Release metadata contained a non-official asset URL."); return uri; }
    private static bool IsOfficialAssetUri(Uri uri) => uri.Scheme == Uri.UriSchemeHttps && string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) && uri.AbsolutePath.StartsWith("/Lightyear4509/NovaLauncher/releases/download/", StringComparison.Ordinal);
    private static bool ChannelAccepts(UpdateChannel channel, string tag, bool prerelease) => channel switch { UpdateChannel.Stable => !prerelease, UpdateChannel.Beta => !prerelease || tag.Contains("beta", StringComparison.OrdinalIgnoreCase), _ => true };
    private static bool TryParseVersion(string value, out UpdateVersion version)
    {
        var parts = value.TrimStart('v', 'V').Split('-', 2); version = default;
        if (!Version.TryParse(parts[0], out var core)) return false;
        var suffix = parts.Length == 1 ? string.Empty : parts[1];
        var rank = suffix.Length == 0 ? 5 : suffix.StartsWith("rc", StringComparison.OrdinalIgnoreCase) ? 4 : suffix.StartsWith("beta", StringComparison.OrdinalIgnoreCase) ? 3 : suffix.StartsWith("alpha", StringComparison.OrdinalIgnoreCase) ? 2 : suffix.StartsWith("experimental", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var sequence = int.TryParse(suffix.Split('.').LastOrDefault(), out var parsed) ? parsed : 0;
        version = new(core, rank, sequence); return true;
    }
    private readonly record struct UpdateVersion(Version Core, int Rank, int Sequence) : IComparable<UpdateVersion>
    {
        public int CompareTo(UpdateVersion other) { var core = Core.CompareTo(other.Core); if (core != 0) return core; var rank = Rank.CompareTo(other.Rank); return rank != 0 ? rank : Sequence.CompareTo(other.Sequence); }
    }
}
