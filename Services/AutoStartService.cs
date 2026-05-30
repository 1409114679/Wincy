using Microsoft.Win32;

namespace Wincy.Services;

public static class AutoStartService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "Wincy";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) != null;
        }
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true);
        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            key?.SetValue(AppName, exePath);
        }
        else
        {
            try { key?.DeleteValue(AppName, false); } catch { }
        }
    }
}