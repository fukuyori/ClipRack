using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace Clippy.Services;

public static class ThemeManager
{
    public static void Apply(string theme)
    {
        var dark = string.Equals(theme, "Dark", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(theme, "System", StringComparison.OrdinalIgnoreCase) && IsWindowsDarkMode());
        var resources = Application.Current.Resources;
        Set(resources, "WindowBackgroundBrush", dark ? "#FF1E1F22" : "#FFF6F7F9");
        Set(resources, "SurfaceBackgroundBrush", dark ? "#FF282A2E" : "#FFFFFFFF");
        Set(resources, "ControlBackgroundBrush", dark ? "#FF34373C" : "#FFF2F4F7");
        Set(resources, "InputBackgroundBrush", dark ? "#FF222428" : "#FFFFFFFF");
        Set(resources, "TextBrush", dark ? "#FFF1F3F5" : "#FF20242A");
        Set(resources, "SecondaryTextBrush", dark ? "#FFB0B6C0" : "#FF667085");
        Set(resources, "BorderBrush", dark ? "#FF50545B" : "#FFD0D5DD");
        Set(resources, "HoverBrush", dark ? "#FF41454C" : "#FFE7ECF2");
        Set(resources, "SelectionBrush", dark ? "#FF395B82" : "#FFD8E9FB");
    }

    private static bool IsWindowsDarkMode()
    {
        try
        {
            return Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme",
                1) is int appsUseLightTheme && appsUseLightTheme == 0;
        }
        catch (System.Security.SecurityException)
        {
            return false;
        }
    }

    private static void Set(ResourceDictionary resources, string key, string color)
    {
        resources[key] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
    }
}
