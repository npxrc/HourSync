#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE1006 // this aint an english class i'm not capitalising anything
#pragma warning disable 0649 // it is actually assigned to!
#pragma warning disable 0169 // it is actually assigned to!
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using DiscordRPC;
using HourSyncCoreLib;
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

    public Home homePage = null;

    private NavigationViewItem createSubmission
    {
        get; set;
    }
    public NavigationViewModel NavigationViewModel
    {
        get; private set;
    }
    public MainWindow m_window;

    // Properties to hold parameters
    public LoginResult LoginResult
    {
        get; set;
    }
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
    public string HomeResult
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
    private string currentPageForNav = "home";
    private string previousPageForNav = "null";

    public DiscordRpcClient client = new("1342974846090481766");
    private DiscordRPC.Button[] buttons =
    [
        new DiscordRPC.Button() { Label = "Download HourSync", Url = "https://hoursync.net" },
        new DiscordRPC.Button()
        {
            Label = "View on GitHub",
            Url = "https://github.com/npxrc/HourSync",
        },
    ];

    public DateTime startTime = DateTime.Now;

    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        FileMgr.Log("Started at " + startTime.ToString());
        bool isVM = VmChecker.IsVirtualMachine();
        if (isVM)
        {
            FileMgr.Log("----------\r\nVirtual machine detected. Please use legitimate hardware.");
        }

        m_window = new MainWindow();

        rootFrame = new Frame();
        rootFrame.NavigationFailed += OnNavigationFailed;

        NavigationViewModel = new NavigationViewModel();

        NavigationView = new NavigationView
        {
            MenuItemsSource = NavigationViewModel.MenuItems,
            FooterMenuItemsSource = NavigationViewModel.FooterItems,
            SelectedItem = NavigationViewModel.SelectedItem,
            IsSettingsVisible = false, // we’re handling Settings ourselves
            Content = rootFrame,
        };

        NavigationView.ItemInvoked += NavigationView_ItemInvoked;

        // Create a new TransitionCollection
        TransitionCollection transitionCollection =
        [
            // Add a NavigationThemeTransition to the TransitionCollection
            new NavigationThemeTransition(),
        ];

        // Set the ContentTransitions property of the rootFrame to the created TransitionCollection
        rootFrame.ContentTransitions = transitionCollection;

        m_window.Content = NavigationView;
        m_window.Activate();

        client.Initialize();

        rootFrame.Navigate(typeof(Login), true);
    }

    private void NavigationView_ItemInvoked(
        NavigationView sender,
        NavigationViewItemInvokedEventArgs args
    )
    {
        if (args.InvokedItemContainer is NavigationViewItem item)
        {
            SlideNavigationTransitionInfo effect;
            previousPageForNav = currentPageForNav;
            if (previousPageForNav == item.Tag.ToString())
            {
                return;
            }

            string targetPage = item.Tag.ToString();

            // simple page order: left-to-right layout
            var pageOrder = new List<string> { "home", "create", "leaderboard" };

            if (targetPage == "settings")
            {
                // settings always slides vertically
                if (previousPageForNav == "settings")
                {
                    return;
                }

                effect = new SlideNavigationTransitionInfo()
                {
                    Effect = SlideNavigationTransitionEffect.FromBottom,
                };
            }
            else
            {
                // both target and previous are in the linear strip
                int prevIndex = pageOrder.IndexOf(previousPageForNav);
                int newIndex = pageOrder.IndexOf(targetPage);

                if (prevIndex < newIndex)
                {
                    // going right
                    effect = new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromRight,
                    };
                }
                else
                {
                    // going left
                    effect = new SlideNavigationTransitionInfo()
                    {
                        Effect = SlideNavigationTransitionEffect.FromLeft,
                    };
                }
            }

            currentPageForNav = targetPage;

            // navigate to correct page type
            Type targetType = targetPage switch
            {
                "login" => typeof(Login),
                "home" => typeof(Home),
                "create" => typeof(RequestMaker),
                "leaderboard" => typeof(Leaderboard),
                "settings" => typeof(Settings),
                _ => null,
            };

            if (targetType != null)
            {
                rootFrame.Navigate(
                    targetType,
                    new object[] { LoginResult, Username, Password, HomeResult },
                    effect
                );
            }
        }
    }

    public void LoggedIn(
        LoginResult loginResult,
        string username,
        string password,
        string getresp,
        bool navigate = true
    )
    {
        LoginResult = loginResult;
        Username = username;
        Password = password;
        PhpSessionId = loginResult.PhpSessionId;
        NameOfPerson = loginResult.StudentName;
        NameOfAcademy = loginResult.StudentAcademy;
        HomeResult = getresp;

        if (navigate)
        {
            NavigationViewModel.RefreshMenuItems(isLoggedIn: true);
            NavigationView.SelectedItem = NavigationViewModel.SelectedItem; // Added: Update NavigationView's SelectedItem to match the model
            rootFrame.Navigate(
                typeof(Home),
                new object[] { loginResult, username, password, getresp },
                new DrillInNavigationTransitionInfo()
            );
        }
    }

    public void LoggedOut()
    {
        // Clear user-specific data
        previousPageForNav = currentPageForNav;
        Username = null;
        Password = null;
        PhpSessionId = null;
        NameOfPerson = null;
        NameOfAcademy = null;
        HomeResult = null;
        CookieContainer = null;
        Handler = null;
        Client = null;

        NavigationViewModel.RefreshMenuItems(isLoggedIn: false);
        NavigationView.SelectedItem = NavigationViewModel.SelectedItem; // Added: Update NavigationView's SelectedItem to match the model
        rootFrame.Navigate(typeof(Login), false);
        currentPageForNav = "login";
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
        HomeResult = getresp;
        rootFrame.Navigate(
            typeof(Home),
            new object[] { LoginResult, Username, Password, getresp },
            new DrillInNavigationTransitionInfo()
        );
    }

    public void UpdateHomeContent(string getresp)
    {
        HomeResult = getresp;
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
            FileMgr.Log("setting presence");
            client.SetPresence(
                new RichPresence()
                {
                    Details = details,
                    State = state,
                    Assets = new Assets() { LargeImageKey = "logo", LargeImageText = "HourSync" },
                    Buttons = buttons,
                    Type = ActivityType.Playing,
                }
            );
            System.Diagnostics.Trace.WriteLine(
                $"Set presence to {state} with details \"{details}\""
            );
        }
        else
        {
            client.SetPresence(
                new RichPresence()
                {
                    State = state,
                    Assets = new Assets() { LargeImageKey = "logo", LargeImageText = "HourSync" },
                    Buttons = buttons,
                    Type = ActivityType.Playing,
                }
            );
            System.Diagnostics.Trace.WriteLine("Set presence to default");
        }
    }
}
