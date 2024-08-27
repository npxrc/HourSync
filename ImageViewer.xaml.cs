using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace HourSync
{
    public sealed partial class ImageViewer : Window
    {
        public ImageViewer(string imageUrl)
        {
            this.InitializeComponent();

            var bitmapImage = new BitmapImage(new Uri(imageUrl));

            // Set the image source
            DisplayedImage.Source = bitmapImage;

            // Update the title with the image file name
            Title.Text = imageUrl.Split('/')[^1];

            Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
            };
            this.SystemBackdrop = micaBackdrop;
            ExtendsContentIntoTitleBar = true;
        }
    }
}