using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Clippy.Services;

public static class WindowPlacementService
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    public static System.Windows.Point GetAnchor(bool preferCaret)
    {
        var anchor = preferCaret ? TryGetCaretPosition() : null;
        anchor ??= GetMousePosition();
        return new System.Windows.Point(anchor.Value.X, anchor.Value.Y);
    }

    public static void PlaceNearAnchor(Window window, System.Windows.Point anchor)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || !GetWindowRect(handle, out var windowRect)) return;

        var monitorPoint = new NativePoint { X = (int)anchor.X, Y = (int)anchor.Y };
        var monitor = MonitorFromPoint(monitorPoint, MonitorDefaultToNearest);
        var monitorInfo = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (monitor == IntPtr.Zero || !GetMonitorInfo(monitor, ref monitorInfo)) return;

        var workArea = monitorInfo.WorkArea;
        var width = windowRect.Right - windowRect.Left;
        var height = windowRect.Bottom - windowRect.Top;
        var x = monitorPoint.X + 16;
        var y = monitorPoint.Y + 20;
        x = Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - width));
        y = Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - height));

        SetWindowPos(handle, IntPtr.Zero, x, y, 0, 0, SwpNoSize | SwpNoZOrder | SwpNoActivate);
    }

    private static NativePoint? TryGetCaretPosition()
    {
        var foregroundWindow = GetForegroundWindow();
        if (foregroundWindow == IntPtr.Zero) return null;

        var threadId = GetWindowThreadProcessId(foregroundWindow, out _);
        var info = new GuiThreadInfo { Size = Marshal.SizeOf<GuiThreadInfo>() };
        if (threadId == 0 || !GetGuiThreadInfo(threadId, ref info) || info.CaretWindow == IntPtr.Zero)
            return null;

        var point = new NativePoint { X = info.CaretRect.Left, Y = info.CaretRect.Top };
        return ClientToScreen(info.CaretWindow, ref point) ? point : null;
    }

    private static NativePoint GetMousePosition()
    {
        var point = new NativePoint();
        GetCursorPos(out point);
        return point;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public NativeRect CaretRect;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MonitorInfo
    {
        public int Size;
        public NativeRect MonitorArea;
        public NativeRect WorkArea;
        public uint Flags;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

    [DllImport("user32.dll", EntryPoint = "GetGUIThreadInfo", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetGuiThreadInfo(uint threadId, ref GuiThreadInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(IntPtr window, ref NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
