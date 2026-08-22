using NovaLauncher.Application.Input;
using NovaLauncher.Infrastructure.Input;

namespace NovaLauncher.Infrastructure.Tests;

public sealed class WindowsXInputControllerInputTests
{
    [Fact]
    public void MapsDPadThumbstickAndActionButtons()
    {
        var buttons = WindowsXInputControllerInput.MapState(
            buttonMask: 0x0001 | 0x1000 | 0x2000 | 0x4000 | 0x0100 | 0x0200,
            thumbX: short.MaxValue,
            thumbY: short.MinValue);

        Assert.True(buttons.HasFlag(ControllerButtons.Up));
        Assert.True(buttons.HasFlag(ControllerButtons.Down));
        Assert.True(buttons.HasFlag(ControllerButtons.Right));
        Assert.True(buttons.HasFlag(ControllerButtons.Primary));
        Assert.True(buttons.HasFlag(ControllerButtons.Back));
        Assert.True(buttons.HasFlag(ControllerButtons.Context));
        Assert.True(buttons.HasFlag(ControllerButtons.Previous));
        Assert.True(buttons.HasFlag(ControllerButtons.Next));
    }

    [Fact]
    public void DeadZoneDoesNotCreatePhantomNavigation()
    {
        var buttons = WindowsXInputControllerInput.MapState(0, 4_000, -4_000);

        Assert.Equal(ControllerButtons.None, buttons);
    }

    [Fact]
    public void MapsGenericJoystickAxesPovAndButtons()
    {
        var buttons = WindowsXInputControllerInput.MapJoystickState(
            buttonMask: 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020,
            xPosition: 65_535,
            yPosition: 0,
            pov: 18_000);

        Assert.True(buttons.HasFlag(ControllerButtons.Up));
        Assert.True(buttons.HasFlag(ControllerButtons.Down));
        Assert.True(buttons.HasFlag(ControllerButtons.Right));
        Assert.True(buttons.HasFlag(ControllerButtons.Primary));
        Assert.True(buttons.HasFlag(ControllerButtons.Back));
        Assert.True(buttons.HasFlag(ControllerButtons.Context));
        Assert.True(buttons.HasFlag(ControllerButtons.Previous));
        Assert.True(buttons.HasFlag(ControllerButtons.Next));
    }

    [Fact]
    public void GenericJoystickUsesDeviceSpecificNeutralAxisCalibration()
    {
        var buttons = WindowsXInputControllerInput.MapJoystickState(
            buttonMask: 0,
            xPosition: 0,
            yPosition: 0,
            pov: 65_535,
            centerX: 0,
            centerY: 0);

        Assert.Equal(ControllerButtons.None, buttons);
    }

    [Fact]
    public void NativeProbeIsBoundedAndDoesNotRequireHardware()
    {
        using var input = new WindowsXInputControllerInput();

        var connected = input.TryGetState(out var state);

        Assert.NotEmpty(input.BackendName);
        if (connected) Assert.InRange(state.ControllerIndex, 0, 15);
    }
}
