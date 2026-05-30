using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Wincy.Models;
using Wincy.Services;

namespace Wincy;

public partial class SearchWindow : Window
{
    private readonly DatabaseService _database;
    private readonly ClipboardService _clipboardService;
    private bool _isClosing = false;
    private bool _isCtrlHeld = false;
    private bool _isAltHeld = false;

    public SearchWindow(DatabaseService database, ClipboardService clipboardService)
    {
        InitializeComponent();
        _database = database;
        _clipboardService = clipboardService;

        Resources["BoolToVisibilityConverter"] = new BoolToVisibilityConverter();
        Resources["IndexToShortcutConverter"] = new IndexToShortcutConverter();

        Loaded += (s, e) =>
        {
            RefreshList();
            SearchBox.Focus();
            SearchBox.SelectAll();
        };

        // Attach key down for results list
        ResultsList.PreviewKeyDown += ResultsList_PreviewKeyDown;
    }

    public void RefreshAndShow()
    {
        SearchBox.Text = "";
        RefreshList();
        Show();
        Activate();
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void RefreshList()
    {
        var search = SearchBox?.Text;
        var items = _database.GetHistory(string.IsNullOrEmpty(search) ? null : search);
        ResultsList.ItemsSource = items;

        if (ResultsList.Items.Count > 0 && ResultsList.SelectedIndex < 0)
            ResultsList.SelectedIndex = 0;
    }

    private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        RefreshList();
    }

    private void SearchBox_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (ResultsList.Items.Count > 0)
                {
                    ResultsList.Focus();
                    if (ResultsList.SelectedIndex < 0)
                        ResultsList.SelectedIndex = 0;
                }
                e.Handled = true;
                break;

            case Key.Enter:
                if (_isAltHeld)
                    PasteSelected();
                else
                    CopySelected();
                e.Handled = true;
                break;

            case Key.Escape:
                HideWindow();
                e.Handled = true;
                break;

            case Key.Up:
                if (ResultsList.Items.Count > 0)
                {
                    ResultsList.Focus();
                    ResultsList.SelectedIndex = Math.Max(0, ResultsList.SelectedIndex);
                }
                e.Handled = true;
                break;
        }
    }

    private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.LeftAlt:
            case Key.RightAlt:
                _isAltHeld = true;
                break;
            case Key.LeftCtrl:
            case Key.RightCtrl:
                _isCtrlHeld = true;
                break;
            case Key.LeftShift:
            case Key.RightShift:
                break;
            case Key.Escape:
                HideWindow();
                break;
        }

        // Number shortcuts
        if (_isCtrlHeld)
        {
            int index = e.Key switch
            {
                Key.D1 => 0, Key.D2 => 1, Key.D3 => 2, Key.D4 => 3, Key.D5 => 4,
                Key.D6 => 5, Key.D7 => 6, Key.D8 => 7, Key.D9 => 8, Key.D0 => 9,
                _ => -1
            };

            if (index >= 0 && index < ResultsList.Items.Count)
            {
                ResultsList.SelectedIndex = index;
                if (_isAltHeld)
                    PasteSelected();
                else
                    CopySelected();
                e.Handled = true;
            }
        }
    }

    private void Window_KeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.LeftAlt:
            case Key.RightAlt:
                _isAltHeld = false;
                break;
            case Key.LeftCtrl:
            case Key.RightCtrl:
                _isCtrlHeld = false;
                break;
            case Key.LeftShift:
            case Key.RightShift:
                break;
        }
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        if (!_isClosing)
            HideWindow();
    }

    private void ResultsList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
    }

    private void ResultsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isAltHeld)
            PasteSelected();
        else
            CopySelected();
    }

    private void ResultsList_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Enter:
                if (_isAltHeld)
                    PasteSelected();
                else
                    CopySelected();
                e.Handled = true;
                break;

            case Key.Delete:
                if (_isAltHeld)
                    DeleteSelected();
                e.Handled = true;
                break;

            case Key.P:
                if (_isAltHeld)
                {
                    TogglePinSelected();
                    e.Handled = true;
                }
                break;

            case Key.Back:
                if (ResultsList.Items.Count > 0)
                {
                    SearchBox.Focus();
                    e.Handled = true;
                }
                break;

            case Key.Up:
                if (ResultsList.SelectedIndex == 0)
                {
                    SearchBox.Focus();
                    e.Handled = true;
                }
                break;
        }
    }

    private void CopySelected()
    {
        if (ResultsList.SelectedItem is ClipboardItem item)
        {
            _clipboardService.CopyToClipboard(item);
            _isClosing = true;
            Hide();
            _isClosing = false;
        }
    }

    private void PasteSelected()
    {
        if (ResultsList.SelectedItem is ClipboardItem item)
        {
            _isClosing = true;
            Hide();
            _isClosing = false;
            _clipboardService.PasteToForeground(item);
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

    private void HideWindow()
    {
        _isClosing = true;
        Hide();
        _isClosing = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        _isClosing = true;
        base.OnClosing(e);
    }

    #region Native
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        var exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_TOOLWINDOW);
    }

    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    #endregion
}

#region Converters
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class IndexToShortcutConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is ClipboardItem item)
        {
            var listBox = GetListBoxFromItem(item);
            if (listBox != null)
            {
                var index = listBox.Items.IndexOf(item);
                if (index >= 0 && index < 10)
                    return $"Ctrl+{(index + 1) % 10}";
            }
        }
        return "";
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static System.Windows.Controls.ListBox? GetListBoxFromItem(ClipboardItem item)
    {
        var window = System.Windows.Application.Current.Windows.OfType<SearchWindow>().FirstOrDefault();
        return window?.ResultsList;
    }
}
#endregion