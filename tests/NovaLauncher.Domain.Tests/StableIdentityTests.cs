using NovaLauncher.Domain.Library;

namespace NovaLauncher.Domain.Tests;

public sealed class StableIdentityTests
{
    [Fact]
    public void SteamIdentityIsDeterministicAndNamespaced()
    {
        var first = GameId.FromSteamAppId(570);
        var second = GameId.FromSteamAppId(570);

        Assert.Equal(first, second);
        Assert.NotEqual(first, GameId.FromSteamAppId(730));
        Assert.NotEqual(Guid.Empty, first.Value);
    }

    [Fact]
    public void EmptyGameIdIsRejected()
    {
        Assert.Throws<ArgumentException>(() => new GameId(Guid.Empty));
    }

    [Fact]
    public void NewGameIdsAreNonEmptyAndDistinct()
    {
        var first = GameId.New();
        var second = GameId.New();

        Assert.NotEqual(Guid.Empty, first.Value);
        Assert.NotEqual(first, second);
    }
}
