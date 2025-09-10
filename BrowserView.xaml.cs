using System;
using Microsoft.UI.Xaml;
using Microsoft.Web.WebView2.Core;

namespace HourSync;

public sealed partial class BrowserView : Window
{
    private readonly string _phpSessionId;

    public BrowserView(string phpSessionId = "")
    {
        InitializeComponent();
        _phpSessionId = phpSessionId;
        if (string.IsNullOrWhiteSpace(_phpSessionId) || string.IsNullOrEmpty(_phpSessionId))
        {
            throw new ArgumentException();
        }

        AppWindow.SetIcon("Assets/logo.ico");
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(null); // since ExtendsContentIntoTitleBar is true

        BackButton.Click += (_, __) => { if (Browser.CanGoBack) Browser.GoBack(); };
        ForwardButton.Click += (_, __) => { if (Browser.CanGoForward) Browser.GoForward(); };
        ReloadButton.Click += (_, __) => Browser.Reload();

        Activated += BrowserView_Activated;
        Closed += BrowserView_Closed;
    }

    private async void BrowserView_Activated(object _, WindowActivatedEventArgs __)
    {
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

    private void Browser_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        UrlBox.Text = Browser.Source.ToString();
    }

    private void BrowserView_Closed(object sender, WindowEventArgs args)
    {
        if (Browser.CoreWebView2 != null)
        {
            Browser.CoreWebView2.NavigationCompleted -= Browser_NavigationCompleted;
            Activated -= BrowserView_Activated;
        }

        Browser?.Close();
    }
}
