using NovaLauncher.Domain.Library;

namespace NovaLauncher.Application.Launching;

public enum GameLaunchStatus
{
    Started,
    InvalidTarget,
    TargetMissing,
    Failed,
}

public sealed record GameLaunchResult(GameLaunchStatus Status, int? ProcessId, string? Error);

public interface IGameLauncher
{
    Task<GameLaunchResult> LaunchAsync(LaunchTarget target, CancellationToken cancellationToken);

    Task<GameLaunchResult> LaunchAsync(
        LaunchTarget target,
        bool runAsAdministrator,
        CancellationToken cancellationToken) => LaunchAsync(target, cancellationToken);

    Task<TimeSpan?> WaitForExitAsync(int processId, DateTimeOffset startedAtUtc, CancellationToken cancellationToken) =>
        Task.FromResult<TimeSpan?>(null);
}
