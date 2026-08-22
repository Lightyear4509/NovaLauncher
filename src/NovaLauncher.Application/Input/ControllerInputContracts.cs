namespace NovaLauncher.Application.Input;

[Flags]
public enum ControllerButtons
{
    None = 0,
    Up = 1 << 0,
    Down = 1 << 1,
    Left = 1 << 2,
    Right = 1 << 3,
    Primary = 1 << 4,
    Back = 1 << 5,
    Previous = 1 << 6,
    Next = 1 << 7,
    Context = 1 << 8,
}

public sealed record ControllerInputState(int ControllerIndex, ControllerButtons Buttons);

public interface IControllerInputService
{
    string BackendName { get; }

    bool TryGetState(out ControllerInputState state);
}
