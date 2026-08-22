using Microsoft.Win32;
using NovaLauncher.Application.Lifecycle;

namespace NovaLauncher.Infrastructure.Lifecycle;

public interface ICurrentUserRunKey
{
    string? Read(string name);
    void Write(string name, string value);
    void Delete(string name);
}

public sealed class WindowsCurrentUserRunKey : ICurrentUserRunKey
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public string? Read(string name)
    {
        if (!OperatingSystem.IsWindows()) return null;
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: false);
        return key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) as string;
    }

    public void Write(string name, string value)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Windows startup integration requires Windows.");
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true);
        key.SetValue(name, value, RegistryValueKind.String);
    }

    public void Delete(string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath, writable: true);
        key?.DeleteValue(name, throwOnMissingValue: false);
    }
}

public sealed class WindowsStartupIntegration(ICurrentUserRunKey runKey, string executablePath) : IStartupIntegration
{
    private const string ValueName = "NovaLauncher";
    private readonly string _expectedCommand = $"\"{Path.GetFullPath(executablePath)}\" --background";

    public StartupIntegrationResult GetStatus()
    {
        try
        {
            var value = runKey.Read(ValueName);
            var enabled = string.Equals(value, _expectedCommand, StringComparison.Ordinal);
            return new(true, enabled, enabled ? "Start with Windows is enabled." :
                value is null ? "Start with Windows is disabled." : "A stale NovaLauncher startup entry was detected and is not trusted.");
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return new(false, false, $"Windows startup status could not be read: {exception.Message}");
        }
    }

    public Task<StartupIntegrationResult> ConfigureAsync(bool enabled, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (enabled) runKey.Write(ValueName, _expectedCommand);
            else runKey.Delete(ValueName);
            var status = GetStatus();
            return Task.FromResult(status.Success && status.IsEnabled == enabled
                ? status
                : new StartupIntegrationResult(false, status.IsEnabled, "Windows startup integration could not be verified."));
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException)
        {
            return Task.FromResult(new StartupIntegrationResult(false, !enabled, $"Windows startup integration failed: {exception.Message}"));
        }
    }
}
