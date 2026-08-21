using System.Diagnostics;
using NovaLauncher.Application.Lifecycle;

namespace NovaLauncher.Infrastructure.Lifecycle;

public sealed class ShellUpdateInstallerLauncher : IUpdateInstallerLauncher
{
    public bool Launch(string verifiedInstallerPath)
    {
        if (!OperatingSystem.IsWindows()) return false;
        var process = Process.Start(new ProcessStartInfo
        {
            FileName = Path.GetFullPath(verifiedInstallerPath),
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(Path.GetFullPath(verifiedInstallerPath)),
        });
        process?.Dispose(); return process is not null;
    }
}
