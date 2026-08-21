using System.Diagnostics;
using NovaLauncher.Application.GameTransfer;

namespace NovaLauncher.Infrastructure.GameTransfer;

public sealed class WindowsDefenderContentScanner : IReceivedContentScanner
{
    public async Task<ReceivedContentScanResult> ScanAsync(string directory, CancellationToken cancellationToken)
    {
        var platform = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidates = new[]
        {
            Path.Combine(platform, "Windows Defender", "MpCmdRun.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Microsoft", "Windows Defender", "Platform"),
        };
        var executable = File.Exists(candidates[0]) ? candidates[0] : FindLatestPlatformScanner(candidates[1]);
        if (executable is null) return new(false, false, "Windows Security command-line scanning is unavailable; no antivirus result is available.");
        var start = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        start.ArgumentList.Add("-Scan"); start.ArgumentList.Add("-ScanType"); start.ArgumentList.Add("3"); start.ArgumentList.Add("-File"); start.ArgumentList.Add(Path.GetFullPath(directory)); start.ArgumentList.Add("-DisableRemediation");
        using var process = Process.Start(start);
        if (process is null) return new(false, false, "Windows Security could not be started; the package remains staged and unlaunched.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        try { await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        return process.ExitCode == 0
            ? new(true, true, "Windows Security scan completed without a reported threat.")
            : new(true, false, $"Windows Security scan exited with code {process.ExitCode}.");
    }

    private static string? FindLatestPlatformScanner(string root)
    {
        if (!Directory.Exists(root)) return null;
        return Directory.EnumerateDirectories(root).OrderByDescending(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => Path.Combine(path, "MpCmdRun.exe")).FirstOrDefault(File.Exists);
    }
}
