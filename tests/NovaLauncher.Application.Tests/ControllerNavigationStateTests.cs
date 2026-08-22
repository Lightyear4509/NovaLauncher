using NovaLauncher.Application.Input;

namespace NovaLauncher.Application.Tests;

public sealed class ControllerNavigationStateTests
{
    [Fact]
    public void DirectionMovesImmediatelyThenRepeatsAtControlledRate()
    {
        var navigation = new ControllerNavigationState();
        var start = DateTimeOffset.UtcNow;

        Assert.Equal(ControllerButtons.Right, navigation.Update(ControllerButtons.Right, start));
        Assert.Equal(ControllerButtons.None, navigation.Update(ControllerButtons.Right, start.AddMilliseconds(349)));
        Assert.Equal(ControllerButtons.Right, navigation.Update(ControllerButtons.Right, start.AddMilliseconds(350)));
        Assert.Equal(ControllerButtons.None, navigation.Update(ControllerButtons.Right, start.AddMilliseconds(459)));
        Assert.Equal(ControllerButtons.Right, navigation.Update(ControllerButtons.Right, start.AddMilliseconds(460)));
    }

    [Fact]
    public void ReleasingAndPressingAgainMovesImmediately()
    {
        var navigation = new ControllerNavigationState();
        var start = DateTimeOffset.UtcNow;

        Assert.Equal(ControllerButtons.Down, navigation.Update(ControllerButtons.Down, start));
        Assert.Equal(ControllerButtons.None, navigation.Update(ControllerButtons.None, start.AddMilliseconds(50)));
        Assert.Equal(ControllerButtons.Down, navigation.Update(ControllerButtons.Down, start.AddMilliseconds(100)));
    }

    [Fact]
    public void DiagonalInputProducesOneStableMovementPerPoll()
    {
        var navigation = new ControllerNavigationState();
        var start = DateTimeOffset.UtcNow;

        Assert.Equal(ControllerButtons.Up, navigation.Update(ControllerButtons.Up | ControllerButtons.Right, start));
        Assert.Equal(ControllerButtons.None, navigation.Update(ControllerButtons.Up | ControllerButtons.Right, start.AddMilliseconds(100)));
        Assert.Equal(ControllerButtons.Up, navigation.Update(ControllerButtons.Up | ControllerButtons.Right, start.AddMilliseconds(350)));
    }

    [Fact]
    public void ActionButtonsRemainEdgeTriggeredWhileDirectionRepeats()
    {
        var navigation = new ControllerNavigationState();
        var start = DateTimeOffset.UtcNow;
        var held = ControllerButtons.Left | ControllerButtons.Primary;

        Assert.Equal(held, navigation.Update(held, start));
        var repeated = navigation.Update(held, start.AddMilliseconds(350));
        Assert.Equal(ControllerButtons.None, repeated & ControllerButtons.Primary);
        Assert.Equal(ControllerButtons.Left, repeated);
    }
}
