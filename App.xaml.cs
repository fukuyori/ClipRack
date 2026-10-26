using Hardcodet.Wpf.TaskbarNotification;
using Clippy.Services;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

namespace Clippy;

public partial class App : Application
{
    private const string MutexName = "Local\\Clippy_37A0C65D_3B80_44AB_9F09_75A6D8A07D12";
    private const string ActivationEventName = "Local\\Clippy_Activate_37A0C65D_3B80_44AB_9F09_75A6D8A07D12";

    private Mutex? singleInstanceMutex;
    private EventWaitHandle? activationEvent;
    private TaskbarIcon? trayIcon;
    private ClipboardTextAutoNormalizer? clipboardTextAutoNormalizer;
    private GlobalHotKeyService? globalHotKeyService;
    private Icon? trayIconImage;
    private Stream? trayIconStream;
    private bool ownsMutex;

    public bool IsExiting { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!TryAcquireSingleInstance())
        {
            SignalExistingInstance();
            Shutdown();
            return;
        }

        activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ActivationEventName);
        StartActivationListener();

        var window = new MainWindow();
        MainWindow = window;
        trayIcon = new TaskbarIcon
        {
            Icon = LoadTrayIcon(),
            ToolTipText = LocalizationManager.T("ClipboardTitle"),
            ContextMenu = CreateTrayMenu()
        };
        trayIcon.TrayMouseDoubleClick += (_, _) => ShowMainWindow();
        SessionEnding += OnSessionEnding;
        window.SourceInitialized += (_, _) =>
        {
            globalHotKeyService = new GlobalHotKeyService(window, ShowMainWindow);
            if (!globalHotKeyService.TryConfigure(window.ConfiguredHotkey, out var shortcutStatus))
                window.ReportClipboardNormalizerStatus(shortcutStatus);

            try
            {
                clipboardTextAutoNormalizer = new ClipboardTextAutoNormalizer(window, window.ReportClipboardNormalizerStatus);
                clipboardTextAutoNormalizer.Start();
            }
            catch (Exception ex)
            {
                window.ReportClipboardNormalizerStatus(LocalizationManager.Format("ClipboardListenerError", ex.Message));
            }
        };
        window.StartInTray();
    }

    private bool TryAcquireSingleInstance()
    {
        singleInstanceMutex = new Mutex(initiallyOwned: true, MutexName, out var createdNew);
        ownsMutex = createdNew;
        return createdNew;
    }

    private void SignalExistingInstance()
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                using var signal = EventWaitHandle.OpenExisting(ActivationEventName);
                signal.Set();
                return;
            }
            catch (WaitHandleCannotBeOpenedException)
            {
                Thread.Sleep(50);
            }
        }
    }

    private void StartActivationListener()
    {
        var signal = activationEvent!;
        var listener = new Thread(() =>
        {
            try
            {
                while (signal.WaitOne())
                {
                    if (IsExiting) return;
                    Dispatcher.BeginInvoke(new Action(ShowMainWindow));
                }
            }
            catch (ObjectDisposedException)
            {
                // The application is already shutting down.
            }
        })
        {
            IsBackground = true,
            Name = "Clippy single-instance activation listener"
        };
        listener.Start();
    }

    private Icon LoadTrayIcon()
    {
        var resource = GetResourceStream(new Uri("pack://application:,,,/Resources/cliprack.ico", UriKind.Absolute))
            ?? throw new InvalidOperationException("ClipRack tray icon resource is missing.");
        trayIconStream = resource.Stream;
        trayIconImage = new Icon(trayIconStream);
        return trayIconImage;
    }

    private ContextMenu CreateTrayMenu()
    {
        var menu = new ContextMenu();
        var openItem = new MenuItem { Header = LocalizationManager.T("TrayOpen") };
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);

        var exitItem = new MenuItem { Header = LocalizationManager.T("TrayExit") };
        exitItem.Click += (_, _) => ExitApplication();
        menu.Items.Add(exitItem);
        return menu;
    }

    public void RefreshTrayLocalization()
    {
        if (trayIcon is null) return;
        trayIcon.ToolTipText = LocalizationManager.T("ClipboardTitle");
        trayIcon.ContextMenu = CreateTrayMenu();
    }

    private void ShowMainWindow()
    {
        if (MainWindow is not MainWindow window) return;
        window.RestoreFromTray();
    }

    private void ExitApplication()
    {
        if (MainWindow is MainWindow window && !window.ConfirmPendingMacroChanges()) return;
        IsExiting = true;
        Shutdown();
    }

    private void OnSessionEnding(object? sender, SessionEndingCancelEventArgs e)
    {
        if (MainWindow is MainWindow window && !window.ConfirmPendingMacroChanges())
        {
            e.Cancel = true;
            return;
        }

        IsExiting = true;
    }

    public void StopClipboardTextAutoNormalizer()
    {
        clipboardTextAutoNormalizer?.Dispose();
        clipboardTextAutoNormalizer = null;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        IsExiting = true;
        activationEvent?.Set();
        StopClipboardTextAutoNormalizer();
        globalHotKeyService?.Dispose();
        globalHotKeyService = null;
        trayIcon?.Dispose();
        trayIcon = null;
        trayIconImage?.Dispose();
        trayIconImage = null;
        trayIconStream?.Dispose();
        trayIconStream = null;
        activationEvent?.Dispose();
        activationEvent = null;

        if (ownsMutex)
        {
            singleInstanceMutex?.ReleaseMutex();
            ownsMutex = false;
        }
        singleInstanceMutex?.Dispose();
        singleInstanceMutex = null;

        base.OnExit(e);
    }

    public bool ConfigureGlobalHotkey(string shortcut, out string status)
    {
        if (globalHotKeyService is null)
        {
            status = LocalizationManager.T("ShortcutInitError");
            return false;
        }
        return globalHotKeyService.TryConfigure(shortcut, out status);
    }
}
