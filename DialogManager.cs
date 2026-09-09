#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1822 // This can NOT be marked static or everything is thrown off
#pragma warning disable CA2211 // IsDialogOpen is used in Login.xaml.cs
using System;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HourSync;
public static class DialogManager
{
    public static bool IsDialogOpen = false;
}

public class DialogService
{
    public async Task<ContentDialogResult> ShowDialog(string title, object content, string closeButtonText = "", string primaryButtonText = "", string secondaryButtonText = "", XamlRoot root = null)
    {
        if (DialogManager.IsDialogOpen)
        {
            return ContentDialogResult.None;
        }

        DialogManager.IsDialogOpen = true;

        title ??= "";
        content ??= "";
        primaryButtonText ??= "";
        secondaryButtonText ??= "";
        closeButtonText ??= "";

        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = closeButtonText.Length > 0 ? closeButtonText : null,
            PrimaryButtonText = primaryButtonText.Length > 0 ? primaryButtonText : null,
            SecondaryButtonText = secondaryButtonText.Length > 0 ? secondaryButtonText : null,
            XamlRoot = root ?? ((App)Application.Current).m_window.Content.XamlRoot
        };

        dialog.Closed += (_, __) => DialogManager.IsDialogOpen = false;

        return await dialog.ShowAsync();
    }

    public async Task<bool> ShowErrorDialog(string content, bool shouldAppExit, XamlRoot xamlRoot)
    {
        FileMgr.LogError(content);
        var title = LocalizationService.GetString("GenericErrorTitle");
        if (shouldAppExit)
        {
            var applicationWillClose = LocalizationService.GetString("HourSyncWillClose");
            var closeText = LocalizationService.GetString("GenericCloseText");
            if (content.EndsWith('.') || content.Trim().EndsWith('.')) // Checks if the sentence ends with a period
            {
                await ShowDialog(title, content.Trim() + " " + applicationWillClose + ".", closeText, null, null, xamlRoot);
            }
            else
            {
                await ShowDialog(title, content.Trim() + ". " + applicationWillClose + ".", closeText, null, null, xamlRoot);
            }
            Application.Current.Exit();
            return false; // not sure why this is needed if the app is closing
        }
        else
        {
            var okay = LocalizationService.GetString("GenericOKText");
            await ShowDialog(title, content.Trim(), okay, null, null, xamlRoot);
            return true;
        }
    }
}