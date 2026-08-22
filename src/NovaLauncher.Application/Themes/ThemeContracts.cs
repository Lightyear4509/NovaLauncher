using NovaLauncher.Application.Persistence;

namespace NovaLauncher.Application.Themes;

public sealed record ThemeOption(string Id, string DisplayName);

public sealed record LibraryViewPreferences(
    string ViewMode,
    string CardSize,
    string Sort,
    string SourceFilter,
    string PlatformFilter,
    string AvailabilityFilter,
    bool FavoritesOnly);

public sealed record HomeViewPreferences(
    IReadOnlyList<string> SectionOrder,
    IReadOnlySet<string> HiddenSections);

public interface IThemeHost
{
    string CurrentThemeId { get; }

    bool ReduceMotion { get; }

    bool Apply(string themeId);

    bool ApplyMotionPreference(bool reduceMotion);
}

public interface IThemeService
{
    IReadOnlyList<ThemeOption> Themes { get; }

    string CurrentThemeId { get; }

    bool ReduceMotion { get; }

    bool ControllerMode { get; }

    LibraryViewPreferences LibraryPreferences { get; }

    HomeViewPreferences HomePreferences { get; }

    string? TailscalePeerAddress { get; }

    string UpdateChannel { get; }

    Task<string?> InitializeAsync(CancellationToken cancellationToken);

    Task<string?> ApplyAsync(string themeId, CancellationToken cancellationToken);

    Task<string?> ConfigureReduceMotionAsync(bool reduceMotion, CancellationToken cancellationToken);

    Task<string?> ConfigureControllerModeAsync(bool enabled, CancellationToken cancellationToken);

    Task<string?> SaveLibraryPreferencesAsync(LibraryViewPreferences preferences, CancellationToken cancellationToken);

    Task<string?> SaveHomePreferencesAsync(HomeViewPreferences preferences, CancellationToken cancellationToken);

    Task<string?> ConfigureTailscalePeerAsync(string address, CancellationToken cancellationToken);

    Task<string?> ConfigureUpdateChannelAsync(string channel, CancellationToken cancellationToken);
}

