using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ETSOverlay
{
    /// <summary>
    /// The notification-area icon. It owns a hidden top-level window that receives the icon's
    /// mouse messages and the "TaskbarCreated" broadcast, so the icon comes back after Explorer
    /// restarts. All members must be used on the UI thread.
    /// </summary>
    internal sealed class TrayIconService : IDisposable
    {
        private const int WM_NULL = 0x0000;
        private const int WM_CONTEXTMENU = 0x007B;
        private const int WM_LBUTTONDBLCLK = 0x0203;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_TRAYICON = 0x8000 + 0x51;

        private const int NIM_ADD = 0;
        private const int NIM_MODIFY = 1;
        private const int NIM_DELETE = 2;
        private const int NIF_MESSAGE = 1;
        private const int NIF_ICON = 2;
        private const int NIF_TIP = 4;

        private const uint MF_STRING = 0x0000;
        private const uint MF_SEPARATOR = 0x0800;
        private const uint TPM_RIGHTBUTTON = 0x0002;
        private const uint TPM_RETURNCMD = 0x0100;
        private const uint TPM_NONOTIFY = 0x0080;

        private const int MenuToggle = 1;
        private const int MenuExit = 2;
        private const uint IconId = 1;

        private readonly Action<string> _log;
        private HwndSource? _source;
        private IntPtr _icon = IntPtr.Zero;
        private bool _ownsIcon;
        private uint _taskbarCreatedMessage;
        private bool _added;
        private bool _disposed;
        private string _toolTip = "TruckSim Widget";

        /// <summary>Double click on the icon.</summary>
        public event Action? ShowRequested;
        /// <summary>"Show widget" / "Minimize to tray" in the icon menu.</summary>
        public event Action? ToggleRequested;
        /// <summary>"Exit" in the icon menu.</summary>
        public event Action? ExitRequested;
        /// <summary>The icon could not be restored after Explorer restarted.</summary>
        public event Action? IconUnavailable;

        /// <summary>Texts of the two menu items for the current language and state.</summary>
        public Func<(string toggle, string exit)>? MenuText { get; set; }

        /// <summary>The icon is in the notification area right now.</summary>
        public bool IsAvailable => _added && !_disposed;

        public TrayIconService(Action<string> log)
        {
            _log = log;
        }

        /// <summary>Creates the icon if it is not there. False when Windows refuses it.</summary>
        public bool EnsureIcon()
        {
            if (_disposed) return false;
            if (_added) return true;
            try
            {
                EnsureWindow();
                if (_source == null) return false;
                if (_icon == IntPtr.Zero) LoadIcon();

                var data = CreateData(NIF_MESSAGE | NIF_ICON | NIF_TIP);
                _added = Shell_NotifyIcon(NIM_ADD, ref data);
                if (!_added)
                {
                    // A stale icon with the same id may be left from before an Explorer restart.
                    Shell_NotifyIcon(NIM_DELETE, ref data);
                    _added = Shell_NotifyIcon(NIM_ADD, ref data);
                }
                if (!_added) _log("[TRAY] Windows did not accept the notification icon.");
                return _added;
            }
            catch (Exception ex)
            {
                _log($"[TRAY] Could not create the notification icon: {ex.Message}");
                _added = false;
                return false;
            }
        }

        public void SetToolTip(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            _toolTip = text.Length > 120 ? text.Substring(0, 120) : text;
            if (!_added || _disposed) return;
            try
            {
                var data = CreateData(NIF_TIP);
                Shell_NotifyIcon(NIM_MODIFY, ref data);
            }
            catch (Exception ex) { _log($"[TRAY] Tooltip update failed: {ex.Message}"); }
        }

        private void EnsureWindow()
        {
            if (_source != null) return;
            var parameters = new HwndSourceParameters("TruckSimWidgetTray")
            {
                Width = 0,
                Height = 0,
                PositionX = -32000,
                PositionY = -32000,
                WindowStyle = unchecked((int)0x80000000), // WS_POPUP, never shown
                ExtendedWindowStyle = 0x00000080 // WS_EX_TOOLWINDOW: no taskbar button, no Alt+Tab
            };
            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);
            _taskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");
        }

        private void LoadIcon()
        {
            try
            {
                string? path = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(path))
                {
                    var small = new IntPtr[1];
                    if (ExtractIconEx(path, 0, null, small, 1) > 0 && small[0] != IntPtr.Zero)
                    {
                        _icon = small[0];
                        _ownsIcon = true;
                        return;
                    }
                }
            }
            catch (Exception ex) { _log($"[TRAY] Application icon not loaded: {ex.Message}"); }

            _icon = LoadIconW(IntPtr.Zero, new IntPtr(32512)); // IDI_APPLICATION
            _ownsIcon = false;
        }

        private NOTIFYICONDATA CreateData(int flags)
        {
            return new NOTIFYICONDATA
            {
                cbSize = Marshal.SizeOf<NOTIFYICONDATA>(),
                hWnd = _source?.Handle ?? IntPtr.Zero,
                uID = IconId,
                uFlags = flags,
                uCallbackMessage = WM_TRAYICON,
                hIcon = _icon,
                szTip = _toolTip,
                szInfo = "",
                szInfoTitle = ""
            };
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (_disposed) return IntPtr.Zero;

            if (msg == WM_TRAYICON)
            {
                int mouse = unchecked((int)lParam.ToInt64()) & 0xFFFF;
                if (mouse == WM_LBUTTONDBLCLK) ShowRequested?.Invoke();
                else if (mouse == WM_RBUTTONUP || mouse == WM_CONTEXTMENU) ShowMenu(hwnd);
                handled = true;
            }
            else if (_taskbarCreatedMessage != 0 && (uint)msg == _taskbarCreatedMessage)
            {
                // Explorer restarted and dropped every icon: put ours back, same instance.
                _added = false;
                if (!EnsureIcon())
                {
                    _log("[TRAY] Icon not restored after Explorer restart.");
                    IconUnavailable?.Invoke();
                }
                else
                {
                    _log("[TRAY] Icon restored after Explorer restart.");
                }
            }
            return IntPtr.Zero;
        }

        private void ShowMenu(IntPtr hwnd)
        {
            IntPtr menu = CreatePopupMenu();
            if (menu == IntPtr.Zero) return;
            try
            {
                var (toggle, exit) = MenuText?.Invoke() ?? ("Show widget", "Exit");
                AppendMenu(menu, MF_STRING, new IntPtr(MenuToggle), toggle);
                AppendMenu(menu, MF_SEPARATOR, IntPtr.Zero, null);
                AppendMenu(menu, MF_STRING, new IntPtr(MenuExit), exit);

                GetCursorPos(out POINT point);
                // Without a foreground window the menu would not close on a click elsewhere.
                SetForegroundWindow(hwnd);
                int command = TrackPopupMenuEx(menu, TPM_RIGHTBUTTON | TPM_RETURNCMD | TPM_NONOTIFY, point.X, point.Y, hwnd, IntPtr.Zero);
                PostMessage(hwnd, WM_NULL, IntPtr.Zero, IntPtr.Zero);

                if (command == MenuToggle) ToggleRequested?.Invoke();
                else if (command == MenuExit) ExitRequested?.Invoke();
            }
            finally
            {
                DestroyMenu(menu);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_added)
                {
                    var data = CreateData(0);
                    Shell_NotifyIcon(NIM_DELETE, ref data);
                    _added = false;
                }
            }
            catch { }
            try
            {
                _source?.RemoveHook(WndProc);
                _source?.Dispose();
                _source = null;
            }
            catch { }
            try
            {
                if (_ownsIcon && _icon != IntPtr.Zero) DestroyIcon(_icon);
                _icon = IntPtr.Zero;
            }
            catch { }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NOTIFYICONDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uID;
            public int uFlags;
            public int uCallbackMessage;
            public IntPtr hIcon;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
            public int dwState;
            public int dwStateMask;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
            public int uVersion;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
            public int dwInfoFlags;
            public Guid guidItem;
            public IntPtr hBalloonIcon;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "Shell_NotifyIconW")]
        private static extern bool Shell_NotifyIcon(int message, ref NOTIFYICONDATA data);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "ExtractIconExW")]
        private static extern uint ExtractIconEx(string file, int index, IntPtr[]? large, IntPtr[]? small, uint count);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "RegisterWindowMessageW")]
        private static extern uint RegisterWindowMessage(string name);

        [DllImport("user32.dll", EntryPoint = "LoadIconW")]
        private static extern IntPtr LoadIconW(IntPtr instance, IntPtr name);

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        [DllImport("user32.dll")]
        private static extern IntPtr CreatePopupMenu();

        [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "AppendMenuW")]
        private static extern bool AppendMenu(IntPtr menu, uint flags, IntPtr id, string? text);

        [DllImport("user32.dll")]
        private static extern bool DestroyMenu(IntPtr menu);

        [DllImport("user32.dll")]
        private static extern int TrackPopupMenuEx(IntPtr menu, uint flags, int x, int y, IntPtr hwnd, IntPtr parameters);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT point);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hwnd);

        [DllImport("user32.dll", EntryPoint = "PostMessageW")]
        private static extern bool PostMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam);
    }
}
