using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Clippy.Services;

public static class PasteService
{
    private const uint InputKeyboard = 1;
    private const uint KeyEventKeyUp = 0x0002;
    private const ushort VirtualKeyControl = 0x11;
    private const ushort VirtualKeyV = 0x56;

    public static IntPtr GetForegroundWindowHandle() => GetForegroundWindow();

    public static bool IsAvailableTarget(IntPtr hwnd) => hwnd != IntPtr.Zero && IsWindow(hwnd);

    public static async Task<DataPackage> CreatePackageAsync(DataPackageView content, string? fallbackText, bool keepFormatting)
    {
        var data = new DataPackage();
        if (content.Contains(StandardDataFormats.Text))
            data.SetText(await content.GetTextAsync());
        else if (fallbackText is not null)
            data.SetText(fallbackText);
        if (!keepFormatting) return data;

        if (content.Contains(StandardDataFormats.Rtf))
            data.SetRtf(await content.GetRtfAsync());
        if (content.Contains(StandardDataFormats.Html))
            data.SetHtmlFormat(await content.GetHtmlFormatAsync());
        if (content.Contains(StandardDataFormats.Bitmap))
            data.SetBitmap(await content.GetBitmapAsync());
        return data;
    }

    // Content placed for pasting is excluded from Windows clipboard history so pasting never changes the history.
    public static void SetClipboardWithoutHistory(DataPackage data)
    {
        var options = new ClipboardContentOptions { IsAllowedInHistory = false, IsRoamable = false };
        if (!WinClipboard.SetContentWithOptions(data, options))
            throw new InvalidOperationException("The clipboard did not accept the content.");
        WinClipboard.Flush();
    }

    public static async Task<bool> PasteToWindowAsync(IntPtr target)
    {
        if (!IsAvailableTarget(target)) return false;
        SetForegroundWindow(target);

        // Wait briefly for the target window to become active before sending the paste shortcut.
        for (var attempt = 0; attempt < 20 && GetForegroundWindow() != target; attempt++)
            await Task.Delay(25);
        if (GetForegroundWindow() != target) return false;

        await Task.Delay(50);
        var inputs = new[]
        {
            KeyInput(VirtualKeyControl, 0),
            KeyInput(VirtualKeyV, 0),
            KeyInput(VirtualKeyV, KeyEventKeyUp),
            KeyInput(VirtualKeyControl, KeyEventKeyUp)
        };
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>()) == inputs.Length;
    }

    private static Input KeyInput(ushort virtualKey, uint flags) => new()
    {
        Type = InputKeyboard,
        Data = new InputUnion { Keyboard = new KeyboardInput { VirtualKey = virtualKey, Flags = flags } }
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct Input
    {
        public uint Type;
        public InputUnion Data;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MouseInput Mouse;
        [FieldOffset(0)] public KeyboardInput Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseInput
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KeyboardInput
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public IntPtr ExtraInfo;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint count, Input[] inputs, int size);
}
