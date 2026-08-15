using System.Diagnostics;
using NovaLauncher.Application.Launching;
using NovaLauncher.Domain.Library;
using NovaLauncher.Infrastructure.Launching;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class SafeGameLauncherTests
{
    [Theory]
    [InlineData("relative.exe")]
    [InlineData("C:\\Games\\script.cmd")]
    public async Task InvalidExecutableTargetsAreRejected(string target)
    {
        var result = await new SafeGameLauncher().LaunchAsync(
            new LaunchTarget(target, [], null, LaunchTargetKind.Executable),
            CancellationToken.None);

        Assert.Equal(GameLaunchStatus.InvalidTarget, result.Status);
    }

    [Fact]
    public async Task MissingExecutableIsReportedWithoutThrowing()
    {
        var result = await new SafeGameLauncher().LaunchAsync(
            new LaunchTarget("C:\\DefinitelyMissing\\Game.exe", [], null, LaunchTargetKind.Executable),
            CancellationToken.None);

        Assert.Equal(GameLaunchStatus.TargetMissing, result.Status);
    }

    [Fact]
    public async Task MissingWorkingDirectoryIsReported()
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");
        var result = await new SafeGameLauncher().LaunchAsync(
            new LaunchTarget(executable, [], "C:\\DefinitelyMissingDirectory", LaunchTargetKind.Executable),
            CancellationToken.None);

        Assert.Equal(GameLaunchStatus.TargetMissing, result.Status);
    }

    [Fact]
    public async Task ProcessStartFailureReturnsTypedFailure()
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");
        var result = await new SafeGameLauncher(new ThrowingProcessStarter()).LaunchAsync(
            new LaunchTarget(executable, [], Path.GetDirectoryName(executable), LaunchTargetKind.Executable),
            CancellationToken.None);

        Assert.Equal(GameLaunchStatus.Failed, result.Status);
        Assert.Equal("simulated launch failure", result.Error);
    }

    [Fact]
    public async Task NullProcessResultReturnsTypedFailure()
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");
        var result = await new SafeGameLauncher(new NullProcessStarter()).LaunchAsync(
            new LaunchTarget(executable, [], Path.GetDirectoryName(executable), LaunchTargetKind.Executable),
            CancellationToken.None);

        Assert.Equal(GameLaunchStatus.Failed, result.Status);
        Assert.Null(result.ProcessId);
    }

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("file:///C:/Windows/notepad.exe")]
    [InlineData("javascript:alert(1)")]
    public async Task UnsafeUriSchemesAreRejected(string target)
    {
        var result = await new SafeGameLauncher().LaunchAsync(
            new LaunchTarget(target, [], null, LaunchTargetKind.Uri),
            CancellationToken.None);

        Assert.Equal(GameLaunchStatus.InvalidTarget, result.Status);
    }

    [Fact]
    public async Task CancellationBeforeLaunchPropagates()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new SafeGameLauncher().LaunchAsync(
                new LaunchTarget("C:\\Game.exe", [], null, LaunchTargetKind.Executable),
                cancellation.Token));
    }

    [Fact]
    public async Task HarmlessWindowsExecutableStartsWithSeparatedArguments()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var wherePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");
        var result = await new SafeGameLauncher().LaunchAsync(
            new LaunchTarget(wherePath, ["dotnet"], Path.GetDirectoryName(wherePath), LaunchTargetKind.Executable),
            CancellationToken.None);

        Assert.Equal(GameLaunchStatus.Started, result.Status);
        Assert.True(result.ProcessId > 0);
    }

    [Fact]
    public async Task AdministratorPreferenceUsesWindowsShellRunAsWithoutBypassingConsent()
    {
        var executable = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "where.exe");
        var starter = new CapturingProcessStarter();

        await new SafeGameLauncher(starter).LaunchAsync(
            new LaunchTarget(executable, ["dotnet"], Path.GetDirectoryName(executable), LaunchTargetKind.Executable),
            runAsAdministrator: true,
            CancellationToken.None);

        Assert.NotNull(starter.StartInfo);
        Assert.True(starter.StartInfo.UseShellExecute);
        Assert.Equal("runas", starter.StartInfo.Verb);
        Assert.Equal(["dotnet"], starter.StartInfo.ArgumentList);
    }

    [Fact]
    public async Task AdministratorPreferenceIsRejectedForStorefrontUris()
    {
        var result = await new SafeGameLauncher(new NullProcessStarter()).LaunchAsync(
            new LaunchTarget("steam://run/570", [], null, LaunchTargetKind.Uri),
            runAsAdministrator: true,
            CancellationToken.None);

        Assert.Equal(GameLaunchStatus.InvalidTarget, result.Status);
    }

    private sealed class ThrowingProcessStarter : IProcessStarter
    {
        public Process? Start(ProcessStartInfo startInfo) =>
            throw new System.ComponentModel.Win32Exception("simulated launch failure");
    }

    private sealed class NullProcessStarter : IProcessStarter
    {
        public Process? Start(ProcessStartInfo startInfo) => null;
    }

    private sealed class CapturingProcessStarter : IProcessStarter
    {
        public ProcessStartInfo? StartInfo { get; private set; }

        public Process? Start(ProcessStartInfo startInfo)
        {
            StartInfo = startInfo;
            return null;
        }
    }
}
