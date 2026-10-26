using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Windows.ApplicationModel.DataTransfer;
using WinClipboard = Windows.ApplicationModel.DataTransfer.Clipboard;

namespace Clippy.Services;

public sealed class ClipboardTextAutoNormalizer : IDisposable
{
    private const int WmClipboardUpdate = 0x031D;
    private static readonly HashSet<string> TextOnlyFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        StandardDataFormats.Text,
        "UnicodeText",
        "AnsiText",
        "OEMText",
        "Locale"
    };

    private readonly Window window;
    private readonly Action<string> reportStatus;
    private HwndSource? source;
    private IntPtr listenerWindowHandle;
    private uint? pendingSequenceNumber;
    private uint? ownUpdateSequenceNumber;
    private bool isProcessing;
    private bool isDisposed;

    public ClipboardTextAutoNormalizer(Window window, Action<string> reportStatus)
    {
        this.window = window;
        this.reportStatus = reportStatus;
    }

    public void Start()
    {
        if (isDisposed) throw new ObjectDisposedException(nameof(ClipboardTextAutoNormalizer));
        if (source is not null) return;

        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) throw new InvalidOperationException("The main window handle is not available yet.");

        source = HwndSource.FromHwnd(hwnd)
            ?? throw new InvalidOperationException("Could not access the main window message source.");
        source.AddHook(WindowProcedure);
        if (!AddClipboardFormatListener(hwnd))
        {
            source.RemoveHook(WindowProcedure);
            source = null;
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not register the clipboard listener.");
        }
        listenerWindowHandle = hwnd;
    }

    private IntPtr WindowProcedure(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message != WmClipboardUpdate) return IntPtr.Zero;

        handled = true;
        var sequenceNumber = GetClipboardSequenceNumber();
        if (ownUpdateSequenceNumber == sequenceNumber)
        {
            ownUpdateSequenceNumber = null;
            return IntPtr.Zero;
        }
        QueueNormalization(sequenceNumber);
        return IntPtr.Zero;
    }

    private void QueueNormalization(uint sequenceNumber)
    {
        if (isDisposed || sequenceNumber == 0) return;

        // Keep the newest update if another application changes the clipboard while text is read.
        pendingSequenceNumber = sequenceNumber;
        if (isProcessing) return;

        isProcessing = true;
        _ = ProcessPendingUpdatesAsync();
    }

    private async Task ProcessPendingUpdatesAsync()
    {
        try
        {
            while (!isDisposed && pendingSequenceNumber is uint sequenceNumber)
            {
                pendingSequenceNumber = null;
                await NormalizeClipboardAsync(sequenceNumber);
            }
        }
        finally
        {
            isProcessing = false;
            if (!isDisposed && pendingSequenceNumber.HasValue)
            {
                isProcessing = true;
                _ = ProcessPendingUpdatesAsync();
            }
        }
    }

    private async Task NormalizeClipboardAsync(uint expectedSequenceNumber)
    {
        try
        {
            if (GetClipboardSequenceNumber() != expectedSequenceNumber) return;

            var content = WinClipboard.GetContent();
            var formats = content.AvailableFormats.ToArray();
            if (!IsPlainTextOnly(content, formats))
            {
                reportStatus(LocalizationManager.Format("ClipboardSkip", string.Join(", ", formats)));
                return;
            }

            var text = await content.GetTextAsync();
            if (GetClipboardSequenceNumber() != expectedSequenceNumber) return;

            var normalized = TextTransformService.NormalizeNewlines(text, "CRLF");
            if (string.Equals(text, normalized, StringComparison.Ordinal))
            {
                reportStatus(LocalizationManager.T("ClipboardAlreadyCrLf"));
                return;
            }
            if (GetClipboardSequenceNumber() != expectedSequenceNumber) return;

            var data = new DataPackage();
            data.SetText(normalized);
            WinClipboard.SetContent(data);
            WinClipboard.Flush();
            ownUpdateSequenceNumber = GetClipboardSequenceNumber();
            reportStatus(LocalizationManager.T("ClipboardNormalized"));
        }
        catch (Exception)
        {
            if (!isDisposed)
                reportStatus(LocalizationManager.T("ClipboardNormalizeError"));
        }
    }

    private static bool IsPlainTextOnly(DataPackageView content, IReadOnlyCollection<string> formats)
    {
        if (!content.Contains(StandardDataFormats.Text) ||
            content.Contains(StandardDataFormats.Rtf) ||
            content.Contains(StandardDataFormats.Html) ||
            content.Contains(StandardDataFormats.Bitmap) ||
            content.Contains(StandardDataFormats.StorageItems) ||
            content.Contains(StandardDataFormats.Uri) ||
            content.Contains(StandardDataFormats.WebLink) ||
            content.Contains(StandardDataFormats.ApplicationLink))
        {
            return false;
        }

        return formats.All(TextOnlyFormats.Contains);
    }

    public void Dispose()
    {
        if (isDisposed) return;
        isDisposed = true;
        pendingSequenceNumber = null;

        if (source is not null)
        {
            source.RemoveHook(WindowProcedure);
            source = null;
        }
        if (listenerWindowHandle != IntPtr.Zero)
        {
            RemoveClipboardFormatListener(listenerWindowHandle);
            listenerWindowHandle = IntPtr.Zero;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern uint GetClipboardSequenceNumber();
}
