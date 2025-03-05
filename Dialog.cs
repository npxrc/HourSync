using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HourSync;
public static class Dialog
{
    private static async Task<bool> ShowDialog(string title, string content, string primaryButtonText, string secondaryButtonText, XamlRoot xamlroot)
    {
        try
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = secondaryButtonText,
                PrimaryButtonText = primaryButtonText,
                XamlRoot = xamlroot
            };
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }
        catch (Exception ex)
        {
            FileMgr.LogError(ex.Message);
            return false;
        }
    }
    public static async Task<bool> OkayDialog(string title, string content, XamlRoot xamlroot)
    {
        return (await ShowDialog(title, content, "Okay", null, xamlroot));
    }
    public static async Task<bool> YesNoDialog(string title, string content, XamlRoot xamlroot)
    {
        return (await ShowDialog(title, content, "Yes", "No", xamlroot));
    }
    public static async Task<bool> ContinueCancelDialog(string title, string content, XamlRoot xamlroot)
    {
        return (await ShowDialog(title, content, "Continue", "Cancel", xamlroot));
    }
}