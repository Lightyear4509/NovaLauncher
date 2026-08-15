using NovaLauncher.Application.Persistence;

namespace NovaLauncher.Application.Themes;

public sealed record ThemeOption(string Id, string DisplayName);

public interface IThemeHost
{
    string CurrentThemeId { get; }

    bool Apply(string themeId);
}

public interface IThemeService
{
    IReadOnlyList<ThemeOption> Themes { get; }

    string CurrentThemeId { get; }

    string? TailscalePeerAddress { get; }

    Task<string?> InitializeAsync(CancellationToken cancellationToken);

    Task<string?> ApplyAsync(string themeId, CancellationToken cancellationToken);

    Task<string?> ConfigureTailscalePeerAsync(string address, CancellationToken cancellationToken);
}

public sealed class ThemeService(
    IThemeHost host,
    IDocumentStore<SettingsDocument> store) : IThemeService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SettingsDocument _settings = SettingsDocument.Default;

    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new("nova-dark", "Nova Dark"),
        new("midnight-blue", "Midnight Blue"),
        new("ember", "Ember"),
        new("forest", "Forest"),
        new("nova-light", "Nova Light"),
    ];

    public string CurrentThemeId => host.CurrentThemeId;

    public string? TailscalePeerAddress => _settings.Settings.TailscalePeerAddress;

    public async Task<string?> InitializeAsync(CancellationToken cancellationToken)
    {
        var load = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
        _settings = load.Document ?? SettingsDocument.Default;
        var themeId = Themes.Any(item => item.Id == _settings.Settings.ThemeId) ? _settings.Settings.ThemeId : "nova-dark";
        return host.Apply(themeId) ? load.Warning : "The saved theme could not be applied; Nova Dark is active.";
    }

    public async Task<string?> ApplyAsync(string themeId, CancellationToken cancellationToken)
    {
        if (!Themes.Any(item => item.Id == themeId)) return "Unknown theme selection.";
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previousId = host.CurrentThemeId;
            var previousSettings = _settings;
            if (!host.Apply(themeId)) return "The selected theme could not be applied.";
            var staged = _settings with { Settings = _settings.Settings with { ThemeId = themeId } };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved)
            {
                host.Apply(previousId);
                _settings = previousSettings;
                return save.Error ?? "Theme persistence failed; the previous theme was restored.";
            }

            _settings = staged;
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> ConfigureTailscalePeerAsync(string address, CancellationToken cancellationToken)
    {
        if (!SaveSync.TailscalePeerValidator.TryNormalize(address, out var normalized, out var error)) return error;
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var staged = _settings with { Settings = _settings.Settings with { TailscalePeerAddress = normalized } };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? "The peer address could not be saved.";
            _settings = staged;
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}
