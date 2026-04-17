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
    private const int HID_SCAN_INTERVAL_MS = 3000;

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

    private static readonly (int Vid, int Pid, string Name)[] KnownHidControllers =
    [
        (0x054C, 0x0CE6, "DualSense"),          // PS5 DualSense
        (0x054C, 0x0DF2, "DualSense Edge"),      // PS5 DualSense Edge
        (0x054C, 0x09CC, "DualShock 4 v2"),      // PS4 DualShock 4 v2
        (0x054C, 0x05C4, "DualShock 4 v1"),      // PS4 DualShock 4 v1
    ];

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

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
    private static extern uint XInputGetBatteryInformation(uint dwUserIndex, byte devType, ref XINPUT_BATTERY_INFORMATION pBatteryInfo);

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_VIBRATION
    {
        public ushort wLeftMotorSpeed;
        public ushort wRightMotorSpeed;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")]
    private static extern uint XInputSetState(uint dwUserIndex, ref XINPUT_VIBRATION pVibration);

    private const byte BATTERY_DEVTYPE_GAMEPAD = 0x00;
    private const byte BATTERY_TYPE_DISCONNECTED = 0x00;
    private const byte BATTERY_TYPE_WIRED = 0x01;
    private const byte BATTERY_LEVEL_EMPTY = 0x00;
    private const byte BATTERY_LEVEL_LOW = 0x01;
    private const byte BATTERY_LEVEL_MEDIUM = 0x02;
    private const byte BATTERY_LEVEL_FULL = 0x03;

    [StructLayout(LayoutKind.Sequential)]
    private struct XINPUT_BATTERY_INFORMATION
    {
        public byte BatteryType;
        public byte BatteryLevel;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr CreateFile(string lpFileName, uint dwDesiredAccess,
        uint dwShareMode, IntPtr lpSecurityAttributes, uint dwCreationDisposition,
        uint dwFlagsAndAttributes, IntPtr hTemplateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, uint nNumberOfBytesToRead,
        out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);

    [DllImport("hid.dll")]
    private static extern void HidD_GetHidGuid(out Guid hidGuid);

    [DllImport("setupapi.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, IntPtr enumerator,
        IntPtr hwndParent, uint flags);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet,
        IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet,
        ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
        ref SP_DEVICE_INTERFACE_DETAIL_DATA deviceInterfaceDetailData,
        uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

    [DllImport("setupapi.dll")]
    private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

    [DllImport("setupapi.dll", SetLastError = true)]
    private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet,
        uint memberIndex, ref SP_DEVINFO_DATA deviceInfoData);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDevicePropertyW(IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData, ref DEVPROPKEY propertyKey,
        out uint propertyType, [Out] byte[] propertyBuffer, uint propertyBufferSize,
        out uint requiredSize, uint flags);

    [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetupDiGetDeviceInstanceIdW(IntPtr deviceInfoSet,
        ref SP_DEVINFO_DATA deviceInfoData, [Out] char[] deviceInstanceId,
        uint deviceInstanceIdSize, out uint requiredSize);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Locate_DevNodeW(out uint pdnDevInst, string pDeviceID, uint ulFlags);

    [DllImport("CfgMgr32.dll", CharSet = CharSet.Unicode)]
    private static extern uint CM_Get_DevNode_PropertyW(uint dnDevInst, ref DEVPROPKEY propertyKey,
        out uint propertyType, [Out] byte[] propertyBuffer, ref uint propertyBufferSize, uint ulFlags);

    [DllImport("hid.dll")]
    private static extern bool HidD_GetAttributes(IntPtr hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

    [DllImport("hid.dll")]
    private static extern bool HidD_GetPreparsedData(IntPtr hidDeviceObject, out IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

    [DllImport("hid.dll")]
    private static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

    private const uint DIGCF_PRESENT = 0x02;
    private const uint DIGCF_DEVICEINTERFACE = 0x10;
    private const uint DIGCF_ALLCLASSES = 0x04;
    private const uint DEVPROP_TYPE_BYTE = 0x00000003;
    private const uint DEVPROP_TYPE_STRING = 0x00000012;
    private const uint CM_LOCATE_DEVNODE_NORMAL = 0x00000000;
    private const uint CR_SUCCESS = 0;
    private const uint GENERIC_READ = 0x80000000;
    private const uint FILE_SHARE_READ = 0x01;
    private const uint FILE_SHARE_WRITE = 0x02;
    private const uint OPEN_EXISTING = 3;
    private const uint FILE_FLAG_OVERLAPPED = 0x40000000;
    private static readonly IntPtr INVALID_HANDLE = new(-1);

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVICE_INTERFACE_DATA
    {
        public int cbSize;
        public Guid InterfaceClassGuid;
        public int Flags;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct SP_DEVICE_INTERFACE_DETAIL_DATA
    {
        public int cbSize;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 512)]
        public string DevicePath;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDD_ATTRIBUTES
    {
        public int Size;
        public ushort VendorID;
        public ushort ProductID;
        public ushort VersionNumber;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct HIDP_CAPS
    {
        public ushort Usage;
        public ushort UsagePage;
        public ushort InputReportByteLength;
        public ushort OutputReportByteLength;
        public ushort FeatureReportByteLength;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
        public ushort[] Reserved;
        public ushort NumberLinkCollectionNodes;
        public ushort NumberInputButtonCaps;
        public ushort NumberInputValueCaps;
        public ushort NumberInputDataIndices;
        public ushort NumberOutputButtonCaps;
        public ushort NumberOutputValueCaps;
        public ushort NumberOutputDataIndices;
        public ushort NumberFeatureButtonCaps;
        public ushort NumberFeatureValueCaps;
        public ushort NumberFeatureDataIndices;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SP_DEVINFO_DATA
    {
        public int cbSize;
        public Guid ClassGuid;
        public uint DevInst;
        public IntPtr Reserved;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DEVPROPKEY
    {
        public Guid fmtid;
        public uint pid;
    }

    private static readonly DEVPROPKEY DEVPKEY_Device_BatteryLevel = new()
    {
        fmtid = new Guid(0x104EA319, 0x6EE2, 0x4701, 0xBD, 0x47, 0x8D, 0xDB, 0xF4, 0x25, 0xBB, 0xE5),
        pid = 2
    };

    private static readonly DEVPROPKEY DEVPKEY_Device_FriendlyName = new()
    {
        fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
        pid = 14
    };

    private static readonly DEVPROPKEY DEVPKEY_Device_DeviceDesc = new()
    {
        fmtid = new Guid(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0),
        pid = 2
    };

    private IntPtr _hidHandle = INVALID_HANDLE;
    private int _hidReportLength;
    private byte[] _prevHidReport = [];
    private string _hidControllerName = "";
    private bool _hidConnected;

    private readonly DispatcherTimer _timer;
    private ushort _prevButtons;
    private bool _prevStickLeft, _prevStickRight, _prevStickUp, _prevStickDown;
    private bool _isConnected;
    private uint _activeXInputIndex = uint.MaxValue;
    private int _hidScanCounter;

    public event Action<GamepadButton>? ButtonPressed;
    public event Action<bool>? ConnectionChanged;
    public event Action<double>? RightStickY;
    public event Action<int>? BatteryChanged;

    /// <summary>
    /// Disparado quando Start+Y são pressionados juntos por ~0.5 s (abre GLauncher AI).
    /// </summary>
    public event Action? AiComboTriggered;

    // Controle interno do combo Start+Y
    private int _aiComboTicks;
    private bool _aiComboFired;
    private const int AI_COMBO_TICKS_REQUIRED = 8; // 8 × 60ms ≈ 0.5 s

    public bool IsConnected => _isConnected;
    public string ControllerName { get; private set; } = "";
    public int BatteryPercent { get; private set; } = -1;

    private int _lastBatteryPoll;
    private const int BATTERY_POLL_INTERVAL = 50; // ~3 seconds at 60ms poll
    private const int BT_SEARCH_INTERVAL = 10;    // search every ~30s (10 * 3s)
    private string? _btBatteryDeviceId;
    private int _btBatterySearchCooldown;

    public XInputService()
    {
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
        if (TryPollXInput())
            return;

        PollHid();
    }

    private bool TryPollXInput()
    {
        var state = new XINPUT_STATE();

        if (_activeXInputIndex < 4)
        {
            uint result = XInputGetState(_activeXInputIndex, ref state);
            if (result == ERROR_SUCCESS)
            {
                SetConnected(true, "Xbox Controller");
                ProcessXInputState(ref state);
                PollXInputBattery(_activeXInputIndex);
                return true;
            }
            _activeXInputIndex = uint.MaxValue;
        }

        for (uint i = 0; i < 4; i++)
        {
            uint result = XInputGetState(i, ref state);
            if (result == ERROR_SUCCESS)
            {
                _activeXInputIndex = i;
                SetConnected(true, "Xbox Controller");
                ProcessXInputState(ref state);
                PollXInputBattery(i);
                return true;
            }
        }

        if (_activeXInputIndex != uint.MaxValue || (!_hidConnected && _isConnected))
        {
            _activeXInputIndex = uint.MaxValue;
            if (!_hidConnected)
            {
                _prevButtons = 0;
                _prevStickLeft = _prevStickRight = _prevStickUp = _prevStickDown = false;
            }
        }

        return false;
    }

    private void ProcessXInputState(ref XINPUT_STATE state)
    {
        var gp = state.Gamepad;
        ushort buttons = gp.wButtons;
        ushort pressed = (ushort)(buttons & ~_prevButtons);

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

        bool stickLeft = gp.sThumbLX < -STICK_DEADZONE;
        bool stickRight = gp.sThumbLX > STICK_DEADZONE;
        bool stickUp = gp.sThumbLY > STICK_DEADZONE;
        bool stickDown = gp.sThumbLY < -STICK_DEADZONE;

        if (stickLeft && !_prevStickLeft) ButtonPressed?.Invoke(GamepadButton.DPadLeft);
        if (stickRight && !_prevStickRight) ButtonPressed?.Invoke(GamepadButton.DPadRight);
        if (stickUp && !_prevStickUp) ButtonPressed?.Invoke(GamepadButton.DPadUp);
        if (stickDown && !_prevStickDown) ButtonPressed?.Invoke(GamepadButton.DPadDown);

        // Combo Start+Y sustentado por ~0.5 s → abre GLauncher AI
        bool aiComboHeld = (buttons & XINPUT_GAMEPAD_START) != 0 &&
                           (buttons & XINPUT_GAMEPAD_Y) != 0;
        if (aiComboHeld)
        {
            _aiComboTicks++;
            if (_aiComboTicks >= AI_COMBO_TICKS_REQUIRED && !_aiComboFired)
            {
                _aiComboFired = true;
                AiComboTriggered?.Invoke();
            }
        }
        else
        {
            _aiComboTicks = 0;
            _aiComboFired = false;
        }

        _prevButtons = buttons;
        _prevStickLeft = stickLeft;
        _prevStickRight = stickRight;
        _prevStickUp = stickUp;
        _prevStickDown = stickDown;

        double rsY = gp.sThumbRY > STICK_DEADZONE || gp.sThumbRY < -STICK_DEADZONE
            ? gp.sThumbRY / 32767.0
            : 0.0;
        if (rsY != 0.0)
            RightStickY?.Invoke(rsY);
    }

    private void PollHid()
    {
        _hidScanCounter++;

        if (_hidHandle == INVALID_HANDLE)
        {
            if (_hidScanCounter % (HID_SCAN_INTERVAL_MS / POLL_MS) == 0)
                TryOpenHidController();

            if (_hidHandle == INVALID_HANDLE)
            {
                if (_isConnected && !_hidConnected)
                    SetConnected(false, "");
                return;
            }
        }

        var report = new byte[_hidReportLength];
        if (!ReadFile(_hidHandle, report, (uint)_hidReportLength, out uint bytesRead, IntPtr.Zero)
            || bytesRead == 0)
        {
            CloseHid();
            SetConnected(false, "");
            return;
        }

        if (!_hidConnected)
        {
            _hidConnected = true;
            SetConnected(true, _hidControllerName);
        }

        ProcessHidReport(report);
        _prevHidReport = report;
    }

    private void ProcessHidReport(byte[] report)
    {
        if (_prevHidReport.Length == 0) return;


        int off = 0;
        if (report.Length > 10 && report[0] == 0x31) off = 1;

        if (report.Length < off + 7) return;

        byte lx = report[off + 1];
        byte ly = report[off + 2];
        byte buttons1 = report[off + 5];
        byte buttons2 = report[off + 6];
        byte prevButtons1 = _prevHidReport.Length > off + 6 ? _prevHidReport[off + 5] : (byte)0;
        byte prevButtons2 = _prevHidReport.Length > off + 6 ? _prevHidReport[off + 6] : (byte)0;
        byte prevLx = _prevHidReport.Length > off + 2 ? _prevHidReport[off + 1] : (byte)128;
        byte prevLy = _prevHidReport.Length > off + 2 ? _prevHidReport[off + 2] : (byte)128;

        byte hat = (byte)(buttons1 & 0x0F);
        byte prevHat = (byte)(prevButtons1 & 0x0F);

        if (hat != prevHat)
        {
            bool up = hat == 0 || hat == 1 || hat == 7;
            bool down = hat == 3 || hat == 4 || hat == 5;
            bool left = hat == 5 || hat == 6 || hat == 7;
            bool right = hat == 1 || hat == 2 || hat == 3;
            bool prevUp = prevHat == 0 || prevHat == 1 || prevHat == 7;
            bool prevDown = prevHat == 3 || prevHat == 4 || prevHat == 5;
            bool prevLeft = prevHat == 5 || prevHat == 6 || prevHat == 7;
            bool prevRight = prevHat == 1 || prevHat == 2 || prevHat == 3;

            if (up && !prevUp) ButtonPressed?.Invoke(GamepadButton.DPadUp);
            if (down && !prevDown) ButtonPressed?.Invoke(GamepadButton.DPadDown);
            if (left && !prevLeft) ButtonPressed?.Invoke(GamepadButton.DPadLeft);
            if (right && !prevRight) ButtonPressed?.Invoke(GamepadButton.DPadRight);
        }

        byte face = (byte)(buttons1 >> 4);
        byte prevFace = (byte)(prevButtons1 >> 4);
        byte facePressed = (byte)(face & ~prevFace);

        if ((facePressed & 0x02) != 0) ButtonPressed?.Invoke(GamepadButton.A);     // Cross
        if ((facePressed & 0x04) != 0) ButtonPressed?.Invoke(GamepadButton.B);     // Circle
        if ((facePressed & 0x01) != 0) ButtonPressed?.Invoke(GamepadButton.X);     // Square
        if ((facePressed & 0x08) != 0) ButtonPressed?.Invoke(GamepadButton.Y);     // Triangle

        byte b2Pressed = (byte)(buttons2 & ~prevButtons2);
        if ((b2Pressed & 0x01) != 0) ButtonPressed?.Invoke(GamepadButton.LeftShoulder);  // L1
        if ((b2Pressed & 0x02) != 0) ButtonPressed?.Invoke(GamepadButton.RightShoulder); // R1
        if ((b2Pressed & 0x10) != 0) ButtonPressed?.Invoke(GamepadButton.Back);          // Share/Create
        if ((b2Pressed & 0x20) != 0) ButtonPressed?.Invoke(GamepadButton.Start);         // Options

        const byte deadLow = 60, deadHigh = 196;
        bool stickLeft = lx < deadLow;
        bool stickRight = lx > deadHigh;
        bool stickUp = ly < deadLow;
        bool stickDown = ly > deadHigh;
        bool pStickLeft = prevLx < deadLow;
        bool pStickRight = prevLx > deadHigh;
        bool pStickUp = prevLy < deadLow;
        bool pStickDown = prevLy > deadHigh;

        if (stickLeft && !pStickLeft) ButtonPressed?.Invoke(GamepadButton.DPadLeft);
        if (stickRight && !pStickRight) ButtonPressed?.Invoke(GamepadButton.DPadRight);
        if (stickUp && !pStickUp) ButtonPressed?.Invoke(GamepadButton.DPadUp);
        if (stickDown && !pStickDown) ButtonPressed?.Invoke(GamepadButton.DPadDown);

        byte ry = report.Length > off + 4 ? report[off + 4] : (byte)128;
        const byte rsDeadLow = 60, rsDeadHigh = 196;
        double rsY = ry < rsDeadLow ? (128 - ry) / 128.0
                   : ry > rsDeadHigh ? (128 - ry) / 127.0
                   : 0.0;
        if (rsY != 0.0)
            RightStickY?.Invoke(rsY);

        PollHidBattery(report, off);
    }

    private void PollXInputBattery(uint index)
    {
        _lastBatteryPoll++;
        if (_lastBatteryPoll < BATTERY_POLL_INTERVAL) return;
        _lastBatteryPoll = 0;

        int btPct = TryReadBluetoothBattery();
        if (btPct >= 0)
        {
            if (btPct != BatteryPercent)
            {
                BatteryPercent = btPct;
                BatteryChanged?.Invoke(btPct);
            }
            return;
        }

        var battInfo = new XINPUT_BATTERY_INFORMATION();
        uint res = XInputGetBatteryInformation(index, BATTERY_DEVTYPE_GAMEPAD, ref battInfo);
        if (res != ERROR_SUCCESS) return;

        int pct = battInfo.BatteryType switch
        {
            BATTERY_TYPE_WIRED => -1,
            BATTERY_TYPE_DISCONNECTED => -1,
            _ => battInfo.BatteryLevel switch
            {
                BATTERY_LEVEL_EMPTY  => 5,
                BATTERY_LEVEL_LOW    => 30,
                BATTERY_LEVEL_MEDIUM => 65,
                BATTERY_LEVEL_FULL   => 100,
                _ => -1
            }
        };

        if (pct != BatteryPercent)
        {
            BatteryPercent = pct;
            BatteryChanged?.Invoke(pct);
        }
    }

    private void PollHidBattery(byte[] report, int off)
    {
        _lastBatteryPoll++;
        if (_lastBatteryPoll < BATTERY_POLL_INTERVAL) return;
        _lastBatteryPoll = 0;

        int batteryByte = -1;
        if (report.Length >= off + 54 && _hidControllerName.Contains("DualSense", StringComparison.OrdinalIgnoreCase))
        {
            batteryByte = off + 53;
        }
        else if (report.Length >= 31 && _hidControllerName.Contains("DualShock", StringComparison.OrdinalIgnoreCase))
        {
            batteryByte = report[0] == 0x11 ? 32 : 30;
            if (batteryByte >= report.Length) return;
        }

        if (batteryByte < 0 || batteryByte >= report.Length) return;

        byte raw = report[batteryByte];
        int level = raw & 0x0F;
        int pct = Math.Clamp(level * 10, 0, 100);

        if (pct != BatteryPercent)
        {
            BatteryPercent = pct;
            BatteryChanged?.Invoke(pct);
        }
    }

    private int TryReadBluetoothBattery()
    {
        if (_btBatteryDeviceId != null)
        {
            int pct = ReadBatteryFromDeviceId(_btBatteryDeviceId);
            if (pct >= 0) return pct;
            _btBatteryDeviceId = null;
        }

        _btBatterySearchCooldown++;
        if (_btBatterySearchCooldown < BT_SEARCH_INTERVAL) return -1;
        _btBatterySearchCooldown = 0;

        FindBluetoothBatteryDevice();
        return _btBatteryDeviceId != null ? ReadBatteryFromDeviceId(_btBatteryDeviceId) : -1;
    }

    private int ReadBatteryFromDeviceId(string deviceId)
    {
        try
        {
            if (CM_Locate_DevNodeW(out uint devInst, deviceId, CM_LOCATE_DEVNODE_NORMAL) != CR_SUCCESS)
                return -1;

            var key = DEVPKEY_Device_BatteryLevel;
            uint bufSize = 4;
            byte[] buf = new byte[4];
            if (CM_Get_DevNode_PropertyW(devInst, ref key, out uint propType, buf, ref bufSize, 0) != CR_SUCCESS)
                return -1;

            if (propType == DEVPROP_TYPE_BYTE && bufSize >= 1)
                return Math.Clamp((int)buf[0], 0, 100);
        }
        catch { }
        return -1;
    }

    private void FindBluetoothBatteryDevice()
    {
        try
        {
            Guid btGuid = new("e0cbf06c-cd8b-4647-bb8a-263b43f0f974");
            if (SearchBatteryDeviceInClass(btGuid)) return;

            Guid hidGuid = new("745a17a0-74d3-11d0-b6fe-00a0c90f57da");
            if (SearchBatteryDeviceInClass(hidGuid)) return;

            Guid empty = Guid.Empty;
            SearchBatteryDeviceInClass(empty, allClasses: true);
        }
        catch { }
    }

    private bool SearchBatteryDeviceInClass(Guid classGuid, bool allClasses = false)
    {
        uint flags = DIGCF_PRESENT;
        if (allClasses) flags |= DIGCF_ALLCLASSES;

        IntPtr devInfo = SetupDiGetClassDevs(ref classGuid, IntPtr.Zero, IntPtr.Zero, flags);
        if (devInfo == INVALID_HANDLE) return false;

        try
        {
            var devData = new SP_DEVINFO_DATA { cbSize = Marshal.SizeOf<SP_DEVINFO_DATA>() };

            for (uint i = 0; SetupDiEnumDeviceInfo(devInfo, i, ref devData); i++)
            {
                var key = DEVPKEY_Device_BatteryLevel;
                byte[] valBuf = new byte[4];
                if (!SetupDiGetDevicePropertyW(devInfo, ref devData, ref key,
                    out uint propType, valBuf, (uint)valBuf.Length, out _, 0))
                    continue;

                if (propType != DEVPROP_TYPE_BYTE) continue;

                string name = GetDeviceName(devInfo, ref devData);
                if (!name.Contains("Xbox", StringComparison.OrdinalIgnoreCase))
                    continue;

                char[] idBuf = new char[512];
                if (SetupDiGetDeviceInstanceIdW(devInfo, ref devData, idBuf, 512, out uint idLen) && idLen > 1)
                {
                    _btBatteryDeviceId = new string(idBuf, 0, (int)idLen - 1);
                    return true;
                }
            }
        }
        finally
        {
            SetupDiDestroyDeviceInfoList(devInfo);
        }

        return false;
    }

    private static string GetDeviceName(IntPtr devInfo, ref SP_DEVINFO_DATA devData)
    {
        string name = ReadDeviceStringProperty(devInfo, ref devData, DEVPKEY_Device_FriendlyName);
        if (name.Length == 0)
            name = ReadDeviceStringProperty(devInfo, ref devData, DEVPKEY_Device_DeviceDesc);
        return name;
    }

    private static string ReadDeviceStringProperty(IntPtr devInfo, ref SP_DEVINFO_DATA devData, DEVPROPKEY key)
    {
        byte[] buf = new byte[512];
        if (!SetupDiGetDevicePropertyW(devInfo, ref devData, ref key,
            out uint propType, buf, (uint)buf.Length, out uint size, 0))
            return "";

        if (propType == DEVPROP_TYPE_STRING && size > 0)
            return System.Text.Encoding.Unicode.GetString(buf, 0, (int)size).TrimEnd('\0');

        return "";
    }

    private void TryOpenHidController()
    {
        try
        {
            HidD_GetHidGuid(out Guid hidGuid);
            IntPtr devInfo = SetupDiGetClassDevs(ref hidGuid, IntPtr.Zero, IntPtr.Zero,
                DIGCF_PRESENT | DIGCF_DEVICEINTERFACE);

            if (devInfo == INVALID_HANDLE) return;

            try
            {
                var ifData = new SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = Marshal.SizeOf(ifData);

                for (uint i = 0; SetupDiEnumDeviceInterfaces(devInfo, IntPtr.Zero, ref hidGuid, i, ref ifData); i++)
                {
                    SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, IntPtr.Zero, 0, out uint reqSize, IntPtr.Zero);

                    var detailData = new SP_DEVICE_INTERFACE_DETAIL_DATA();
                    detailData.cbSize = IntPtr.Size == 8 ? 8 : 6; // 64-bit vs 32-bit

                    if (!SetupDiGetDeviceInterfaceDetail(devInfo, ref ifData, ref detailData, reqSize, out _, IntPtr.Zero))
                        continue;

                    string path = detailData.DevicePath;

                    IntPtr handle = CreateFile(path, GENERIC_READ,
                        FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, OPEN_EXISTING, 0, IntPtr.Zero);

                    if (handle == INVALID_HANDLE) continue;

                    var attrs = new HIDD_ATTRIBUTES { Size = Marshal.SizeOf<HIDD_ATTRIBUTES>() };
                    if (!HidD_GetAttributes(handle, ref attrs))
                    {
                        CloseHandle(handle);
                        continue;
                    }

                    var match = KnownHidControllers.FirstOrDefault(c => c.Vid == attrs.VendorID && c.Pid == attrs.ProductID);
                    if (match == default)
                    {
                        CloseHandle(handle);
                        continue;
                    }

                    if (HidD_GetPreparsedData(handle, out IntPtr preparsed))
                    {
                        HidP_GetCaps(preparsed, out HIDP_CAPS caps);
                        HidD_FreePreparsedData(preparsed);

                        if (caps.InputReportByteLength > 0 && caps.Usage == 0x05 && caps.UsagePage == 0x01)
                        {
                            _hidHandle = handle;
                            _hidReportLength = caps.InputReportByteLength;
                            _hidControllerName = match.Name;
                            _prevHidReport = [];
                            return;
                        }
                    }

                    CloseHandle(handle);
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(devInfo);
            }
        }
        catch
        {
        }
    }

    private void CloseHid()
    {
        if (_hidHandle != INVALID_HANDLE)
        {
            CloseHandle(_hidHandle);
            _hidHandle = INVALID_HANDLE;
        }
        _hidConnected = false;
        _prevHidReport = [];
    }

    private void SetConnected(bool connected, string name)
    {
        if (connected == _isConnected && name == ControllerName) return;
        _isConnected = connected;
        ControllerName = name;
        if (!connected && BatteryPercent != -1)
        {
            BatteryPercent = -1;
            BatteryChanged?.Invoke(-1);
        }
        _btBatteryDeviceId = null;
        _btBatterySearchCooldown = BT_SEARCH_INTERVAL;
        _lastBatteryPoll = BATTERY_POLL_INTERVAL; // force immediate battery read on connect
        ConnectionChanged?.Invoke(connected);
    }

    private void CheckButton(ushort pressed, ushort mask, GamepadButton button)
    {
        if ((pressed & mask) != 0)
            ButtonPressed?.Invoke(button);
    }

    /// <summary>
    /// Vibra o controle XInput ativo.
    /// </summary>
    /// <param name="leftMotor">Intensidade motor esquerdo (0.0–1.0)</param>
    /// <param name="rightMotor">Intensidade motor direito (0.0–1.0)</param>
    /// <param name="durationMs">Duração em milissegundos</param>
    public void Vibrate(double leftMotor, double rightMotor, int durationMs = 200)
    {
        if (_activeXInputIndex >= 4) return;

        var vib = new XINPUT_VIBRATION
        {
            wLeftMotorSpeed  = (ushort)(Math.Clamp(leftMotor,  0, 1) * 65535),
            wRightMotorSpeed = (ushort)(Math.Clamp(rightMotor, 0, 1) * 65535)
        };
        XInputSetState(_activeXInputIndex, ref vib);

        Task.Delay(durationMs).ContinueWith(_ =>
        {
            var stop = new XINPUT_VIBRATION { wLeftMotorSpeed = 0, wRightMotorSpeed = 0 };
            XInputSetState(_activeXInputIndex, ref stop);
        });
    }

    public void Dispose()
    {
        _timer.Stop();
        _timer.Tick -= Poll;
        CloseHid();
    }
}
