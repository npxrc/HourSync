#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE1006 // this aint an english class i'm not capitalising anything
#pragma warning disable 0649 // it is actually assigned to!
#pragma warning disable 0169 // it is actually assigned to!
using System;
using System.Net;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using DiscordRPC;
using Windows.Graphics.Display;
using System.Runtime.CompilerServices;
using System.Drawing;

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
    private string currentPage = "home";
    private string previousPage = "null";
    
    public DiscordRpcClient client = new("1342974846090481766");
    private DiscordRPC.Button[] buttons = new[]{
        new DiscordRPC.Button()
        {
            Label = "Download HourSync",
            Url = "https://hoursync.net"
        },
        new DiscordRPC.Button()
        {
            Label = "View on GitHub",
            Url = "https://github.com/npxrc/HourSync"
        }
    };

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
        TransitionCollection transitionCollection =
        [
            // Add a NavigationThemeTransition to the TransitionCollection
            new NavigationThemeTransition()
        ];

        // Set the ContentTransitions property of the rootFrame to the created TransitionCollection
        rootFrame.ContentTransitions = transitionCollection;

        m_window.Content = NavigationView;
        m_window.Activate();

        client.Initialize();

        // Pass the isFirstTime parameter as true
        rootFrame.Navigate(typeof(Login), true);
    }

    private void NavigationView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.InvokedItemContainer is NavigationViewItem item)
        {
            SlideNavigationTransitionInfo effect;
            switch (item.Tag.ToString())
            {
                case "home":
                    previousPage = currentPage;
                    if (currentPage == "home") break;
                    currentPage = "home";
                    rootFrame.Navigate(typeof(Home), new object[] { Username, Password, PhpSessionId, NameOfPerson, NameOfAcademy, GetRespOnLogin, CookieContainer, Handler, Client }, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft});
                    break;
                case "create":
                    previousPage = currentPage;
                    if (previousPage == "home")
                    {
                        effect = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
                    }
                    else if (previousPage == "settings" || previousPage == "leaderboard") // pages to the right
                    {
                        effect = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft };
                    }
                    else
                    {
                        effect = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromBottom };
                    }
                    if (currentPage == "create") break;
                    currentPage = "create";
                    rootFrame.Navigate(typeof(RequestMaker), new object[] { Username, Password, PhpSessionId, NameOfPerson, NameOfAcademy, GetRespOnLogin, CookieContainer, Handler, Client }, effect);
                    break;
                case "settings":
                    previousPage = currentPage;
                    if (previousPage == "create" || previousPage == "home") // pages to the left
                    {
                        effect = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight };
                    }
                    else if (previousPage == "leaderboard")
                    {
                        effect = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromLeft };
                    }
                    else
                    {
                        effect = new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromBottom };
                    }
                    if (currentPage == "settings") break;
                    currentPage = "settings";
                    rootFrame.Navigate(typeof(Settings), new object[] { Username, Password, PhpSessionId, NameOfPerson, NameOfAcademy, GetRespOnLogin, CookieContainer, Handler, Client }, effect);
                    break;
                case "leaderboard":
                    previousPage = currentPage;
                    if (currentPage == "leaderboard") break;
                    currentPage = "leaderboard";
                    rootFrame.Navigate(typeof(Leaderboard), new object[] { Username, Password, PhpSessionId, NameOfPerson, NameOfAcademy, GetRespOnLogin, CookieContainer, Handler, Client }, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });
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
        previousPage = currentPage;
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
        rootFrame.Navigate(typeof(Login), false);
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

    public void UpdatePresence(string page, string details)
    {
        string state = "";

        switch (page)
        {
            case "login":
                state = "Logging in";
                break;
            case "home":
                state = "Viewing Homepage";
                break;
            case "create":
                state = "Submitting eHours";
                break;
            case "settings":
                state = "Changing Settings";
                break;
        }
        if (details.Length > 0)
        {
            FileMgr.Log($"setting presence");
            client.SetPresence(new RichPresence()
            {
                Details = details,
                State = state,
                Assets = new Assets()
                {
                    LargeImageKey = "logo",
                    LargeImageText = "HourSync",
                },
                Buttons = buttons,
                Type = ActivityType.Playing
            });
            System.Diagnostics.Trace.WriteLine($"Set presence to {state} with details \"{details}\"");
        }
        else
        {
            client.SetPresence(new RichPresence()
            {
                State = state,
                Assets = new Assets()
                {
                    LargeImageKey = "logo",
                    LargeImageText = "HourSync",
                },
                Buttons = buttons,
                Type = ActivityType.Playing
            });
            System.Diagnostics.Trace.WriteLine($"Set presence to default");
        }
    }
}