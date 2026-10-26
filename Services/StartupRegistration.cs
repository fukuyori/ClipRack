using Microsoft.Win32;

namespace Clippy.Services;

public sealed class StartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ClipRack";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true)
            ?? throw new InvalidOperationException(LocalizationManager.T("StartupRegistryError"));

        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
            throw new InvalidOperationException(LocalizationManager.T("ExecutablePathError"));

        key.SetValue(ValueName, $"\"{executablePath}\"", RegistryValueKind.String);
    }
}
