using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Wincy.Models;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace Wincy;

public partial class SettingsWindow : Window
{
    // Current recording state
    private string? _recordingTag;
    private Key _recordedKey;
    private ModifierKeys _recordedModifiers;
    private bool _hasRecordedKey;
    private bool _isCtrlDown, _isAltDown, _isShiftDown, _isWinDown;

    // Hotkey value store (persisted changes)
    private readonly HotkeySettings _settings;
    private readonly HotkeySettings _original;

    // Label -> Tag mapping
    private readonly Dictionary<string, TextBlock> _tagToLabel = new();

    public HotkeySettings CurrentSettings => _settings;

    public SettingsWindow(HotkeySettings? current = null)
    {
        InitializeComponent();
        _settings = current?.Clone() ?? HotkeySettings.Defaults;
        _original = current?.Clone() ?? HotkeySettings.Defaults;

        // Map tag → label TextBlocks
        _tagToLabel["ShowHotkey"] = Lbl_ShowHotkey;
        _tagToLabel["CopyHotkey"] = Lbl_CopyHotkey;
        _tagToLabel["PasteHotkey"] = Lbl_PasteHotkey;
        _tagToLabel["DeleteHotkey"] = Lbl_DeleteHotkey;
        _tagToLabel["PinHotkey"] = Lbl_PinHotkey;

        LoadLabels();
    }

    private void LoadLabels()
    {
        Lbl_ShowHotkey.Text = HotkeyToString(_settings.ShowHotkey);
        Lbl_CopyHotkey.Text = HotkeyToString(_settings.CopyHotkey);
        Lbl_PasteHotkey.Text = HotkeyToString(_settings.PasteHotkey);
        Lbl_DeleteHotkey.Text = HotkeyToString(_settings.DeleteHotkey);
        Lbl_PinHotkey.Text = HotkeyToString(_settings.PinHotkey);
    }

    // ===== Click on hotkey label → start recording =====
    private void HotkeyLabel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is TextBlock tb && tb.Tag is string tag)
            StartRecording(tag);
        e.Handled = true;
    }

    // ===== Click on row background → start recording =====
    private void HotkeyRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Grid g && g.Tag is string tag)
            StartRecording(tag);
    }

    private void StartRecording(string tag)
    {
        // Stop any existing recording
        StopRecording();

        _recordingTag = tag;
        _hasRecordedKey = false;
        _isCtrlDown = _isAltDown = _isShiftDown = _isWinDown = false;

        // Highlight the recording label
        if (_tagToLabel.TryGetValue(tag, out var label))
        {
            label.Text = "...";
            label.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0xFF, 0xD7, 0x00));
            label.FontWeight = FontWeights.Bold;
        }

        RecordingHint.Visibility = Visibility.Visible;
        Focus(); // ensure window receives key events
    }

    private void StopRecording()
    {
        _recordingTag = null;
        _hasRecordedKey = false;
        _isCtrlDown = _isAltDown = _isShiftDown = _isWinDown = false;
        RecordingHint.Visibility = Visibility.Collapsed;

        // Reset all label styles
        foreach (var kv in _tagToLabel)
        {
            kv.Value.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(0x00, 0x78, 0xD4));
        }

        LoadLabels();
    }

    // ===== Keyboard capture =====
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingTag == null) return;

        // Track modifiers
        switch (e.Key)
        {
            case Key.LeftCtrl: case Key.RightCtrl: _isCtrlDown = true; break;
            case Key.LeftAlt: case Key.RightAlt: _isAltDown = true; break;
            case Key.LeftShift: case Key.RightShift: _isShiftDown = true; break;
            case Key.LWin: case Key.RWin: _isWinDown = true; break;
            case Key.Escape:
                StopRecording();
                e.Handled = true;
                return;
        }

        // Ignore modifier-only presses (wait for the real key)
        if (IsModifierKey(e.Key))
        {
            // Show partial combo while holding modifiers
            UpdateRecordingHint();
            e.Handled = true;
            return;
        }

        // Record the actual key + current modifiers
        _recordedKey = e.Key;
        _recordedModifiers = GetCurrentModifiers();
        _hasRecordedKey = true;

        // Save to settings
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
        if (parts.Count > 0)
            RecordingHint.Text = $"⏺ Recording: {string.Join("+", parts)}+? ... (Esc to cancel)";
        else
            RecordingHint.Text = "⏺ Recording: Press your key combination... (Esc to cancel)";
    }

    // ===== Done / Reset =====
    private void Done_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
        DialogResult = true;
        Close();
    }

    private void ResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        StopRecording();
        var defaults = HotkeySettings.Defaults;
        _settings.ShowHotkey = defaults.ShowHotkey.Clone();
        _settings.CopyHotkey = defaults.CopyHotkey.Clone();
        _settings.PasteHotkey = defaults.PasteHotkey.Clone();
        _settings.DeleteHotkey = defaults.DeleteHotkey.Clone();
        _settings.PinHotkey = defaults.PinHotkey.Clone();
        LoadLabels();
    }

    // ===== Helpers =====
    private static bool IsModifierKey(Key key)
    {
        return key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
            or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
    }

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