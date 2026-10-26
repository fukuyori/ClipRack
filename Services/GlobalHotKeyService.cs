using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Input;

namespace Clippy.Services;

public sealed class GlobalHotKeyService : IDisposable
{
    private const int HotKeyId = 0x4352;
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;

    private readonly HwndSource source;
    private readonly Action activate;
    private bool registered;
    private bool disposed;
    private string activeShortcut = "";

    public GlobalHotKeyService(Window window, Action activate)
    {
        this.activate = activate;
        var handle = new WindowInteropHelper(window).Handle;
        source = HwndSource.FromHwnd(handle)
            ?? throw new InvalidOperationException(LocalizationManager.T("ShortcutWindowError"));
        source.AddHook(WindowProc);
    }

    public bool TryConfigure(string? shortcut, out string status)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.Equals(shortcut?.Trim(), activeShortcut, StringComparison.OrdinalIgnoreCase))
        {
            status = string.IsNullOrWhiteSpace(shortcut) ? LocalizationManager.T("ShortcutDisabled") : LocalizationManager.Format("ShortcutRegistering", shortcut);
            return true;
        }

        Unregister();

        if (string.IsNullOrWhiteSpace(shortcut))
        {
            activeShortcut = "";
            status = LocalizationManager.T("ShortcutDisabled");
            return true;
        }

        if (!TryParse(shortcut, out var modifiers, out var virtualKey))
        {
            status = LocalizationManager.T("ShortcutInvalid");
            return false;
        }

        if (!RegisterHotKey(source.Handle, HotKeyId, modifiers | ModNoRepeat, (uint)virtualKey))
        {
            RestoreActiveShortcut();
            status = LocalizationManager.T("ShortcutInUse");
            return false;
        }

        registered = true;
        activeShortcut = shortcut.Trim();
        status = LocalizationManager.Format("ShortcutRegistered", shortcut);
        return true;
    }

    private static bool TryParse(string shortcut, out uint modifiers, out int virtualKey)
    {
        modifiers = 0;
        virtualKey = 0;
        var parts = shortcut.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2) return false;

        for (var index = 0; index < parts.Length - 1; index++)
        {
            modifiers |= parts[index] switch
            {
                "Ctrl" => ModControl,
                "Alt" => ModAlt,
                "Shift" => ModShift,
                "Win" => ModWin,
                _ => 0
            };
        }

        if (modifiers == 0 || !Enum.TryParse<Key>(parts[^1], ignoreCase: false, out var key) || key is Key.None or Key.System)
            return false;

        virtualKey = KeyInterop.VirtualKeyFromKey(key);
        return virtualKey != 0;
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam.ToInt32() == HotKeyId)
        {
            handled = true;
            activate();
        }
        return IntPtr.Zero;
    }

    private void Unregister()
    {
        if (!registered) return;
        UnregisterHotKey(source.Handle, HotKeyId);
        registered = false;
    }

    private void RestoreActiveShortcut()
    {
        if (string.IsNullOrWhiteSpace(activeShortcut)
            || !TryParse(activeShortcut, out var modifiers, out var virtualKey)) return;
        registered = RegisterHotKey(source.Handle, HotKeyId, modifiers | ModNoRepeat, (uint)virtualKey);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Unregister();
        source.RemoveHook(WindowProc);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}
