namespace NovaLauncher.Domain.Settings;

public sealed record ProfileViewSettings(
    string LibraryViewMode,
    string LibraryCardSize,
    string LibrarySort,
    string LibrarySourceFilter,
    string LibraryPlatformFilter,
    string LibraryAvailabilityFilter,
    bool LibraryFavoritesOnly,
    string HomeSectionOrder,
    string HomeHiddenSections,
    bool SharedScreenMode = false);

public sealed record LauncherSettings(
    string ThemeId,
    bool ReduceMotion,
    bool ConfirmBeforeRemovingLibraryItems,
    string? TailscalePeerAddress = null,
    string LibraryViewMode = "Grid",
    string LibraryCardSize = "Medium",
    string LibrarySort = "Name",
    string LibrarySourceFilter = "All sources",
    string LibraryPlatformFilter = "All platforms",
    string LibraryAvailabilityFilter = "All games",
    bool LibraryFavoritesOnly = false,
    string UpdateChannel = "Stable",
    string HomeSectionOrder = "Highlights,RecentlyPlayed,MostPlayed",
    string HomeHiddenSections = "",
    bool ControllerMode = false,
    IReadOnlyDictionary<string, ProfileViewSettings>? ProfileViews = null,
    bool StartWithWindows = false,
    bool MinimizeToTray = false,
    double TextScale = 1,
    double FocusScale = 1,
    string ContrastPreset = "Standard",
    bool ShowControllerHints = true,
    string Culture = "en-US")
{
    public static LauncherSettings Default { get; } = new(
        "nova-dark",
        ReduceMotion: false,
        ConfirmBeforeRemovingLibraryItems: true,
        TailscalePeerAddress: null);
}
