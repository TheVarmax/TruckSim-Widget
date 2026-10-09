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

        private const int WM_MOVING = 0x0216;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        private sealed class GuardState
        {
            public SizeToContent SizeToContent;
            public bool AppMinimize;
            // The visible part of the window (the card); the rest is a transparent margin/shadow.
            public FrameworkElement? Content;
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
        /// <paramref name="visibleContent"/> is the visible card inside the transparent window:
        /// dragging keeps it on screen, and restored positions are checked against it.
        /// </summary>
        public static void Attach(Window window, FrameworkElement? visibleContent = null)
        {
            // CanMinimize keeps the minimize box (the app minimizes windows itself)
            // but drops WS_MAXIMIZEBOX and WS_THICKFRAME, so Windows no longer offers
            // maximize, Aero Snap or snap layouts for this window.
            window.ResizeMode = ResizeMode.CanMinimize;

            var state = States.GetValue(window, _ => new GuardState());
            state.SizeToContent = window.SizeToContent;
            state.Content = visibleContent;

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
                        case WM_MOVING:
                            // User drag (DragMove): keep the visible card inside the monitor under
                            // the cursor, so it can be placed flush to an edge but not past it.
                            if (lParam != IntPtr.Zero && ClampDragRect(window, lParam))
                            {
                                handled = true;
                                return new IntPtr(1);
                            }
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

        // Moves smaller than this are treated as "already in place".
        private const double PositionTolerance = 0.5;

        /// <summary>
        /// Pure decision for a restored window: <paramref name="content"/> (the visible card, in
        /// DIPs) must fit fully inside one of <paramref name="monitors"/>.
        /// Reset: position is unusable (not finite or on no monitor); the caller applies its
        /// default. Clamped: <paramref name="position"/> is the card's top-left moved inside the
        /// monitor it overlaps most. A card flush to an edge is left alone.
        /// </summary>
        internal static PositionFix SanitizeBounds(Rect content, IReadOnlyList<Rect> monitors, out Point position)
        {
            position = new Point(double.NaN, double.NaN);
            if (!IsFinite(content.X) || !IsFinite(content.Y) || !IsFinite(content.Width) || !IsFinite(content.Height))
                return PositionFix.Reset;

            Rect best = Rect.Empty;
            double bestArea = 0;
            foreach (var monitor in monitors)
            {
                if (monitor.IsEmpty) continue;
                var hit = Rect.Intersect(content, monitor);
                double area = hit.IsEmpty ? 0 : hit.Width * hit.Height;
                if (area > bestArea)
                {
                    bestArea = area;
                    best = monitor;
                }
            }
            if (best.IsEmpty) return PositionFix.Reset;

            var offset = ClampOffset(content, best);
            position = new Point(content.Left + offset.X, content.Top + offset.Y);

            return Math.Abs(offset.X) > PositionTolerance || Math.Abs(offset.Y) > PositionTolerance
                ? PositionFix.Clamped
                : PositionFix.None;
        }

        /// <summary>
        /// Shift that moves <paramref name="content"/> inside <paramref name="bounds"/>
        /// (aligned to the left/top edge when it is larger than the bounds).
        /// </summary>
        internal static Vector ClampOffset(Rect content, Rect bounds)
        {
            double dx = 0, dy = 0;
            if (content.Left < bounds.Left || content.Width > bounds.Width) dx = bounds.Left - content.Left;
            else if (content.Right > bounds.Right) dx = bounds.Right - content.Right;
            if (content.Top < bounds.Top || content.Height > bounds.Height) dy = bounds.Top - content.Top;
            else if (content.Bottom > bounds.Bottom) dy = bounds.Bottom - content.Bottom;
            return new Vector(dx, dy);
        }

        /// <summary>
        /// Keeps a restored window's visible card fully inside a monitor (the same rule as
        /// dragging, so a card placed flush to an edge stays where it is).
        /// On Reset, <paramref name="applyDefault"/> positions the window, which is then clamped.
        /// </summary>
        public static PositionFix FitToScreen(Window window, Action applyDefault)
        {
            if (window.WindowState != WindowState.Normal) return PositionFix.None;

            var monitors = GetMonitorAreas(window);
            var fix = SanitizeBounds(GetContentBounds(window), monitors, out var pos);
            if (fix == PositionFix.Reset)
            {
                applyDefault();
                if (SanitizeBounds(GetContentBounds(window), monitors, out pos) == PositionFix.Reset)
                    return fix;
            }

            var current = GetContentBounds(window);
            double dx = pos.X - current.X, dy = pos.Y - current.Y;
            if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01)
            {
                window.Left += dx;
                window.Top += dy;
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

        /// <summary>
        /// The visible card inside the window, relative to the window's top-left (DIPs).
        /// Falls back to the whole window when no card was given or it is not laid out yet.
        /// </summary>
        private static Rect GetContentRect(Window window)
        {
            var size = GetSize(window);
            var whole = new Rect(0, 0, Math.Max(1, size.Width), Math.Max(1, size.Height));
            if (!States.TryGetValue(window, out var state) || state.Content == null) return whole;

            var content = state.Content;
            if (!content.IsLoaded || content.ActualWidth <= 0 || content.ActualHeight <= 0) return whole;
            try
            {
                var rect = content.TransformToAncestor(window).TransformBounds(new Rect(content.RenderSize));
                rect.Intersect(whole);
                return rect.IsEmpty || rect.Width < 1 || rect.Height < 1 ? whole : rect;
            }
            catch (InvalidOperationException)
            {
                return whole;
            }
        }

        private static Rect GetContentBounds(Window window)
        {
            var rect = GetContentRect(window);
            if (!IsFinite(window.Left) || !IsFinite(window.Top))
                return new Rect(double.NaN, double.NaN, rect.Width, rect.Height);
            return new Rect(window.Left + rect.X, window.Top + rect.Y, rect.Width, rect.Height);
        }

        /// <summary>
        /// WM_MOVING: shifts the proposed window rect (physical pixels) so the visible card stays
        /// inside the monitor under the cursor. Returns true when the rect was changed.
        /// </summary>
        private static bool ClampDragRect(Window window, IntPtr lParam)
        {
            try
            {
                var source = PresentationSource.FromVisual(window);
                if (source?.CompositionTarget == null) return false;
                var toDevice = source.CompositionTarget.TransformToDevice;

                var proposed = Marshal.PtrToStructure<RECT>(lParam);
                var card = GetContentRect(window);
                var cardTopLeft = toDevice.Transform(card.TopLeft);
                var cardBottomRight = toDevice.Transform(card.BottomRight);
                var content = new Rect(
                    proposed.Left + cardTopLeft.X, proposed.Top + cardTopLeft.Y,
                    cardBottomRight.X - cardTopLeft.X, cardBottomRight.Y - cardTopLeft.Y);

                if (!GetCursorPos(out var cursor)) return false;
                var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                if (!GetMonitorInfo(MonitorFromPoint(cursor, MONITOR_DEFAULTTONEAREST), ref mi)) return false;
                var monitor = new Rect(mi.rcMonitor.Left, mi.rcMonitor.Top,
                    mi.rcMonitor.Right - mi.rcMonitor.Left, mi.rcMonitor.Bottom - mi.rcMonitor.Top);

                var offset = ClampOffset(content, monitor);
                int dx = (int)Math.Round(offset.X), dy = (int)Math.Round(offset.Y);
                if (dx == 0 && dy == 0) return false;

                proposed.Left += dx; proposed.Right += dx;
                proposed.Top += dy; proposed.Bottom += dy;
                Marshal.StructureToPtr(proposed, lParam, false);
                return true;
            }
            catch
            {
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X, Y; }

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

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint dwFlags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetCursorPos(out POINT lpPoint);

        /// <summary>
        /// Full bounds of all monitors (taskbar included: the widgets are topmost and may sit
        /// over it), in the window's DIPs.
        /// </summary>
        private static List<Rect> GetMonitorAreas(Window window)
        {
            var monitors = new List<Rect>();
            var fromDevice = PresentationSource.FromVisual(window)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
            try
            {
                EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, hdc, rect, data) =>
                {
                    var mi = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
                    if (GetMonitorInfo(hMonitor, ref mi))
                    {
                        monitors.Add(new Rect(
                            fromDevice.Transform(new Point(mi.rcMonitor.Left, mi.rcMonitor.Top)),
                            fromDevice.Transform(new Point(mi.rcMonitor.Right, mi.rcMonitor.Bottom))));
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch
            {
                // Fall back to the primary monitor below.
            }
            if (monitors.Count == 0)
                monitors.Add(new Rect(0, 0, SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight));
            return monitors;
        }

        private static void QueueEnsureOnScreen(Window window)
        {
            // Let Windows finish re-laying out monitors before checking.
            window.Dispatcher.BeginInvoke(new Action(() => EnsureOnScreen(window)), DispatcherPriority.ApplicationIdle);
        }

        private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);
    }
}
