using System.Runtime.InteropServices;
using NovaLauncher.Application.Input;

namespace NovaLauncher.Infrastructure.Input;

public sealed class WindowsXInputControllerInput : IControllerInputService, IDisposable
{
    private const uint ErrorSuccess = 0;
    private const short ThumbDeadZone = 12_000;
    private const uint JoystickReturnAll = 0x000000FF;
    private const uint JoystickCenteredPov = 0x0000FFFF;
    private const uint JoystickHasPov = 0x00000010;
    private const int JoystickCenter = 32_767;
    private const int JoystickDeadZone = 8_000;
    private readonly nint _library;
    private readonly nint _joystickLibrary;
    private readonly XInputGetStateDelegate? _getState;
    private readonly JoyGetPositionDelegate? _getJoystickPosition;
    private readonly JoyGetCapabilitiesDelegate? _getJoystickCapabilities;
    private readonly Dictionary<uint, (uint X, uint Y)> _joystickCenters = [];
    private readonly Dictionary<uint, JoystickCapabilities> _joystickCapabilityCache = [];
    private string _backendName = "Windows controller APIs unavailable";

    public WindowsXInputControllerInput()
    {
        if (!OperatingSystem.IsWindows()) return;
        foreach (var libraryName in new[] { "xinput1_4.dll", "xinput9_1_0.dll", "xinput1_3.dll" })
        {
            if (!NativeLibrary.TryLoad(libraryName, out var library)) continue;
            if (NativeLibrary.TryGetExport(library, "XInputGetState", out var export))
            {
                _library = library;
                _getState = Marshal.GetDelegateForFunctionPointer<XInputGetStateDelegate>(export);
                _backendName = "Windows XInput";
                break;
            }
            NativeLibrary.Free(library);
        }
        if (NativeLibrary.TryLoad("winmm.dll", out var joystickLibrary))
        {
            if (NativeLibrary.TryGetExport(joystickLibrary, "joyGetPosEx", out var joystickExport) &&
                NativeLibrary.TryGetExport(joystickLibrary, "joyGetDevCapsW", out var capabilitiesExport))
            {
                _joystickLibrary = joystickLibrary;
                _getJoystickPosition = Marshal.GetDelegateForFunctionPointer<JoyGetPositionDelegate>(joystickExport);
                _getJoystickCapabilities = Marshal.GetDelegateForFunctionPointer<JoyGetCapabilitiesDelegate>(capabilitiesExport);
                if (_getState is null) _backendName = "Windows generic joystick";
            }
            else NativeLibrary.Free(joystickLibrary);
        }
    }

    public string BackendName => _backendName;

    public bool TryGetState(out ControllerInputState state)
    {
        state = new(0, ControllerButtons.None);
        for (uint index = 0; _getState is not null && index < 4; index++)
        {
            if (_getState(index, out var native) != ErrorSuccess) continue;
            _backendName = "Windows XInput";
            state = new((int)index, MapState(native.Gamepad.Buttons, native.Gamepad.ThumbLX, native.Gamepad.ThumbLY));
            return true;
        }
        if (_getJoystickPosition is null || _getJoystickCapabilities is null) return false;
        for (uint index = 0; index < 16; index++)
        {
            if (!TryGetUsefulJoystickCapabilities(index, out var capabilities)) continue;
            var joystick = new JoystickInfo
            {
                Size = (uint)Marshal.SizeOf<JoystickInfo>(),
                Flags = JoystickReturnAll,
            };
            if (_getJoystickPosition(index, ref joystick) != ErrorSuccess) continue;
            _backendName = "Windows generic joystick";
            if (!_joystickCenters.TryGetValue(index, out var center))
            {
                center = (joystick.XPosition, joystick.YPosition);
                _joystickCenters[index] = center;
            }
            var pov = capabilities.HasPov ? joystick.Pov : JoystickCenteredPov;
            state = new((int)index, MapJoystickState(joystick.Buttons, joystick.XPosition, joystick.YPosition, pov, center.X, center.Y));
            return true;
        }
        _joystickCenters.Clear();
        return false;
    }

    private bool TryGetUsefulJoystickCapabilities(uint index, out JoystickCapabilities capabilities)
    {
        if (_joystickCapabilityCache.TryGetValue(index, out capabilities))
            return capabilities.IsUseful;

        capabilities = new JoystickCapabilities();
        var result = _getJoystickCapabilities!(index, ref capabilities, (uint)Marshal.SizeOf<JoystickCapabilities>());
        if (result != ErrorSuccess) return false;
        _joystickCapabilityCache[index] = capabilities;
        return capabilities.IsUseful;
    }

