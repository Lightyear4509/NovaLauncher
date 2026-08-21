namespace NovaLauncher.Application.Lifecycle;

public enum UpdateChannel { Stable, Beta, Alpha }

public sealed record UpdateRelease(string Version, string Tag, string ReleaseNotes, Uri InstallerUri, Uri ChecksumsUri, long InstallerBytes, bool IsPrerelease);
public sealed record UpdateCheckResult(bool Success, UpdateRelease? Release, string Message);
public sealed record UpdateStageResult(bool Success, string? StagedInstallerPath, string Message);
public sealed record CrashRecoveryState(bool PreviousSessionInterrupted, string Message);
public sealed record DiagnosticExportResult(bool Success, string? ExportPath, string Message);
public sealed record AuthenticodeVerification(bool Trusted, string Message);

public interface IUpdateService
{
    Task<UpdateCheckResult> CheckAsync(UpdateChannel channel, CancellationToken cancellationToken);
    Task<UpdateStageResult> StageAsync(UpdateRelease release, IProgress<double>? progress, CancellationToken cancellationToken);
}

public interface IAuthenticodeVerifier
{
    AuthenticodeVerification Verify(string path, IReadOnlySet<string> trustedCertificateSha256);
}

public interface ICrashRecoveryService
{
    CrashRecoveryState BeginSession();
    void CompleteSession();
}

public interface IDiagnosticExportService
{
    Task<DiagnosticExportResult> ExportAsync(string destinationPath, CancellationToken cancellationToken);
}
