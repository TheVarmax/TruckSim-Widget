using System;
using System.Runtime.InteropServices;
using System.Windows.Input;

namespace ETSOverlay
{
    public class SpeedLimiterService
    {
        public static SpeedLimiterService Instance { get; } = new SpeedLimiterService();
        public Action<string>? LogAction { get; set; }

        public bool IsEnabled { get; set; }
        public int SpeedThresholdKmh { get; set; } = 98;
        public int SpeedThresholdMph { get; set; } = 78;
        public bool IsBraking { get; private set; }
        public Key BrakeKey { get; set; } = Key.Down;

        [StructLayout(LayoutKind.Sequential)]
        struct INPUT { public uint type; public InputUnion u; }

        [StructLayout(LayoutKind.Explicit)]
        struct InputUnion {
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct KEYBDINPUT {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MOUSEINPUT {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct HARDWAREINPUT {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }

        const uint INPUT_KEYBOARD = 1;
        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;
        const uint MAPVK_VK_TO_VSC = 0;

        [DllImport("user32.dll", SetLastError = true)]
        static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [DllImport("user32.dll")]
        static extern uint MapVirtualKey(uint uCode, uint uMapType);

        const uint KEYEVENTF_EXTENDEDKEY = 0x0001;

        private SpeedLimiterService() { }

        public void OnSpeedUpdate(float rawSpeedMs, bool useMiles)
        {
            if (!IsEnabled)
            {
                if (IsBraking) ReleaseBrake();
                return;
            }

            float currentSpeed = rawSpeedMs * (useMiles ? 2.236936f : 3.6f);
            float threshold = useMiles ? SpeedThresholdMph : SpeedThresholdKmh;
            float releaseMargin = useMiles ? 2f : 2f; // Reduced margin because physics carry the braking further

            if (currentSpeed >= threshold)
            {
                if (!IsBraking)
                {
                    LogAction?.Invoke($"Speed {currentSpeed:F1} >= limit {threshold}. Engaging brake (Key: {BrakeKey}).");
                    IsBraking = true;
                    SendBrakeInput(true);
                }
            }
            else if (currentSpeed < (threshold - releaseMargin) && IsBraking)
            {
                LogAction?.Invoke($"Speed {currentSpeed:F1} < {threshold - releaseMargin}. Releasing brake.");
                IsBraking = false;
                SendBrakeInput(false);
            }
        }

        public void ReleaseBrake()
        {
            if (IsBraking)
            {
                IsBraking = false;
                SendBrakeInput(false);
            }
        }

        public void Disable()
        {
            IsEnabled = false;
            ReleaseBrake();
        }

        const uint KEYEVENTF_SCANCODE = 0x0008;

        private void SendBrakeInput(bool keyDown)
        {
            uint virtualKey = (uint)KeyInterop.VirtualKeyFromKey(BrakeKey);
            uint scanCode = MapVirtualKey(virtualKey, MAPVK_VK_TO_VSC);

            uint flags = KEYEVENTF_SCANCODE | (keyDown ? 0 : KEYEVENTF_KEYUP);
            
            // Add extended key flag for arrows, etc.
            if (BrakeKey == Key.Up || BrakeKey == Key.Down || BrakeKey == Key.Left || BrakeKey == Key.Right)
            {
                flags |= KEYEVENTF_EXTENDEDKEY;
            }

            // keybd_event is older but often bypasses game engine hooks better than SendInput
            keybd_event((byte)0, (byte)scanCode, flags, 0);

            LogAction?.Invoke($"Sent keybd_event: ScanCode={scanCode}, Flags={flags}");
        }
    }
}
