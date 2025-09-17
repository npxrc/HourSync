using System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Web.WebView2.Core;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HourSync;
/// <summary>
/// An empty page that can be used on its own or navigated to within a Frame.
/// </summary>
public sealed partial class BrowserView : Page
{
    private string _phpSessionId;
    public event EventHandler GoBack;

    public BrowserView()
    {
        InitializeComponent();

        BackButton.Click += (_, __) => { if (Browser.CanGoBack) Browser.GoBack(); };
        ForwardButton.Click += (_, __) => { if (Browser.CanGoForward) Browser.GoForward(); };
        ReloadButton.Click += (_, __) => Browser.Reload();
    }

    protected async override void OnNavigatedTo(NavigationEventArgs e)
    {

        if (e.Parameter.GetType() == typeof(string))
        {
            _phpSessionId = (string)e.Parameter;
        }
        else
        {
            await new ContentDialog()
            {
                Title = "Error",
                Content = "The token was not set. Please log in again.",
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
        }

        base.OnNavigatedTo(e);

        await Browser.EnsureCoreWebView2Async();
        Browser.CoreWebView2.NavigationCompleted += Browser_NavigationCompleted;

        // Set PHPSESSID cookie
        var cookie = Browser.CoreWebView2.CookieManager.CreateCookie(
            "PHPSESSID",
            _phpSessionId,
            ".olatheschools.com", // TODO: replace with your domain
            "/"
        );
        // Convert DateTimeOffset to seconds since Unix epoch
        var expires = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        cookie.Expires = expires;

        Browser.CoreWebView2.CookieManager.AddOrUpdateCookie(cookie);

        // Navigate to your start page
        Browser.Source = new Uri("https://academyendorsement.olatheschools.com/Student/studentEHours.php");
        //Browser.Source = new Uri("https://google.com");
    }
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);

        if (Browser.CoreWebView2 != null)
        {
            Browser.CoreWebView2.NavigationCompleted -= Browser_NavigationCompleted;
        }

        Browser?.Close();
    }

    private void Browser_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        UrlBox.Text = Browser.Source.ToString();
    }

    private void BackToRequest(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
    {
        GoBack?.Invoke(this, null);
    }
}
