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
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using Windows.ApplicationModel;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace HourSync;

public sealed partial class Login : Page
{
    private readonly string appDataFolder = "HourSync";
    private readonly string logFilePath;
    private string phpSessionId;
    private string nameOfPerson;
    private string nameOfAcademy;
    private string username;
    private string password;

    private DialogService dialogManager = new();

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

    private UISettings uiSettings;

    public Login()
    {
        try
        {
            bool isVM = VmChecker.IsVirtualMachine();
            if (isVM)
            {
                return;
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError(ex.Message);
        }
        InitializeComponent();
        this.DataContext = new LoginViewModel();

        // Replace timer with event-based theme handling
        var uiSettings = new UISettings();
        uiSettings.ColorValuesChanged += OnSystemThemeChanged;

        // Initial theme check
        ApplyTheme(IsDarkTheme());

        logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appDataFolder,
            "log.txt"
        );

        ((App)Application.Current).UpdatePresence("login", "");

        string exePath = Assembly.GetEntryAssembly().Location;

        // Get the directory of the executable
        string exeDirectory = Path.GetDirectoryName(exePath);

        System.Diagnostics.Trace.WriteLine("Executable is located in: " + exeDirectory);
    }

    private void OnSystemThemeChanged(UISettings _, object __)
    {
        // Use dispatcher to update UI thread
        DispatcherQueue.TryEnqueue(() => ApplyTheme(IsDarkTheme()));
    }

    // Clean up event handlers when page is navigated away from
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        var uiSettings = new UISettings();
        uiSettings.ColorValuesChanged -= OnSystemThemeChanged;

