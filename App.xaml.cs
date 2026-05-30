using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Wincy.Services;

namespace Wincy;

public partial class App : System.Windows.Application
{
    private System.Windows.Forms.NotifyIcon? _notifyIcon;
    private ClipboardService? _clipboardService;
    private DatabaseService? _database;
    private HotkeyService? _hotkeyService;
    private SearchWindow? _searchWindow;
    private IntPtr _clipboardViewerHwnd;
    private IntPtr _nextClipboardViewer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _database = new DatabaseService();
        _clipboardService = new ClipboardService(_database);
        _hotkeyService = new HotkeyService();

        _searchWindow = new SearchWindow(_database, _clipboardService);

        CreateSystemTray();
        SetupClipboardMonitoring();
        RegisterGlobalHotkey();
    }

    private void CreateSystemTray()
    {
        var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "icon.ico");
        var icon = File.Exists(iconPath)
            ? new Icon(iconPath)
            : System.Drawing.SystemIcons.Application;

        _notifyIcon = new System.Windows.Forms.NotifyIcon
        {
            Icon = icon,
            Text = "Wincy - Clipboard Manager",
            Visible = true
        };

        var contextMenu = new System.Windows.Forms.ContextMenuStrip();

        var showItem = new System.Windows.Forms.ToolStripMenuItem("Show History");
        showItem.Click += (s, e) => ShowSearchWindow();
        contextMenu.Items.Add(showItem);

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var clearItem = new System.Windows.Forms.ToolStripMenuItem("Clear History");
        clearItem.Click += (s, e) => _database?.ClearAll(keepPinned: true);
        contextMenu.Items.Add(clearItem);

        var clearAllItem = new System.Windows.Forms.ToolStripMenuItem("Clear All (including pinned)");
        clearAllItem.Click += (s, e) => _database?.ClearAll(keepPinned: false);
        contextMenu.Items.Add(clearAllItem);

        contextMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());

        var exitItem = new System.Windows.Forms.ToolStripMenuItem("Exit");
        exitItem.Click += (s, e) => ShutdownWincy();
        contextMenu.Items.Add(exitItem);

        _notifyIcon.ContextMenuStrip = contextMenu;
        _notifyIcon.DoubleClick += (s, e) => ShowSearchWindow();
    }

    private void SetupClipboardMonitoring()
    {
        var helperWindow = new Window
        {
            Width = 0,
            Height = 0,
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent
        };

        helperWindow.Loaded += (s, e) =>
        {
            var hwnd = new WindowInteropHelper(helperWindow).Handle;
            _clipboardViewerHwnd = hwnd;
            _clipboardService?.StartMonitoring(hwnd);
            var source = HwndSource.FromHwnd(hwnd);
            source?.AddHook(WndProc);
        };

        helperWindow.Show();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_DRAWCLIPBOARD = 0x0308;
        const int WM_CHANGECBCHAIN = 0x030D;
        const int WM_HOTKEY = 0x0312;

        switch (msg)
        {
            case WM_DRAWCLIPBOARD:
                _clipboardService?.HandleClipboardUpdate();
                break;

            case WM_CHANGECBCHAIN:
                if (wParam == _nextClipboardViewer)
                    _nextClipboardViewer = lParam;
                else if (_nextClipboardViewer != IntPtr.Zero)
                    PostMessage(_nextClipboardViewer, WM_CHANGECBCHAIN, wParam, lParam);
                break;

            case WM_HOTKEY:
                ShowSearchWindow();
                break;
        }

        return IntPtr.Zero;
    }

    private void RegisterGlobalHotkey()
    {
        if (_hotkeyService != null)
        {
            _hotkeyService.Initialize(_clipboardViewerHwnd, _ =>
            {
                Dispatcher.Invoke(() => ShowSearchWindow());
            });

            var id = _hotkeyService.RegisterHotkey(
                System.Windows.Input.ModifierKeys.Control | System.Windows.Input.ModifierKeys.Shift,
                System.Windows.Input.Key.V
            );

            if (id < 0)
            {
                System.Diagnostics.Debug.WriteLine("Failed to register hotkey Ctrl+Shift+V");
            }
        }
    }

    private void ShowSearchWindow()
    {
        if (_searchWindow != null)
        {
            var cursorPos = System.Windows.Forms.Cursor.Position;
            var screen = System.Windows.Forms.Screen.FromPoint(cursorPos);
            var workingArea = screen.WorkingArea;

            var left = Math.Min(cursorPos.X, workingArea.Right - _searchWindow.Width);
            var top = Math.Min(cursorPos.Y + 20, workingArea.Bottom - _searchWindow.Height);

            _searchWindow.Left = Math.Max(left, workingArea.Left);
            _searchWindow.Top = Math.Max(top, workingArea.Top);

            _searchWindow.RefreshAndShow();
        }
    }

    private void ShutdownWincy()
    {
        _hotkeyService?.Dispose();
        _clipboardService?.Dispose();
        _notifyIcon?.Dispose();
        _searchWindow?.Close();
        Current.Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkeyService?.Dispose();
        _clipboardService?.Dispose();
        _notifyIcon?.Dispose();
        base.OnExit(e);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr PostMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
}