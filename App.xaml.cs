using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Wincy.Models;
using Wincy.Services;

namespace Wincy;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private ClipboardService? _clipboardService;
    private DatabaseService? _database;
    private SearchWindow? _searchWindow;
    private string? _lastClipboardText;
    private IntPtr _hotkeyHwnd;
    private HotkeyService? _hotkeyService;
    private HotkeySettings _hotkeySettings = HotkeySettings.Defaults.Clone();

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogService.Info("=== Wincy v3 ===");

        try
        {
            _database = new DatabaseService();
            _clipboardService = new ClipboardService();
            _searchWindow = new SearchWindow(_database, _clipboardService, _hotkeySettings, OpenSettings);

            CreateSystemTray();
            StartClipboardMonitor();
            CreateHotkeyWindow();

            // Show window on startup
            ShowSearchWindow();
            LogService.Info("Started successfully");
        }
        catch (Exception ex)
        {
            LogService.Error("Startup failed", ex);
            System.Windows.MessageBox.Show($"Wincy Error: {ex.Message}", "Wincy",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            Shutdown();
        }
    }

    // ===== Hotkey: dedicated invisible window that never closes =====
    private void CreateHotkeyWindow()
    {
        var hotkeyWindow = new Window
        {
            Width = 0, Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false, ShowActivated = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };

        hotkeyWindow.Loaded += (s, e) =>
        {
            _hotkeyHwnd = new WindowInteropHelper(hotkeyWindow).Handle;
            var source = HwndSource.FromHwnd(_hotkeyHwnd);
            source?.AddHook(WndProc);
            RegisterShowHotkey();
            LogService.Info("Hotkey window ready");
        };

        hotkeyWindow.Show();
    }

    private void RegisterShowHotkey()
    {
        if (_hotkeyService != null)
        {
            _hotkeyService.UnregisterAll();
            _hotkeyService.Dispose();
        }

        _hotkeyService = new HotkeyService();
        _hotkeyService.Initialize(_hotkeyHwnd, _ => { });

        var showHk = _hotkeySettings.ShowHotkey;

        // Try configured hotkey first
        int id = _hotkeyService.RegisterHotkey(showHk);
        if (id >= 0)
        {
            LogService.Info($"Hotkey: {SettingsWindow.HotkeyToString(showHk)}");
            return;
        }

        // Fallback: try without Win modifier (Win is most likely to conflict)
        if (showHk.Modifiers.HasFlag(System.Windows.Input.ModifierKeys.Windows))
        {
            var fallbackMods = showHk.Modifiers & ~System.Windows.Input.ModifierKeys.Windows;
            if (fallbackMods != System.Windows.Input.ModifierKeys.None || showHk.Key != System.Windows.Input.Key.None)
            {
                id = _hotkeyService.RegisterHotkey(fallbackMods, showHk.Key);
                if (id >= 0)
                {
                    LogService.Info($"Hotkey (fallback): {SettingsWindow.HotkeyToString(new HotkeyInfo { Key = showHk.Key, Modifiers = fallbackMods })}");
                    return;
                }
            }
        }

        // Last resort: Ctrl+;
        id = _hotkeyService.RegisterHotkey(
            System.Windows.Input.ModifierKeys.Control,
            System.Windows.Input.Key.OemSemicolon);
        if (id >= 0) { LogService.Info("Hotkey (last resort): Ctrl+;"); return; }

        id = _hotkeyService.RegisterHotkey(
            System.Windows.Input.ModifierKeys.Alt,
            System.Windows.Input.Key.OemSemicolon);
        LogService.Info(id >= 0 ? "Hotkey (last resort): Alt+;" : "ALL HOTKEYS FAILED");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_HOTKEY = 0x0312;
        if (msg == WM_HOTKEY)
        {
            LogService.Info("HOTKEY!");
            SearchWindow.CaptureAnchorPoint();
            Dispatcher.Invoke(() => ShowSearchWindow());
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ===== Settings =====
    private void OpenSettings()
    {
        var settingsWindow = new SettingsWindow(_hotkeySettings);
        var result = settingsWindow.ShowDialog();

        if (result == true)
        {
            // Apply new settings
            _hotkeySettings = settingsWindow.CurrentSettings.Clone();
            _searchWindow?.UpdateHotkeySettings(_hotkeySettings);
            RegisterShowHotkey(); // re-register global hotkey
            LogService.Info("Hotkey settings updated");
        }
    }

    // ===== System Tray =====
    private void CreateSystemTray()
    {
        System.Drawing.Icon? trayIcon = null;
        var icoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Wincy.ico");
        if (System.IO.File.Exists(icoPath))
        {
            try { trayIcon = new System.Drawing.Icon(icoPath); }
            catch { }
        }
        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = trayIcon ?? System.Drawing.SystemIcons.Application,
            Text = "Wincy - Clipboard Manager", Visible = true
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        var show = menu.Items.Add("Show");
        show.Click += (s, e) => Dispatcher.Invoke(() => ShowSearchWindow());
        menu.Items.Add("-");
        var settings = menu.Items.Add("Preferences");
        settings.Click += (s, e) => Dispatcher.Invoke(() => OpenSettings());
        menu.Items.Add("-");
        var clear = menu.Items.Add("Clear History");
        clear.Click += (s, e) => _database?.ClearAll(true);
        var clearAll = menu.Items.Add("Clear All");
        clearAll.Click += (s, e) => _database?.ClearAll(false);
        menu.Items.Add("-");
        var exit = menu.Items.Add("Exit");
        exit.Click += (s, e) => ShutdownWincy();
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (s, e) => Dispatcher.Invoke(() => ShowSearchWindow());
    }

    // ===== Clipboard Monitor =====
    private void StartClipboardMonitor()
    {
        var timer = new System.Windows.Forms.Timer { Interval = 500 };
        timer.Tick += (s, e) =>
        {
            try
            {
                var (text, imageData, contentType) = _clipboardService!.GetClipboardContent();
                if (text == null && imageData == null) return;
                if (text == _lastClipboardText) return;
                _lastClipboardText = text;
                var (title, path) = GetActiveWindowInfo();
                _database?.AddItem(new ClipboardItem
                {
                    Text = text, ImageData = imageData, ContentType = contentType,
                    CopiedAt = DateTime.Now, SourceApplication = title, SourceAppPath = path
                });
                _searchWindow?.OnClipboardChanged();
            }
            catch (Exception ex) { LogService.Error("Clipboard", ex); }
        };
        timer.Start();
    }

    private static (string? title, string? path) GetActiveWindowInfo()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return (null, null);
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(hwnd, sb, 256);
        var title = sb.ToString();

        GetWindowThreadProcessId(hwnd, out uint pid);
        string? path = null;
        if (pid > 0)
        {
            try
            {
                using var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                path = proc.MainModule?.FileName;
            }
            catch { }
        }
        return (title, path);
    }

    // ===== Show/Hide =====
    private void ShowSearchWindow()
    {
        if (_searchWindow == null) return;
        try
        {
            SearchWindow.CaptureAnchorPoint();
            _searchWindow.RefreshAndShow();
        }
        catch (Exception ex) { LogService.Error("Show", ex); }
    }

    private void ShutdownWincy()
    {
        LogService.Info("Shutdown");
        _hotkeyService?.Dispose();
        _clipboardService?.Dispose();
        _notifyIcon?.Dispose();
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _clipboardService?.Dispose();
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder t, int c);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
}