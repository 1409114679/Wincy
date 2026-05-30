using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wincy.Models;
using Wincy.Services;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using RadioButton = System.Windows.Controls.RadioButton;
using Color = System.Windows.Media.Color;

namespace Wincy;

public partial class SettingsWindow : Window
{
    private string? _recordingTag;
    private Key _recordedKey;
    private ModifierKeys _recordedModifiers;
    private bool _hasRecordedKey;
    private bool _isCtrlDown, _isAltDown, _isShiftDown, _isWinDown;
    private readonly HotkeySettings _settings;
    private readonly HotkeySettings _original;
    private readonly Dictionary<string, TextBlock> _tagToLabel = new();
    private bool _suppressLangEvent;

    public HotkeySettings CurrentSettings => _settings;
    public int MaxItems { get; private set; } = 200;

    public SettingsWindow(HotkeySettings? current = null)
    {
        InitializeComponent();
        _settings = current?.Clone() ?? HotkeySettings.Defaults;
        _original = current?.Clone() ?? HotkeySettings.Defaults;

        _tagToLabel["ShowHotkey"] = Lbl_ShowHotkey;
        _tagToLabel["CopyHotkey"] = Lbl_CopyHotkey;
        _tagToLabel["PasteHotkey"] = Lbl_PasteHotkey;
        _tagToLabel["DeleteHotkey"] = Lbl_DeleteHotkey;
        _tagToLabel["PinHotkey"] = Lbl_PinHotkey;

        MaxItemsBox.Text = DatabaseService.GetMaxItems().ToString();
        AutoStartCheck.IsChecked = AutoStartService.IsEnabled;

        _suppressLangEvent = true;
        if (LocalizationService.CurrentLanguage == Wincy.Services.Language.Chinese)
            LangCN.IsChecked = true;
        else
            LangEN.IsChecked = true;
        _suppressLangEvent = false;

        LoadLabels();
        ApplyLocalization();
    }

    private void LoadLabels()
    {
        Lbl_ShowHotkey.Text = HotkeyToString(_settings.ShowHotkey);
        Lbl_CopyHotkey.Text = HotkeyToString(_settings.CopyHotkey);
        Lbl_PasteHotkey.Text = HotkeyToString(_settings.PasteHotkey);
        Lbl_DeleteHotkey.Text = HotkeyToString(_settings.DeleteHotkey);
        Lbl_PinHotkey.Text = HotkeyToString(_settings.PinHotkey);
    }

    private void ApplyLocalization()
    {
        TitleLabel.Text = Loc("Settings.Title");
        MaxItemsLabel.Text = CurrentLangStr == "Chinese" ? "最大条目数：" : "Max items: ";
        ShortcutHeader.Text = Loc("Settings.Shortcuts");
        Lbl_ShowHideText.Text = Loc("Settings.ShowHide");
        Lbl_CopyText.Text = Loc("Settings.Copy");
        Lbl_PasteText.Text = Loc("Settings.Paste");
        Lbl_DeleteText.Text = Loc("Settings.Delete");
        Lbl_PinText.Text = Loc("Settings.Pin");
        AutoStartCheck.Content = Loc("Settings.AutoStart");
        ResetBtn.Content = Loc("Settings.Reset");
        DoneBtn.Content = Loc("Settings.Done");
        HintText.Text = Loc("Settings.Hint");
        RecordingHint.Text = Loc("Settings.Recording");
    }

    private static string Loc(string key) => LocalizationService.Get(key);
    private static string CurrentLangStr => LocalizationService.CurrentLanguage == Wincy.Services.Language.Chinese ? "Chinese" : "English";

    private void Language_Checked(object sender, RoutedEventArgs e)
    {
        if (_suppressLangEvent) return;
        var newLang = (sender as RadioButton)?.Tag is "Chinese" ? Wincy.Services.Language.Chinese : Wincy.Services.Language.English;
        if (newLang != LocalizationService.CurrentLanguage)
        {
            LocalizationService.SetLanguage(newLang);
            ApplyLocalization();
            if (_recordingTag != null) UpdateRecordingHint();
            LocalizationService.NotifyLanguageChanged();
        }
    }

