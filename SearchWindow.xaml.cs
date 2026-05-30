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
using Wincy.Models;
using Wincy.Services;

namespace Wincy;

public partial class SearchWindow : Window
{
    private readonly DatabaseService _database;
    private readonly ClipboardService _clipboardService;
    private readonly Action _openSettings;
    private bool _isClosing = false;
    private bool _isAltHeld = false;
    private HotkeySettings _hotkeySettings;

    // Hover detail: show full text or image in inline panel after 3s hover
    private DispatcherTimer? _hoverDetailTimer;
    private ClipboardItem? _hoveredDetailItem;

    public SearchWindow(DatabaseService database, ClipboardService clipboardService,
        HotkeySettings hotkeySettings, Action openSettings)
    {
        InitializeComponent();
        _database = database;
        _clipboardService = clipboardService;
        _hotkeySettings = hotkeySettings;
        _openSettings = openSettings;
        ResultsList.PreviewKeyDown += ResultsList_PreviewKeyDown;

        // Hover detail timer: 3 seconds
        _hoverDetailTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _hoverDetailTimer.Tick += HoverDetailTimer_Tick;

        UpdateFooterText();
        LogService.Info("SearchWindow created");
    }

    public void UpdateHotkeySettings(HotkeySettings settings)
    {
        _hotkeySettings = settings;
        UpdateFooterText();
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

    public void RefreshAndShow()
    {
        try
        {
            SearchBox.Text = "";
            RefreshList();
            Show();
            Activate();
            SearchBox.Focus();
            SearchBox.SelectAll();
        }
        catch (Exception ex) { LogService.Error("RefreshAndShow", ex); }
    }

    private void RefreshList()
    {
        var search = SearchBox?.Text;
        var items = _database.GetHistory(string.IsNullOrEmpty(search) ? null : search);
        ResultsList.ItemsSource = items;
        if (ResultsList.Items.Count > 0 && ResultsList.SelectedIndex < 0)
            ResultsList.SelectedIndex = 0;
    }

    // ===== Window Mouse/Deactivated =====
    private void Window_MouseMove(object sender, MouseEventArgs e)
    {
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        HideWindow();
    }

    // ===== Window Drag =====
    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
            this.DragMove();
    }

