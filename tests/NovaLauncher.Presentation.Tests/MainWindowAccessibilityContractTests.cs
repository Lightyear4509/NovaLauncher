namespace NovaLauncher.Presentation.Tests;

public sealed class MainWindowAccessibilityContractTests
{
    [Fact]
    public void AppStartupDoesNotSynchronouslyBlockOnThemeInitialization()
    {
        var appCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "App.axaml.cs"));

        Assert.DoesNotContain("InitializeAsync(CancellationToken.None).GetAwaiter().GetResult()", appCode, StringComparison.Ordinal);
        Assert.Contains("Dispatcher.UIThread.Post(InitializeThemeAsync", appCode, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrimaryControlsExposeNamesStatusAndKeyboardAccess()
    {
        var xaml = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "MainWindow.axaml"),
            CancellationToken.None);

        Assert.Contains("AutomationProperties.Name=\"Search library\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Library sort order\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Choose an installed game executable\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Game card library\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Open game details\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Save manual game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Launch selected game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Remove selected game from NovaLauncher\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Export local NovaLauncher backup from Saves page\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Polite\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Preview Steam library import\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Steam import preview\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Import Steam games\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Refresh selected game metadata and artwork\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Selected game artwork\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Game cover artwork\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Choose custom artwork for manual game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Remove selected custom artwork from manual game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Manual game identity matching\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Confirm this game identity candidate\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Save protected manual game metadata\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Review provider artwork variants\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Stretch=\"UniformToFill\"", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Artwork.Cover.Location", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Refresh metadata\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Save game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Content=\"Play\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Request administrator permission for this game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"SteamGridDB API key\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Open Home page\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Open Library page\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Open Saves page\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Configure cross-device save linking\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Choose manual game save folder\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"NovaLauncher 24-hour device invitation\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Generate 24-hour invitation", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Copy six-digit invitation code\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Paste six-digit invitation code\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Preview bounded game transfer manifest\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Trusted game transfer recipient\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Attest game copy rights\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Authorize reviewed game transfer to selected peer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Authorized peer game transfer offers\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Download or resume verified peer game transfer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Pause active game transfer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Peer game transfer byte progress\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Local peer game transfer audit history\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Current save transfer progress\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Create shared save identity for selected game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Apply shared save identity to selected game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Derive shared save identity from paired credential\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Retry queued save uploads to paired device\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Upload selected game saves now\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Confirm upload of existing save files\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Decline upload of existing save files\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Open Settings and Diagnostics page\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Built-in color theme\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Apply selected built-in theme\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Tailscale peer IP address\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Official NovaLauncher update channel\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Check official NovaLauncher releases\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Verify and stage signed NovaLauncher installer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Official update download progress\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Official update release notes\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Confirm launching verified NovaLauncher installer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Open reverified NovaLauncher installer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Previous session recovery status\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Failed update rollback status\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Open verified previous NovaLauncher installer\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Export sanitized local diagnostics\"", xaml, StringComparison.Ordinal);
    }
}
