using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ETSOverlay
{
    /// <summary>
    /// Keeps widget windows under the user's control only. Windows itself can no longer
    /// maximize, snap, resize, minimize or move them (Win+arrows, Win+M, Aero Shake,
    /// title double-click, Alt+Space / taskbar menu, snap layouts, tablet mode, Explorer
    /// cascade/stack). Moving by dragging and hiding/minimizing from the widget's own
    /// buttons keep working, and windows are pulled back on screen after display changes.
    /// </summary>
    internal static class WindowGuard
    {
        private const int GWL_STYLE = -16;
        private const int WS_THICKFRAME = 0x00040000;
        private const int WS_MAXIMIZEBOX = 0x00010000;

        private const int WM_SIZE = 0x0005;
        private const int WM_WINDOWPOSCHANGING = 0x0046;
        private const int WM_DISPLAYCHANGE = 0x007E;
        private const int WM_SETTINGCHANGE = 0x001A;
        private const int WM_SYSCOMMAND = 0x0112;
        private const int WM_NCLBUTTONDBLCLK = 0x00A3;
        private const int WM_DPICHANGED = 0x02E0;

        private const int SC_SIZE = 0xF000;
        private const int SC_MOVE = 0xF010;
        private const int SC_MINIMIZE = 0xF020;
        private const int SC_MAXIMIZE = 0xF030;
        private const int SC_KEYMENU = 0xF100;

        private const int SIZE_MAXIMIZED = 2;
        private const int SPI_SETWORKAREA = 0x002F;

        private const int SWP_NOSIZE = 0x0001;
        private const int SWP_NOMOVE = 0x0002;

        // Minimum part of a window (in DIPs) that has to stay on screen to be grabbable.
        private const double MinVisibleWidth = 60;
        private const double MinVisibleHeight = 30;

        private sealed class GuardState
        {
            public SizeToContent SizeToContent;
            public bool AppMinimize;
        }

        private static readonly ConditionalWeakTable<Window, GuardState> States = new();

        [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        // True while handling a message sent from another thread/process (Explorer, the shell,
        // other apps). Our own Left/Top changes and DragMove run on the window's thread.
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool InSendMessage();

        /// <summary>
        /// Call from the window constructor (after InitializeComponent).
        /// </summary>
        public static void Attach(Window window)
        {
            // CanMinimize keeps the minimize box (the app minimizes windows itself)
            // but drops WS_MAXIMIZEBOX and WS_THICKFRAME, so Windows no longer offers
            // maximize, Aero Snap or snap layouts for this window.
            window.ResizeMode = ResizeMode.CanMinimize;

            var state = States.GetValue(window, _ => new GuardState());
            state.SizeToContent = window.SizeToContent;

            window.SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(window).Handle;
                if (hwnd == IntPtr.Zero) return;

                int style = GetWindowLong(hwnd, GWL_STYLE);
                SetWindowLong(hwnd, GWL_STYLE, style & ~WS_MAXIMIZEBOX & ~WS_THICKFRAME);

                HwndSource.FromHwnd(hwnd)?.AddHook((IntPtr h, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) =>
                {
                    switch (msg)
                    {
                        case WM_SYSCOMMAND:
                        {
                            int raw = (int)(wParam.ToInt64() & 0xFFFF);
                            int cmd = raw & 0xFFF0;
                            // SC_MOVE with low bits set (0xF012) is DragMove: keep it.
                            // Plain SC_MOVE (0xF010) is "Move" from the system menu (keyboard move).
                            if (cmd == SC_MAXIMIZE || cmd == SC_SIZE || cmd == SC_MINIMIZE || raw == SC_MOVE)
                                handled = true;
                            // Alt+Space opens the system menu.
                            else if (cmd == SC_KEYMENU && lParam.ToInt64() == ' ')
                                handled = true;
                            break;
                        }
                        case WM_NCLBUTTONDBLCLK:
                            handled = true;
                            break;
                        case WM_WINDOWPOSCHANGING:
                            // Move/resize requested by another process (Win+Shift+arrows, Explorer
                            // cascade/stack, other tools): keep our position and size, let z-order
                            // and show/hide through.
                            if (InSendMessage() && lParam != IntPtr.Zero && window.WindowState == WindowState.Normal)
                            {
                                int flagsOffset = 2 * IntPtr.Size + 4 * sizeof(int);
                                int flags = Marshal.ReadInt32(lParam, flagsOffset);
                                if ((flags & (SWP_NOMOVE | SWP_NOSIZE)) != (SWP_NOMOVE | SWP_NOSIZE))
                                    Marshal.WriteInt32(lParam, flagsOffset, flags | SWP_NOMOVE | SWP_NOSIZE);
                            }
                            break;
                        case WM_SIZE:
                            // Windows (or WPF after an external resize) dropped SizeToContent:
                            // put it back so the window hugs its content again.
                            if (wParam.ToInt64() != SIZE_MAXIMIZED && window.SizeToContent != state.SizeToContent)
                            {
                                window.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    if (window.WindowState == WindowState.Normal) window.SizeToContent = state.SizeToContent;
                                }), DispatcherPriority.Background);
                            }
                            break;
                        case WM_DISPLAYCHANGE:
                        case WM_DPICHANGED:
                            QueueEnsureOnScreen(window);
                            break;
                        case WM_SETTINGCHANGE:
                            if (wParam.ToInt64() == SPI_SETWORKAREA) QueueEnsureOnScreen(window);
                            break;
                    }
                    return IntPtr.Zero;
                });
            };

            // Last line of defence for state changes that bypass WM_SYSCOMMAND
            // (Win+M, Aero Shake, tablet mode, other tools calling ShowWindow).
            window.StateChanged += (s, e) =>
            {
                switch (window.WindowState)
                {
                    case WindowState.Normal:
                        state.AppMinimize = false;
                        break;
                    case WindowState.Minimized when state.AppMinimize:
                        break;
                    default:
                        window.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (window.WindowState == WindowState.Normal) return;
                            if (window.WindowState == WindowState.Minimized && state.AppMinimize) return;
                            window.WindowState = WindowState.Normal;
                            window.SizeToContent = state.SizeToContent;
                            EnsureOnScreen(window);
                        }), DispatcherPriority.Send);
                        break;
                }
            };
        }

        /// <summary>
        /// Minimizes the window on the widget's own request (the minimize button).
        /// Any other minimize is reverted.
        /// </summary>
        public static void MinimizeByApp(Window window)
        {
            if (States.TryGetValue(window, out var state)) state.AppMinimize = true;
            window.WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// Position to persist: never the maximized/minimized geometry.
        /// Returns NaN when nothing sensible is available so the caller keeps its previous value.
        /// </summary>
        public static Point GetSavablePosition(Window window)
        {
            if (window.WindowState == WindowState.Normal)
                return new Point(window.Left, window.Top);

            var rb = window.RestoreBounds;
            if (rb.IsEmpty || !IsFinite(rb.Left) || !IsFinite(rb.Top))
                return new Point(double.NaN, double.NaN);
            return new Point(rb.Left, rb.Top);
        }

        /// <summary>
        /// True when the rectangle (DIPs) is finite and enough of it lies on the virtual screen
        /// for the user to see and grab it.
        /// </summary>
        public static bool IsPositionVisible(double left, double top, double width, double height)
        {
            if (!IsFinite(left) || !IsFinite(top)) return false;
            if (!IsFinite(width) || width <= 0) width = MinVisibleWidth;
            if (!IsFinite(height) || height <= 0) height = MinVisibleHeight;

            double vsLeft = SystemParameters.VirtualScreenLeft;
            double vsTop = SystemParameters.VirtualScreenTop;
            double vsRight = vsLeft + SystemParameters.VirtualScreenWidth;
            double vsBottom = vsTop + SystemParameters.VirtualScreenHeight;

            double visibleW = Math.Min(left + width, vsRight) - Math.Max(left, vsLeft);
            double visibleH = Math.Min(top + height, vsBottom) - Math.Max(top, vsTop);

            return visibleW >= Math.Min(MinVisibleWidth, width) && visibleH >= Math.Min(MinVisibleHeight, height);
        }

        /// <summary>
        /// Moves the window back to the primary work area if it ended up off-screen.
        /// Returns true when the window was moved.
        /// </summary>
        public static bool EnsureOnScreen(Window window)
        {
            if (window.WindowState != WindowState.Normal) return false;

            double width = window.ActualWidth > 0 ? window.ActualWidth : window.DesiredSize.Width;
            double height = window.ActualHeight > 0 ? window.ActualHeight : window.DesiredSize.Height;

            if (IsPositionVisible(window.Left, window.Top, width, height)) return false;

            var wa = SystemParameters.WorkArea;
            if (!IsFinite(width) || width <= 0) width = 0;
            if (!IsFinite(height) || height <= 0) height = 0;
            window.Left = wa.Left + Math.Max(0, (wa.Width - width) / 2);
            window.Top = wa.Top + Math.Max(0, (wa.Height - height) / 2);
            return true;
        }

        internal enum PositionFix { None, Clamped, Reset }

        // A maximized window sits a few pixels (its invisible frame, ~7-8 DIPs) beyond the
        // top-left corner of the work area. Positions like that came from the 1.6.6 maximize bug.
        private const double MaximizeOffsetMin = 0.5;
        private const double MaximizeOffsetMax = 16;

        /// <summary>
        /// Pure decision for a restored window: <paramref name="bounds"/> must fit fully inside
        /// one of <paramref name="workAreas"/> (all in DIPs).
        /// Reset: position is unusable (not finite, on no monitor, or a maximize leftover);
        /// the caller applies its default. Clamped: <paramref name="position"/> is the bounds'
        /// top-left moved inside the work area it overlaps most.
        /// </summary>
        internal static PositionFix SanitizeBounds(Rect bounds, IReadOnlyList<Rect> workAreas, out Point position, bool detectMaximize = true)
        {
            position = new Point(double.NaN, double.NaN);
            if (!IsFinite(bounds.X) || !IsFinite(bounds.Y) || !IsFinite(bounds.Width) || !IsFinite(bounds.Height))
                return PositionFix.Reset;

            Rect best = Rect.Empty;
            double bestArea = 0;
            foreach (var wa in workAreas)
            {
                if (wa.IsEmpty) continue;
                var hit = Rect.Intersect(bounds, wa);
                double area = hit.IsEmpty ? 0 : hit.Width * hit.Height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = wa;
                }
            }
            if (best.IsEmpty) return PositionFix.Reset;

            if (detectMaximize && IsMaximizeLeftover(bounds, best))
                return PositionFix.Reset;

            double left = Math.Max(best.Left, Math.Min(bounds.Left, best.Right - bounds.Width));
            double top = Math.Max(best.Top, Math.Min(bounds.Top, best.Bottom - bounds.Height));
            position = new Point(left, top);

            return Math.Abs(left - bounds.Left) > MaximizeOffsetMin || Math.Abs(top - bounds.Top) > MaximizeOffsetMin
                ? PositionFix.Clamped
                : PositionFix.None;
        }

        /// <summary>
        /// True when <paramref name="bounds"/> sits just beyond the top-left corner of the work
        /// area it overlaps most, where Windows puts a maximized window.
        /// </summary>
        internal static bool IsMaximizeLeftover(Rect bounds, IReadOnlyList<Rect> workAreas)
        {
            if (!IsFinite(bounds.X) || !IsFinite(bounds.Y)) return false;
            Rect best = Rect.Empty;
            double bestArea = 0;
            foreach (var wa in workAreas)
            {
                if (wa.IsEmpty) continue;
                var hit = Rect.Intersect(bounds, wa);
                double area = hit.IsEmpty ? 0 : hit.Width * hit.Height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = wa;
                }
            }
            return !best.IsEmpty && IsMaximizeLeftover(bounds, best);
        }

        private static bool IsMaximizeLeftover(Rect bounds, Rect workArea)
        {
            double dx = workArea.Left - bounds.Left;
            double dy = workArea.Top - bounds.Top;
            return dx >= MaximizeOffsetMin && dx <= MaximizeOffsetMax && dy >= MaximizeOffsetMin && dy <= MaximizeOffsetMax;
        }

        /// <summary>
        /// Fits a restored window (plus <paramref name="extraTop"/> DIPs of attached content
        /// drawn above it, e.g. the header overlay) fully inside a monitor.
        /// On Reset, <paramref name="applyDefault"/> positions the window, which is then clamped.
        /// </summary>
        public static PositionFix FitToWorkArea(Window window, double extraTop, Action applyDefault)
        {
            if (window.WindowState != WindowState.Normal) return PositionFix.None;
            if (!IsFinite(extraTop) || extraTop < 0) extraTop = 0;

            var (workAreas, monitorAreas) = GetMonitorAreas(window);
            var fix = PositionFix.None;
            // Maximize leftovers sit just beyond a work-area corner: detect them on the window itself.
            if (IsMaximizeLeftover(GetBounds(window, 0), workAreas))
            {
                applyDefault();
                fix = PositionFix.Reset;
            }

            // Keep the window (with its attached content) on a monitor. The widgets are topmost,
            // so overlapping the taskbar is fine (the default HUD position sits over it).
            var clamp = SanitizeBounds(GetBounds(window, extraTop), monitorAreas, out var pos, detectMaximize: false);
            if (clamp == PositionFix.Reset && fix != PositionFix.Reset)
            {
                applyDefault();
                fix = PositionFix.Reset;
                clamp = SanitizeBounds(GetBounds(window, extraTop), monitorAreas, out pos, detectMaximize: false);
            }
            if (clamp == PositionFix.Reset) return fix;
            if (fix == PositionFix.None) fix = clamp;

            if (!double.IsNaN(pos.X) && (Math.Abs(window.Left - pos.X) > 0.01 || Math.Abs(window.Top - extraTop - pos.Y) > 0.01))
            {
                window.Left = pos.X;
                window.Top = pos.Y + extraTop;
            }
            return fix;
        }

        /// <summary>Centers the window in the primary monitor's work area.</summary>
        public static void CenterOnPrimary(Window window)
        {
            var size = GetSize(window);
            var wa = SystemParameters.WorkArea;
            window.Left = wa.Left + Math.Max(0, (wa.Width - size.Width) / 2);
            window.Top = wa.Top + Math.Max(0, (wa.Height - size.Height) / 2);
        }

        private static Size GetSize(Window window)
        {
            double width = window.ActualWidth > 0 ? window.ActualWidth : window.DesiredSize.Width;
            double height = window.ActualHeight > 0 ? window.ActualHeight : window.DesiredSize.Height;
            if (!IsFinite(width) || width < 0) width = 0;
            if (!IsFinite(height) || height < 0) height = 0;
            return new Size(width, height);
        }

        private static Rect GetBounds(Window window, double extraTop)
        {
            // Not laid out yet: use a minimal size so the position can still be checked.
            var size = GetSize(window);
            double width = Math.Max(1, size.Width);
            double height = Math.Max(1, size.Height) + extraTop;
            if (!IsFinite(window.Left) || !IsFinite(window.Top))
                return new Rect(double.NaN, double.NaN, width, height);
            return new Rect(window.Left, window.Top - extraTop, width, height);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdc, IntPtr lprcMonitor, IntPtr data);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        /// <summary>
        /// Work areas (without taskbars) and full bounds of all monitors, in the window's DIPs.
        /// </summary>
        private static (List<Rect> WorkAreas, List<Rect> MonitorAreas) GetMonitorAreas(Window window)
        {
            var work = new List<Rect>();
            var monitors = new List<Rect>();
            var fromDevice = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            Rect ToDips(RECT r) => new Rect(fromDevice.Transform(new Point(r.Left, r.Top)), fromDevice.Transform(new Point(r.Right, r.Bottom)));
            try
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, hdc, rect, data) =>
                {
                    var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        work.Add(ToDips(mi.rcWork));
                        monitors.Add(ToDips(mi.rcMonitor));
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch
            {
                // Fall back to the primary monitor below.
            }
            if (work.Count == 0) work.Add(SystemParameters.WorkArea);
            if (monitors.Count == 0)
                monitors.Add(new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight));
            return (work, monitors);
        }

        private static void QueueEnsureOnScreen(Window window)
        {
            // Let Windows finish re-laying out monitors before checking.
            window.Dispatcher.BeginInvoke(new Action(() => EnsureOnScreen(window)), DispatcherPriority.ApplicationIdle);
        }

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
