using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using MouseEventArgs = System.Windows.Input.MouseEventArgs;
using Point = System.Windows.Point;
using Wincy.Models;
using Wincy.Services;

namespace Wincy;

public partial class SearchWindow : Window
{
    private readonly DatabaseService _database;
    private readonly ClipboardService _clipboardService;
    private readonly Action _openSettings;
    private readonly DetailWindow _detailWindow;
    private bool _isClosing = false;
    private bool _isAltHeld = false;
    private HotkeySettings _hotkeySettings;

    private DispatcherTimer? _hoverDetailTimer;
    private ClipboardItem? _hoveredDetailItem;

    private static Point _mousePoint;
    private static bool _isFirstShow = true;

    public SearchWindow(DatabaseService database, ClipboardService clipboardService,
        HotkeySettings hotkeySettings, Action openSettings)
    {
        InitializeComponent();
        var dpi = VisualTreeHelper.GetDpi(this);
        double scale = dpi.DpiScaleX;
        double sw = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Width / scale;
        double listW = Math.Round(sw * 0.20);
        if (listW < 200) listW = 200;
        if (listW > 400) listW = 400;
        Width = listW;
        Height = 0;

        _database = database;
        _clipboardService = clipboardService;
        _hotkeySettings = hotkeySettings;
        _openSettings = openSettings;
        _detailWindow = new DetailWindow();
        ResultsList.PreviewKeyDown += ResultsList_PreviewKeyDown;

        _hoverDetailTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _hoverDetailTimer.Tick += HoverDetailTimer_Tick;

        UpdateFooterText();
        ApplyFooterLocalization();
        LocalizationService.LanguageChanged += ApplyFooterLocalization;
        LogService.Info("SearchWindow created");
    }

    public void UpdateHotkeySettings(HotkeySettings settings)
    {
        _hotkeySettings = settings;
        UpdateFooterText();
    }

    private void ApplyFooterLocalization()
    {
        if (FooterShortcuts.Inlines.FirstOrDefault() is System.Windows.Documents.Run copyRun)
            copyRun.Text = LocalizationService.Get("Search.FooterCopy");
        if (FooterShortcuts.Inlines.Count > 2 && FooterShortcuts.Inlines.ElementAt(2) is System.Windows.Documents.Run pasteRun)
            pasteRun.Text = LocalizationService.Get("Search.FooterPaste");
    }

    private void UpdateFooterText()
    {
        FooterText.Inlines.Clear();
        FooterText.Inlines.Add(new System.Windows.Documents.Run("0 items"));
        FooterText.Inlines.Add(new System.Windows.Documents.Run(
            $" | {SettingsWindow.HotkeyToString(_hotkeySettings.ShowHotkey)}")
        { Foreground = System.Windows.Media.Brushes.Gray });
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LogService.Info("SearchWindow loaded");
        RefreshList();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    public static void CaptureAnchorPoint()
    {
        var p = System.Windows.Forms.Cursor.Position;
        _mousePoint = new Point(p.X, p.Y);
    }

    public void RefreshAndShow()
    {
        try
        {
            SearchBox.Text = "";
            PositionPopup();
            Show();
            RefreshList();
            Activate();
            SearchBox.Focus();
            SearchBox.SelectAll();
            _isFirstShow = false;
        }
        catch (Exception ex) { LogService.Error("RefreshAndShow", ex); }
    }

    private double _maxListH;

    private void PositionPopup()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double scale = dpi.DpiScaleX;

        double mx = _mousePoint.X / scale;
        double my = _mousePoint.Y / scale;

        var physPt = new System.Drawing.Point((int)_mousePoint.X, (int)_mousePoint.Y);
        var screen = System.Windows.Forms.Screen.FromPoint(physPt);
        var rPhys = screen.WorkingArea;
        double screenLeft = rPhys.Left / scale;
        double screenTop = rPhys.Top / scale;
        double screenRight = rPhys.Right / scale;
        double screenBottom = rPhys.Bottom / scale;

        double listW = Width;
        double detW = listW;
        double gap = 6;
        double redundancy = 1.10;
        double totalW = (listW + gap + detW) * redundancy;

        _maxListH = (screenBottom - screenTop) * 0.40;
        ResultsList.MaxHeight = _maxListH;

        double listLeft, detLeft;
        bool rightSide;

        if (mx + totalW <= screenRight)
        {
            rightSide = true;
            listLeft = mx + gap;
            detLeft = listLeft + listW + gap;
        }
        else
        {
            rightSide = false;
            listLeft = mx - listW - gap;
            if (listLeft < screenLeft) listLeft = screenLeft + gap;
            detLeft = listLeft - detW - gap;
        }

        listLeft = Math.Max(screenLeft, Math.Min(listLeft, screenRight - listW));
        Left = listLeft;

        double listH = 300;
        double top = my - listH - gap >= screenTop ? my - listH - gap : my + gap;
        if (top + listH > screenBottom) top = screenBottom - listH;
        if (top < screenTop) top = screenTop + gap;
        Top = top;

        // Calculate detail position based on list window position
        CalcDetailPosition(listLeft, listW, detW, top);

        LogService.Info($"Position: mouse=({mx:F0},{my:F0}) listLeft={listLeft:F0} top={top:F0} detLeft={_detailLeft:F0} rightSide={rightSide}");
    }