public sealed class ThemeService(
    IThemeHost host,
    IDocumentStore<SettingsDocument> store) : IThemeService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SettingsDocument _settings = SettingsDocument.Default;
    private bool _initialized;

    public IReadOnlyList<ThemeOption> Themes { get; } =
    [
        new("nova-dark", "Nova Dark"),
        new("midnight-blue", "Midnight Blue"),
        new("ember", "Ember"),
        new("forest", "Forest"),
        new("nova-light", "Nova Light"),
    ];

    public string CurrentThemeId => host.CurrentThemeId;

    public bool ReduceMotion => _settings.Settings.ReduceMotion;

    public bool ControllerMode => _settings.Settings.ControllerMode;

    public LibraryViewPreferences LibraryPreferences => new(
        _settings.Settings.LibraryViewMode,
        _settings.Settings.LibraryCardSize,
        _settings.Settings.LibrarySort,
        _settings.Settings.LibrarySourceFilter,
        _settings.Settings.LibraryPlatformFilter,
        _settings.Settings.LibraryAvailabilityFilter,
        _settings.Settings.LibraryFavoritesOnly);

    public HomeViewPreferences HomePreferences => new(
        ParseHomeSections(_settings.Settings.HomeSectionOrder),
        ParseHiddenHomeSections(_settings.Settings.HomeHiddenSections));

    public string? TailscalePeerAddress => _settings.Settings.TailscalePeerAddress;

    public string UpdateChannel => _settings.Settings.UpdateChannel;

    public async Task<string?> InitializeAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_initialized) return null;
            var load = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            _settings = load.Document ?? SettingsDocument.Default;
            if (load.Status == DocumentLoadStatus.MigratedLegacy)
            {
                var migrationSave = await store.SaveAsync(_settings, cancellationToken).ConfigureAwait(false);
                if (migrationSave.Status != DocumentSaveStatus.Saved) return migrationSave.Error ?? "The settings migration could not be committed; the previous file was preserved.";
            }
            var themeId = Themes.Any(item => item.Id == _settings.Settings.ThemeId) ? _settings.Settings.ThemeId : "nova-dark";
            if (!host.Apply(themeId)) return "The saved theme could not be applied; Nova Dark is active.";
            if (!host.ApplyMotionPreference(_settings.Settings.ReduceMotion))
                return "The saved motion preference could not be applied; standard motion is active.";
            _initialized = true;
            return load.Warning;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> SaveLibraryPreferencesAsync(LibraryViewPreferences preferences, CancellationToken cancellationToken)
    {
        if (!IsValidLibraryPreferences(preferences)) return "The selected Library view preferences are invalid.";
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var staged = _settings with
            {
                Settings = _settings.Settings with
                {
                    LibraryViewMode = preferences.ViewMode,
                    LibraryCardSize = preferences.CardSize,
                    LibrarySort = preferences.Sort,
                    LibrarySourceFilter = preferences.SourceFilter,
                    LibraryPlatformFilter = preferences.PlatformFilter,
                    LibraryAvailabilityFilter = preferences.AvailabilityFilter,
                    LibraryFavoritesOnly = preferences.FavoritesOnly,
                },
            };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? "Library preferences could not be saved.";
            _settings = staged;
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> SaveHomePreferencesAsync(HomeViewPreferences preferences, CancellationToken cancellationToken)
    {
        if (!IsValidHomePreferences(preferences)) return "The selected Home layout is invalid.";
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var staged = _settings with
            {
                Settings = _settings.Settings with
                {
                    HomeSectionOrder = string.Join(',', preferences.SectionOrder),
                    HomeHiddenSections = string.Join(',', preferences.HiddenSections.Order(StringComparer.Ordinal)),
                },
            };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? "Home layout preferences could not be saved.";
            _settings = staged;
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private static bool IsValidLibraryPreferences(LibraryViewPreferences value) =>
        value.ViewMode is "Grid" or "List" &&
        value.CardSize is "Small" or "Medium" or "Large" &&
        value.Sort is "Name" or "Recently played" or "Date added" or "Playtime" or "Release date" or "Platform" or "Recently updated" &&
        value.SourceFilter is "All sources" or "Manual" or "Steam" &&
        value.PlatformFilter is "All platforms" or "Windows" or "Linux" or "macOS" or "Other" &&
        value.AvailabilityFilter is "All games" or "Available" or "Missing target";

    private static readonly string[] HomeSectionIds = ["Highlights", "RecentlyPlayed", "MostPlayed"];

    private static string[] ParseHomeSections(string value)
    {
        var sections = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return sections.Length == HomeSectionIds.Length && sections.ToHashSet(StringComparer.Ordinal).SetEquals(HomeSectionIds)
            ? sections
            : HomeSectionIds;
    }

    private static HashSet<string> ParseHiddenHomeSections(string value) => value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(id => HomeSectionIds.Contains(id, StringComparer.Ordinal))
        .ToHashSet(StringComparer.Ordinal);

    private static bool IsValidHomePreferences(HomeViewPreferences value) =>
        value.SectionOrder.Count == HomeSectionIds.Length &&
        value.SectionOrder.ToHashSet(StringComparer.Ordinal).SetEquals(HomeSectionIds) &&
        value.HiddenSections.All(id => HomeSectionIds.Contains(id, StringComparer.Ordinal));

    public async Task<string?> ConfigureReduceMotionAsync(bool reduceMotion, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previous = _settings.Settings.ReduceMotion;
            if (!host.ApplyMotionPreference(reduceMotion)) return "The motion preference could not be applied.";
            var staged = _settings with { Settings = _settings.Settings with { ReduceMotion = reduceMotion } };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved)
            {
                host.ApplyMotionPreference(previous);
                return save.Error ?? "Motion preference persistence failed; the previous preference was restored.";
            }

            _settings = staged;
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<string?> ConfigureControllerModeAsync(bool enabled, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var staged = _settings with { Settings = _settings.Settings with { ControllerMode = enabled } };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? "Controller mode preference could not be saved.";
            _settings = staged;
            return null;
        }
        finally { _gate.Release(); }
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

    public async Task<string?> ConfigureUpdateChannelAsync(string channel, CancellationToken cancellationToken)
    {
        if (channel is not ("Stable" or "Beta" or "Alpha")) return "The selected update channel is invalid.";
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var staged = _settings with { Settings = _settings.Settings with { UpdateChannel = channel } };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? "The update channel could not be saved.";
            _settings = staged; return null;
        }
        finally { _gate.Release(); }
    }

    public void Dispose() => _gate.Dispose();
}
