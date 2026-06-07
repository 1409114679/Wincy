using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Wincy.Models;

namespace Wincy;    

public partial class DetailWindow : Window
{
    public DetailWindow()
    {
        InitializeComponent();
    }

    public void ShowDetail(ClipboardItem item, Window owner)
    {
        if (item.HasImage && item.FullImage != null)
        {
            DetailTextBlock.Visibility = Visibility.Collapsed;
            DetailImageView.Source = item.FullImage;
            DetailImageView.Visibility = Visibility.Visible;
        }
        else
        {
            DetailImageView.Visibility = Visibility.Collapsed;
            DetailTextBlock.Text = item.Text;
            DetailTextBlock.Visibility = Visibility.Visible;
        }

        Owner = owner;

        // Get pre-calculated position from SearchWindow
        var (detLeft, detTop, _, _) = SearchWindow.GetDetailPosition();

        Width = owner.Width; // same width as list window

        // Cap height: 50% of screen or fit content
        var dpi = VisualTreeHelper.GetDpi(this);
        double scale = dpi.DpiScaleX;
        double maxH = System.Windows.Forms.Screen.PrimaryScreen.WorkingArea.Height / scale * 0.50;
        MaxHeight = maxH;

        Left = detLeft;
        Top = detTop;
        Show();
    }

    public void HideDetail()
    {
        Hide();
        DetailImageView.Source = null;
        DetailTextBlock.Text = null;
    }

    public void Reposition(Window owner)
    {
        var (detLeft, detTop, _, _) = SearchWindow.GetDetailPosition();
        Left = detLeft;
        Top = detTop;
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
    }
}