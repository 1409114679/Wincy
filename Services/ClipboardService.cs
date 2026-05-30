using System.Runtime.InteropServices;

namespace Wincy.Services;

/// <summary>
/// Monitors the Windows clipboard for changes using Clipboard Format Listener.
/// </summary>
public class ClipboardService : IDisposable
{
    private IntPtr _hWnd;
    private bool _isListening;

    public void StartListening(IntPtr hWnd)
    {
        _hWnd = hWnd;
        _isListening = AddClipboardFormatListener(hWnd);
    }

    public bool IsListening => _isListening;

    public (string? text, byte[]? imageData, string contentType) GetClipboardContent()
    {
        try
        {
            if (System.Windows.Clipboard.ContainsText(System.Windows.TextDataFormat.Text))
            {
                var text = System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.Text);
                if (!string.IsNullOrEmpty(text))
                    return (text, null, "text/plain");
            }

            if (System.Windows.Clipboard.ContainsImage())
            {
                var image = System.Windows.Clipboard.GetImage();
                if (image != null)
                {
                    using var ms = new System.IO.MemoryStream();
                    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                    encoder.Save(ms);
                    return (null, ms.ToArray(), "image/png");
                }
            }
        }
        catch
        {
            // Clipboard busy or empty
        }

        return (null, null, "");
    }

    public void CopyToClipboard(string? text, byte[]? imageData)
    {
        try
        {
            if (imageData != null)
            {
                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                using var ms = new System.IO.MemoryStream(imageData);
                bitmap.BeginInit();
                bitmap.StreamSource = ms;
                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                System.Windows.Clipboard.SetImage(bitmap);
            }
            else if (text != null)
            {
                System.Windows.Clipboard.SetText(text);
            }
        }
        catch { }
    }

    public void SimulatePaste()
    {
        keybd_event(0x11, 0, 0, 0); // Ctrl down
        keybd_event(0x56, 0, 0, 0); // V down
        keybd_event(0x56, 0, 2, 0); // V up
        keybd_event(0x11, 0, 2, 0); // Ctrl up
    }

    public void Dispose()
    {
        if (_isListening && _hWnd != IntPtr.Zero)
            RemoveClipboardFormatListener(_hWnd);
    }

    [DllImport("user32.dll")]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
}