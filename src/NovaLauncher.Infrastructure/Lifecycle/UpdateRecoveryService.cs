using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using NovaLauncher.Application.Lifecycle;
using NovaLauncher.Domain;

namespace NovaLauncher.Infrastructure.Lifecycle;

public sealed partial class UpdateRecoveryService : IUpdateRecoveryService
{
    private const long MaximumCachedInstallerBytes = GitHubUpdateService.MaximumInstallerBytes;
    private readonly IAuthenticodeVerifier _authenticode;
    private readonly IUpdateInstallerLauncher _launcher;
    private readonly IReadOnlySet<string> _trustedPins;
    private readonly TimeProvider _timeProvider;
    private readonly string _receiptPath;
    private readonly string _cacheRoot;
    private readonly bool _receiptPresentAtStartup;

    public UpdateRecoveryService(string dataRoot, IAuthenticodeVerifier authenticode, IUpdateInstallerLauncher launcher, IReadOnlySet<string> trustedPins, TimeProvider timeProvider)
    {
        _authenticode = authenticode; _launcher = launcher; _trustedPins = trustedPins; _timeProvider = timeProvider;
        var updateRoot = Path.Combine(Path.GetFullPath(dataRoot), "Updates"); _receiptPath = Path.Combine(updateRoot, "pending-update.json"); _cacheRoot = Path.Combine(updateRoot, "InstallerCache"); _receiptPresentAtStartup = File.Exists(_receiptPath);
        State = ReadState();
    }

    public UpdateRecoveryState State { get; private set; }

    public async Task RecordPendingAsync(string targetVersion, CancellationToken cancellationToken)
    {
        if (!SafeVersion().IsMatch(targetVersion) || !SafeVersion().IsMatch(ProductIdentity.Version)) throw new InvalidDataException("Update recovery received an unsafe version.");
        Directory.CreateDirectory(Path.GetDirectoryName(_receiptPath)!); var temporary = _receiptPath + $".{Guid.NewGuid():N}.tmp";
        var receipt = new PendingUpdateReceipt(ProductIdentity.Version, targetVersion, _timeProvider.GetUtcNow());
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(receipt), cancellationToken).ConfigureAwait(false); File.Move(temporary, _receiptPath, true); State = ReadState();
    }

    public Task<UpdateLaunchResult> LaunchRollbackAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_trustedPins.Count == 0) return Task.FromResult(new UpdateLaunchResult(false, "Rollback is disabled because this build has no trusted publisher pin."));
        var receipt = ReadReceipt(); if (receipt is null) return Task.FromResult(new UpdateLaunchResult(false, "No valid pending update recovery receipt exists."));
        var installer = Path.Combine(_cacheRoot, $"NovaLauncher-Setup-{receipt.PreviousVersion}-win-x64.exe");
        if (!File.Exists(installer)) return Task.FromResult(new UpdateLaunchResult(false, "The previous signed NovaLauncher installer is not cached on this device."));
        var info = new FileInfo(installer); if (info.Length is <= 0 or > MaximumCachedInstallerBytes) return Task.FromResult(new UpdateLaunchResult(false, "The cached rollback installer has an invalid size."));
        var signature = _authenticode.Verify(installer, _trustedPins); if (!signature.Trusted) return Task.FromResult(new UpdateLaunchResult(false, signature.Message));
        var fileVersion = FileVersionInfo.GetVersionInfo(installer); var expectedCore = receipt.PreviousVersion.Split('-', 2)[0];
        if (!string.Equals($"{fileVersion.FileMajorPart}.{fileVersion.FileMinorPart}.{fileVersion.FileBuildPart}", expectedCore, StringComparison.Ordinal)) return Task.FromResult(new UpdateLaunchResult(false, "The cached installer version does not match the recovery receipt."));
        return Task.FromResult(_launcher.Launch(installer)
            ? new UpdateLaunchResult(true, $"The verified NovaLauncher {receipt.PreviousVersion} rollback installer was opened.")
            : new UpdateLaunchResult(false, "Windows did not open the verified rollback installer."));
    }

    public void CompleteHealthySession()
    {
        var receipt = ReadReceipt();
        if (receipt is not null && (string.Equals(receipt.TargetVersion, ProductIdentity.Version, StringComparison.OrdinalIgnoreCase) || _receiptPresentAtStartup && string.Equals(receipt.PreviousVersion, ProductIdentity.Version, StringComparison.OrdinalIgnoreCase)))
        {
            File.Delete(_receiptPath); State = new(false, "No update rollback is pending.");
        }
        CleanupCache();
    }

    private UpdateRecoveryState ReadState()
    {
        var receipt = ReadReceipt(); if (receipt is null) return new(false, "No update rollback is pending.");
        var installer = Path.Combine(_cacheRoot, $"NovaLauncher-Setup-{receipt.PreviousVersion}-win-x64.exe");
        return File.Exists(installer)
            ? new(true, $"Recovery is available for the previous NovaLauncher {receipt.PreviousVersion} installation.")
            : new(false, "An update was pending, but no previous signed installer is cached for automatic rollback.");
    }

    private PendingUpdateReceipt? ReadReceipt()
    {
        try
        {
            if (!File.Exists(_receiptPath) || new FileInfo(_receiptPath).Length is <= 0 or > 4096) return null;
            var receipt = JsonSerializer.Deserialize<PendingUpdateReceipt>(File.ReadAllText(_receiptPath));
            return receipt is not null && SafeVersion().IsMatch(receipt.PreviousVersion) && SafeVersion().IsMatch(receipt.TargetVersion) && receipt.CreatedAtUtc <= _timeProvider.GetUtcNow().AddMinutes(5) ? receipt : null;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException) { return null; }
    }

    private void CleanupCache()
    {
        if (!Directory.Exists(_cacheRoot)) return;
        foreach (var file in Directory.EnumerateFiles(_cacheRoot, "NovaLauncher-Setup-*-win-x64.exe", SearchOption.TopDirectoryOnly).Select(static path => new FileInfo(path)).OrderByDescending(static info => info.LastWriteTimeUtc).Skip(3)) file.Delete();
    }

    [GeneratedRegex(@"^[0-9]+\.[0-9]+\.[0-9]+(?:-[a-z]+(?:\.[0-9]+)?)?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex SafeVersion();
    private sealed record PendingUpdateReceipt(string PreviousVersion, string TargetVersion, DateTimeOffset CreatedAtUtc);
}
