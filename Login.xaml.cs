#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json.Linq;
using Windows.Security.Credentials;
using Windows.Storage;
using Windows.System;
using Windows.UI;
using Windows.UI.ViewManagement;

namespace HourSync;

public sealed partial class Login : Page
{
    private readonly string APPVERSIONNUMBER = "1.1.0";
    private readonly string appDataFolder = "HourSync";
    private readonly string logFilePath;
    private string phpSessionId;
    private string nameOfPerson;
    private string nameOfAcademy;
    private string username;
    private string password;

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
            Interval = TimeSpan.FromMilliseconds(250), // Check every 250 ms
        };
        _themeCheckTimer.Tick += (sender, e) => UpdateTheme();
        _themeCheckTimer.Start();

        // Initial theme check
        UpdateTheme();
        ApplyTheme(IsDarkTheme());

        logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            appDataFolder,
            "log.txt"
        );
        FileMgr.Log("----------\r\nLogging started for session " + DateTime.Now);

        ((App)Application.Current).UpdatePresence("login", "");

        string exePath = Assembly.GetEntryAssembly().Location;

        // Get the directory of the executable
        string exeDirectory = Path.GetDirectoryName(exePath);

        System.Diagnostics.Trace.WriteLine("Executable is located in: " + exeDirectory);
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        bool isFirstTime = (bool)e.Parameter;

        // Check if the credential thing works or not
        (string UserNameFromVault, string PassFromVault) = GetCreds();
        if (UserNameFromVault == null || PassFromVault == null)
        {
            FileMgr.Log("No credentials found in vault.");
            try
            {
                if (isFirstTime)
                {
                    Loaded += async (sender, e) =>
                    {
                        await Task.Delay(200);
                        var autoLoginEnabled = await FileMgr.GetSettingValueAsync(
                            "autologin.IsEnabled"
                        );

                        if (autoLoginEnabled.ToString() == "NOSETTINGSFILE")
                        {
                            FileMgr.Log("Welcome to HourSync! Downloading current settings.");
                            ContentDialog welcomeDialog = new()
                            {
                                Title = "Welcome!",
                                Content = "Welcome to HourSync! This app requires the internet to initialize for the first time (and also to even log in!), so if you haven't connected, please do so now, or exit the app and try again later.\n\nThis app is meant to provide a centralized and easy to use experience to manage your eHours. Experience a modern design, an accessible interface, draft saving, and completely optional AI features. Updates are sent through Google Firebase and Amazon Web Services, so please ensure those are unblocked (they should be on school laptops!).\n\nThe author of this app, Neil, wishes you enjoy this app and find it useful. Thanks!",
                                XamlRoot = XamlRoot,
                                PrimaryButtonText = "Download",
                                CloseButtonText = "Cancel",
                            };

                            var result = await welcomeDialog.ShowAsync();
                            if (result == ContentDialogResult.Primary)
                            {
                                FileMgr.Log("Downloading settings.json from Firestore.");
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

                        if (autoLoginEnabled.ToString().ToLower() == "true")
                        {
                            FileMgr.Log("AutoLogin is enabled. Logging in automatically.");
                            if (await CheckForUpdate())
                            {
                                return;
                            }
                            LoginButton_Click(null, null);
                        }
                        else
                        {
                            FileMgr.Log("AutoLogin is disabled.");
                        }
                    };
                }
            }
            catch(Exception ex)
            {
                FileMgr.LogError(ex.Message);
            }
            return;
        }
        if (PassFromVault.Length > 0)
        {
            FileMgr.Log(
                "Credentials successfully retrieved for user "
                    + UserNameFromVault
                    + ".\r\nLogging in for user."
            );
            UsernameTextBox.Text = UserNameFromVault;
            PasswordBox.Password = PassFromVault;

            FileMgr.Log(
                $"It is {(isFirstTime ? "the first time" : "not the first time")} logging in."
            );

            if (isFirstTime)
            {
                Loaded += async (sender, e) =>
                {
                    await Task.Delay(200);
                    var autoLoginEnabled = await FileMgr.GetSettingValueAsync(
                        "autologin.IsEnabled"
                    );

                    if (autoLoginEnabled.ToString() == "NOSETTINGSFILE")
                    {
                        FileMgr.Log("Welcome to HourSync! Downloading current settings.");
                        ContentDialog welcomeDialog = new()
                        {
                            Title = "Welcome!",
                            Content = "Welcome to HourSync! This app requires the internet to initialize for the first time (and also to even log in!), so if you haven't connected, please do so now, or exit the app and try again later.\n\nThis app is meant to provide a centralized and easy to use experience to manage your eHours. Experience a modern design, an accessible interface, draft saving, and completely optional AI features. Updates are sent through Google Firebase and Amazon Web Services, so please ensure those are unblocked (they should be on school laptops!).\n\nThe author of this app, Neil, wishes you enjoy this app and find it useful. Thanks!",
                            XamlRoot = XamlRoot,
                            PrimaryButtonText = "Download",
                            CloseButtonText = "Cancel",
                        };

                        var result = await welcomeDialog.ShowAsync();
                        if (result == ContentDialogResult.Primary)
                        {
                            FileMgr.Log("Downloading settings.json from Firestore.");
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

                    if (autoLoginEnabled.ToString().ToLower() == "true")
                    {
                        FileMgr.Log("AutoLogin is enabled. Logging in automatically.");
                        if (await CheckForUpdate())
                        {
                            return;
                        }
                        LoginButton_Click(null, null);
                    }
                    else
                    {
                        FileMgr.Log("AutoLogin is disabled.");
                    }
                };
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
                var resp = await _client.GetAsync(
                    "https://firestore.googleapis.com/v1/projects/hoursync-storage/databases/(default)/documents/settings/"
                );
                string json = await resp.Content.ReadAsStringAsync();
                // Parse and write the settings JSON
                ParseAndWriteSettingsJson(json);
                waitForLoginProgressBar.IsIndeterminate = false;
                waitForLoginProgressBar.Value = 100;
                waitForLogin.Content = waitForLoginProgressBar;
                waitForLogin.Title = "Download Complete";
                waitForLogin.CloseButtonText = "OK";
            } catch(Exception ex)
            {
                FileMgr.LogError("Error when downloading settings: "+ex.Message);
            }
        }
        else
        {
            FileMgr.Log("Internet connection is not available. Cannot download settings.json.");
            waitForLoginProgressBar.ShowError = true;
            waitForLogin.Content = waitForLoginProgressBar;
            waitForLogin.Title = "No Internet";
            waitForLogin.CloseButtonText = "OK";
        }
    }

    private static void ParseAndWriteSettingsJson(string rawJson)
    {
        using JsonDocument doc = JsonDocument.Parse(rawJson);
        var selectSettings = new Dictionary<string, object>();
        var otherSettings = new Dictionary<string, object>();

        foreach (var docElement in doc.RootElement.GetProperty("documents").EnumerateArray())
        {
            var fields = docElement.GetProperty("fields");
            var key = fields.GetProperty("key").GetProperty("stringValue").GetString();
            var type = fields.GetProperty("type").GetProperty("stringValue").GetString();

            var setting = new Dictionary<string, object>
            {
                ["Key"] = key,
                ["Title"] = fields.GetProperty("title").GetProperty("stringValue").GetString(),
                ["Description"] = fields.GetProperty("description").GetProperty("stringValue").GetString(),
                ["Type"] = type,
                ["IsEnabled"] = true,
                ["DefaultValue"] = fields.TryGetProperty("defaultValue", out var defValProp) && defValProp.TryGetProperty("booleanValue", out var boolVal)
                        ? boolVal.GetBoolean()
                        : (object?)null,
                ["Options"] = null,
                ["SelectedValue"] = null,
            };

            if (fields.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Object)
            {
                if (optionsProp.TryGetProperty("arrayValue", out var arrayVal) && arrayVal.TryGetProperty("values", out var valuesArray))
                {
                    var opts = new List<Dictionary<string, string>>();
                    foreach (var option in valuesArray.EnumerateArray())
                    {
                        var fieldsMap = option.GetProperty("mapValue").GetProperty("fields");

                        var friendlyName = fieldsMap.GetProperty("FriendlyName").GetProperty("stringValue").GetString();
                        var keyName = fieldsMap.TryGetProperty("Key", out var k)
                                        ? k.GetProperty("stringValue").GetString()
                                      : fieldsMap.TryGetProperty("key", out var k2)
                                        ? k2.GetProperty("stringValue").GetString()
                                      : null;

                        opts.Add(new Dictionary<string, string>
                        {
                            ["FriendlyName"] = friendlyName,
                            ["Key"] = keyName,
                        });
                    }

                    setting["Options"] = opts;
                    if (opts.Count > 0)
                        setting["SelectedValue"] = opts[0];
                }
            }

            if (type == "select")
                selectSettings[key] = setting;
            else
                otherSettings[key] = setting;
        }

        var finalSettings = new Dictionary<string, object>();
        foreach (var kvp in selectSettings)
            finalSettings[kvp.Key] = kvp.Value;
        foreach (var kvp in otherSettings)
            finalSettings[kvp.Key] = kvp.Value;

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver() // Enable reflection-based serialization
        };

        string finalJson = JsonSerializer.Serialize(finalSettings, options);
        FileMgr.WriteToFile("settings.json", finalJson);
    }

    public static (string userName, string password) GetCreds()
    {
        PasswordCredential credential = null;
        var vault = new PasswordVault();

        try
        {
            // Retrieve all credentials from the vault
            IReadOnlyList<PasswordCredential> allCredentials = vault.RetrieveAll();

            // Filter credentials for the resource "HourSync"
            List<PasswordCredential> credentialList = [];
            foreach (var cred in allCredentials)
            {
                if (cred.Resource == "HourSync")
                {
                    credentialList.Add(cred);
                }
            }

            if (credentialList.Count > 0)
            {
                // Use the first available credential
                credential = credentialList[0];
            }
        }
        catch (Exception ex)
        {
            // Log any exceptions
            FileMgr.Log("Exception occurred: " + ex.Message);
            return (null, null);
        }

        if (credential != null)
        {
            credential.RetrievePassword();
            return (credential.UserName, credential.Password);
        }
        else
        {
            // Handle the case when no credentials are available
            FileMgr.Log("Null.");
            return (null, null);
        }
    }

    private void UpdateTheme()
    {
        bool isDarkTheme = IsDarkTheme();
        if (isDarkTheme != _currentThemeIsDark)
        {
            _currentThemeIsDark = isDarkTheme;
            ApplyTheme(isDarkTheme);
        }

        if (UsernameTextBox.Text.Length < 1)
        {
            UsernameTextBox.BorderBrush = null;
            UsernameTextBox.BorderThickness = new Thickness(0);
            return;
        }

        string pattern = @"^\d{3}[a-zA-Z]{3}(0[1-9]|[12][0-9]|3[01])$";

        if (!Regex.IsMatch(UsernameTextBox.Text, pattern))
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
                XamlRoot = XamlRoot,
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
            Content =
                "Working on a school computer? Press CTRL + Alt + Delete and click \"Change a Password.\"\r\nOtherwise, go to \"https://accounts.microsoft.com\", enter your username, and click \"Forgot Password.\"",
            PrimaryButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private bool isShiftPressedInUsernameBox = false;

    private async void UsernameTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
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

    private void UsernameTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
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

        string pattern = @"^\d{3}[a-zA-Z]{3}(0[1-9]|[12][0-9]|3[01])$";

        if (!Regex.IsMatch(UsernameTextBox.Text, pattern))
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

    private void PasswordBox_KeyDown(object sender, KeyRoutedEventArgs e)
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

    private async void ShowLoginProgressBarAsync()
    {
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
            nameOfAcademy = resp.Split(
                    new string[] { "<h2>Welcome to your " },
                    StringSplitOptions.None
                )[1]
                .Split(new string[] { " Endorsement" }, StringSplitOptions.None)[0];
            nameOfPerson = resp.Split(new string[] { "Tracking, " }, StringSplitOptions.None)[1]
                .Split(new string[] { "</h2>" }, StringSplitOptions.None)[0];

            FileMgr.Log("Getting home page");
            var getresp = await Get(
                "https://academyendorsement.olatheschools.com/Student/studentEHours.php"
            );
            FileMgr.Log("Successfully got home");

            // Remove previous credentials and add new ones
            var vault = new PasswordVault();
            try
            {
                // Retrieve all credentials from the vault
                IReadOnlyList<PasswordCredential> allCredentials = vault.RetrieveAll();

                foreach (var cred in allCredentials)
                {
                    if (cred.Resource == "HourSync")
                    {
                        vault.Remove(cred);
                    }
                }
            }
            catch (Exception)
            {
                // Handle exception appropriately
            }
            vault.Add(new PasswordCredential("HourSync", username, password));
            FileMgr.Log("Updated credentials in vault");

            FileMgr.Log("Navigating to Home");
            waitForLogin.Hide();
            Frame.Navigate(
                typeof(Home),
                new object[]
                {
                    username,
                    password,
                    phpSessionId,
                    nameOfPerson,
                    nameOfAcademy,
                    getresp,
                    _cookieContainer,
                    _handler,
                    _client,
                },
                new DrillInNavigationTransitionInfo()
            );

            // Notify the App instance about successful login
            FileMgr.Log("Running App.LoggedIn");
            ((App)Application.Current).LoggedIn(
                username,
                password,
                phpSessionId,
                nameOfPerson,
                nameOfAcademy,
                getresp,
                _cookieContainer,
                _handler,
                _client
            );
        }
        else
        {
            waitForLoginProgressBar.ShowError = true;
            waitForLogin.Content = waitForLoginProgressBar;
            waitForLogin.Title = "Incorrect username or password.";
            waitForLogin.CloseButtonText = "OK";
        }
    }

    private async Task<string> Post()
    {
        var values = new Dictionary<string, string>
        {
            { "uName", username },
            { "uPass", password },
        };

        var content = new FormUrlEncodedContent(values);

        var response = await _client.PostAsync(
            "https://academyendorsement.olatheschools.com/loginuserstudent.php",
            content
        );
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
                CloseButtonText = "OK",
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

    private async Task<bool> CheckForUpdate()
    {
        var resp = await _client.GetAsync(
            "https://firestore.googleapis.com/v1/projects/hoursync-storage/databases/(default)/documents/version/num"
        );
        string json = await resp.Content.ReadAsStringAsync();

        var obj = JObject.Parse(json);
        string latestVersion = (string)obj["fields"]?["version"]?["stringValue"];

        string result = IsNewerVersion(latestVersion, APPVERSIONNUMBER);

        if (result == "true")
        {
            // Display the content dialog if version is greater than currentVersion
            var contentDialog = new ContentDialog
            {
                Title = "New Update Available",
                Content = "A newer version is available. Would you like to update?",
                CloseButtonText = "Later",
                PrimaryButtonText = "Update",
                XamlRoot = XamlRoot,
            };
            var clicked = await contentDialog.ShowAsync();
            if (clicked == ContentDialogResult.Primary)
            {
                ShowLoginProgressBarAsync();

                // Get the directory where the app's executable is located
                string appExeDirectory = Path.GetDirectoryName(
                    Assembly.GetEntryAssembly().Location
                );
                string parentDirectory = Directory.GetParent(appExeDirectory).FullName;

                // Download the EXE file to the parent directory
                var downloadUri = new Uri(
                    "https://hoursync.s3.us-east-2.amazonaws.com/HourSync.exe"
                );

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
                            Arguments = $"-o\"{extractPath}\" -y", // Specify extraction path
                            UseShellExecute = true,
                            CreateNoWindow = true,
                        }
                    );

                    FileMgr.Log("Handing it off to 7zip.");

                    Application.Current.Exit();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An error occurred: {ex.Message}");
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
            var contentDialog = new ContentDialog
            {
                Title = "Update Check Error",
                Content = "An error occurred while checking for updates.",
                PrimaryButtonText = "Okay",
                XamlRoot = XamlRoot,
            };
            await contentDialog.ShowAsync();
            return true;
        }

        return false;
    }

    private static string IsNewerVersion(string fetchedVersion, string currentVersion)
    {
        // Handle empty or null version strings
        if (string.IsNullOrWhiteSpace(fetchedVersion) || fetchedVersion.Split('.').Length != 3)
        {
            return "error";
        }

        var fetchedVersionParts = fetchedVersion.Split('.');
        var currentVersionParts = currentVersion.Split('.');

        // Check if both version parts have exactly 3 elements (major, minor, patch)
        if (fetchedVersionParts.Length != 3 || currentVersionParts.Length != 3)
        {
            return "error";
        }

        // Loop through each version part
        for (int i = 0; i < 3; i++)
        {
            try
            {
                int fetchedPart = int.Parse(fetchedVersionParts[i]);
                int currentPart = int.Parse(currentVersionParts[i]);

                if (fetchedPart > currentPart)
                {
                    return "true"; // Newer version
                }
                else if (fetchedPart < currentPart)
                {
                    return "false"; // Current version is newer or equal
                }
            }
            catch (FormatException)
            {
                return "error"; // Return error if there is a format issue in version parsing
            }
        }

        return "false"; // Both versions are the same
    }
}
