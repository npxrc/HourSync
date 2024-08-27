using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using System.IO;
using Windows.ApplicationModel.Core;
using Windows.UI.ViewManagement;
using Microsoft.UI.Xaml.Documents;
using System.Collections;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace HourSync;

public sealed partial class Login : Page
{
    private readonly string appDataFolder = "eHours";
    private string phpSessionId;
    private string nameOfPerson;
    private string nameOfAcademy;
    private string username;
    private string password;

    private static readonly CookieContainer _cookieContainer = new();
    private static readonly HttpClientHandler _handler = new()
    {
        CookieContainer = _cookieContainer,
        AllowAutoRedirect = true
    };
    private static readonly HttpClient _client = new(_handler);

    private readonly DispatcherTimer _themeCheckTimer;
    private bool _currentThemeIsDark = false;

    private MainWindow _mainWindow;

    private void Log(string toLog)
    {
        WriteToFile("log.txt", (ReadFromFile("log.txt") + $"\r\n{toLog}"));
    }

    public Login()
    {
        this.InitializeComponent();
        _mainWindow = (MainWindow)((App)Application.Current).m_window;

        // Initialize the timer
        _themeCheckTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250) // Check every 250 ms
        };
        _themeCheckTimer.Tick += OnThemeCheckTimerTick;
        _themeCheckTimer.Start();

        // Initial theme check
        UpdateTheme();
        ApplyTheme(IsDarkTheme());
        Log("----------\r\nLogging started for session " + DateTime.Now);
    }

    private void OnThemeCheckTimerTick(object sender, object e)
    {
        UpdateTheme();
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
        Log("Login button clicked");
        username = UsernameTextBox.Text;
        password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            var dialog = new ContentDialog()
            {
                Title = "Error",
                Content = "Please enter both username and password.",
                CloseButtonText = "OK"
            };
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
            return;
        }
        Log("Running PerformLogin()");
        await PerformLogin();
    }

    private async void ForgotPassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Forgot Password",
            Content = "Working on a school computer? Press CTRL + Alt + Delete and click \"Change a Password.\"\r\nOtherwise, go to \"https://accounts.microsoft.com\", enter your username, and click \"Forgot Password.\"",
            PrimaryButtonText = "OK",
        };
        dialog.XamlRoot = this.XamlRoot;
        await dialog.ShowAsync();
    }

    private void SaveCredentials()
    {
        CredentialManager.WriteCredential("HourSync", username, password);
    }
    
    private string ReadFromFile(string filename)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var dataPath = Path.Combine(localAppDataPath, appDataFolder);

            var filePath = Path.Combine(dataPath, filename);

            if (File.Exists(filePath))
            {
                var textFromFile = File.ReadAllText(filePath);
                return textFromFile;
            }
            else
            {
                return null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            return null;
        }
    }
    private void WriteToFile(string filename, string towrite)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var dataPath = Path.Combine(localAppDataPath, appDataFolder);

            var filePath = Path.Combine(dataPath, filename);

            Directory.CreateDirectory(dataPath);

            File.WriteAllText(filePath, towrite);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    private async Task PerformLogin()
    {
        Log("Running await Post()");
        var resp = await Post();
        Log("POSTed");
        resp = resp.Replace("\n", "");
        if (resp.Contains("<h2>Welcome to your"))
        {
            Log("Successful login");
            nameOfAcademy = resp.Split(new string[] { "<h2>Welcome to your " }, StringSplitOptions.None)[1].Split(new string[] { " Endorsement" }, StringSplitOptions.None)[0];
            nameOfPerson = resp.Split(new string[] { "Tracking, " }, StringSplitOptions.None)[1].Split(new string[] { "</h2>" }, StringSplitOptions.None)[0];

            Log("Getting home page");
            var getresp = await Get("https://academyendorsement.olatheschools.com/Student/studentEHours.php");
            Log("Successfully got home");

            var dialog = new ContentDialog()
            {
                Title = "Thanks!",
                Content = $"Hi, {nameOfPerson}! Thanks for beta testing HourSync! Please take note of any bugs or places which could be improved, and share them with me.",
                CloseButtonText = "OK"
            };
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();

            Log("Navigating to Home");
            Frame.Navigate(typeof(Home), new object[] { username, password, phpSessionId, nameOfPerson, nameOfAcademy, getresp, _cookieContainer, _handler, _client }, new DrillInNavigationTransitionInfo());

            // Notify the App instance about successful login
            Log("Running App.LoggedIn");
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
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }
        /*var resp = ReadFromFile("home.html");
        resp = resp.Replace("\n", "");
        if (resp.Contains("<h2>Welcome to your"))
        {
            nameOfAcademy = resp.Split(new string[] { "<h2>Welcome to your " }, StringSplitOptions.None)[1].Split(new string[] { " Endorsement" }, StringSplitOptions.None)[0];
            nameOfPerson = resp.Split(new string[] { "Tracking, " }, StringSplitOptions.None)[1].Split(new string[] { "</h2>" }, StringSplitOptions.None)[0];

            var getresp = ReadFromFile("ehours.html");
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

        _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

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