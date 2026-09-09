#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible

//Login.xaml.cs

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HourSyncCoreLib;
using HourSync.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace HourSync;

public sealed partial class Login : Page
{
    private readonly string appDataFolder = "HourSync";
    private readonly string logFilePath;
    private readonly DialogService dialogManager = new();
    private readonly UISettings uiSettings;

    private string phpSessionId;
    private string nameOfPerson;
    private string nameOfAcademy;
    private string username;
    private string password;

    private MetricsSyncManager metricsManager;

    private static readonly CookieContainer _cookieContainer = new();
    private static readonly HttpClientHandler _handler = new()
    {
        CookieContainer = _cookieContainer,
        AllowAutoRedirect = true,
    };
    private static readonly HttpClient _client = new(_handler)
    {
        DefaultRequestHeaders =
        {
            {
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
            },
        },
    };

    private ProgressBar waitForLoginProgressBar = new()
    {
        IsIndeterminate = true,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Width = 200,
        Height = 20,
    };
    private ContentDialog waitForLogin = new()
    {
        Title = "Loading",
        CloseButtonText = null,
        PrimaryButtonText = null,
    };

    private bool isProgressBarActive;
    private bool isVm = VmChecker.IsVirtualMachine();

    public Login()
    {
        try
        {
            if (isVm)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError(ex.Message);
        }

        InitializeComponent();
        DataContext = new LoginViewModel();

        uiSettings = new UISettings();
        uiSettings.ColorValuesChanged += OnSystemThemeChanged;

        ApplyTheme(IsDarkTheme());

        logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appDataFolder,
            "log.txt"
        );

        ((App)Application.Current).UpdatePresence("login", "");

