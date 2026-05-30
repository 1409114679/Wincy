using System.Diagnostics;
using System.IO;

namespace Wincy.Services;

/// <summary>
/// Simple file-based logger for diagnosing issues.
/// </summary>
public static class LogService
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Wincy", "wincy.log");

    static LogService()
    {
        var dir = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
    }

    public static void Info(string message)
    {
        Write("INFO", message);
    }

    public static void Warn(string message)
    {
        Write("WARN", message);
    }

    public static void Error(string message, Exception? ex = null)
    {
        var msg = ex != null ? $"{message}\n{ex}" : message;
        Write("ERROR", msg);
    }

    private static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            File.AppendAllText(LogPath, line + Environment.NewLine);
            Debug.WriteLine(line);
        }
        catch { }
    }

    public static string[] GetRecentLogs(int lines = 50)
    {
        try
        {
            if (!File.Exists(LogPath)) return Array.Empty<string>();
            var all = File.ReadAllLines(LogPath);
            return all.Skip(Math.Max(0, all.Length - lines)).ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}