#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE1006 // this aint a fucking english class i'm not capitalising shit
using System;
using System.Net;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;

namespace HourSync;
public partial class App : Application
{
    private Frame rootFrame;
    public NavigationView NavigationView;
    private NavigationViewItem loginTag;
    private NavigationViewItem homeTag;
    private NavigationViewItem createSubmission
    {
        get; set;
    }
    public NavigationViewModel NavigationViewModel
    {
        get; private set;
    }
    public Window m_window;

    // Properties to hold parameters
    public string Username
    {
        get; set;
    }
    public string Password
    {
        get; set;
    }
    public string PhpSessionId
    {
        get; set;
    }
    public string NameOfPerson
    {
        get; set;
    }
    public string NameOfAcademy
    {
        get; set;
    }
    public string GetRespOnLogin
    {
        get; set;
    }
    public CookieContainer CookieContainer
    {
        get; set;
    }
    public HttpClientHandler Handler
    {
        get; set;
    }
    public HttpClient Client
    {
        get; set;
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        m_window = new MainWindow();

        rootFrame = new Frame();
        rootFrame.NavigationFailed += OnNavigationFailed;

        NavigationViewModel = new NavigationViewModel();

        NavigationView = new NavigationView
        {
            MenuItemsSource = NavigationViewModel.MenuItems,
            SelectedItem = NavigationViewModel.SelectedItem,
            IsSettingsVisible = false,
            Content = rootFrame
        };

        NavigationView.ItemInvoked += NavigationView_ItemInvoked;

        // Create a new TransitionCollection
        TransitionCollection transitionCollection = new()
        {
            // Add a NavigationThemeTransition to the TransitionCollection
            new NavigationThemeTransition()
        };

        // Set the ContentTransitions property of the rootFrame to the created TransitionCollection
        rootFrame.ContentTransitions = transitionCollection;

        rootFrame.Navigate(typeof(Login));

        m_window.Content = NavigationView;
        m_window.Activate();
    }

    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item)
        {
            switch (item.Tag.ToString())
            {
                case "home":
                    rootFrame.Navigate(typeof(Home), new object[] { Username, Password, PhpSessionId, NameOfPerson, NameOfAcademy, GetRespOnLogin, CookieContainer, Handler, Client }, new DrillInNavigationTransitionInfo());
                    break;
                case "create":
                    rootFrame.Navigate(typeof(RequestMaker), new object[] { Username, Password, PhpSessionId, NameOfPerson, NameOfAcademy, GetRespOnLogin, CookieContainer, Handler, Client }, new DrillInNavigationTransitionInfo());
                    break;
            }
        }
    }

    public void LoggedIn(string username, string password, string phpSessionId, string nameOfPerson, string nameOfAcademy, string getresp, CookieContainer cookieContainer, HttpClientHandler handler, HttpClient client)
    {
        Username = username;
        Password = password;
        PhpSessionId = phpSessionId;
        NameOfPerson = nameOfPerson;
        NameOfAcademy = nameOfAcademy;
        GetRespOnLogin = getresp;
        CookieContainer = cookieContainer;
        Handler = handler;
        Client = client;

        NavigationViewModel.RefreshMenuItems(isLoggedIn: true);
        rootFrame.Navigate(typeof(Home), new object[] { Username, Password, PhpSessionId, NameOfPerson, NameOfAcademy, GetRespOnLogin, CookieContainer, Handler, Client }, new DrillInNavigationTransitionInfo());
    }

    public void LoggedOut()
    {
        // Clear user-specific data
        Username = null;
        Password = null;
        PhpSessionId = null;
        NameOfPerson = null;
        NameOfAcademy = null;
        GetRespOnLogin = null;
        CookieContainer = null;
        Handler = null;
        Client = null;

        NavigationViewModel.RefreshMenuItems(isLoggedIn: false);
        rootFrame.Navigate(typeof(Login));
    }

    public void BackClicked()
    {
        if (rootFrame.CanGoBack)
        {
            rootFrame.GoBack();
            NavigationView.SelectedItem = homeTag;
        }
    }

    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        Console.WriteLine($"Failed to load Page {e.SourcePageType.FullName}");
        Console.WriteLine($"Error: {e.Exception}");
    }

    public void GoToHomeAfterDel(string getresp)
    {
        GetRespOnLogin = getresp;
        rootFrame.Navigate(typeof(Home), new object[] { Username, Password, PhpSessionId, NameOfPerson, NameOfAcademy, getresp, CookieContainer, Handler, Client }, new DrillInNavigationTransitionInfo());
    }

    public void UpdateHomeContent(string getresp)
    {
        GetRespOnLogin = getresp;
    }
}