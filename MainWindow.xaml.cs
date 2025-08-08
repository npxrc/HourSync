#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible
// MainWindow.xaml.cs
using System.Net;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Windows.Foundation;
using Windows.UI.ViewManagement;

namespace HourSync;

public sealed partial class MainWindow : Window
{
    private RequestViewer _requestViewer;

    public MainWindow()
    {
        InitializeComponent();
        Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt,
        };
        SystemBackdrop = micaBackdrop;
        ExtendsContentIntoTitleBar = true;
        Title = "HourSync";

        Closed += Closing;
    }

    private void Closing(object sender, WindowEventArgs args)
    {
        _requestViewer?.Close();
        ((App)Application.Current).client.Dispose();
    }

    public void OpenRequestViewer(
        string idOfItem,
        string phpSessionId,
        string eventName,
        CookieContainer cookieContainer,
        HttpClientHandler handler,
        HttpClient client,
        string nameOfAcademy,
        string status,
        string username,
        string password
    )
    {
        _requestViewer = new RequestViewer(
            idOfItem,
            phpSessionId,
            eventName,
            cookieContainer,
            handler,
            client,
            nameOfAcademy,
            status,
            username,
            password
        );
        _requestViewer.Activate();
    }
}
