using System.Runtime.InteropServices;

namespace Wincy.Services;

/// <summary>
/// Registers global hotkeys so Wincy can be summoned from anywhere.
/// </summary>
public class HotkeyService : IDisposable
{
    private const int MOD_ALT = 0x0001;
    private const int MOD_CONTROL = 0x0002;
    private const int MOD_SHIFT = 0x0004;
    private const int MOD_WIN = 0x0008;

    private int _currentHotkeyId = 9000;
    private readonly Dictionary<int, (System.Windows.Input.ModifierKeys Modifiers, System.Windows.Input.Key Key)> _registeredHotkeys = new();
    private IntPtr _hWnd;
    private Action<int>? _hotkeyCallback;

    public event EventHandler<int>? HotkeyPressed;

    public void Initialize(IntPtr hWnd, Action<int> hotkeyCallback)
    {
        _hWnd = hWnd;
        _hotkeyCallback = hotkeyCallback;
    }

    /// <summary>
    /// Register a global hotkey. Returns the hotkey ID.
    /// </summary>
    public int RegisterHotkey(System.Windows.Input.ModifierKeys modifiers, System.Windows.Input.Key key)
    {
        var id = _currentHotkeyId++;
        uint mod = 0;

        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Alt)) mod |= MOD_ALT;
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Control)) mod |= MOD_CONTROL;
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Shift)) mod |= MOD_SHIFT;
        if (modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows)) mod |= MOD_WIN;

        var vk = System.Windows.Input.KeyInterop.VirtualKeyFromKey(key);

        var result = RegisterHotKey(_hWnd, id, mod, (uint)vk);
        if (result)
        {
            _registeredHotkeys[id] = (modifiers, key);
        }

        return result ? id : -1;
    }

    /// <summary>
    /// Unregister a previously registered hotkey.
    /// </summary>
    public void UnregisterHotkey(int id)
    {
        UnregisterHotKey(_hWnd, id);
        _registeredHotkeys.Remove(id);
    }

    /// <summary>
    /// Unregister all hotkeys.
    /// </summary>
    public void UnregisterAll()
    {
        foreach (var id in _registeredHotkeys.Keys.ToList())
        {
            UnregisterHotKey(_hWnd, id);
        }
        _registeredHotkeys.Clear();
    }

    public void HandleWmHotkey(IntPtr wParam)
    {
        var id = wParam.ToInt32();
        HotkeyPressed?.Invoke(this, id);
        _hotkeyCallback?.Invoke(id);
    }

    public void Dispose()
    {
        UnregisterAll();
    }

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
}