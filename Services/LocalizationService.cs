using System.IO;
using System.Text.Json;

namespace Wincy.Services;

public enum Language { Chinese, English }

public static class LocalizationService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Wincy", "config.json");

    public static Language CurrentLanguage { get; private set; } = Language.English;

    static LocalizationService()
    {
        Load();
    }

    public static void SetLanguage(Language lang)
    {
        CurrentLanguage = lang;
        Save();
    }

    private static void Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("Language", out var langProp))
                {
                    CurrentLanguage = langProp.GetString() == "Chinese" ? Language.Chinese : Language.English;
                }
            }
        }
        catch { }
    }

    private static void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath)!;
            Directory.CreateDirectory(dir);
            var obj = new { Language = CurrentLanguage.ToString() };
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(obj));
        }
        catch { }
    }

    // ===== UI Strings =====
    public static string Get(string key) => key switch
    {
        // Settings window
        "Settings.Title" => CurrentLanguage == Language.Chinese ? "设置" : "Preferences",
        "Settings.Shortcuts" => CurrentLanguage == Language.Chinese ? "键盘快捷键" : "Keyboard Shortcuts",
        "Settings.ShowHide" => CurrentLanguage == Language.Chinese ? "显示/隐藏 Wincy" : "Show/Hide Wincy",
        "Settings.Copy" => CurrentLanguage == Language.Chinese ? "选择并复制" : "Select and Copy",
        "Settings.Paste" => CurrentLanguage == Language.Chinese ? "选择并粘贴" : "Select and Paste",
        "Settings.Delete" => CurrentLanguage == Language.Chinese ? "删除条目" : "Delete Item",
        "Settings.Pin" => CurrentLanguage == Language.Chinese ? "置顶/取消置顶" : "Pin/Unpin",
        "Settings.Reset" => CurrentLanguage == Language.Chinese ? "恢复默认" : "Reset to Defaults",
        "Settings.Done" => CurrentLanguage == Language.Chinese ? "完成" : "Done",
        "Settings.Hint" => CurrentLanguage == Language.Chinese
            ? "点击快捷键，然后按下新的组合键"
            : "Click a shortcut, then press your new keys",
        "Settings.Recording" => CurrentLanguage == Language.Chinese
            ? "⏺ 录制中：按下你的组合键...（Esc 取消）"
            : "⏺ Recording: Press your key combination... (Esc to cancel)",
        "Settings.RecordingPartial" => CurrentLanguage == Language.Chinese
            ? "⏺ 录制中：{0}+? ...（Esc 取消）"
            : "⏺ Recording: {0}+? ... (Esc to cancel)",
        "Settings.AutoStart" => CurrentLanguage == Language.Chinese
            ? "开机自启"
            : "Launch at startup",

        // Language selector
        "Settings.Language" => CurrentLanguage == Language.Chinese ? "语言" : "Language",
        "Settings.LangCN" => "中文",
        "Settings.LangEN" => "English",

        // Search window
        "Search.FooterCopy" => CurrentLanguage == Language.Chinese ? "↵ 复制" : "↵ Copy",
        "Search.FooterPaste" => CurrentLanguage == Language.Chinese ? "⌥↵ 粘贴" : "⌥↵ Paste",

        _ => key
    };

    public static event Action? LanguageChanged;

    public static void NotifyLanguageChanged() => LanguageChanged?.Invoke();
}