    /// <summary>
    /// Calculate detail window position based on list window position.
    /// Prefers the same side as list relative to mouse, flips if out of bounds.
    /// </summary>
    private void CalcDetailPosition(double listLeft, double listW, double detW, double top)
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        double scale = dpi.DpiScaleX;

        // Get screen bounds based on list window position
        var physPt = new System.Drawing.Point((int)(listLeft * scale + listW * scale / 2), (int)(top * scale));
        var screen = System.Windows.Forms.Screen.FromPoint(physPt);
        var rPhys = screen.WorkingArea;
        double screenLeft = rPhys.Left / scale;
        double screenRight = rPhys.Right / scale;

        double gap = 6;
        double listRight = listLeft + listW;

        // Determine preferred side: same as list relative to mouse
        bool preferRight = _mousePoint.X / scale < listLeft + listW / 2;

        // Try preferred side first, flip if out of bounds
        double detLeft;
        if (preferRight)
        {
            detLeft = listRight + gap;
            if (detLeft + detW > screenRight)
                detLeft = listLeft - detW - gap; // flip to left
        }
        else
        {
            detLeft = listLeft - detW - gap;
            if (detLeft < screenLeft)
                detLeft = listRight + gap; // flip to right
        }

        // Final clamp to screen bounds
        detLeft = Math.Max(screenLeft, Math.Min(detLeft, screenRight - detW));

