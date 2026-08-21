namespace NovaLauncher.Domain.Settings;

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
    string UpdateChannel = "Stable")
{
    public static LauncherSettings Default { get; } = new(
        "nova-dark",
        ReduceMotion: false,
        ConfirmBeforeRemovingLibraryItems: true,
        TailscalePeerAddress: null);
}
