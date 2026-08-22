namespace NovaLauncher.Application.Lifecycle;

public sealed record StartupIntegrationResult(bool Success, bool IsEnabled, string Message);

public interface IStartupIntegration
{
    StartupIntegrationResult GetStatus();

    Task<StartupIntegrationResult> ConfigureAsync(bool enabled, CancellationToken cancellationToken);
}
