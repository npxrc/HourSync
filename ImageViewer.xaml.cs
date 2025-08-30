#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

namespace HourSync;

public sealed partial class ImageViewer : Window
{
    public ImageViewer(string imageUrl)
    {
        InitializeComponent();

        var bitmapImage = new BitmapImage(new Uri(imageUrl));

        // Set the image source
        DisplayedImage.Source = bitmapImage;

        // Update the title with the image file name
        ImgTitle.Text = imageUrl.Split('/')[^1];

        Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base,
        };
        SystemBackdrop = micaBackdrop;

        DisplayedImage.ImageOpened += (s, e) =>
        {
            var bmp = (BitmapImage)DisplayedImage.Source;
            AppWindow.Resize(new Windows.Graphics.SizeInt32(bmp.PixelWidth, bmp.PixelHeight + 30));
            FileMgr.Log($"Width: {bmp.PixelWidth}, Height: {bmp.PixelHeight}");
        };
    }
}