        _detailLeft = detLeft;
        _detailTop = top;
    }

    private static double _detailLeft, _detailTop;
    public static (double left, double top, double listW, bool rightSide) GetDetailPosition()
        => (_detailLeft, _detailTop, 0, false);

    private void StartDetailTimer(ClipboardItem? item)
    {
        _detailWindow.HideDetail();
        _hoverDetailTimer?.Stop();

        _hoveredDetailItem = item;
        if (item != null && ShouldShowDetail(item))
            _hoverDetailTimer?.Start();
    }

    // ===== Mouse hover → sync selection highlight immediately =====
    private void ResultsList_MouseMove(object sender, MouseEventArgs e)
    {
        var element = ResultsList.InputHitTest(e.GetPosition(ResultsList)) as DependencyObject;
        while (element != null && element != ResultsList)
        {
            if (element is ListBoxItem lbi && lbi.DataContext is ClipboardItem ci)
            {
                if (ci != _hoveredDetailItem)
                {
                    // Immediately update selection highlight on hover
                    ResultsList.SelectedItem = ci;
                    StartDetailTimer(ci);
                }
                return;
            }
            element = VisualTreeHelper.GetParent(element);
        }
        // Hovering empty area: keep selection, just stop the timer
        _hoverDetailTimer?.Stop();
    }

    private void ResultsList_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverDetailTimer?.Stop();
    }

    // ===== Mouse click → acts like Enter (copy or paste) =====
    private void ResultsList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var element = ResultsList.InputHitTest(e.GetPosition(ResultsList)) as DependencyObject;
        while (element != null && element != ResultsList)
        {
            if (element is ListBoxItem && element is FrameworkElement fe && fe.DataContext is ClipboardItem)
            {
                // Click triggers copy immediately (like Enter)
                if (_isAltHeld) PasteSelected(); else CopySelected();
                e.Handled = true;
                return;
            }
            element = VisualTreeHelper.GetParent(element);
        }
    }

    private void HoverDetailTimer_Tick(object? sender, EventArgs e)
    {
        _hoverDetailTimer?.Stop();
        if (_hoveredDetailItem != null && ShouldShowDetail(_hoveredDetailItem))
            _detailWindow.ShowDetail(_hoveredDetailItem, this);
    }

    private void NavigateResults(int delta)
    {
        if (ResultsList.Items.Count == 0) return;
        var oldIdx = ResultsList.SelectedIndex;
        if (oldIdx < 0) oldIdx = 0;
        var newIdx = oldIdx + delta;
        if (newIdx < 0) newIdx = ResultsList.Items.Count - 1;
        if (newIdx >= ResultsList.Items.Count) newIdx = 0;
        ResultsList.SelectedIndex = newIdx;
        ResultsList.ScrollIntoView(ResultsList.SelectedItem);
        ResultsList.Focus();

        var selectedItem = ResultsList.SelectedItem as ClipboardItem;
        StartDetailTimer(selectedItem);
    }

    private static bool ShouldShowDetail(ClipboardItem item)
    {
        if (item.HasImage) return true;
        if (string.IsNullOrEmpty(item.Text)) return false;
        var t = item.Text;
        return t.Contains('\n') || t.Contains('\r') || t.Length > 80;
    }

    // ===== Search Box =====
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshList();

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down: NavigateResults(1); e.Handled = true; break;
            case Key.Enter: if (_isAltHeld) PasteSelected(); else CopySelected(); e.Handled = true; break;
            case Key.Escape: HideWindow(); e.Handled = true; break;
            case Key.Up: NavigateResults(-1); e.Handled = true; break;
        }
    }

    // ===== Window =====
    private void Window_MouseMove(object sender, MouseEventArgs e) { }
    private void Window_Deactivated(object? sender, EventArgs e) { HideWindow(); }
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    { if (e.ChangedButton == MouseButton.Left) this.DragMove(); }
    protected override void OnLocationChanged(EventArgs e)
    {
        base.OnLocationChanged(e);

        // Always recalculate so detail window opens at correct position later
        CalcDetailPosition(Left, Width, Width, Top);

        if (_detailWindow.IsVisible)
            _detailWindow.Reposition(this);
    }
    private void SettingsButton_Click(object sender, RoutedEventArgs e) => _openSettings();
    private void Window_KeyDown(object sender, KeyEventArgs e)
    { switch (e.Key) { case Key.LeftAlt: case Key.RightAlt: _isAltHeld = true; break; case Key.Escape: HideWindow(); break; } }
    private void Window_KeyUp(object sender, KeyEventArgs e)
    { switch (e.Key) { case Key.LeftAlt: case Key.RightAlt: _isAltHeld = false; break; } }

    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    { if (_isAltHeld) PasteSelected(); else CopySelected(); }

    private void ResultsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter: if (_isAltHeld) PasteSelected(); else CopySelected(); e.Handled = true; break;
            case Key.Delete: if (_isAltHeld) DeleteSelected(); e.Handled = true; break;
            case Key.P: if (_isAltHeld) { TogglePinSelected(); e.Handled = true; } break;
            case Key.Back: if (ResultsList.Items.Count > 0) { SearchBox.Focus(); e.Handled = true; } break;
            case Key.Up: NavigateResults(-1); e.Handled = true; break;
            case Key.Down: NavigateResults(1); e.Handled = true; break;
        }
    }

    // ===== Actions =====
    private void CopySelected()
    {
        if (ResultsList.SelectedItem is ClipboardItem item)
        { _detailWindow.HideDetail(); _database.TouchItem(item.Id); _clipboardService.CopyToClipboard(item.Text, item.ImageData); _isClosing = true; Hide(); _isClosing = false; }
    }
    private void PasteSelected()
    {
        if (ResultsList.SelectedItem is ClipboardItem item)
        { _detailWindow.HideDetail(); _database.TouchItem(item.Id); _isClosing = true; Hide(); _isClosing = false; _clipboardService.CopyToClipboard(item.Text, item.ImageData); System.Threading.Thread.Sleep(50); _clipboardService.SimulatePaste(); }
    }
    private void DeleteSelected()
    { if (ResultsList.SelectedItem is ClipboardItem item) { _database.DeleteItem(item.Id); RefreshList(); } }
    private void TogglePinSelected()
    { if (ResultsList.SelectedItem is ClipboardItem item) { _database.TogglePin(item.Id); RefreshList(); } }
    public void OnClipboardChanged()
    { Dispatcher.BeginInvoke(() => { if (IsVisible) RefreshList(); }); }

    private void HideWindow()
    { _detailWindow.HideDetail(); _isClosing = true; Hide(); _isClosing = false; LogService.Info("Hidden"); }

    private void RefreshList()
    {
        var search = SearchBox?.Text;
        var items = _database.GetHistory(string.IsNullOrEmpty(search) ? null : search);
        ResultsList.ItemsSource = items;
        if (ResultsList.Items.Count > 0 && ResultsList.SelectedIndex < 0)
        {
            ResultsList.SelectedIndex = 0;
            if (!_isFirstShow && ResultsList.SelectedItem is ClipboardItem first && ShouldShowDetail(first))
                StartDetailTimer(first);
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    { _detailWindow.Close(); _isClosing = true; base.OnClosing(e); }

    #region Native
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var ex = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, ex | WS_EX_TOOLWINDOW);
    }
    private const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hWnd, ref RECT lpRect);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOZORDER = 0x0004;
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    { public uint cbSize; public uint flags; public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public RECT rcCaret; }
    #endregion
}

#region Converters
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        (v is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) => throw new NotImplementedException();
}
public class AppIconConverter : IValueConverter
{
    private static readonly BitmapSource? _defaultIcon;
    static AppIconConverter()
    {
        var bmp = new WriteableBitmap(16, 16, 96, 96, PixelFormats.Pbgra32, null);
        var pixels = new byte[16 * 16 * 4];
        for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 0;
        bmp.WritePixels(new Int32Rect(0, 0, 16, 16), pixels, 16 * 4, 0);
        bmp.Freeze();
        _defaultIcon = bmp;
    }
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path)) { var icon = ExtractIcon(path); if (icon != null) return icon; }
        return _defaultIcon!;
    }
    private static BitmapSource? ExtractIcon(string filePath) { try { using var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath); if (icon == null) return null; using var bitmap = icon.ToBitmap(); var hBitmap = bitmap.GetHbitmap(); try { var w = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions()); w.Freeze(); return w; } finally { NativeMethods.DeleteObject(hBitmap); } } catch { return null; } }
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) => throw new NotImplementedException();
}
public class ImageToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}
public class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) => throw new NotImplementedException();
}
internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr hObject);
}
#endregion