    // ===== Settings =====
    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        _openSettings();
    }

    // ===== Inline Detail Panel (Maccy-style, below the list) =====

    private void ResultsList_MouseMove(object sender, MouseEventArgs e)
    {
        var element = ResultsList.InputHitTest(e.GetPosition(ResultsList)) as DependencyObject;
        ClipboardItem? hoveredItem = null;

        while (element != null && element != ResultsList)
        {
            if (element is ListBoxItem lbi && lbi.DataContext is ClipboardItem ci)
            {
                hoveredItem = ci;
                break;
            }
            element = VisualTreeHelper.GetParent(element);
        }

        if (hoveredItem != _hoveredDetailItem)
        {
            HideDetailPanel();
            _hoveredDetailItem = hoveredItem;
            if (hoveredItem != null && ShouldShowDetail(hoveredItem))
            {
                _hoverDetailTimer?.Stop();
                _hoverDetailTimer?.Start();
            }
        }
    }

    private void ResultsList_MouseLeave(object sender, MouseEventArgs e)
    {
        _hoverDetailTimer?.Stop();
        _hoveredDetailItem = null;
        // Keep the detail panel visible — user might mouse into it
        // It will be hidden on next hover change or selection change
    }

    private void HoverDetailTimer_Tick(object? sender, EventArgs e)
    {
        _hoverDetailTimer?.Stop();
        if (_hoveredDetailItem == null) return;
        if (!ShouldShowDetail(_hoveredDetailItem)) return;
        ShowDetailPanel(_hoveredDetailItem);
    }

    /// <summary>
    /// Show detail for the currently selected item (keyboard navigation).
    /// Uses inline panel, never overlaps any part of the window.
    /// </summary>
    private void ShowDetailForSelection()
    {
        if (ResultsList.SelectedItem is ClipboardItem item && ShouldShowDetail(item))
        {
            ShowDetailPanel(item);
        }
        else
        {
            HideDetailPanel();
        }
    }

    private void ShowDetailPanel(ClipboardItem item)
    {
        if (item.HasImage && item.FullImage != null)
        {
            DetailText.Visibility = Visibility.Collapsed;
            DetailImage.Source = item.FullImage;
            DetailImage.MaxHeight = 160;
            DetailImage.Visibility = Visibility.Visible;
        }
        else
        {
            DetailImage.Visibility = Visibility.Collapsed;
            DetailText.Text = item.Text;
            DetailText.Visibility = Visibility.Visible;
        }
        DetailPanel.Visibility = Visibility.Visible;
    }

    private void HideDetailPanel()
    {
        DetailPanel.Visibility = Visibility.Collapsed;
        DetailImage.Source = null;
        DetailText.Text = null;
    }

    private static bool ShouldShowDetail(ClipboardItem item)
    {
        if (item.HasImage) return true;
        if (string.IsNullOrEmpty(item.Text)) return false;
        var text = item.Text;
        if (text.Contains('\n') || text.Contains('\r')) return true;
        if (text.Length > 80) return true;
        return false;
    }

    // ===== Search Box =====
    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => RefreshList();

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                NavigateResults(1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (_isAltHeld) PasteSelected(); else CopySelected();
                e.Handled = true;
                break;
            case Key.Escape:
                HideWindow();
                e.Handled = true;
                break;
            case Key.Up:
                NavigateResults(-1);
                e.Handled = true;
                break;
        }
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

        ShowDetailForSelection();
    }

    // ===== Window Keys =====
    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.LeftAlt: case Key.RightAlt: _isAltHeld = true; break;
            case Key.Escape: HideWindow(); break;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.LeftAlt: case Key.RightAlt: _isAltHeld = false; break;
        }
    }

    // ===== Results List =====
    private void ResultsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        ShowDetailForSelection();
    }

    private void ResultsList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (_isAltHeld) PasteSelected(); else CopySelected();
    }

    private void ResultsList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (_isAltHeld) PasteSelected(); else CopySelected();
                e.Handled = true;
                break;
            case Key.Delete:
                if (_isAltHeld) DeleteSelected();
                e.Handled = true;
                break;
            case Key.P:
                if (_isAltHeld) { TogglePinSelected(); e.Handled = true; }
                break;
            case Key.Back:
                if (ResultsList.Items.Count > 0) { SearchBox.Focus(); e.Handled = true; }
                break;
            case Key.Up:
                NavigateResults(-1);
                e.Handled = true;
                break;
            case Key.Down:
                NavigateResults(1);
                e.Handled = true;
                break;
        }
    }

    // ===== Actions =====
    private void CopySelected()
    {
        if (ResultsList.SelectedItem is ClipboardItem item)
        {
            _database.TouchItem(item.Id);
            _clipboardService.CopyToClipboard(item.Text, item.ImageData);
            _isClosing = true; Hide(); _isClosing = false;
        }
    }

    private void PasteSelected()
    {
        if (ResultsList.SelectedItem is ClipboardItem item)
        {
            _database.TouchItem(item.Id);
            _isClosing = true; Hide(); _isClosing = false;
            _clipboardService.CopyToClipboard(item.Text, item.ImageData);
            System.Threading.Thread.Sleep(50);
            _clipboardService.SimulatePaste();
        }
    }

    private void DeleteSelected()
    {
        if (ResultsList.SelectedItem is ClipboardItem item)
        {
            _database.DeleteItem(item.Id);
            RefreshList();
        }
    }

    private void TogglePinSelected()
    {
        if (ResultsList.SelectedItem is ClipboardItem item)
        {
            _database.TogglePin(item.Id);
            RefreshList();
        }
    }

    public void OnClipboardChanged()
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!IsVisible) return;
            RefreshList();
        });
    }

    private void HideWindow()
    {
        HideDetailPanel();
        _isClosing = true; Hide(); _isClosing = false;
        LogService.Info("Hidden");
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _isClosing = true; base.OnClosing(e);
    }

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
    #endregion
}

#region Converters
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        (v is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        throw new NotImplementedException();
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

    public object Convert(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture)
    {
        if (value is string path && !string.IsNullOrEmpty(path))
        {
            var icon = ExtractIcon(path);
            if (icon != null) return icon;
        }
        return _defaultIcon!;
    }

    private static BitmapSource? ExtractIcon(string filePath)
    {
        try
        {
            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(filePath);
            if (icon == null) return null;

            using var bitmap = icon.ToBitmap();
            var hBitmap = bitmap.GetHbitmap();
            try
            {
                var wpfBitmap = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                    hBitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                wpfBitmap.Freeze();
                return wpfBitmap;
            }
            finally
            {
                NativeMethods.DeleteObject(hBitmap);
            }
        }
        catch { return null; }
    }

    public object ConvertBack(object v, Type t, object p, System.Globalization.CultureInfo c) =>
        throw new NotImplementedException();
}

public class ImageToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture) =>
        (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture) =>
        throw new NotImplementedException();
}

public class TextToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture) =>
        (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter,
        System.Globalization.CultureInfo culture) =>
        throw new NotImplementedException();
}

internal static class NativeMethods
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    public static extern bool DeleteObject(IntPtr hObject);
}
#endregion