using NovaLauncher.Application.Persistence;
using NovaLauncher.Application.Themes;
using NovaLauncher.Domain.Settings;
using NovaLauncher.Application.SaveSync;

namespace NovaLauncher.Application.Tests;

public sealed class ThemeServiceTests
{
    [Fact]
    public async Task CatalogContainsFiveTrustedThemesAndLoadsSavedSelection()
    {
        var host = new Host();
        var store = new Store(new SettingsDocument(1, LauncherSettings.Default with { ThemeId = "forest" }));
        using var service = new ThemeService(host, store);

        var error = await service.InitializeAsync(CancellationToken.None);

        Assert.Null(error);
        Assert.Equal(5, service.Themes.Count);
        Assert.Equal("forest", host.CurrentThemeId);
        Assert.Equal(5, service.Themes.Select(static item => item.Id).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task ApplyPersistsAtomicallyAndRollsBackRuntimeOnSaveFailure()
    {
        var host = new Host();
        var store = new Store(SettingsDocument.Default);
        using var service = new ThemeService(host, store);
        await service.InitializeAsync(CancellationToken.None);

        Assert.Null(await service.ApplyAsync("ember", CancellationToken.None));
        Assert.Equal("ember", host.CurrentThemeId);
        Assert.Equal("ember", store.Value!.Settings.ThemeId);

        store.FailSave = true;
        var error = await service.ApplyAsync("forest", CancellationToken.None);
        Assert.NotNull(error);
        Assert.Equal("ember", host.CurrentThemeId);
        Assert.Equal("ember", store.Value.Settings.ThemeId);
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
    public async Task UnknownThemeAndCancellationAreRejected()
    {
        var host = new Host();
        using var service = new ThemeService(host, new Store(SettingsDocument.Default));
        Assert.NotNull(await service.ApplyAsync("downloaded-theme", CancellationToken.None));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ApplyAsync("forest", cancellation.Token));
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
        public bool Apply(string themeId) { CurrentThemeId = themeId; return true; }
        public bool ApplyMotionPreference(bool reduceMotion) { ReduceMotion = reduceMotion; return true; }
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
