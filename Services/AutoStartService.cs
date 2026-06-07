using System.IO;
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
            // Use the absolute path to Wincy.exe in the app's base directory
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var exePath = Path.Combine(appDir, "Wincy.exe");
            key?.SetValue(AppName, exePath);
        }
        else
        {
            try { key?.DeleteValue(AppName, false); } catch { }
        }
    }
}
