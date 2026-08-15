namespace NovaLauncher.Domain.Settings;

public sealed record LauncherSettings(
    string ThemeId,
    bool ReduceMotion,
    bool ConfirmBeforeRemovingLibraryItems,
    string? TailscalePeerAddress = null)
{
    public static LauncherSettings Default { get; } = new(
        "nova-dark",
        ReduceMotion: false,
        ConfirmBeforeRemovingLibraryItems: true,
        TailscalePeerAddress: null);
}
