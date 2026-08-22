namespace NovaLauncher.Application.Input;

public sealed class ControllerNavigationState
{
    public static readonly TimeSpan InitialRepeatDelay = TimeSpan.FromMilliseconds(350);
    public static readonly TimeSpan RepeatInterval = TimeSpan.FromMilliseconds(110);

    private ControllerButtons _previousButtons;
    private ControllerButtons _heldDirection;
    private DateTimeOffset _nextRepeatAt;

    public ControllerButtons Update(ControllerButtons buttons, DateTimeOffset now)
    {
        var pressed = buttons & ~_previousButtons;
        _previousButtons = buttons;

        var direction = SelectDirection(buttons & DirectionButtons, _heldDirection);
        if (direction == ControllerButtons.None)
        {
            _heldDirection = ControllerButtons.None;
            _nextRepeatAt = default;
            return pressed & ~DirectionButtons;
        }

        if (direction != _heldDirection)
        {
            _heldDirection = direction;
            _nextRepeatAt = now + InitialRepeatDelay;
            return (pressed & ~DirectionButtons) | direction;
        }

        if (now < _nextRepeatAt) return pressed & ~DirectionButtons;
        _nextRepeatAt = now + RepeatInterval;
        return (pressed & ~DirectionButtons) | direction;
    }

    public void Reset()
    {
        _previousButtons = ControllerButtons.None;
        _heldDirection = ControllerButtons.None;
        _nextRepeatAt = default;
    }

    private const ControllerButtons DirectionButtons =
        ControllerButtons.Up | ControllerButtons.Down | ControllerButtons.Left | ControllerButtons.Right;

    private static ControllerButtons SelectDirection(ControllerButtons directions, ControllerButtons previous)
    {
        if (previous != ControllerButtons.None && (directions & previous) != 0) return previous;
        if ((directions & ControllerButtons.Up) != 0) return ControllerButtons.Up;
        if ((directions & ControllerButtons.Down) != 0) return ControllerButtons.Down;
        if ((directions & ControllerButtons.Left) != 0) return ControllerButtons.Left;
        if ((directions & ControllerButtons.Right) != 0) return ControllerButtons.Right;
        return ControllerButtons.None;
    }
}
