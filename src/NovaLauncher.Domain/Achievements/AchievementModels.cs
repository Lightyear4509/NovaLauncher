using NovaLauncher.Domain.Library;

namespace NovaLauncher.Domain.Achievements;

public readonly record struct AchievementId(string Provider, string Value)
{
    public override string ToString() => $"{Provider}:{Value}";
}

public sealed record Achievement(
    AchievementId Id,
    string Name,
    string Description,
    bool IsUnlocked,
    DateTimeOffset? UnlockedAtUtc,
    string Provider);

public sealed record GameAchievements(
    GameId GameId,
    string Provider,
    IReadOnlyList<Achievement> Items,
    DateTimeOffset RefreshedAtUtc,
    bool IsStale)
{
    public int UnlockedCount => Items.Count(static item => item.IsUnlocked);

    public decimal CompletionPercent => Items.Count == 0 ? 0 : decimal.Round(UnlockedCount * 100m / Items.Count, 1);
}
