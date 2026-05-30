using System.Windows.Input;

namespace Wincy.Models;

public class HotkeyInfo
{
    public Key Key { get; set; } = Key.None;
    public ModifierKeys Modifiers { get; set; } = ModifierKeys.None;

    public HotkeyInfo Clone() => new() { Key = Key, Modifiers = Modifiers };
}

public class HotkeySettings
{
    public HotkeyInfo ShowHotkey { get; set; } = new();
    public HotkeyInfo CopyHotkey { get; set; } = new();
    public HotkeyInfo PasteHotkey { get; set; } = new();
    public HotkeyInfo DeleteHotkey { get; set; } = new();
    public HotkeyInfo PinHotkey { get; set; } = new();

    public static HotkeySettings Defaults => new()
    {
        ShowHotkey = new() { Key = Key.OemSemicolon, Modifiers = ModifierKeys.Control },   // Ctrl+;
        CopyHotkey = new() { Key = Key.Enter, Modifiers = ModifierKeys.None },              // Enter
        PasteHotkey = new() { Key = Key.Enter, Modifiers = ModifierKeys.Alt },             // Alt+Enter
        DeleteHotkey = new() { Key = Key.Delete, Modifiers = ModifierKeys.Alt },           // Alt+Delete
        PinHotkey = new() { Key = Key.P, Modifiers = ModifierKeys.Alt }                     // Alt+P
    };

    public HotkeySettings Clone() => new()
    {
        ShowHotkey = ShowHotkey.Clone(),
        CopyHotkey = CopyHotkey.Clone(),
        PasteHotkey = PasteHotkey.Clone(),
        DeleteHotkey = DeleteHotkey.Clone(),
        PinHotkey = PinHotkey.Clone()
    };
}