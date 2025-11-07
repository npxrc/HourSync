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
    // Static boolean flag to track if any dialog is open
    public static bool IsDialogOpen = false;
}

public class DialogService
{
    // The method to show the dialog with your parameters
    public async Task<ContentDialogResult> ShowDialog(string title, object content, string closeButtonText = "", string primaryButtonText = "", string secondaryButtonText = "", XamlRoot root = null)
    {
        // Check if a dialog is already open
        if (DialogManager.IsDialogOpen)
        {
            return ContentDialogResult.None; // Or handle as per your needs (maybe return null or an error)
        }

        // Set the flag to indicate a dialog is being shown
        DialogManager.IsDialogOpen = true;

        // Create the ContentDialog with provided values
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = closeButtonText.Length > 0 ? closeButtonText : null,
            PrimaryButtonText = primaryButtonText.Length > 0 ? primaryButtonText : null,
            SecondaryButtonText = secondaryButtonText.Length > 0 ? secondaryButtonText : null,
            // Use the passed XamlRoot, or fallback to the default if none is provided
            XamlRoot = root ?? ((App)Application.Current).m_window.Content.XamlRoot
        };

        // Subscribe to the Closed event to reset the dialog flag
        dialog.Closed += (_, __) => DialogManager.IsDialogOpen = false;

        // Show the dialog and return the result
        return await dialog.ShowAsync();
    }

    public async Task<bool> ShowErrorDialog(string content, bool shouldAppExit, XamlRoot xamlRoot)
    {
        FileMgr.LogError(content);
        if (shouldAppExit)
        {
            if (content.EndsWith('.') || content.Trim().EndsWith('.')) // Checks if the sentence ends with a period
            {
                await ShowDialog("Error", content.Trim() + " The application will now close.", "Close", null, null, xamlRoot);
            }
            else
            {
                await ShowDialog("Error", content.Trim() + ". The application will now close.", "Close", null, null, xamlRoot);
            }
            Application.Current.Exit();
            return false; // not sure why this is needed if the app is closing
        }
        else
        {
            await ShowDialog("Error", content.Trim(), "Okay", null, null, xamlRoot);
            return true;
        }
    }
}