    public static ControllerButtons MapState(ushort buttonMask, short thumbX, short thumbY)
    {
        var result = ControllerButtons.None;
        if ((buttonMask & 0x0001) != 0 || thumbY > ThumbDeadZone) result |= ControllerButtons.Up;
        if ((buttonMask & 0x0002) != 0 || thumbY < -ThumbDeadZone) result |= ControllerButtons.Down;
        if ((buttonMask & 0x0004) != 0 || thumbX < -ThumbDeadZone) result |= ControllerButtons.Left;
        if ((buttonMask & 0x0008) != 0 || thumbX > ThumbDeadZone) result |= ControllerButtons.Right;
        if ((buttonMask & 0x1000) != 0) result |= ControllerButtons.Primary;
        if ((buttonMask & 0x2000) != 0) result |= ControllerButtons.Back;
        if ((buttonMask & 0x4000) != 0) result |= ControllerButtons.Context;
        if ((buttonMask & 0x0100) != 0) result |= ControllerButtons.Previous;
        if ((buttonMask & 0x0200) != 0) result |= ControllerButtons.Next;
        return result;
    }

    public static ControllerButtons MapJoystickState(
        uint buttonMask,
        uint xPosition,
        uint yPosition,
        uint pov,
        uint centerX = JoystickCenter,
        uint centerY = JoystickCenter)
    {
        var result = ControllerButtons.None;
        var xOffset = (long)xPosition - centerX;
        var yOffset = (long)yPosition - centerY;
        if (xOffset < -JoystickDeadZone) result |= ControllerButtons.Left;
        if (xOffset > JoystickDeadZone) result |= ControllerButtons.Right;
        if (yOffset < -JoystickDeadZone) result |= ControllerButtons.Up;
        if (yOffset > JoystickDeadZone) result |= ControllerButtons.Down;
        if ((buttonMask & 0x0001) != 0) result |= ControllerButtons.Primary;
        if ((buttonMask & 0x0002) != 0) result |= ControllerButtons.Back;
        if ((buttonMask & 0x0004) != 0) result |= ControllerButtons.Context;
        if ((buttonMask & 0x0010) != 0) result |= ControllerButtons.Previous;
        if ((buttonMask & 0x0020) != 0) result |= ControllerButtons.Next;
        if (pov != JoystickCenteredPov)
        {
            var degrees = pov / 100d;
            if (degrees >= 315 || degrees <= 45) result |= ControllerButtons.Up;
            if (degrees is >= 45 and <= 135) result |= ControllerButtons.Right;
            if (degrees is >= 135 and <= 225) result |= ControllerButtons.Down;
            if (degrees is >= 225 and <= 315) result |= ControllerButtons.Left;
        }
        return result;
    }

    public void Dispose()
    {
        if (_library != 0) NativeLibrary.Free(_library);
        if (_joystickLibrary != 0) NativeLibrary.Free(_joystickLibrary);
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint XInputGetStateDelegate(uint userIndex, out XInputState state);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint JoyGetPositionDelegate(uint joystickId, ref JoystickInfo information);

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate uint JoyGetCapabilitiesDelegate(uint joystickId, ref JoystickCapabilities capabilities, uint size);

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputState
    {
        public uint PacketNumber;
        public XInputGamepad Gamepad;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XInputGamepad
    {
        public ushort Buttons;
        public byte LeftTrigger;
        public byte RightTrigger;
        public short ThumbLX;
        public short ThumbLY;
        public short ThumbRX;
        public short ThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JoystickInfo
    {
        public uint Size;
        public uint Flags;
        public uint XPosition;
        public uint YPosition;
        public uint ZPosition;
        public uint RPosition;
        public uint UPosition;
        public uint VPosition;
        public uint Buttons;
        public uint ButtonNumber;
        public uint Pov;
        public uint Reserved1;
        public uint Reserved2;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct JoystickCapabilities
    {
        public ushort ManufacturerId;
        public ushort ProductId;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string ProductName;
        public uint XMinimum;
        public uint XMaximum;
        public uint YMinimum;
        public uint YMaximum;
        public uint ZMinimum;
        public uint ZMaximum;
        public uint ButtonCount;
        public uint PeriodMinimum;
        public uint PeriodMaximum;
        public uint RMinimum;
        public uint RMaximum;
        public uint UMinimum;
        public uint UMaximum;
        public uint VMinimum;
        public uint VMaximum;
        public uint Capabilities;
        public uint MaximumAxes;
        public uint AxisCount;
        public uint MaximumButtons;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string RegistryKey;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string OemDriver;

        public readonly bool HasPov => (Capabilities & JoystickHasPov) != 0;
        public readonly bool IsUseful => AxisCount > 0 || ButtonCount > 0 || HasPov;
    }
}
