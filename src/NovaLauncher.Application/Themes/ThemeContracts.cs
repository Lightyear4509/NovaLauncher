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
    bool FavoritesOnly,
    bool SharedScreenMode = false);

public sealed record HomeViewPreferences(
    IReadOnlyList<string> SectionOrder,
    IReadOnlySet<string> HiddenSections);

public sealed record AccessibilityPreferences(
    double TextScale,
    double FocusScale,
    string ContrastPreset,
    bool ShowControllerHints,
    string Culture);

public interface IThemeHost
{
    string CurrentThemeId { get; }

    bool ReduceMotion { get; }

    bool Apply(string themeId);

    bool ApplyMotionPreference(bool reduceMotion);

    bool ApplyAccessibility(AccessibilityPreferences preferences) => true;
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

    bool StartWithWindows => false;

    bool MinimizeToTray => false;

    AccessibilityPreferences Accessibility => new(1, 1, "Standard", true, "en-US");

    void SetActiveProfile(Guid profileId) { }

    Task<string?> InitializeAsync(CancellationToken cancellationToken);

    Task<string?> ReloadAsync(CancellationToken cancellationToken) => InitializeAsync(cancellationToken);

    Task<string?> ApplyAsync(string themeId, CancellationToken cancellationToken);

    Task<string?> ConfigureReduceMotionAsync(bool reduceMotion, CancellationToken cancellationToken);

    Task<string?> ConfigureControllerModeAsync(bool enabled, CancellationToken cancellationToken);

    Task<string?> SaveLibraryPreferencesAsync(LibraryViewPreferences preferences, CancellationToken cancellationToken);

    Task<string?> SaveHomePreferencesAsync(HomeViewPreferences preferences, CancellationToken cancellationToken);

    Task<string?> ConfigureTailscalePeerAsync(string address, CancellationToken cancellationToken);

    Task<string?> ConfigureUpdateChannelAsync(string channel, CancellationToken cancellationToken);

    Task<string?> ConfigureStartupBehaviorAsync(bool startWithWindows, bool minimizeToTray, CancellationToken cancellationToken) =>
        Task.FromResult<string?>("Startup behavior is unavailable.");

    Task<string?> ConfigureAccessibilityAsync(AccessibilityPreferences preferences, CancellationToken cancellationToken) =>
        Task.FromResult<string?>("Accessibility preferences are unavailable.");
}

