//great in theory but does not work lmaoo
#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
using System;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using CommunityToolkit.WinUI;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Controls;

namespace HourSync;
public class NetworkMonitor
{
    private MainWindow _mainWindow;
    private bool _isInternetAvailable = true;
    private ContentDialog _networkStatusDialog;

    public NetworkMonitor(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;

        // Start periodic checks
        //_ = StartPeriodicChecksAsync();
    }

    private async Task StartPeriodicChecksAsync()
    {
        while (true)
        {
            await CheckInternetConnectivityAsync();
            await Task.Delay(5000); // Wait 5 seconds before next check
        }
    }

    private async Task CheckInternetConnectivityAsync()
    {
        var isCurrentlyAvailable = await IsInternetAvailableAsync();

        if (isCurrentlyAvailable != _isInternetAvailable)
        {
            _isInternetAvailable = isCurrentlyAvailable;
            await UpdateNetworkStatusUIAsync(!_isInternetAvailable);
        }
    }

    private static async Task<bool> IsInternetAvailableAsync()
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private async Task UpdateNetworkStatusUIAsync(bool showDialog)
    {
        if (showDialog)
        {
            _networkStatusDialog = new ContentDialog
            {
                Title = "Network Disconnected",
                Content = "Your internet connection has been lost. Please reconnect to use online features.",
                PrimaryButtonText = "Ok",
                XamlRoot = _mainWindow.Content.XamlRoot
            };
            await _networkStatusDialog.ShowAsync();
        }
        else
        {
            _networkStatusDialog.Hide();
        }
    }
}