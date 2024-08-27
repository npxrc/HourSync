using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.UI.Xaml.Shapes;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using System.Reflection.Metadata;
using Windows.Media.Protection.PlayReady;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;

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
        var transitionCollection = new TransitionCollection();

        // Add a NavigationThemeTransition to the TransitionCollection
        transitionCollection.Add(new NavigationThemeTransition());

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

}