        base.OnNavigatedFrom(e);
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
            loginBanner.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                new Uri("ms-appx:///Assets/hoursync-dark-login-banner.png")
            );
        }
        else
        {
            loginBanner.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(
                new Uri("ms-appx:///Assets/hoursync-light-login-banner.png")
            );
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        try
        {
            if (VmChecker.IsVirtualMachine())
            {
                Loaded += async (_, __) =>
                {
                    await dialogManager.ShowDialog("Virtual Machine Detected", "Please use legitimate hardware in order to use HourSync. If you believe this is a false detection, please email neil@hoursync.net.", "OK", "", "", XamlRoot);
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

        bool isFirstTime = false;
        if (e.Parameter.GetType() == typeof(bool))
        {
            isFirstTime = (bool)e.Parameter;
            if (isFirstTime)
            {
                FileMgr.StartLogSession();
            }
        }

        (string UserNameFromVault, string PassFromVault) = CredMgr.GetCreds();

        if (UserNameFromVault == null || PassFromVault == null)
        {
            FileMgr.Log("No credentials found in vault.");
            try
            {
                if (isFirstTime)
                {
                    Loaded += async (_, __) =>
                        await HandleFirstTimeSetupAsync();
                }
            }
            catch (Exception ex)
            {
                FileMgr.LogError(ex.Message);
            }
            return;
        }
        if (PassFromVault.Length > 0)
        {

            FileMgr.Log($"Credentials successfully retrieved for user {UserNameFromVault}.");
            UsernameTextBox.Text = UserNameFromVault;
            PasswordBox.Password = PassFromVault;
            if (isFirstTime)
            {
                Loaded += async (_, __) =>
                    await HandleFirstTimeSetupAsync();
            }
        }
    }

    private async Task HandleFirstTimeSetupAsync()
    {
        await Task.Delay(200);
        var autoLoginEnabled = await FileMgr.GetSettingValueAsync("autologin.IsEnabled");

        if (autoLoginEnabled.ToString() == "NOSETTINGSFILE")
        {
            FileMgr.Log("Welcome to HourSync! Downloading current settings.");

            var result = await dialogManager.ShowDialog("Welcome!", "Welcome to HourSync! This app requires the internet to initialize for the first time (and also to even log in!), so if you haven't connected, please do so now, or exit the app and try again later.\n\nThis app is meant to provide a centralized and easy to use experience to manage your eHours. Experience a modern design, an accessible interface, draft saving, and completely optional AI features.\n\nThe author of this app, Neil, wishes you enjoy this app and find it useful. Thanks!", "Cancel", "Download", "", XamlRoot);
            if (result == ContentDialogResult.Primary)
            {
                FileMgr.Log("Downloading settings.json.");
                await DownloadSettings();
            }
            else
            {
                FileMgr.Log("User canceled the download of settings.json.");
                FileMgr.Log("Exiting.");
                App.Current.Exit();
                return;
            }
        }

        if (autoLoginEnabled.ToString().Equals("true", StringComparison.CurrentCultureIgnoreCase))
        {
            FileMgr.Log("AutoLogin is enabled. Logging in automatically.");
            if (await CheckForUpdateIfNeeded())
            {
                return;
            }

            ShowLoginProgressBarAsync();

            var localSettings = ApplicationData.Current.LocalSettings;

            // check if the lastRequest was less than 5 minutes from now
            if (localSettings.Values.TryGetValue("lastRequest", out object storedDateObj) &&
                DateTime.TryParse(storedDateObj.ToString(), out DateTime storedDate) &&
                localSettings.Values.TryGetValue("loginResult", out object loginResultObj))
            {
                if ((DateTime.Now - storedDate) < TimeSpan.FromMinutes(5))
                {
                    FileMgr.Log("Session found, testing with HourSyncCore");

                    // Deserialize JSON back to your LoginResult type
                    var loginRes = Newtonsoft.Json.JsonConvert.DeserializeObject<LoginResult>(
                        loginResultObj.ToString()
                    );

                    // Now you can use loginRes safely
                    if (await HourSyncCore.IsSessionActive(loginRes.PhpSessionId))
                    {
                        FileMgr.Log("Session is active.");
                        var getresp = await HourSyncCore.GetRequestsPage(loginRes.PhpSessionId);

                        Frame.Navigate(
                            typeof(Home),
                            new object[]
                            {
                                loginRes,
                                username,
                                password,
                                getresp
                            },
                            new DrillInNavigationTransitionInfo()
                        );

                        HideLoginProgressBar();

                        FileMgr.Log("Running App.LoggedIn");
                        ((App)Application.Current).LoggedIn(
                            loginRes, username, password, getresp, true
                        );
                        return;
                    }
                }
            }

            FileMgr.Log("No active session, starting a new one.");
            LoginButton_Click(null, null);

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
            var reply = await ping.SendPingAsync("8.8.8.8", 3000); // 3-second timeout
            return reply.Status == System.Net.NetworkInformation.IPStatus.Success;
        }
        catch (Exception ex)
        {
            FileMgr.Log($"Ping failed: {ex.Message}");
            return false;
        }
    }


    private async Task DownloadSettings()
    {
        ShowLoginProgressBarAsync();
        // Check internet connectivity by pinging Google's public DNS
        if (await IsInternetAvailableAsync())
        {
            try
            {
                FileMgr.Log("Internet connection is available. Downloading settings.json from Firestore.");
                var settingsURL = Private.Settings();
                var resp = await _client.GetAsync(settingsURL);
                string json = await resp.Content.ReadAsStringAsync();
                // Parse and write the settings JSON
                var finalJson = FileMgr.ParseAndWriteSettingsJson(json);
                FileMgr.WriteToFile("settings.json", finalJson);
                waitForLoginProgressBar.IsIndeterminate = false;
                waitForLoginProgressBar.Value = 100;
                waitForLogin.Content = waitForLoginProgressBar;
                waitForLogin.Title = "Download Complete";
                waitForLogin.CloseButtonText = "OK";
            }
            catch (Exception ex)
            {
                FileMgr.LogError("Error when downloading settings: " + ex.Message);
            }
        }
        else
        {
            FileMgr.Log("Internet connection is not available. Cannot download settings.json.");
            waitForLoginProgressBar.ShowError = true;
            waitForLogin.Content = waitForLoginProgressBar;
            waitForLogin.Title = "No Internet";
            waitForLogin.CloseButtonText = "OK";
            waitForLogin.CloseButtonClick += (_, __) => ((App)Application.Current).m_window.Close();
        }
    }

    private async void LoginButton_Click(object _, RoutedEventArgs __)
    {
        FileMgr.Log("Login button clicked");
        username = UsernameTextBox.Text;
        password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await dialogManager.ShowDialog("Error", "Please enter both a username and password", "OK", "", "", XamlRoot);
            return;
        }
        FileMgr.Log("Running PerformLogin()");
        await PerformLogin();
    }

    private async void ForgotPassword_Click(object _, RoutedEventArgs __)
    {

        TextBlock textBlock = new()
        {
            TextWrapping = TextWrapping.Wrap
        };

        textBlock.Inlines.Add(new Run
        {
            Text = "Working on a school computer? Press CTRL + Alt + Delete and click \"Change a Password.\" "
        });
        textBlock.Inlines.Add(new LineBreak());

        textBlock.Inlines.Add(new Run { Text = "Otherwise, " });

        Hyperlink hyperlink = new Hyperlink();
        hyperlink.Inlines.Add(new Run { Text = "enter your username in the Microsoft signin screen" });

        string loginHint = "";
        if (UsernameRegex().IsMatch(UsernameTextBox.Text))
        {
            loginHint = $"&login_hint={UsernameTextBox.Text}@stu.olatheschools.org";
        }
        hyperlink.Click += async (_, __) => await Launcher.LaunchUriAsync(new Uri("https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id=19db86c3-b2b9-44cc-b339-36da233a3be2&redirect_uri=https%3A%2F%2Fmysignins.microsoft.com&scope=openid+profile+email+offline_access&response_type=code&response_mode=fragment&code_challenge=9PI188Gb7y4H7AP9K0Sy4sYivPrB1h-r8EY62pWg-MM&code_challenge_method=S256&state=461c659f-7fe7-4fb5-a11b-0e87f8e46500" + loginHint));

        textBlock.Inlines.Add(hyperlink);

        textBlock.Inlines.Add(new Run { Text = " and click \"Forgot Password\"." });

        var dialog = new ContentDialog
        {
            Title = "Forgot Password",
            Content = textBlock,
            PrimaryButtonText = "OK",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
    }

    private bool isShiftPressedInUsernameBox = false;

    private async void UsernameTextBox_KeyDown(object _, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            PasswordBox.Focus(FocusState.Keyboard);
            e.Handled = true; // Optional: to prevent further handling
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
                Title = "Use your username",
                Content =
                    "Use the same login you use for the regular eHours portal! You don't have to use your Gmail username; in fact it actually won't work if you do.",
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot,
            }.ShowAsync();

            UsernameTextBox.Text = UsernameTextBox.Text[..^1];
            PasswordBox.Focus(FocusState.Keyboard);
            isShiftPressedInUsernameBox = false;
        }
    }

    private void UsernameTextBox_KeyUp(object _, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Shift)
        {
            isShiftPressedInUsernameBox = false;
            e.Handled = true;
        }

        if (UsernameTextBox.Text.Length < 1)
        {
            UsernameTextBox.BorderBrush = null;
            UsernameTextBox.BorderThickness = new Thickness(0);
            return;
        }

        if (!UsernameRegex().IsMatch(UsernameTextBox.Text))
        {
            // Handle invalid input, e.g., show a message or change the TextBox border color
            UsernameTextBox.BorderThickness = new Thickness(2);
            UsernameTextBox.BorderBrush = new SolidColorBrush(Color.FromArgb(255, 200, 50, 30));
        }
        else
        {
            // Reset the border color if the input is valid
            UsernameTextBox.BorderBrush = null;
            UsernameTextBox.BorderThickness = new Thickness(0);
        }
    }

    private void PasswordBox_KeyDown(object _, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Enter)
        {
            LoginButton_Click(null, null);
            e.Handled = true; // Optional: to prevent further handling
        }
    }

    private ProgressBar waitForLoginProgressBar = new()
    {
        IsIndeterminate = true,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Width = 200, // Set width as needed
        Height = 20, // Set height as needed
    };
    private ContentDialog waitForLogin = new()
    {
        Title = "Loading",
        CloseButtonText = null,
        PrimaryButtonText = null, // Ensure there's no default button
    };

    private bool IsProgressBarActive = false;
    private async void ShowLoginProgressBarAsync()
    {
        if (IsProgressBarActive) return;
        IsProgressBarActive = true;
        // Initialize and configure the ContentDialog
        waitForLoginProgressBar = new()
        {
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 200, // Set width as needed
            Height = 20, // Set height as needed
        };

        waitForLogin = new()
        {
            Title = "Loading",
            CloseButtonText = null,
            PrimaryButtonText = null, // Ensure there's no default button
            Content = waitForLoginProgressBar,

            // Ensure the ContentDialog is set to the correct XamlRoot
            XamlRoot = XamlRoot,
        };

        // Show the ContentDialog asynchronously
        if (!DialogManager.IsDialogOpen)
        {
            await waitForLogin.ShowAsync();
        }

        waitForLogin.Closed += (_, __) => IsProgressBarActive = false;
    }

    private void HideLoginProgressBar()
    {
        if (waitForLogin != null && IsProgressBarActive)
        {
            waitForLogin.Hide();
            IsProgressBarActive = false;
        }
    }

    private async Task PerformLogin()
    {
        ShowLoginProgressBarAsync();

        try
        {
            var localSettings = ApplicationData.Current.LocalSettings;

            FileMgr.Log("Attempting login via HourSyncCore");
            if (password.Length < 6)
            {
                ShowLoginError("Password length is too short");
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

            // Replace the problematic code with the following to ensure the value being written is serializable
            localSettings.Values["loginResult"] = Newtonsoft.Json.JsonConvert.SerializeObject(loginResult);
            localSettings.Values["lastRequest"] = DateTime.Now.ToString("o"); // Use ISO 8601 format for serialization

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
                new object[]
                {
                    loginResult,
                    username,
                    password,
                    getresp
                },
                new DrillInNavigationTransitionInfo()
            );

            FileMgr.Log("Running App.LoggedIn");
            ((App)Application.Current).LoggedIn(
                loginResult, username, password, getresp, true
            );
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

    //check for update if it's been 24 hours since last check, mainly to prevent me, the developer, from calling the API a ton of times
    private async Task<bool> CheckForUpdateIfNeeded()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        string lastCheckKey = "LastUpdateCheck";

        DateTime now = DateTime.Now;

        if (localSettings.Values.TryGetValue(lastCheckKey, out object storedDateObj) &&
            DateTime.TryParse(storedDateObj.ToString(), out DateTime storedDate))
        {
            if ((now - storedDate) < TimeSpan.FromDays(1))
            {
                // Last check was less than 24h ago, skip
                return false;
            }
        }

        // Save current time
        localSettings.Values[lastCheckKey] = now.ToString("o"); // "o" = round-trip ISO 8601

        // Run the actual update check
        return await CheckForUpdate();
    }


    private async Task<bool> CheckForUpdate()
    {
        string versionURL = Private.Version();
        var resp = await _client.GetAsync(versionURL);
        string json = await resp.Content.ReadAsStringAsync();

        var obj = JObject.Parse(json);
        string latestVersion = (string)obj["fields"]?["version"]?["stringValue"];

        var package = Package.Current.Id.Version;
        var versionString = $"{package.Major}.{package.Minor}.{package.Revision}";

        string result = Utils.IsNewerVersion(latestVersion, versionString);

        if (result == "true")
        {
            var clicked = await dialogManager.ShowDialog("New Update Available", "A newer version is available. Would you like to update?", "Later", "Update", "", XamlRoot);
            if (clicked == ContentDialogResult.Primary)
            {
                ShowLoginProgressBarAsync();

                try
                {
                    var settingsURL = Private.Settings();
                    var settingsResp = await _client.GetAsync(settingsURL);
                    string settingsJson = await settingsResp.Content.ReadAsStringAsync();

                    // Deserialize into Dictionary<string, SettingDefinition>
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
                        FileMgr.Log("Settings merged successfully.");
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

                // Get the directory where the app's executable is located
                string appExeDirectory = Path.GetDirectoryName(
                    Assembly.GetEntryAssembly().Location
                );
                string parentDirectory = Directory.GetParent(appExeDirectory).FullName;

                // Download the EXE file to the parent directory
                var appURL = Private.AppURL();
                var downloadUri = new Uri(appURL);

                // Set the download file path to the parent directory
                string downloadPath = Path.Combine(parentDirectory, "HourSync.exe");

                try
                {
                    // Download the executable file
                    FileMgr.Log("Downloading.");
                    var fileBytes = await _client.GetByteArrayAsync(downloadUri);
                    await File.WriteAllBytesAsync(downloadPath, fileBytes);

                    // Specify the extraction path for the 7zip self-extractor
                    string extractPath = parentDirectory;

                    // Run the 7zip self-extractor with the specified extraction path
                    Process.Start(
                        new ProcessStartInfo
                        {
                            FileName = downloadPath,
                            Arguments = "/SILENT",
                            UseShellExecute = true,
                            CreateNoWindow = true,
                        }
                    );

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
            else
            {
                return false;
            }
        }
        else if (result == "error")
        {
            await dialogManager.ShowDialog("Update Check Error", "An error occurred while checking for updates.", "", "Okay", "", XamlRoot);
            return true;
        }

        return false;
    }

    [GeneratedRegex(@"^\d{3}[a-zA-Z]{3}(0[1-9]|[12][0-9]|3[01])$")]
    private static partial Regex UsernameRegex();
}
// ViewModel
public class LoginViewModel : INotifyPropertyChanged
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

        const string pattern = @"^\d{3}[a-zA-Z]{3}(0[1-9]|[12][0-9]|3[01])$";
        HasValidationError = !Regex.IsMatch(Username, pattern);
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

    // INotifyPropertyChanged implementation
    public event PropertyChangedEventHandler PropertyChanged;
    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}