    private void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressLangEvent) return;
        AutoStartService.SetEnabled(AutoStartCheck.IsChecked == true);
    }

    private void MaxItems_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = !int.TryParse(e.Text, out _);
    }

    // ===== Hotkey recording =====
    private void HotkeyLabel_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is string tag) StartRecording(tag);
        e.Handled = true;
    }

    private void HotkeyRow_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Grid g && g.Tag is string tag) StartRecording(tag);
    }

    private void StartRecording(string tag)
    {
        StopRecording();
        _recordingTag = tag;
        _hasRecordedKey = false;
        _isCtrlDown = _isAltDown = _isShiftDown = _isWinDown = false;
        if (_tagToLabel.TryGetValue(tag, out var label))
        {
            label.Text = "...";
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
            label.FontWeight = FontWeights.Bold;
        }
        RecordingHint.Text = Loc("Settings.Recording");
        RecordingHint.Visibility = Visibility.Visible;
        Focus();
    }

    private void StopRecording()
    {
        _recordingTag = null;
        _hasRecordedKey = false;
        _isCtrlDown = _isAltDown = _isShiftDown = _isWinDown = false;
        RecordingHint.Visibility = Visibility.Collapsed;
        foreach (var kv in _tagToLabel) kv.Value.Foreground = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
        LoadLabels();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingTag == null) return;
        switch (e.Key)
        {
            case Key.LeftCtrl: case Key.RightCtrl: _isCtrlDown = true; break;
            case Key.LeftAlt: case Key.RightAlt: _isAltDown = true; break;
            case Key.LeftShift: case Key.RightShift: _isShiftDown = true; break;
            case Key.LWin: case Key.RWin: _isWinDown = true; break;
            case Key.Escape: StopRecording(); e.Handled = true; return;
        }
        if (IsModifierKey(e.Key)) { UpdateRecordingHint(); e.Handled = true; return; }
        _recordedKey = e.Key;
        _recordedModifiers = GetCurrentModifiers();
        _hasRecordedKey = true;
        SaveRecording();
        e.Handled = true;
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (_recordingTag == null) return;
        switch (e.Key)
        {
            case Key.LeftCtrl: case Key.RightCtrl: _isCtrlDown = false; break;
            case Key.LeftAlt: case Key.RightAlt: _isAltDown = false; break;
            case Key.LeftShift: case Key.RightShift: _isShiftDown = false; break;
            case Key.LWin: case Key.RWin: _isWinDown = false; break;
        }
        UpdateRecordingHint();
    }

    private void SaveRecording()
    {
        if (_recordingTag == null || !_hasRecordedKey) return;
        var hk = new HotkeyInfo { Key = _recordedKey, Modifiers = _recordedModifiers };
        switch (_recordingTag)
        {
            case "ShowHotkey": _settings.ShowHotkey = hk; break;
            case "CopyHotkey": _settings.CopyHotkey = hk; break;
            case "PasteHotkey": _settings.PasteHotkey = hk; break;
            case "DeleteHotkey": _settings.DeleteHotkey = hk; break;
            case "PinHotkey": _settings.PinHotkey = hk; break;
        }
        StopRecording();
    }

    private void UpdateRecordingHint()
    {
        var parts = new List<string>();
        if (_isCtrlDown) parts.Add("Ctrl");
        if (_isAltDown) parts.Add("Alt");
        if (_isShiftDown) parts.Add("Shift");
        if (_isWinDown) parts.Add("Win");
        RecordingHint.Text = parts.Count > 0
            ? LocalizationService.Get("Settings.RecordingPartial").Replace("{0}", string.Join("+", parts))
            : Loc("Settings.Recording");
    }

    private void Done_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
        if (int.TryParse(MaxItemsBox.Text, out int max) && max > 0)
        {
            MaxItems = max;
            DatabaseService.SetMaxItems(max);
        }
        DialogResult = true;
        Close();
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
        var d = HotkeySettings.Defaults;
        _settings.ShowHotkey = d.ShowHotkey.Clone();
        _settings.CopyHotkey = d.CopyHotkey.Clone();
        _settings.PasteHotkey = d.PasteHotkey.Clone();
        _settings.DeleteHotkey = d.DeleteHotkey.Clone();
        _settings.PinHotkey = d.PinHotkey.Clone();
        LoadLabels();
        MaxItemsBox.Text = "200";
    }

    private static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private ModifierKeys GetCurrentModifiers()
    {
        var m = ModifierKeys.None;
        if (_isCtrlDown) m |= ModifierKeys.Control;
        if (_isAltDown) m |= ModifierKeys.Alt;
        if (_isShiftDown) m |= ModifierKeys.Shift;
        if (_isWinDown) m |= ModifierKeys.Windows;
        return m;
    }

    public static string HotkeyToString(HotkeyInfo hk)
    {
        if (hk.Key == Key.None) return "—";
        var parts = new List<string>();
        if (hk.Modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (hk.Modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (hk.Modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (hk.Modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(KeyToDisplay(hk.Key));
        return string.Join("+", parts);
    }

    private static string KeyToDisplay(Key key) => key switch
    {
        Key.D0 => "0", Key.D1 => "1", Key.D2 => "2", Key.D3 => "3", Key.D4 => "4",
        Key.D5 => "5", Key.D6 => "6", Key.D7 => "7", Key.D8 => "8", Key.D9 => "9",
        Key.OemSemicolon => ";", Key.OemComma => ",",
        Key.OemPeriod => ".", Key.Oem2 => "/", Key.Oem3 => "`",
        Key.Oem4 => "[", Key.Oem5 => "\\", Key.Oem6 => "]", Key.Oem7 => "'",
        Key.OemPlus => "=", Key.OemMinus => "-",
        Key.Delete => "Delete", Key.Back => "Backspace",
        Key.Tab => "Tab", Key.Space => "Space",
        Key.Return => "Enter", Key.Capital => "CapsLock",
        Key.Escape => "Esc", Key.Home => "Home", Key.End => "End",
        Key.PageUp => "PgUp", Key.PageDown => "PgDn",
        Key.Left => "←", Key.Right => "→", Key.Up => "↑", Key.Down => "↓",
        Key.Insert => "Insert", Key.PrintScreen => "PrtSc",
        Key.F1 => "F1", Key.F2 => "F2", Key.F3 => "F3", Key.F4 => "F4",
        Key.F5 => "F5", Key.F6 => "F6", Key.F7 => "F7", Key.F8 => "F8",
        Key.F9 => "F9", Key.F10 => "F10", Key.F11 => "F11", Key.F12 => "F12",
        _ => key.ToString()
    };
}