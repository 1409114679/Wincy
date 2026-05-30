using System.Runtime.InteropServices;
using System.Text;
using Wincy.Models;

namespace Wincy.Services;

/// <summary>
/// Monitors the Windows clipboard for changes and records new content.
/// </summary>
public class ClipboardService : IDisposable
{
    private readonly DatabaseService _database;
    private IntPtr _nextViewer;
    private IntPtr _hWnd;
    private bool _isMonitoring;
    private string? _lastText;

    public event EventHandler<ClipboardItem>? ClipboardChanged;

    public ClipboardService(DatabaseService database)
    {
        _database = database;
    }

    public void StartMonitoring(IntPtr hWnd)
    {
        if (_isMonitoring) return;
        _hWnd = hWnd;
        _nextViewer = SetClipboardViewer(hWnd);
        _isMonitoring = true;
    }

    public void StopMonitoring()
    {
        if (!_isMonitoring) return;
        ChangeClipboardChain(_hWnd, _nextViewer);
        _isMonitoring = false;
    }

    public void HandleClipboardUpdate()
    {
        try
        {
            if (!System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.Text) &&
                !System.Windows.Clipboard.ContainsImage())
                return;

            string? text = null;
            byte[]? imageData = null;
            string contentType = "text";

            if (System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.Text))
            {
                text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.Text);
            }
            else if (System.Windows.Clipboard.ContainsImage())
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image != null)
                {
                    using var ms = new System.IO.MemoryStream();
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                    encoder.Save(ms);
                    imageData = ms.ToArray();
                    contentType = "image/png";
                }
            }

            if (text == null && imageData == null)
                return;

            if (text == _lastText && imageData == null)
                return;
            _lastText = text;

            var sourceApp = GetForegroundWindowTitle();
            var item = new ClipboardItem
            {
                Text = text,
                ImageData = imageData,
                ContentType = contentType,
                CopiedAt = DateTime.Now,
                SourceApplication = sourceApp
            };

            _database.AddItem(item);
            ClipboardChanged?.Invoke(this, item);
        }
        catch
        {
            // Clipboard might be locked or empty
        }
    }

    public void CopyToClipboard(ClipboardItem item)
    {
        try
        {
            if (item.ImageData != null)
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                using var ms = new System.IO.MemoryStream(item.ImageData);
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                System.Windows.Clipboard.SetImage(bitmap);
            }
            else if (item.Text != null)
            {
                System.Windows.Clipboard.SetText(item.Text);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"CopyToClipboard error: {ex.Message}");
        }
    }

    public void PasteToForeground(ClipboardItem item)
    {
        CopyToClipboard(item);
        System.Threading.Thread.Sleep(50);
        SendPaste();
    }

    private static void SendPaste()
    {
        keybd_event((byte)System.Windows.Forms.Keys.ControlKey, 0, 0, 0);
        keybd_event((byte)'V', 0, 0, 0);
        keybd_event((byte)'V', 0, 2, 0);
        keybd_event((byte)System.Windows.Forms.Keys.ControlKey, 0, 2, 0);
    }

    private static string? GetForegroundWindowTitle()
    {
        var hWnd = GetForegroundWindow();
        if (hWnd == IntPtr.Zero) return null;
        var sb = new StringBuilder(256);
        GetWindowText(hWnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public void Dispose()
    {
        StopMonitoring();
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetClipboardViewer(IntPtr hWndNewViewer);

    [DllImport("user32.dll")]
    private static extern bool ChangeClipboardChain(IntPtr hWndRemove, IntPtr hWndNewNext);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
}