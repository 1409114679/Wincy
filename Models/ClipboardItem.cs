using System.Windows.Media.Imaging;

namespace Wincy.Models;

/// <summary>
/// Represents a single clipboard history entry.
/// </summary>
public class ClipboardItem
{
    public long Id { get; set; }
    public string? Text { get; set; }
    public byte[]? ImageData { get; set; }
    public string? ContentType { get; set; }
    public bool IsPinned { get; set; }
    public DateTime CopiedAt { get; set; }
    public string? SourceApplication { get; set; }
    public string? SourceAppPath { get; set; }

    public bool HasImage => ImageData != null && ImageData.Length > 0;

    public string? Preview
    {
        get
        {
            if (HasImage)
                return null; // image items handled by thumbnail, no text preview

            if (string.IsNullOrEmpty(Text))
                return null;

            var trimmed = Text.Replace('\n', ' ').Replace('\r', ' ');
            return trimmed.Length > 100 ? trimmed[..100] + "…" : trimmed;
        }
    }

    /// <summary>
    /// Lazy-loaded thumbnail BitmapImage for list display (cached).
    /// </summary>
    private BitmapImage? _thumbnailImage;
    public BitmapImage? ThumbnailImage
    {
        get
        {
            if (_thumbnailImage != null) return _thumbnailImage;
            if (ImageData == null || ImageData.Length == 0) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new System.IO.MemoryStream(ImageData);
                bmp.DecodePixelHeight = 28; // small thumbnail height
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze(); // allow cross-thread access
                _thumbnailImage = bmp;
                return _thumbnailImage;
            }
            catch { return null; }
        }
        set => _thumbnailImage = value;
    }

    /// <summary>
    /// Full-resolution BitmapImage for detail tooltip (cached).
    /// </summary>
    private BitmapImage? _fullImage;
    public BitmapImage? FullImage
    {
        get
        {
            if (_fullImage != null) return _fullImage;
            if (ImageData == null || ImageData.Length == 0) return null;
            try
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.StreamSource = new System.IO.MemoryStream(ImageData);
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                _fullImage = bmp;
                return _fullImage;
            }
            catch { return null; }
        }
        set => _fullImage = value;
    }
}