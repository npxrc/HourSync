#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable CA1861 // Avoid constant arrays as arguments
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.UI.ViewManagement;

namespace HourSync;

public sealed partial class Login : Page
{
    private readonly string appDataFolder = "eHours";
    private string phpSessionId;
    private string nameOfPerson;
    private string nameOfAcademy;
    private string username;
    private string password;
    private readonly string logFilePath;

    private static readonly CookieContainer _cookieContainer = new();
    private static readonly HttpClientHandler _handler = new()
    {
        CookieContainer = _cookieContainer,
        AllowAutoRedirect = true
    };
    private static readonly HttpClient _client = new(_handler)
    {
        DefaultRequestHeaders = { { "User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36" } }
    };

    private readonly DispatcherTimer _themeCheckTimer;
    private bool _currentThemeIsDark = false;

    private MainWindow _mainWindow;

    public Login()
    {
        InitializeComponent();
        _mainWindow = (MainWindow)((App)Application.Current).m_window;

        // Initialize the timer
        _themeCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250) // Check every 250 ms
        };
        _themeCheckTimer.Tick += (sender, e)=>UpdateTheme();
        _themeCheckTimer.Start();

        // Initial theme check
        UpdateTheme();
        ApplyTheme(IsDarkTheme());

        logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), appDataFolder, "log.txt");
        FileMgr.Log("----------\r\nLogging started for session " + DateTime.Now);
    }

    private void UpdateTheme()
    {
        bool isDarkTheme = IsDarkTheme();
        if (isDarkTheme != _currentThemeIsDark)
        {
            _currentThemeIsDark = isDarkTheme;
            ApplyTheme(isDarkTheme);
        }
    }

    public static bool IsDarkTheme()
    {
        var uiSettings = new UISettings();
        var color = uiSettings.GetColorValue(UIColorType.Background);

        // Simple heuristic to determine if the theme is dark
        return color.R < 128 && color.G < 128 && color.B < 128;
    }

    private void ApplyTheme(bool isDarkTheme)
    {
        if (isDarkTheme)
        {
            loginBanner.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/hoursync-dark-login-banner.png"));
        }
        else
        {
            loginBanner.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/hoursync-light-login-banner.png"));
        }
    }

    private async void LoginButton_Click(object sender, RoutedEventArgs e)
    {
        FileMgr.Log("Login button clicked");
        username = UsernameTextBox.Text;
        password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            var dialog = new ContentDialog
            {
                Title = "Error",
                Content = "Please enter both username and password.",
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            return;
        }
        FileMgr.Log("Running PerformLogin()");
        await PerformLogin();
    }

    private async void ForgotPassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Forgot Password",
            Content = "Working on a school computer? Press CTRL + Alt + Delete and click \"Change a Password.\"\r\nOtherwise, go to \"https://accounts.microsoft.com\", enter your username, and click \"Forgot Password.\"",
            PrimaryButtonText = "OK",
            XamlRoot = XamlRoot
        };
        await dialog.ShowAsync();
    }



    private ProgressBar waitForLoginProgressBar = new()
    {
        IsIndeterminate = true,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Width = 200, // Set width as needed
        Height = 20 // Set height as needed
    };
    private ContentDialog waitForLogin = new()
    {
        Title = "Loading",
        CloseButtonText = null,
        PrimaryButtonText = null // Ensure there's no default button
    };
    private async void ShowLoginProgressBarAsync()
    {
        // Initialize and configure the ContentDialog
        waitForLoginProgressBar = new()
        {
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 200, // Set width as needed
            Height = 20 // Set height as needed
        };

        waitForLogin = new()
        {
            Title = "Loading",
            CloseButtonText = null,
            PrimaryButtonText = null, // Ensure there's no default button
            Content = waitForLoginProgressBar,

            // Ensure the ContentDialog is set to the correct XamlRoot
            XamlRoot = XamlRoot
        };

        // Show the ContentDialog asynchronously
        await waitForLogin.ShowAsync();
    }

    private async Task PerformLogin()
    {
        ShowLoginProgressBarAsync();

        FileMgr.Log("Running await Post()");
        var resp = await Post();
        FileMgr.Log("POSTed");

        resp = resp.Replace("\n", "");
        if (resp.Contains("<h2>Welcome to your"))
        {
            FileMgr.Log("Successful login");
            nameOfAcademy = resp.Split(new string[] { "<h2>Welcome to your " }, StringSplitOptions.None)[1].Split(new string[] { " Endorsement" }, StringSplitOptions.None)[0];
            nameOfPerson = resp.Split(new string[] { "Tracking, " }, StringSplitOptions.None)[1].Split(new string[] { "</h2>" }, StringSplitOptions.None)[0];

            FileMgr.Log("Getting home page");
            var getresp = await Get("https://academyendorsement.olatheschools.com/Student/studentEHours.php");
            FileMgr.Log("Successfully got home");

            FileMgr.Log("Navigating to Home");
            waitForLogin.Hide();
            Frame.Navigate(typeof(Home), new object[] { username, password, phpSessionId, nameOfPerson, nameOfAcademy, getresp, _cookieContainer, _handler, _client }, new DrillInNavigationTransitionInfo());

            // Notify the App instance about successful login
            FileMgr.Log("Running App.LoggedIn");
            ((App)Application.Current).LoggedIn(username, password, phpSessionId, nameOfPerson, nameOfAcademy, getresp, _cookieContainer, _handler, _client);
        }
        else
        {
            waitForLoginProgressBar.ShowError = true;
            waitForLogin.Content = waitForLoginProgressBar;
            waitForLogin.Title = "Incorrect username or password.";
            waitForLogin.CloseButtonText = "OK";
        }
        /*var resp = FileMgr.ReadFromFile("home.html");
        resp = resp.Replace("\n", "");
        if (resp.Contains("<h2>Welcome to your"))
        {
            nameOfAcademy = resp.Split(new string[] { "<h2>Welcome to your " }, StringSplitOptions.None)[1].Split(new string[] { " Endorsement" }, StringSplitOptions.None)[0];
            nameOfPerson = resp.Split(new string[] { "Tracking, " }, StringSplitOptions.None)[1].Split(new string[] { "</h2>" }, StringSplitOptions.None)[0];

            var getresp = FileMgr.ReadFromFile("ehours.html");
            Frame.Navigate(typeof(Home), new object[] { username, password, phpSessionId, nameOfPerson, nameOfAcademy, getresp, _cookieContainer, _handler, _client });

            // Notify the App instance about successful login
            ((App)Application.Current).LoggedIn(username, password, phpSessionId, nameOfPerson, nameOfAcademy, getresp, _cookieContainer, _handler, _client);
        }
        else
        {
            var dialog = new ContentDialog()
            {
                Title = "Error",
                Content = "Incorrect username or password.",
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
        }*/
    }

    private async Task<string> Post()
    {
        var values = new Dictionary<string, string>
        {
            { "uName", username },
            { "uPass", password }
        };

        var content = new FormUrlEncodedContent(values);

        var response = await _client.PostAsync("https://academyendorsement.olatheschools.com/loginuserstudent.php", content);
        var responseString = await response.Content.ReadAsStringAsync();

        Uri uri = new Uri("https://academyendorsement.olatheschools.com/");
        var cookies = _cookieContainer.GetCookies(uri);
        phpSessionId = cookies["PHPSESSID"]?.Value;

        return responseString;
    }

    private async Task<string> Get(string url)
    {
        if (string.IsNullOrEmpty(phpSessionId))
        {
            var dialog = new ContentDialog()
            {
                Title = "Error",
                Content = "PHPSESSID cookie is not set.",
                CloseButtonText = "OK"
            };
            await dialog.ShowAsync();
            return "$$FAIL$$";
        }

        Uri uri = new Uri(url);
        _cookieContainer.Add(uri, new Cookie("PHPSESSID", phpSessionId));

        var response = await _client.GetAsync(url);
        var responseString = await response.Content.ReadAsStringAsync();

        return responseString;
    }
}