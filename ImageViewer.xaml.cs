using System;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.System;
using Windows.UI.Core;

namespace HourSync;

public sealed partial class ImageViewer : Window
{
    private readonly string _imageUrl;

    public ImageViewer(string imageUrl)
    {
        try
        {
            InitializeComponent();
            BottomBar.Background = AcrylicHelpers.CreateAcrylic(tint: Microsoft.UI.Colors.Black, tintOpacity: 0.4);
            // Extend content into title bar
            ExtendsContentIntoTitleBar = true;
            Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt,
            };
            SystemBackdrop = micaBackdrop;
            SetTitleBar(AppTitleBar);

            LoadImage(imageUrl);

            ImageScrollViewer.PointerWheelChanged += (sender, e) =>
            {
                var scrollViewer = (ScrollViewer)sender;
                var pointerPoint = e.GetCurrentPoint(scrollViewer);

                // Check if the Shift key is down
                if ((!pointerPoint.Properties.IsLeftButtonPressed && !pointerPoint.Properties.IsRightButtonPressed) &&
                    (InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift) & CoreVirtualKeyStates.Down) == CoreVirtualKeyStates.Down)
                {
                    // Scroll horizontally
                    double delta = pointerPoint.Properties.MouseWheelDelta;
                    scrollViewer.ChangeView(scrollViewer.HorizontalOffset - delta, null, null);
                    e.Handled = true;
                }
            };
        }
        catch (Exception ex)
        {
            FileMgr.Log("Error when setting up Image Viewer: " + ex.Message);
            Close();
        }
    }

    private async void LoadImage(string imageUrl)
    {
        FileMgr.Log(imageUrl);
        try
        {
            var bitmapImage = new BitmapImage(new Uri(imageUrl));

            // Wait for image to load to get actual dimensions
            bitmapImage.ImageOpened += (s, e) =>
            {
                // Auto-fit the image when it first loads
                FitImageToWindow();
            };

            bitmapImage.ImageFailed += (s, e) =>
            {
                // Handle image loading failure
            };

            DisplayedImage.Source = bitmapImage;
        }
        catch (Exception ex)
        {
            await new DialogService().ShowDialog("Error", $"Unable to load image: {ex.Message}", "OK", "", "", Content.XamlRoot);
        }
    }

    private void FitButton_Click(object _, RoutedEventArgs __)
    {
        if (DisplayedImage.Source is BitmapImage bitmap)
        {
            // Wait for image to load if needed
            if (bitmap.PixelWidth > 0 && bitmap.PixelHeight > 0)
            {
                FitImageToWindow();
            }
            else
            {
                bitmap.ImageOpened += (s, e) => FitImageToWindow();
            }
        }
    }

    private void FitImageToWindow()
    {
        DisplayedImage.Stretch = Microsoft.UI.Xaml.Media.Stretch.Uniform;

        // Reset zoom and scroll position
        ImageScrollViewer.ChangeView(0, 0, 1.0f);

        // Center the image
        DispatcherQueue.TryEnqueue(() =>
        {
            ImageScrollViewer.ScrollToHorizontalOffset(0);
            ImageScrollViewer.ScrollToVerticalOffset(0);
        });
    }

    private void ActualSizeButton_Click(object _, RoutedEventArgs __)
    {
        DisplayedImage.Stretch = Microsoft.UI.Xaml.Media.Stretch.None;

        // Center the image at actual size
        DispatcherQueue.TryEnqueue(() =>
        {
            if (DisplayedImage.ActualWidth > ImageScrollViewer.ViewportWidth ||
                DisplayedImage.ActualHeight > ImageScrollViewer.ViewportHeight)
            {
                // Center the image
                var horizontalOffset = (DisplayedImage.ActualWidth - ImageScrollViewer.ViewportWidth) / 2;
                var verticalOffset = (DisplayedImage.ActualHeight - ImageScrollViewer.ViewportHeight) / 2;
                ImageScrollViewer.ChangeView(horizontalOffset, verticalOffset, 1.0f);
            }
            else
            {
                ImageScrollViewer.ChangeView(0, 0, 1.0f);
            }
        });
    }

    private void ZoomInButton_Click(object _, RoutedEventArgs __)
    {
        var newZoom = Math.Min(ImageScrollViewer.ZoomFactor + 0.25f, 5.0f);
        ImageScrollViewer.ChangeView(null, null, newZoom);
    }

    private void ZoomOutButton_Click(object _, RoutedEventArgs __)
    {
        var newZoom = Math.Max(ImageScrollViewer.ZoomFactor - 0.25f, 0.25f);
        ImageScrollViewer.ChangeView(null, null, newZoom);
    }

    private void Window_Activated(object _, Microsoft.UI.Xaml.WindowActivatedEventArgs __)
    {
        // Could update titlebar colors or focus styles here if wanted
    }
}
