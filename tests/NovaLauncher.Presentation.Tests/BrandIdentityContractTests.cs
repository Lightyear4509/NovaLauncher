namespace NovaLauncher.Presentation.Tests;

public sealed class BrandIdentityContractTests
{
    private static readonly string[] RequiredBrushes =
    [
        "ThemeBackground",
        "ThemeSurface",
        "ThemeElevated",
        "ThemeBorder",
        "ThemeText",
        "ThemeMuted",
        "ThemeAccent",
        "ThemeAccentSecondary",
        "ThemeSuccess",
        "ThemeWarning",
        "ThemeDanger",
        "ThemeOverlay",
        "ThemeNavSelected",
        "ThemeButton",
        "ThemeButtonHover",
        "ThemeButtonPressed",
    ];

    [Fact]
    public async Task ApplicationDefinesTheCompleteBrandTokenContract()
    {
        var xaml = await ReadAsync("App.axaml");
        var host = await ReadAsync("AvaloniaThemeHost.cs");

        foreach (var brush in RequiredBrushes)
        {
            Assert.Contains($"x:Key=\"{brush}\"", xaml, StringComparison.Ordinal);
            Assert.Contains($"resources[\"{brush}\"]", host, StringComparison.Ordinal);
        }

        Assert.Contains("Selector=\"Button.primary\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.nav\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Button.game-card\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.card\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Selector=\"TextBlock.page-title\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MotionDurationsStayWithinTheDocumentedInteractionBudget()
    {
        var xaml = await ReadAsync("App.axaml");
        Assert.Contains("<x:TimeSpan x:Key=\"MotionDuration\">0:0:0.16</x:TimeSpan>", xaml, StringComparison.Ordinal);
        Assert.Equal(5, xaml.Split("Duration=\"{DynamicResource MotionDuration}\"", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public async Task ShellUsesOriginalBrandAssetAndAccessibleIdentityStates()
    {
        var xaml = await ReadPresentationAsync();

        Assert.Contains("novalauncher-mark-concept.png", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"NovaLauncher brand mark\"", xaml, StringComparison.Ordinal);
        Assert.Contains("YOUR GAMES · YOUR SPACE", xaml, StringComparison.Ordinal);
        Assert.Contains("LOCAL-FIRST", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"NOVA\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"LAUNCHER\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Classes=\"primary hero-action\" Content=\"Play\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Classes=\"game-tile\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Reduce interface motion\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Toggle navigation size\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Go to previous page\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Go to next page\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"NovaLauncher operation in progress\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinWidth=\"880\"", xaml, StringComparison.Ordinal);
        Assert.Contains("MinHeight=\"640\"", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ReusableFeedbackAndEmptyStateComponentsRemainAvailable()
    {
        var app = await ReadAsync("App.axaml");
        var window = await ReadPresentationAsync();

        Assert.Contains("Selector=\"Border.dialog\"", app, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.toast\"", app, StringComparison.Ordinal);
        Assert.Contains("Selector=\"Border.empty-state\"", app, StringComparison.Ordinal);
        Assert.Contains("Selector=\"ProgressBar\"", app, StringComparison.Ordinal);
        Assert.Contains("Selector=\"ToolTip\"", app, StringComparison.Ordinal);
        Assert.Contains("Classes=\"empty-state\"", window, StringComparison.Ordinal);
    }

    [Fact]
    public void WindowsIconContainsRequiredMultiSizeEntries()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "NovaLauncher.ico");
        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream);

        Assert.Equal(0, reader.ReadUInt16());
        Assert.Equal(1, reader.ReadUInt16());
        Assert.Equal(7, reader.ReadUInt16());
    }

    [Fact]
    public async Task PhaseTwoHomeUsesHonestLocalDashboardStates()
    {
        var xaml = await ReadPresentationAsync();

        Assert.Contains("AutomationProperties.Name=\"Launch featured game\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Ordered Home sections\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Home section layout\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Workspace.TotalLibraryPlayTime", xaml, StringComparison.Ordinal);
        Assert.Contains("monitored locally", xaml, StringComparison.Ordinal);
        Assert.DoesNotContain("Not tracked. NovaLauncher does not monitor playtime", xaml, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PhaseTwoLibraryExposesLocalFiltersAndResponsiveViewControls()
    {
        var xaml = await ReadPresentationAsync();

        Assert.Contains("AutomationProperties.Name=\"Search library\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Filter library by source\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Filter library by platform\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Filter library by target availability\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Library sort order\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Show library as grid\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Show library as list\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Library card size\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Missing game target warning\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Filter library by collection\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Clear library collection filter\"", xaml, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"Locate replacement executable for selected manual game\"", xaml, StringComparison.Ordinal);
    }

    private static Task<string> ReadAsync(string fileName) =>
        File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, fileName), CancellationToken.None);

    private static async Task<string> ReadPresentationAsync()
    {
        var files = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.axaml", SearchOption.AllDirectories)
            .Where(path => Path.GetFileName(path) != "App.axaml")
            .OrderBy(static path => path, StringComparer.Ordinal);
        var content = new List<string>();
        foreach (var file in files) content.Add(await File.ReadAllTextAsync(file, CancellationToken.None));
        return string.Join(Environment.NewLine, content);
    }
}
