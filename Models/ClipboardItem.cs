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

    public string? Preview
    {
        get
        {
            if (ImageData != null)
                return "[Image]";

            if (string.IsNullOrEmpty(Text))
                return null;

            var trimmed = Text.Replace('\n', ' ').Replace('\r', ' ');
            return trimmed.Length > 100 ? trimmed[..100] + "…" : trimmed;
        }
    }
}