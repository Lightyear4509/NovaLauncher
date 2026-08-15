using System.Diagnostics;
namespace NovaLauncher.UiSmokeTests;

public sealed class ResponsiveWindowSmokeTests
{
    [Fact]
    public async Task AppStartsWithResponsiveTitledWindow()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var appAssembly = typeof(NovaLauncher.App.App).Assembly.Location;
        var dotnetHost = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet";
        var testDataRoot = Path.Combine(Path.GetTempPath(), $"NovaLauncher-Smoke-{Guid.NewGuid():N}");
        var startInfo = new ProcessStartInfo(dotnetHost)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = Path.GetDirectoryName(appAssembly)!,
        };
        startInfo.ArgumentList.Add(appAssembly);
        startInfo.ArgumentList.Add("--smoke-test");
        startInfo.Environment["NOVALAUNCHER_TEST_DATA_ROOT"] = testDataRoot;

        using var process = Process.Start(startInfo);
        Assert.NotNull(process);

        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                process.Refresh();
                if (process.HasExited)
                {
                    break;
                }

                if (process.MainWindowHandle != IntPtr.Zero &&
                    process.Responding &&
                    string.Equals(process.MainWindowTitle, "NovaLauncher", StringComparison.Ordinal))
                {
                    return;
                }

                await Task.Delay(100);
            }

            var exitDescription = process.HasExited ? $"exit code {process.ExitCode}" : "still running without a responsive window";
            var standardError = process.HasExited ? await process.StandardError.ReadToEndAsync() : string.Empty;
            Assert.Fail($"NovaLauncher did not expose a responsive titled window: {exitDescription}. {standardError}");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            if (Directory.Exists(testDataRoot))
            {
                await DeleteDirectoryWithRetryAsync(testDataRoot);
            }
        }
    }

    private static async Task DeleteDirectoryWithRetryAsync(string path)
    {
        for (var attempt = 0; attempt < 10; attempt++)
        {
            try
            {
                Directory.Delete(path, recursive: true);
                return;
            }
            catch (IOException) when (attempt < 9)
            {
                await Task.Delay(100);
            }
        }
    }
}
