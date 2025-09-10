using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace HourSync;
public static class AcrylicHelpers
{
    // In-app acrylic brush for control backgrounds
    public static AcrylicBrush CreateAcrylic(
        Windows.UI.Color? tint = null,
        double tintOpacity = 0.6,
        double luminosityOpacity = 0.0,
        bool forceFallback = false)
    {
        return new AcrylicBrush
        {
            TintColor = tint ?? Microsoft.UI.Colors.White,
            TintOpacity = tintOpacity,
            TintLuminosityOpacity = luminosityOpacity,
            AlwaysUseFallback = forceFallback
        };
    }

    // Window/background acrylic (Windows 11) 
    public static void EnableDesktopAcrylic(Window window)
    {
        if (window is null)
        {
            return;
        }

        window.SystemBackdrop = new Microsoft.UI.Xaml.Media.DesktopAcrylicBackdrop();
    }
}