        string exePath = Assembly.GetEntryAssembly().Location;
        string exeDirectory = Path.GetDirectoryName(exePath);
        Trace.WriteLine("Executable is located in: " + exeDirectory);
    }

    private void OnSystemThemeChanged(UISettings sender, object args)
    {
        DispatcherQueue.TryEnqueue(() => ApplyTheme(IsDarkTheme()));
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        uiSettings.ColorValuesChanged -= OnSystemThemeChanged;
        base.OnNavigatedFrom(e);
    }

    public static bool IsDarkTheme()
    {
        var settings = new UISettings();
        var color = settings.GetColorValue(UIColorType.Background);
        return color.R < 128 && color.G < 128 && color.B < 128;
    }

    private void ApplyTheme(bool isDarkTheme)
    {
        var uri = isDarkTheme
            ? "ms-appx:///Assets/hoursync-dark-login-banner.png"
            : "ms-appx:///Assets/hoursync-light-login-banner.png";
        loginBanner.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(uri));
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            if (isVm)
            {
                Loaded += async (_, _) =>
                {
                    try
                    {
                        await dialogManager.ShowDialog(
                        "Virtual Machine Detected",
                        "HourSync is not designed to be used on virtual machines as they can be used to bypass security measures. As a result, HourSync disallows the use of virtual machines in order to protect user security and the district potal. Please use legitimate hardware in order to use HourSync. If you believe this is a false detection, please email neil@hoursync.net.",
                        LocalizationService.GetString("OK"),
                        "",
                        "",
                        XamlRoot
                    );
                    } catch( Exception ex ) {
                        FileMgr.LogError(ex.Message);
                        Application.Current.Exit();
                    }
                    Application.Current.Exit();
                };
                return;
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError(ex.Message);
        }

        base.OnNavigatedTo(e);

        bool isFirstTime = e.Parameter is bool param && param;

        var (userNameFromVault, passFromVault) = CredMgr.GetCreds();

        if (string.IsNullOrEmpty(userNameFromVault) || string.IsNullOrEmpty(passFromVault))
        {
            FileMgr.Log("No credentials found in vault.");
            if (isFirstTime)
            {
                Loaded += async (_, _) => await HandleFirstTimeSetupAsync();
            }
            return;
        }

        if (!string.IsNullOrEmpty(passFromVault))
        {
            FileMgr.Log($"Credentials successfully retrieved for user {userNameFromVault}.");
            UsernameTextBox.Text = userNameFromVault;
            PasswordBox.Password = passFromVault;
            username = userNameFromVault;
            password = passFromVault;
            if (isFirstTime)
            {
                Loaded += async (_, _) => await HandleFirstTimeSetupAsync();
            }
        }
    }

    private async Task HandleFirstTimeSetupAsync()
    {
        await Task.Delay(200);
        var autoLoginEnabled = await FileMgr.GetSettingValueAsync("autologin.IsEnabled");

        if (autoLoginEnabled.ToString() == "NOSETTINGSFILE")
        {
            FileMgr.Log("Welcome to HourSync! Intial setup in progress.");

            await dialogManager.ShowDialog(
                "Welcome!",
                "Welcome to HourSync!\r\n\r\nHourSync provides a centralized and easy-to-use interface to manage your eHours. Experience a modern and accessible interface that puts the focus on logging your eHours. Never worry again about losing an eHour submission with Draft Saving and a Save to OneDrive feature. And never write the same submission twice with automatic request splitting for things like trips that are worth more than 100 hours.\r\n\r\nThe developer of this app, Neil, wishes you enjoy HourSync and find it useful. Thanks!",
                "",
                "Continue",
                "",
                XamlRoot
            );
            FileMgr.WriteToFile("settings.json", "{}");
        }
        FileMgr.Log(autoLoginEnabled.ToString());
        if (autoLoginEnabled.ToString().Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            FileMgr.Log("AutoLogin is enabled. Logging in automatically.");
            if (await CheckForUpdateIfNeeded())
            {
                return;
            }

            ShowLoginProgressBar();

            var appSettings = FileMgr.LoadAppSettings();

            if (appSettings.TryGetValue("lastRequest", out var storedDateObj) &&
                DateTime.TryParse(storedDateObj.ToString(), out var storedDate) &&
                appSettings.TryGetValue("loginResult", out var loginResultObj) &&
                (DateTime.Now - storedDate) < TimeSpan.FromMinutes(5))
            {
                FileMgr.Log("Session found, testing with HourSyncCore");

                var loginRes = Newtonsoft.Json.JsonConvert.DeserializeObject<LoginResult>(loginResultObj.ToString());

                if (await HourSyncCore.IsSessionActive(loginRes.PhpSessionId))
                {
                    FileMgr.Log("Session is active.");
                    var getresp = await HourSyncCore.GetRequestsPage(loginRes.PhpSessionId);

                    Frame.Navigate(
                        typeof(Home),
                        new object[] { loginRes, username, password, getresp },
                        new DrillInNavigationTransitionInfo()
                    );

                    HideLoginProgressBar();

                    // Track auto-login (stored locally)
                    metricsManager = new MetricsSyncManager(username);
                    metricsManager.TrackAutoLogin();

                    FileMgr.Log("Running App.LoggedIn");
                    ((App)Application.Current).LoggedIn(loginRes, username, password, getresp, true);
                    return;
                }
            }

            FileMgr.Log("No active session, starting a new one.");
            await PerformLoginAsync();
        }
        else
        {
            FileMgr.Log("AutoLogin is disabled.");
            if (await CheckForUpdateIfNeeded())
            {
                return;
            }
        }
    }

    private static async Task<bool> IsInternetAvailableAsync()
    {
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000);
            return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
        }
        catch (Exception ex)
        {
            FileMgr.Log($"Ping failed: {ex.Message}");
            return false;
        }
    }

    private async void LoginButton_Click(object _, RoutedEventArgs __)
    {
        try
        {
            FileMgr.Log("Login button clicked");
            username = UsernameTextBox.Text;
            password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                await dialogManager.ShowDialog(
                    "Error",
                    "Please enter both a username and password",
                    "OK",
                    "",
                    "",
                    XamlRoot
                );
                return;
            }
            FileMgr.Log("Running PerformLogin()");
            await PerformLoginAsync();
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error in LoginButton_Click: {ex.Message}");
        }
    }

    private async void ForgotPassword_Click(object _, RoutedEventArgs __)
    {
        try
        {
            var textBlock = new TextBlock { TextWrapping = TextWrapping.Wrap };

            textBlock.Inlines.Add(new Run { Text = "Working on a school computer? Press CTRL + Alt + Delete and click \"Change a Password.\" " });
            textBlock.Inlines.Add(new LineBreak());
            textBlock.Inlines.Add(new Run { Text = "Otherwise, " });

            var hyperlink = new Hyperlink();
            hyperlink.Inlines.Add(new Run { Text = "enter your username in the Microsoft signin screen" });

            string loginHint = "";
            if (UsernameRegex().IsMatch(UsernameTextBox.Text))
            {
                loginHint = $"&login_hint={UsernameTextBox.Text}@stu.olatheschools.org";
            }
            hyperlink.Click += async (_, _) =>
                await Launcher.LaunchUriAsync(new Uri(
                    "https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id=19db86c3-b2b9-44cc-b339-36da233a3be2&redirect_uri=https%3A%2F%2Fmysignins.microsoft.com&scope=openid+profile+email+offline_access&response_type=code&response_mode=fragment&code_challenge=9PI188Gb7y4H7AP9K0Sy4sYivPrB1h-r8EY62pWg-MM&code_challenge_method=S256&state=461c659f-7fe7-4fb5-a11b-0e87f8e46500" + loginHint
                ));

            textBlock.Inlines.Add(hyperlink);
            textBlock.Inlines.Add(new Run { Text = " and click \"Forgot Password\"." });

            var dialog = new ContentDialog
            {
                Title = "Forgot Password",
                Content = textBlock,
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot,
            };

            await dialog.ShowAsync();
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error in ForgotPassword_Click: {ex.Message}");
        }
    }

    private bool isShiftPressedInUsernameBox;

    private async void UsernameTextBox_KeyDown(object _, KeyRoutedEventArgs e)
    {
        try
        {
            if (e.Key == VirtualKey.Enter)
            {
                PasswordBox.Focus(FocusState.Keyboard);
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Shift)
            {
                isShiftPressedInUsernameBox = true;
                e.Handled = true;
            }
            else if (e.Key == VirtualKey.Number2 && isShiftPressedInUsernameBox)
            {
                await new ContentDialog()
                {
                    Title = LocalizationService.GetString("UseYourUsernameTitle"),
                    Content = LocalizationService.GetString("UseYourUsernameContent"),
                    PrimaryButtonText = LocalizationService.GetString("GenericOKText"),
                    XamlRoot = XamlRoot,
                }.ShowAsync();

                UsernameTextBox.Text = UsernameTextBox.Text[..^1];
                PasswordBox.Focus(FocusState.Keyboard);
                isShiftPressedInUsernameBox = false;
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error in UsernameTextBox_KeyDown: {ex.Message}");
        }
    }

    private void UsernameTextBox_KeyUp(object _, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Shift)
        {
            isShiftPressedInUsernameBox = false;
            e.Handled = true;
        }
    }

    private async void PasswordBox_KeyDown(object _, KeyRoutedEventArgs e)
    {
        try
        {
            if (e.Key == VirtualKey.Enter)
            {
                username = UsernameTextBox.Text;
                password = PasswordBox.Password;
                await PerformLoginAsync();
                e.Handled = true;
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error in PasswordBox_KeyDown: {ex.Message}");
        }
    }

    private void ShowLoginProgressBar()
    {
        if (isProgressBarActive) return;
        isProgressBarActive = true;

        waitForLoginProgressBar = new ProgressBar
        {
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 200,
            Height = 20,
        };

        waitForLogin = new ContentDialog
        {
            Title = LocalizationService.GetString("GenericLoadingText"),
            CloseButtonText = null,
            PrimaryButtonText = null,
            Content = waitForLoginProgressBar,
            XamlRoot = XamlRoot,
        };

        if (!DialogManager.IsDialogOpen)
        {
            _ = waitForLogin.ShowAsync();
        }

        waitForLogin.Closed += (_, _) => isProgressBarActive = false;
    }

    private void HideLoginProgressBar()
    {
        if (waitForLogin != null && isProgressBarActive)
        {
            waitForLogin.Hide();
            isProgressBarActive = false;
        }
    }

    private async Task PerformLoginAsync()
    {
        ShowLoginProgressBar();

        try
        {
            FileMgr.Log("Attempting login via HourSyncCore");
            if (password.Length < 6)
            {
                ShowLoginError(LocalizationService.GetString("PassTooShort"));
                return;
            }
            var loginResult = await HourSyncCore.Login(username, password);
            FileMgr.Log("Done");

            if (!string.IsNullOrEmpty(loginResult.Error) || string.IsNullOrEmpty(loginResult.PhpSessionId))
            {
                FileMgr.Log($"Login failed: {loginResult.Error}");
                ShowLoginError(loginResult.Error ?? "Login failed.");
                return;
            }

            phpSessionId = loginResult.PhpSessionId;
            nameOfPerson = loginResult.StudentName ?? "";
            nameOfAcademy = loginResult.StudentAcademy ?? "";

            FileMgr.Log($"Login successful. Student: {nameOfPerson}, Academy: {nameOfAcademy}");

            FileMgr.Log("Getting home page via HourSyncCore");
            var getresp = await HourSyncCore.GetRequestsPage(phpSessionId);

            FileMgr.Log("Successfully got home page");

            CredMgr.SaveCreds(username, password);

            FileMgr.Log("Navigating to Home");

            HideLoginProgressBar();

            Frame.Navigate(
                typeof(Home),
                new object[] { loginResult, username, password, getresp },
                new DrillInNavigationTransitionInfo()
            );

            FileMgr.Log("Running App.LoggedIn");
            ((App)Application.Current).LoggedIn(loginResult, username, password, getresp, true);
        }
        catch (Exception ex)
        {
            FileMgr.Log($"Error during PerformLogin: {ex}");
            ShowLoginError("An unexpected error occurred while logging in.");
        }
    }

    private void ShowLoginError(string message)
    {
        waitForLoginProgressBar.ShowError = true;
        waitForLogin.Content = waitForLoginProgressBar;
        waitForLogin.Title = message;
        waitForLogin.CloseButtonText = "OK";
    }

    private async Task<bool> CheckForUpdateIfNeeded()
    {
        DateTime now = DateTime.Now;
        bool shouldCheck;
        try
        {
            FileMgr.Log("Getting last update check date");
            var appSettings = FileMgr.LoadAppSettings();

            if (appSettings.TryGetValue("LastUpdateCheck", out var storedDateObj) &&
                DateTimeOffset.TryParse(storedDateObj.ToString(), out var storedDate) &&
                (now - storedDate) < TimeSpan.FromDays(1))
            {
                shouldCheck = false;
            }
            else
            {
                appSettings["LastUpdateCheck"] = now.ToString("o");
                FileMgr.SaveAppSettings(appSettings);
                shouldCheck = true;
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error accessing app settings: {ex.Message}");
            // Default to checking for update if there's an error
            shouldCheck = true;
        }

        if (!shouldCheck)
        {
            return false;
        }

        return await CheckForUpdate();
    }

    private async Task<bool> CheckForUpdate()
    {
        FileMgr.Log("Getting latest version");
        string versionURL = Private.Version();
        var resp = await _client.GetAsync(versionURL);
        string json = await resp.Content.ReadAsStringAsync();
        FileMgr.Log("Got");

        FileMgr.Log("parsing");
        var obj = JObject.Parse(json);
        string latestVersion = (string)obj["fields"]?["version"]?["stringValue"];
        FileMgr.Log("Done parsing, latest version is " + latestVersion);

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        FileMgr.Log("Assembly version: " + version);
        var versionString = $"{version.Major}.{version.Minor}.{version.Revision}";
        FileMgr.Log("Version string: " + versionString);

        string result = Utils.IsNewerVersion(latestVersion, versionString);

        if (result == "true")
        {
            var clicked = await dialogManager.ShowDialog(
                LocalizationService.GetString("NewUpdateAvailableTitle"),
                LocalizationService.GetString("NewUpdateAvailableContent"),
                LocalizationService.GetString("LaterText"),
                LocalizationService.GetString("UpdateText"),
                "",
                XamlRoot
            );
            if (clicked == ContentDialogResult.Primary)
            {
                ShowLoginProgressBar();

                try
                {
                    var settingsURL = Private.Settings();
                    var settingsResp = await _client.GetAsync(settingsURL);
                    string settingsJson = await settingsResp.Content.ReadAsStringAsync();

                    var downloadedSettings = FileMgr.ParseAndWriteSettingsJson(settingsJson);

                    if (downloadedSettings != null)
                    {
                        var settingsDict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, SettingDefinition>>(downloadedSettings);
                        if (settingsDict != null)
                        {
                            FileMgr.MergeSettings(settingsDict);
                            FileMgr.Log("Settings merged successfully.");
                        }
                        else
                        {
                            FileMgr.Log("Failed to deserialize settings.");
                        }
                    }
                    else
                    {
                        FileMgr.Log("Downloaded settings JSON was null or invalid.");
                    }
                }
                catch (Exception ex)
                {
                    FileMgr.LogError("Error when downloading settings: " + ex.Message);
                }

                string appExeDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                string parentDirectory = Directory.GetParent(appExeDirectory).FullName;

                var appURL = Private.AppURL();
                var downloadUri = new Uri(appURL);
                string downloadPath = Path.Combine(parentDirectory, "HourSync.exe");

                try
                {
                    FileMgr.Log("Downloading.");
                    var fileBytes = await _client.GetByteArrayAsync(downloadUri);
                    await File.WriteAllBytesAsync(downloadPath, fileBytes);

                    Process.Start(new ProcessStartInfo
                    {
                        FileName = downloadPath,
                        Arguments = "/SILENT",
                        UseShellExecute = true,
                        CreateNoWindow = true,
                    });

                    FileMgr.Log("Handing it off to Inno.");
                    Application.Current.Exit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
                    HideLoginProgressBar();
                }
                return true;
            }
        }
        else if (result == "error")
        {
            FileMgr.LogError("Version comparison failed. Please check the version strings.");
            await dialogManager.ShowDialog(
                "Update Check Error",
                "An error occurred while checking for updates. Please try again later.",
                "",
                "Okay",
                "",
                XamlRoot
            );
            HideLoginProgressBar();
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"^\d{3}[a-zA-Z]{3}(0[1-9]|[12][0-9]|3[01])$")]
    private static partial Regex UsernameRegex();

}
public partial class LoginViewModel : INotifyPropertyChanged
{
    private string _username;
    private bool _hasValidationError;
    private Brush _borderBrush;
    private Thickness _borderThickness;

    public string Username
    {
        get => _username;
        set
        {
            _username = value;
            OnPropertyChanged();
            ValidateUsername();
        }
    }

    public bool HasValidationError
    {
        get => _hasValidationError;
        set
        {
            _hasValidationError = value;
            OnPropertyChanged();
            UpdateBorderAppearance();
        }
    }

    public Brush BorderBrush
    {
        get => _borderBrush;
        set
        {
            _borderBrush = value;
            OnPropertyChanged();
        }
    }

    public Thickness BorderThickness
    {
        get => _borderThickness;
        set
        {
            _borderThickness = value;
            OnPropertyChanged();
        }
    }

    private void ValidateUsername()
    {
        if (string.IsNullOrEmpty(Username))
        {
            HasValidationError = false;
            return;
        }

        HasValidationError = !UsernameRegex().IsMatch(Username);
    }

    private void UpdateBorderAppearance()
    {
        if (HasValidationError)
        {
            BorderThickness = new Thickness(2);
            BorderBrush = new SolidColorBrush(Color.FromArgb(255, 200, 50, 30));
        }
        else
        {
            BorderThickness = new Thickness(0);
            BorderBrush = null;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    [GeneratedRegex(@"^\d{3}[a-zA-Z]{3}(0[1-9]|[12][0-9]|3[01])$")]
    private static partial Regex UsernameRegex();
}