public sealed class ThemeService(
    IThemeHost host,
    IDocumentStore<SettingsDocument> store) : IThemeService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private SettingsDocument _settings = SettingsDocument.Default;
    private bool _initialized;
    private Guid _activeProfileId = NovaLauncher.Domain.Profiles.LocalProfileDefaults.DefaultProfileId;

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

    public LibraryViewPreferences LibraryPreferences
    {
        get
        {
            var value = ActiveProfileView;
            return new(value.LibraryViewMode, value.LibraryCardSize, value.LibrarySort, value.LibrarySourceFilter,
                value.LibraryPlatformFilter, value.LibraryAvailabilityFilter, value.LibraryFavoritesOnly, value.SharedScreenMode);
        }
    }

    public HomeViewPreferences HomePreferences
    {
        get
        {
            var value = ActiveProfileView;
            return new(ParseHomeSections(value.HomeSectionOrder), ParseHiddenHomeSections(value.HomeHiddenSections));
        }
    }

    public string? TailscalePeerAddress => _settings.Settings.TailscalePeerAddress;

    public string UpdateChannel => _settings.Settings.UpdateChannel;

    public bool StartWithWindows => _settings.Settings.StartWithWindows;

    public bool MinimizeToTray => _settings.Settings.MinimizeToTray;

    public AccessibilityPreferences Accessibility => new(
        _settings.Settings.TextScale,
        _settings.Settings.FocusScale,
        _settings.Settings.ContrastPreset,
        _settings.Settings.ShowControllerHints,
        _settings.Settings.Culture);

    public void SetActiveProfile(Guid profileId)
    {
        if (profileId == Guid.Empty) throw new ArgumentException("The active profile ID cannot be empty.", nameof(profileId));
        _activeProfileId = profileId;
    }

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
            if (!host.ApplyAccessibility(Accessibility))
                return "The saved accessibility preferences could not be applied; standard presentation is active.";
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
                    ProfileViews = WithActiveProfileView(ActiveProfileView with
                    {
                        LibraryViewMode = preferences.ViewMode,
                        LibraryCardSize = preferences.CardSize,
                        LibrarySort = preferences.Sort,
                        LibrarySourceFilter = preferences.SourceFilter,
                        LibraryPlatformFilter = preferences.PlatformFilter,
                        LibraryAvailabilityFilter = preferences.AvailabilityFilter,
                        LibraryFavoritesOnly = preferences.FavoritesOnly,
                        SharedScreenMode = preferences.SharedScreenMode,
                    }),
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
                    ProfileViews = WithActiveProfileView(ActiveProfileView with
                    {
                        HomeSectionOrder = string.Join(',', preferences.SectionOrder),
                        HomeHiddenSections = string.Join(',', preferences.HiddenSections.Order(StringComparer.Ordinal)),
                    }),
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

    private NovaLauncher.Domain.Settings.ProfileViewSettings ActiveProfileView =>
        _settings.Settings.ProfileViews?.GetValueOrDefault(_activeProfileId.ToString("N")) ?? new(
            _settings.Settings.LibraryViewMode,
            _settings.Settings.LibraryCardSize,
            _settings.Settings.LibrarySort,
            _settings.Settings.LibrarySourceFilter,
            _settings.Settings.LibraryPlatformFilter,
            _settings.Settings.LibraryAvailabilityFilter,
            _settings.Settings.LibraryFavoritesOnly,
            _settings.Settings.HomeSectionOrder,
            _settings.Settings.HomeHiddenSections,
            SharedScreenMode: false);

    private Dictionary<string, NovaLauncher.Domain.Settings.ProfileViewSettings> WithActiveProfileView(
        NovaLauncher.Domain.Settings.ProfileViewSettings value)
    {
        var views = new Dictionary<string, NovaLauncher.Domain.Settings.ProfileViewSettings>(
            _settings.Settings.ProfileViews ?? new Dictionary<string, NovaLauncher.Domain.Settings.ProfileViewSettings>(),
            StringComparer.Ordinal);
        views[_activeProfileId.ToString("N")] = value;
        return views;
    }

    public async Task<string?> ReloadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var load = await store.LoadAsync(cancellationToken).ConfigureAwait(false);
            if (load.Document is null) return load.Warning ?? "Settings could not be reloaded.";
            _settings = load.Document;
            return load.Warning;
        }
        finally { _gate.Release(); }
    }

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
            if (!host.ApplyAccessibility(Accessibility))
            {
                host.Apply(previousId);
                host.ApplyAccessibility(Accessibility);
                return "The selected theme could not preserve the accessibility presentation, so the previous theme was restored.";
            }
            var staged = _settings with { Settings = _settings.Settings with { ThemeId = themeId } };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved)
            {
                host.Apply(previousId);
                host.ApplyAccessibility(Accessibility);
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

    public async Task<string?> ConfigureStartupBehaviorAsync(bool startWithWindows, bool minimizeToTray, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var staged = _settings with { Settings = _settings.Settings with { StartWithWindows = startWithWindows, MinimizeToTray = minimizeToTray } };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved) return save.Error ?? "Startup behavior could not be saved.";
            _settings = staged;
            return null;
        }
        finally { _gate.Release(); }
    }

    public async Task<string?> ConfigureAccessibilityAsync(AccessibilityPreferences preferences, CancellationToken cancellationToken)
    {
        if (preferences.TextScale is < 1 or > 2 || preferences.FocusScale is < 1 or > 2 ||
            preferences.ContrastPreset is not ("Standard" or "High") ||
            preferences.Culture != Localization.InterfaceLocalizer.ReviewedCulture)
            return "The selected accessibility preferences are invalid.";
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var previous = Accessibility;
            if (!host.ApplyAccessibility(preferences)) return "The accessibility preferences could not be applied.";
            var staged = _settings with
            {
                Settings = _settings.Settings with
                {
                    TextScale = preferences.TextScale,
                    FocusScale = preferences.FocusScale,
                    ContrastPreset = preferences.ContrastPreset,
                    ShowControllerHints = preferences.ShowControllerHints,
                    Culture = preferences.Culture,
                }
            };
            var save = await store.SaveAsync(staged, cancellationToken).ConfigureAwait(false);
            if (save.Status != DocumentSaveStatus.Saved)
            {
                host.ApplyAccessibility(previous);
                return save.Error ?? "Accessibility preference persistence failed; the previous preferences were restored.";
            }
            _settings = staged;
            return null;
        }
        finally { _gate.Release(); }
    }

    public void Dispose() => _gate.Dispose();
}
