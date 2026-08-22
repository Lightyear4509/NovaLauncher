using NovaLauncher.Infrastructure.Lifecycle;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class WindowsStartupIntegrationTests
{
    [Fact]
    public async Task StartupIsDisabledByDefaultAndDisableRemovesOnlyNovaLauncherEntry()
    {
        var key = new FakeRunKey();
        var integration = new WindowsStartupIntegration(key, "C:\\Apps\\NovaLauncher.App.exe");

        Assert.False(integration.GetStatus().IsEnabled);
        var enabled = await integration.ConfigureAsync(true, CancellationToken.None);
        Assert.True(enabled.Success);
        Assert.True(enabled.IsEnabled);
        Assert.Equal("\"C:\\Apps\\NovaLauncher.App.exe\" --background", key.Values["NovaLauncher"]);

        key.Values["AnotherApp"] = "\"C:\\Other.exe\"";
        var disabled = await integration.ConfigureAsync(false, CancellationToken.None);
        Assert.True(disabled.Success);
        Assert.False(disabled.IsEnabled);
        Assert.False(key.Values.ContainsKey("NovaLauncher"));
        Assert.Equal("\"C:\\Other.exe\"", key.Values["AnotherApp"]);
    }

    [Fact]
    public void StaleEntryIsNotReportedAsEnabled()
    {
        var key = new FakeRunKey();
        key.Values["NovaLauncher"] = "\"C:\\Old\\NovaLauncher.exe\"";
        var status = new WindowsStartupIntegration(key, "C:\\Apps\\NovaLauncher.App.exe").GetStatus();

        Assert.True(status.Success);
        Assert.False(status.IsEnabled);
        Assert.Contains("stale", status.Message, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class FakeRunKey : ICurrentUserRunKey
    {
        public Dictionary<string, string> Values { get; } = new(StringComparer.Ordinal);
        public string? Read(string name) => Values.GetValueOrDefault(name);
        public void Write(string name, string value) => Values[name] = value;
        public void Delete(string name) => Values.Remove(name);
    }
}
