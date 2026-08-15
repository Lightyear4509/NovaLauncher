using System.Diagnostics;
using NovaLauncher.Application.Launching;
using NovaLauncher.Domain.Library;

namespace NovaLauncher.Infrastructure.Launching;

public sealed class SafeGameLauncher : IGameLauncher
{
    private readonly IProcessStarter _processStarter;

    private static readonly HashSet<string> AllowedUriSchemes =
        new(StringComparer.OrdinalIgnoreCase) { "steam", "goggalaxy", "com.epicgames.launcher" };

    public SafeGameLauncher()
        : this(new PhysicalProcessStarter())
    {
    }

    public SafeGameLauncher(IProcessStarter processStarter)
    {
        _processStarter = processStarter ?? throw new ArgumentNullException(nameof(processStarter));
    }

    public Task<GameLaunchResult> LaunchAsync(LaunchTarget target, CancellationToken cancellationToken) =>
        LaunchAsync(target, runAsAdministrator: false, cancellationToken);

    public Task<GameLaunchResult> LaunchAsync(
        LaunchTarget target,
        bool runAsAdministrator,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            ProcessStartInfo startInfo;
            if (target.Kind == LaunchTargetKind.Executable)
            {
                if (!Path.IsPathFullyQualified(target.Target) ||
                    !string.Equals(Path.GetExtension(target.Target), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return Task.FromResult(new GameLaunchResult(GameLaunchStatus.InvalidTarget, null, "Invalid executable target."));
                }

                if (!File.Exists(target.Target))
                {
                    return Task.FromResult(new GameLaunchResult(GameLaunchStatus.TargetMissing, null, "The executable no longer exists."));
                }

                if (!string.IsNullOrWhiteSpace(target.WorkingDirectory) && !Directory.Exists(target.WorkingDirectory))
                {
                    return Task.FromResult(new GameLaunchResult(GameLaunchStatus.TargetMissing, null, "The working directory no longer exists."));
                }

                startInfo = new ProcessStartInfo(target.Target)
                {
                    UseShellExecute = runAsAdministrator,
                    WorkingDirectory = target.WorkingDirectory ?? Path.GetDirectoryName(target.Target)!,
                    Verb = runAsAdministrator ? "runas" : string.Empty,
                };
                foreach (var argument in target.Arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }
            }
            else
            {
                if (runAsAdministrator)
                {
                    return Task.FromResult(new GameLaunchResult(
                        GameLaunchStatus.InvalidTarget,
                        null,
                        "Administrator launch is available only for directly selected executable files."));
                }
                if (!Uri.TryCreate(target.Target, UriKind.Absolute, out var uri) || !AllowedUriSchemes.Contains(uri.Scheme))
                {
                    return Task.FromResult(new GameLaunchResult(GameLaunchStatus.InvalidTarget, null, "Unsafe launcher URI."));
                }

                startInfo = new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true };
            }

            using var process = _processStarter.Start(startInfo);
            return Task.FromResult(process is null
                ? new GameLaunchResult(GameLaunchStatus.Failed, null, "Windows did not start the target.")
                : new GameLaunchResult(GameLaunchStatus.Started, process.Id, null));
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return Task.FromResult(new GameLaunchResult(GameLaunchStatus.Failed, null, exception.Message));
        }
    }

    public async Task<TimeSpan?> WaitForExitAsync(
        int processId,
        DateTimeOffset startedAtUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            var elapsed = DateTimeOffset.UtcNow - startedAtUtc;
            return elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }
}

public interface IProcessStarter
{
    Process? Start(ProcessStartInfo startInfo);
}

public sealed class PhysicalProcessStarter : IProcessStarter
{
    public Process? Start(ProcessStartInfo startInfo) => Process.Start(startInfo);
}
