using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace GameLauncher.Services;

public enum GamepadButton
{
    DPadUp, DPadDown, DPadLeft, DPadRight,
    A, B, X, Y,
    LeftShoulder, RightShoulder,
    Start, Back,
    LeftThumb, RightThumb
}

public sealed class XInputService : IDisposable
{
    private const uint ERROR_SUCCESS = 0;
    private const short STICK_DEADZONE = 18000;
    private const int POLL_MS = 60;

    // Button masks
    private const ushort XINPUT_GAMEPAD_DPAD_UP = 0x0001;
    private const ushort XINPUT_GAMEPAD_DPAD_DOWN = 0x0002;
    private const ushort XINPUT_GAMEPAD_DPAD_LEFT = 0x0004;
    private const ushort XINPUT_GAMEPAD_DPAD_RIGHT = 0x0008;
    private const ushort XINPUT_GAMEPAD_START = 0x0010;
    private const ushort XINPUT_GAMEPAD_BACK = 0x0020;
    private const ushort XINPUT_GAMEPAD_LEFT_THUMB = 0x0040;
    private const ushort XINPUT_GAMEPAD_RIGHT_THUMB = 0x0080;
    private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
    private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
    private const ushort XINPUT_GAMEPAD_A = 0x1000;
    private const ushort XINPUT_GAMEPAD_B = 0x2000;
    private const ushort XINPUT_GAMEPAD_X = 0x4000;
    private const ushort XINPUT_GAMEPAD_Y = 0x8000;

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState(uint dwUserIndex, ref XINPUT_STATE pState);

    private readonly DispatcherTimer _timer;
    private ushort _prevButtons;
    private bool _prevStickLeft, _prevStickRight, _prevStickUp, _prevStickDown;
    private bool _isConnected;
    private uint _userIndex;

    public event Action<GamepadButton>? ButtonPressed;
    public event Action<bool>? ConnectionChanged;

    public bool IsConnected => _isConnected;

    public XInputService(uint userIndex = 0)
    {
        _userIndex = userIndex;
        _timer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(POLL_MS)
        };
        _timer.Tick += Poll;
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();

    private void Poll(object? sender, EventArgs e)
    {
        var state = new XINPUT_STATE();
        uint result = XInputGetState(_userIndex, ref state);

        bool connected = result == ERROR_SUCCESS;
        if (connected != _isConnected)
        {
            _isConnected = connected;
            ConnectionChanged?.Invoke(connected);
        }

        if (!connected)
        {
            _prevButtons = 0;
            _prevStickLeft = _prevStickRight = _prevStickUp = _prevStickDown = false;
            return;
        }

        var gp = state.Gamepad;
        ushort buttons = gp.wButtons;
        ushort pressed = (ushort)(buttons & ~_prevButtons); // newly pressed

        CheckButton(pressed, XINPUT_GAMEPAD_DPAD_UP, GamepadButton.DPadUp);
        CheckButton(pressed, XINPUT_GAMEPAD_DPAD_DOWN, GamepadButton.DPadDown);
        CheckButton(pressed, XINPUT_GAMEPAD_DPAD_LEFT, GamepadButton.DPadLeft);
        CheckButton(pressed, XINPUT_GAMEPAD_DPAD_RIGHT, GamepadButton.DPadRight);
        CheckButton(pressed, XINPUT_GAMEPAD_A, GamepadButton.A);
        CheckButton(pressed, XINPUT_GAMEPAD_B, GamepadButton.B);
        CheckButton(pressed, XINPUT_GAMEPAD_X, GamepadButton.X);
        CheckButton(pressed, XINPUT_GAMEPAD_Y, GamepadButton.Y);
        CheckButton(pressed, XINPUT_GAMEPAD_LEFT_SHOULDER, GamepadButton.LeftShoulder);
        CheckButton(pressed, XINPUT_GAMEPAD_RIGHT_SHOULDER, GamepadButton.RightShoulder);
        CheckButton(pressed, XINPUT_GAMEPAD_START, GamepadButton.Start);
        CheckButton(pressed, XINPUT_GAMEPAD_BACK, GamepadButton.Back);

        // Left stick as digital direction (with edge detection)
        bool stickLeft = gp.sThumbLX < -STICK_DEADZONE;
        bool stickRight = gp.sThumbLX > STICK_DEADZONE;
        bool stickUp = gp.sThumbLY > STICK_DEADZONE;
        bool stickDown = gp.sThumbLY < -STICK_DEADZONE;

        if (stickLeft && !_prevStickLeft) ButtonPressed?.Invoke(GamepadButton.DPadLeft);
        if (stickRight && !_prevStickRight) ButtonPressed?.Invoke(GamepadButton.DPadRight);
        if (stickUp && !_prevStickUp) ButtonPressed?.Invoke(GamepadButton.DPadUp);
        if (stickDown && !_prevStickDown) ButtonPressed?.Invoke(GamepadButton.DPadDown);

        _prevButtons = buttons;
        _prevStickLeft = stickLeft;
        _prevStickRight = stickRight;
        _prevStickUp = stickUp;
        _prevStickDown = stickDown;
    }

    private void CheckButton(ushort pressed, ushort mask, GamepadButton button)
    {
        if ((pressed & mask) != 0)
            ButtonPressed?.Invoke(button);
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Poll;
    }
}
