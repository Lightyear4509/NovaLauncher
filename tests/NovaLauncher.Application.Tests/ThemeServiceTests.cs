using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Themes;
using NovaLauncher.Domain.Settings;
using NovaLauncher.Application.SaveSync;
using NovaLauncher.Domain.Profiles;

namespace NovaLauncher.Application.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public async Task CatalogContainsOnlyNovaAppearanceModesAndFallsBackFromRetiredPalette()
    {
        var host = new Host();
        var store = new Store(new SettingsDocument(1, LauncherSettings.Default with { ThemeId = "forest" }));
        using var service = new ThemeService(host, store);

        var error = await service.InitializeAsync(CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(2, service.Themes.Count);
        Assert.Equal("nova-dark", host.CurrentThemeId);
        Assert.Equal(["nova-dark", "nova-light"], service.Themes.Select(static item => item.Id));
    }

    [Fact]
    public async Task ApplyPersistsAtomicallyAndRollsBackRuntimeOnSaveFailure()
    {
        var host = new Host();
        var store = new Store(SettingsDocument.Default);
        using var service = new ThemeService(host, store);
        await service.InitializeAsync(CancellationToken.None);

        Assert.Null(await service.ApplyAsync("nova-light", CancellationToken.None));
        Assert.Equal("nova-light", host.CurrentThemeId);
        Assert.Equal("nova-light", store.Value!.Settings.ThemeId);

        store.FailSave = true;
        var error = await service.ApplyAsync("nova-dark", CancellationToken.None);
        Assert.NotNull(error);
        Assert.Equal("nova-light", host.CurrentThemeId);
        Assert.Equal("nova-light", store.Value.Settings.ThemeId);
    }

    [Fact]
    public async Task MotionPreferenceLoadsPersistsAndRollsBackOnSaveFailure()
    {
        var host = new Host();
        var store = new Store(new SettingsDocument(1, LauncherSettings.Default with { ReduceMotion = true }));
        using var service = new ThemeService(host, store);

        Assert.Null(await service.InitializeAsync(CancellationToken.None));
        Assert.True(host.ReduceMotion);
        Assert.True(service.ReduceMotion);

        store.FailSave = true;
        Assert.NotNull(await service.ConfigureReduceMotionAsync(false, CancellationToken.None));
        Assert.True(host.ReduceMotion);
        Assert.True(store.Value!.Settings.ReduceMotion);
    }

    [Fact]
    public async Task LibraryPreferencesPersistAtomicallyAndRejectUnknownValues()
    {
        var store = new Store(SettingsDocument.Default);
        using var service = new ThemeService(new Host(), store);
        await service.InitializeAsync(CancellationToken.None);
        var preferences = new LibraryViewPreferences("List", "Large", "Playtime", "Manual", "Windows", "Missing target", true);

        Assert.Null(await service.SaveLibraryPreferencesAsync(preferences, CancellationToken.None));
        Assert.Equal(preferences, service.LibraryPreferences);
        Assert.Equal("List", store.Value!.Settings.ProfileViews![LocalProfileDefaults.DefaultProfileId.ToString("N")].LibraryViewMode);

        var invalid = preferences with { ViewMode = "Downloaded view" };
        Assert.NotNull(await service.SaveLibraryPreferencesAsync(invalid, CancellationToken.None));
        Assert.Equal(preferences, service.LibraryPreferences);

        store.FailSave = true;
        Assert.NotNull(await service.SaveLibraryPreferencesAsync(preferences with { CardSize = "Small" }, CancellationToken.None));
        Assert.Equal("Large", service.LibraryPreferences.CardSize);
    }

    [Fact]
    public async Task HomePreferencesPersistOrderVisibilityAndRejectUnknownSections()
    {
        var store = new Store(SettingsDocument.Default);
        using var service = new ThemeService(new Host(), store);
        await service.InitializeAsync(CancellationToken.None);
        var preferences = new HomeViewPreferences(
            ["MostPlayed", "Highlights", "RecentlyPlayed"],
            new HashSet<string>(["RecentlyPlayed"], StringComparer.Ordinal));

        Assert.Null(await service.SaveHomePreferencesAsync(preferences, CancellationToken.None));
        Assert.Equal(preferences.SectionOrder, service.HomePreferences.SectionOrder);
        Assert.Contains("RecentlyPlayed", service.HomePreferences.HiddenSections);
        Assert.Equal("MostPlayed,Highlights,RecentlyPlayed", store.Value!.Settings.ProfileViews![LocalProfileDefaults.DefaultProfileId.ToString("N")].HomeSectionOrder);

        var invalid = preferences with { SectionOrder = ["MostPlayed", "Highlights", "Downloaded"] };
        Assert.NotNull(await service.SaveHomePreferencesAsync(invalid, CancellationToken.None));
        Assert.Equal(preferences.SectionOrder, service.HomePreferences.SectionOrder);
    }

    [Fact]
    public async Task UnknownThemeAndCancellationAreRejected()
    {
        var host = new Host();
        using var service = new ThemeService(host, new Store(SettingsDocument.Default));
        Assert.NotNull(await service.ApplyAsync("downloaded-theme", CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ApplyAsync("nova-light", cancellation.Token));
    }

    [Fact]
    public async Task AccessibilityPreferencesPersistRejectInvalidValuesAndRollBackOnFailure()
    {
        var host = new Host();
        var store = new Store(SettingsDocument.Default);
        using var service = new ThemeService(host, store);
        await service.InitializeAsync(CancellationToken.None);
        var selected = new AccessibilityPreferences(1.5, 1.75, "High", false, "en-US");

        Assert.Null(await service.ConfigureAccessibilityAsync(selected, CancellationToken.None));
        Assert.Equal(selected, service.Accessibility);
        Assert.Equal(selected, host.LastAccessibility);
        Assert.Equal("en-US", store.Value!.Settings.Culture);
        Assert.NotNull(await service.ConfigureAccessibilityAsync(selected with { TextScale = 2.25 }, CancellationToken.None));

        store.FailSave = true;
        Assert.NotNull(await service.ConfigureAccessibilityAsync(selected with { FocusScale = 1.5 }, CancellationToken.None));
        Assert.Equal(selected, service.Accessibility);

        store.FailSave = false;
        Assert.Null(await service.ApplyAsync("nova-light", CancellationToken.None));
        Assert.Equal(selected, host.LastAccessibility);
    }

    [Fact]
    public async Task LibraryAndHomePreferencesAreIsolatedPerLocalProfile()
    {
        var store = new Store(SettingsDocument.Default);
        using var service = new ThemeService(new Host(), store);
        await service.InitializeAsync(CancellationToken.None);
        var second = Guid.NewGuid();

        await service.SaveLibraryPreferencesAsync(
            new LibraryViewPreferences("List", "Large", "Playtime", "Manual", "Windows", "Available", true, true),
            CancellationToken.None);
        service.SetActiveProfile(second);
        Assert.Equal("Grid", service.LibraryPreferences.ViewMode);
        await service.SaveHomePreferencesAsync(
            new HomeViewPreferences(["MostPlayed", "Highlights", "RecentlyPlayed"], new HashSet<string>(["Highlights"])),
            CancellationToken.None);

        Assert.Equal("Grid", service.LibraryPreferences.ViewMode);
        Assert.Equal("MostPlayed", service.HomePreferences.SectionOrder[0]);
        service.SetActiveProfile(LocalProfileDefaults.DefaultProfileId);
        Assert.Equal("List", service.LibraryPreferences.ViewMode);
        Assert.True(service.LibraryPreferences.SharedScreenMode);
        Assert.Equal("Highlights", service.HomePreferences.SectionOrder[0]);
    }

    [Fact]
    public async Task StartupAndTrayRemainDisabledByDefaultAndPersistExplicitly()
    {
        var store = new Store(SettingsDocument.Default);
        using var service = new ThemeService(new Host(), store);
        await service.InitializeAsync(CancellationToken.None);

        Assert.False(service.StartWithWindows);
        Assert.False(service.MinimizeToTray);
        Assert.Null(await service.ConfigureStartupBehaviorAsync(true, true, CancellationToken.None));
        Assert.True(service.StartWithWindows);
        Assert.True(service.MinimizeToTray);
        Assert.Null(await service.ConfigureStartupBehaviorAsync(false, false, CancellationToken.None));
        Assert.False(store.Value!.Settings.StartWithWindows);
        Assert.False(store.Value.Settings.MinimizeToTray);
    }

    [Theory]
    [InlineData("100.64.0.1", true)]
    [InlineData("100.127.255.254", true)]
    [InlineData("fd7a:115c:a1e0::42", true)]
    [InlineData("192.168.1.10", false)]
    [InlineData("not-an-address", false)]
    public void PeerValidatorAcceptsOnlyTailscaleAddressRanges(string address, bool expected) =>
        Assert.Equal(expected, TailscalePeerValidator.TryNormalize(address, out _, out _));

    [Fact]
    public async Task PeerAddressIsValidatedAndPersistedWithoutEnablingTransfer()
    {
        var store = new Store(SettingsDocument.Default);
        using var service = new ThemeService(new Host(), store);
        await service.InitializeAsync(CancellationToken.None);

        Assert.NotNull(await service.ConfigureTailscalePeerAsync("192.168.1.2", CancellationToken.None));
        Assert.Null(store.Value!.Settings.TailscalePeerAddress);
        Assert.Null(await service.ConfigureTailscalePeerAsync("100.70.80.90", CancellationToken.None));
        Assert.Equal("100.70.80.90", service.TailscalePeerAddress);
        Assert.Equal("100.70.80.90", store.Value.Settings.TailscalePeerAddress);
    }

    private sealed class Host : IThemeHost
    {
        public string CurrentThemeId { get; private set; } = "nova-dark";
        public bool ReduceMotion { get; private set; }
        public AccessibilityPreferences? LastAccessibility { get; private set; }
        public bool Apply(string themeId) { CurrentThemeId = themeId; return true; }
        public bool ApplyMotionPreference(bool reduceMotion) { ReduceMotion = reduceMotion; return true; }
        public bool ApplyAccessibility(AccessibilityPreferences preferences) { LastAccessibility = preferences; return true; }
    }

    private sealed class Store(SettingsDocument initial) : IDocumentStore<SettingsDocument>
    {
        public SettingsDocument? Value { get; private set; } = initial;
        public bool FailSave { get; set; }
        public Task<DocumentLoadResult<SettingsDocument>> LoadAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new DocumentLoadResult<SettingsDocument>(DocumentLoadStatus.Loaded, Value, null));
        public Task<DocumentSaveResult> SaveAsync(SettingsDocument document, CancellationToken cancellationToken)
        {
            if (FailSave) return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Failed, "disk failure"));
            Value = document;
            return Task.FromResult(new DocumentSaveResult(DocumentSaveStatus.Saved, null));
        }
    }
}
