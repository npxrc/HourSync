#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HourSyncCoreLib;
using ImageMagick;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;
using System.Globalization;

namespace HourSync;

public sealed partial class RequestMaker : Page
{
    private List<string> selectedImages = [];
    private LoginResult loginResult;
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

    private DialogService dialogManager = new();

    private int logInAgainAttempts = 0;

    public ObservableCollection<ImageDisplayItem> ImageDisplayItems { get; } = [];

    private bool isAiEnabled = false;

    private ProgressBar conversionProgressBar;
    private ContentDialog conversionDialog;
    private bool isConversionActive = false;

    public RequestMaker()
    {
        InitializeComponent();
        Loaded += loaded;

        eventTitle.LostFocus += (_, __) => UpdatePresence();
        NumericTextBox.LostFocus += (_, __) => UpdatePresence();

        ((App)Application.Current).m_window.SizeChanged += (_, e) => OnWindowSizeChanged(e.Size.Width);
    }

#pragma warning disable IDE1006 // Naming Styles
    private async void loaded(object _, RoutedEventArgs __)
#pragma warning restore IDE1006 // Naming Styles
    {
        // Disable dev tools and context menu
        webView.CoreWebView2Initialized += (_, __) =>
        {
            // Turn off dev tools
            webView.CoreWebView2.Settings.AreDevToolsEnabled = false;

            // Turn off built-in context menus
            webView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
        };

        // Ensure CoreWebView2 is created
        await webView.EnsureCoreWebView2Async();

        var mainWindow = ((App)Application.Current).m_window as MainWindow;

        // Load all settings
        var settings = FileMgr.LoadSettings();

        if (settings.TryGetValue("aiEnabled", out var aiEnabledSetting))
        {
            if (!aiEnabledSetting.IsEnabled)
            {
                webView.Visibility = Visibility.Collapsed;
                webView.Close();

                if (mainWindow != null)
                {
                    OnWindowSizeChanged(mainWindow.WindowSize.Width);
                }

                return;
            }
        }
        if (mainWindow != null)
        {
            OnWindowSizeChanged(mainWindow.WindowSize.Width);
        }

        isAiEnabled = true;

        // Try to get the aiCompanion setting
        if (settings.TryGetValue("aiCompanion", out var aiCompanionSetting))
        {
            // Get the SelectedValue (Option object)
            Option selectedValue = aiCompanionSetting.SelectedValue;

            if (selectedValue != null)
            {
                FileMgr.Log($"aiCompanion SelectedValue: {selectedValue.Key} ({selectedValue.FriendlyName})");

                // Define valid keys and corresponding URLs
                var aiUrls = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "chatgpt", "https://chatgpt.com" },
                    { "gemini", "https://gemini.google.com" },
                    { "claude", "https://claude.ai" },
                    { "deepseek", "https://chat.deepseek.com" },
                    { "copilot", "https://copilot.microsoft.com" },
                    { "duck", "https://duck.ai" },
                    { "perplexity", "https://www.perplexity.ai" },
                    { "grok", "https://grok.com" }
                };

                if (aiUrls.TryGetValue(selectedValue.Key, out var url))
                {
                    webView.Source = new Uri(url);
                    FileMgr.Log($"Navigating webView to {url}");
                }
                else
                {
                    FileMgr.Log($"Unrecognized AI companion key: {selectedValue.Key}");
                }
            }
        }
    }

    private void UpdatePresence()
    {
        FileMgr.Log("Updating presence");
        string anyHours = "to log";
        try
        {
            if (NumericTextBox?.Text.Length > 0)
            {
                anyHours = $"{NumericTextBox.Text} hours for";
            }

            if (eventTitle?.Text.Length > 0)
            {
                FileMgr.Log("Setting presence to `" + $"Requesting {anyHours} \"{eventTitle.Text}\" `");
                ((App)Application.Current).UpdatePresence(
                    "create",
                    $"Requesting {anyHours} \"{eventTitle.Text}\" "
                );
            }
            else
            {
                FileMgr.Log("Setting to default.");
                ((App)Application.Current).UpdatePresence("create", "");
            }
        }
        catch (Exception)
        {
            System.Diagnostics.Trace.WriteLine("Error in UpdatePresence()");
        }
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is object[] parameters)
        {
            // Check the number of parameters
            if (parameters.Length >= 4)
            {
                loginResult = (LoginResult)parameters[0];
                username = parameters[1] as string;
                password = parameters[2] as string;
            }
            else
            {
                // Handle the case where parameters are missing or incorrect
                throw new ArgumentException(
                    "Incorrect number of parameters passed to RequestMaker page. Parameters Length was "
                        + parameters.Length
                );
            }
        }
        else
        {
            // Handle the case where parameters are not in the expected format
            throw new ArgumentException(
                "Parameters passed to RequestMaker page are not in the expected format."
            );
        }
        eventTitle.KeyUp += KeyUp_SaveDraft;
        eventDate.SelectedDateChanged += (sender, __) => KeyUp_SaveDraft(sender, null);
        eventBody.KeyUp += KeyUp_SaveDraft;
        Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object _, RoutedEventArgs __)
    {
        Loaded -= OnPageLoaded;
        bool res = AutoLoadDraft();
        if (res)
        {
            SaveDraft();
        }
    }

    //Button clicks
    private async void ImageUpload_Click(object _, RoutedEventArgs __)
    {
        // Create and initialize the picker
        var openPicker = new FileOpenPicker();

        // Get the current window's HWND
        var app = (App)Application.Current;

        // Make sure to use the correct HWND of the active window
        var hwnd = WindowNative.GetWindowHandle(app.m_window);

        // Associate the HWND with the file picker
        WinRT.Interop.InitializeWithWindow.Initialize(openPicker, hwnd);

        openPicker.ViewMode = PickerViewMode.Thumbnail;
        openPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;

        // Add file types to filter
        openPicker.FileTypeFilter.Add(".jpg");
        openPicker.FileTypeFilter.Add(".jpeg");
        openPicker.FileTypeFilter.Add(".png");
        openPicker.FileTypeFilter.Add(".heic");

        // Open the picker and get the files
        var files = await openPicker.PickMultipleFilesAsync();

        if (files.Count > 0)
        {
            var heicFiles = new List<StorageFile>();
            foreach (StorageFile file in files)
            {
                if (string.Equals(Path.GetExtension(file.Name), ".heic", StringComparison.OrdinalIgnoreCase))
                {
                    heicFiles.Add(file);
                }
                else
                {
                    selectedImages.Add(file.Path);

                    // Add to display collection
                    var displayItem = new ImageDisplayItem
                    {
                        ImagePath = file.Path,
                        FileName = file.Name
                    };
                    ImageDisplayItems.Add(displayItem);
                }
            }

            // Prompt for HEIC conversion if any exist
            if (heicFiles.Count > 0)
            {
                string fileList = string.Join("\n", heicFiles.Select(f => f.Name));
                ContentDialogResult result = await dialogManager.ShowDialog(
                    LSGS("HEICTitle"),
                    LocalizationService.PrepareStatement("HEICConvertContent", fileList),
                    LSGS("No"),
                    LSGS("Yes"),
                    "",
                    XamlRoot
                );

                if (result == ContentDialogResult.Primary) // Yes
                {
                    ShowSubmissionProgressBar();
                    await ConvertHeicFiles(heicFiles);
                }
            }

            UpdateImageUI();
            SaveDraft(); // Save draft when images are added
            HideSubmissionProgressBar();
        }
    }
    private void RemoveImage_Click(object sender, RoutedEventArgs __)
    {
        if (sender is Button button && button.Tag is string imagePath)
        {
            // Remove from selectedImages list
            selectedImages.Remove(imagePath);

            // Remove from display collection
            var itemToRemove = ImageDisplayItems.FirstOrDefault(item => item.ImagePath == imagePath);
            if (itemToRemove != null)
            {
                ImageDisplayItems.Remove(itemToRemove);
            }

            UpdateImageUI();
            SaveDraft(); // Save draft when images are removed
        }
    }
    private async void Image_Tapped(object sender, Microsoft.UI.Xaml.Input.TappedRoutedEventArgs e)
    {
        if (sender is Image image && image.DataContext is ImageDisplayItem item)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(item.ImagePath);
                await Launcher.LaunchFileAsync(file);
            }
            catch (Exception ex)
            {
                await dialogManager.ShowDialog("Error", $"Failed to open image: {ex.Message}", "OK", "", "", XamlRoot);
            }
        }
    }

    private void UpdateImageUI()
    {
        if (selectedImages.Count > 0)
        {
            var stringOfFileNames = $"{LSGS("SelectedFiles")}: {string.Join(", ", selectedImages.Select(Path.GetFileName))}";
            filesSelectedTextBlock.Text = stringOfFileNames;
            ImagePreviewSection.Visibility = Visibility.Visible;
        }
        else
        {
            filesSelectedTextBlock.Text = LSGS("RequestMakerFilesSelectedText.Text");
            ImagePreviewSection.Visibility = Visibility.Collapsed;
        }
    }

    private async void SubmitButton_Click(object _, RoutedEventArgs __)
    {
        ContentDialogResult result = await dialogManager.ShowDialog(LSGS("GenericConfirmTitle"), LSGS("RequestMakerReadySubmit"), LSGS("RequestPageCancelBtn.Content"), LSGS("RequestMakerSubmitButton.Content"), "", XamlRoot);

        // Handle the result
        if (result == ContentDialogResult.Primary)
        {
            // User clicked Yes
            // Get the raw event title text
            string rawText = eventTitle.Text;

            // Define a list of invalid characters for file names
            char[] invalidChars = Path.GetInvalidFileNameChars();

            // Remove invalid characters from the event title
            string cleanedText = new string([.. rawText.Where(c => !invalidChars.Contains(c))]);

            SaveDraft("drafts/" + cleanedText + ".json");

            await MakeRequestAsync(
                eventTitle.Text,
                eventDate.Date.ToString(),
                NumericTextBox.Text,
                eventBody.Text
            );

            eventTitle.Text = "";
            eventDate.SelectedDate = null;
            NumericTextBox.Text = "0";
            eventBody.Text = "";
            SaveDraft();
        }
    }

    private async void ClearButton_Click(object _, RoutedEventArgs __)
    {
        ContentDialogResult result = await dialogManager.ShowDialog("Confirm", "Are you sure you want to clear the form?", "Cancel", "Continue", "", XamlRoot);

        if (result == ContentDialogResult.Primary)
        {
            eventTitle.Text = "";
            eventDate.SelectedDate = null;
            NumericTextBox.Text = "0";
            eventBody.Text = "";
            DraftDeletedSuccessfully.Visibility = Visibility.Visible;

            // Clear images
            selectedImages.Clear();
            ImageDisplayItems.Clear();
            UpdateImageUI();

            FileMgr.DeleteFile("draft.json");
            AutoLoadDraft();
        }
    }

    private async void OpenDraft_Click(object _, RoutedEventArgs __)
    {
        var localAppDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        var dataPath = Path.Combine(localAppDataPath, "HourSync");
        var filePath = Path.Combine(dataPath, "draft.json");
        try
        {
            Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await dialogManager.ShowDialog("Open Draft Error", "An error occurred when opening the draft. Try again later.", "OK", "", "", XamlRoot);
            FileMgr.Log(
                "An exception occurred at "
                    + DateTime.Now
                    + " when opening the draft. Exception: "
                    + ex.Message
            );
        }
    }

    private ProgressBar submissionProgressBar;
    private ContentDialog submissionDialog;
    private bool isSubmissionActive = false;

    private void ShowSubmissionProgressBar()
    {
        if (isSubmissionActive)
            return;

        isSubmissionActive = true;

        submissionProgressBar = new ProgressBar
        {
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 200,
            Height = 20
        };

        submissionDialog = new ContentDialog
        {
            Title = LSGS("GenericLoadingText"),
            CloseButtonText = null,
            PrimaryButtonText = null,
            Content = submissionProgressBar,
            XamlRoot = XamlRoot
        };

        if (!DialogManager.IsDialogOpen)
        {
            _ = submissionDialog.ShowAsync();
        }

        submissionDialog.Closed += (_, _) => isSubmissionActive = false;
    }

    private void HideSubmissionProgressBar()
    {
        if (submissionDialog != null && isSubmissionActive)
        {
            submissionDialog.Hide();
            isSubmissionActive = false;
        }
    }

    private string PushRequestResult = "";
    /// <summary>
    /// Pushes the request to the server. If the response indicates that the session has expired or is invalid, it will attempt to log in again and retry the request. It will retry up to 3 times before returning an error.
    /// </summary>
    /// <param name="title">The title of the request</param>
    /// <param name="date">The date of the request</param>
    /// <param name="hours">The number of hours requested</param>
    /// <param name="desc">The description of the request</param>
    /// <returns>1 when the request is successful, -1 when too many login attempts are made, -2 when the credentials are invalid</returns>
    private async Task<int> PushRequestAsync(string title, string date, string hours, string desc, bool includeImages)
    {
        string formattedDate = DateTime.Parse(date).ToString("yyyy-MM-dd");
        var content = CreateMultipartFormDataContent(title, formattedDate, hours, desc, includeImages);

        Uri uri = new("https://academyendorsement.olatheschools.com/");
        _cookieContainer.Add(uri, new Cookie("PHPSESSID", loginResult.PhpSessionId));

        FileMgr.Log(loginResult.PhpSessionId);

        if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _client.DefaultRequestHeaders.Add(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
            );
        }

        var response = await _client.PostAsync(
            "https://academyendorsement.olatheschools.com/Student/makeRequest.php",
            content
        );
        var responseString = await response.Content.ReadAsStringAsync();

        if (responseString.Contains("See your current eHours"))
        {
            PushRequestResult = responseString;
            return 1;
        }
        else
        {
            FileMgr.Log("Null response, logging in again.");
            if (logInAgainAttempts >= 3)
            {
                FileMgr.LogError("Too many log in attempts reached, check the code.");
                return -1;
            }
            bool isLoggedInAgain = await LogInAgain();
            logInAgainAttempts++;
            if (isLoggedInAgain)
            {
                return await PushRequestAsync(title, date, hours, desc, includeImages);
            }
            else
            {
                return -2;
            }
        }
    }

    public enum TitleStyle
    {
        Number,
        Part,
        Brackets,
        Fraction
    }

    public enum SplitStyle
    {
        Evenly,
        Fill,
        MaxLimit
    }

    public enum ImageMode
    {
        FirstOnly,
        All
    }

    public class MultiRequestSettings
    {
        public string ExampleTitle = "My Request";
        public double ExampleHours = 250;
        public string ExampleBody = "This is an example description for the request. It can be quite long and detailed, providing all necessary information about the request being made.";
        public TitleStyle TitleStyle
        {
            get; set;
        } = TitleStyle.Number;
        public bool AppendTitleToDescription
        {
            get; set;
        } = false;

        public SplitStyle SplitStyle
        {
            get; set;
        } = SplitStyle.Fill;
        public double MaxHoursPerRequest
        {
            get; set;
        } = 99.75;

        public ImageMode ImageMode
        {
            get; set;
        } = ImageMode.FirstOnly;
    }
    public class GeneratedRequest
    {
        public string Title
        {
            get; set;
        }

        public string Body
        {
            get; set;
        }

        public double Hours
        {
            get; set;
        }

        public bool IncludeImages
        {
            get; set;
        }
    }
    /// <summary>
    /// Splits a total number of hours into discrete chunks based on a maximum limit and split strategy.
    /// </summary>
    /// <param name="totalHours">The total hours to distribute.</param>
    /// <param name="splitStyle">The strategy used to split the hours (Evenly, Fill, or MaxLimit).</param>
    /// <param name="customMaxHours">The custom limit when using SplitStyle.MaxLimit.</param>
    /// <returns>A list of rounded hour amounts that sum up exactly to totalHours.</returns>
    public static List<double> CalculateHourSplits(
        double totalHours,
        SplitStyle splitStyle,
        double customMaxHours = 99.75
    )
    {
        List<double> splits = new();

        if (totalHours <= 0)
        {
            return splits;
        }

        const double HardLimit = 99.75;

        // Determine the active cap for calculations
        double activeLimit = splitStyle == SplitStyle.MaxLimit
            ? Math.Min(customMaxHours, HardLimit)
            : HardLimit;

        int totalRequests = (int)Math.Ceiling(totalHours / activeLimit);
        double allocatedHours = 0;

        for (int i = 0; i < totalRequests; i++)
        {
            double reqHours;

            // Final chunk absorbs any rounding differences to ensure total exactness
            if (i == totalRequests - 1)
            {
                reqHours = Math.Round(totalHours - allocatedHours, 2, MidpointRounding.AwayFromZero);
            }
            else if (splitStyle == SplitStyle.Evenly)
            {
                reqHours = Math.Round(totalHours / totalRequests, 2, MidpointRounding.AwayFromZero);
            }
            else
            {
                reqHours = Math.Round(Math.Min(activeLimit, totalHours - allocatedHours), 2, MidpointRounding.AwayFromZero);
            }

            splits.Add(reqHours);
            allocatedHours += reqHours;
        }

        return splits;
    }
    public static List<GeneratedRequest> GenerateRequests(
        string title,
        string body,
        double totalHours,
        MultiRequestSettings settings
    )
    {
        List<GeneratedRequest> toReturn = new();

        // Calculate the splits using the helper method
        List<double> hourSplits = CalculateHourSplits(
            totalHours,
            settings.SplitStyle,
            settings.MaxHoursPerRequest
        );

        int totalRequests = hourSplits.Count;

        for (int i = 0; i < totalRequests; i++)
        {
            string reqTitle = title;
            switch (settings.TitleStyle)
            {
                case TitleStyle.Number:
                    reqTitle = $"{title} - {i + 1}";
                    break;
                case TitleStyle.Part:
                    reqTitle = $"{title} - Part {i + 1}";
                    break;
                case TitleStyle.Brackets:
                    reqTitle = $"{title} [{i + 1}]";
                    break;
                case TitleStyle.Fraction:
                    reqTitle = $"{title} - {i + 1}/{totalRequests}";
                    break;
            }

            string reqBody = body;
            if (settings.AppendTitleToDescription)
            {
                reqBody = $"{reqTitle}\n\n{body}";
            }

            toReturn.Add(new GeneratedRequest
            {
                Title = reqTitle,
                Body = reqBody,
                Hours = hourSplits[i],
                IncludeImages = settings.ImageMode == ImageMode.All || (settings.ImageMode == ImageMode.FirstOnly && i == 0)
            });
        }

        return toReturn;
    }

    //POST request
    private MultiRequestControl mrw = null;
    private ContentDialog mrwDialog = null;
    private async Task MakeRequestAsync(string title, string date, string hours, string desc)
    {
        SubmitButton.IsEnabled = false;

        try
        {
            if (!double.TryParse(hours, NumberStyles.Float, CultureInfo.InvariantCulture, out double totalHours))
            {
                totalHours = double.Parse(hours);
            }

            if (totalHours > 99.75)
            {
                var submitMoreResponse = await dialogManager.ShowDialog("Submitting more than 99.75 eHours", "Submitting requests more than 99.75 eHours is not officially supported. HourSync will attempt to make multiple requests to fulfill the entire " + hours + " eHours.\r\n\r\nMake sure to obtain permission to submit your request for more than 99.75 eHours from your " + loginResult.StudentAcademy + " teacher before submitting.\r\n\r\nClick cancel to stop.", "", "OK", "Cancel", XamlRoot);
                if (submitMoreResponse == ContentDialogResult.Primary)
                {
                    mrw = new(title, totalHours, desc, RequestsGenerated);
                    mrwDialog = new()
                    {
                        Content = mrw,
                        XamlRoot = XamlRoot
                    };
                    await mrwDialog.ShowAsync();

                    async void RequestsGenerated()
                    {
                        mrwDialog.Hide();
                        if (mrw.GeneratedRequests.Count == 0)
                        {
                            return;
                        }

                        bool allsuccessful = true;
                        ShowSubmissionProgressBar();
                        foreach (var req in mrw.GeneratedRequests)
                        {
                            int res = await PushRequestAsync(req.Title, date, $"{req.Hours}", desc, req.IncludeImages);
                            if (res != 1)
                            {
                                HideSubmissionProgressBar();
                                if (res == -1)
                                {
                                    // Too many login attempts
                                    await dialogManager.ShowErrorDialog("Too many login attempts. Please try again later.", false, XamlRoot);
                                    allsuccessful = false;
                                    return;
                                }
                                else if (res == -2)
                                {
                                    // Invalid credentials
                                    await dialogManager.ShowDialog("Incorrect Credentials", $"Your credentials for the user {username} are incorrect. Your progress has been saved. Please log in again.", "OK", "", "", XamlRoot);
                                    Frame.Navigate(
                                        typeof(Login),
                                        new object[]
                                        {
                                            false
                                        }
                                    );
                                    allsuccessful = false;
                                    return;
                                }
                            }
                            logInAgainAttempts = 0; // Reset login attempts after a successful request
                        }
                        if (allsuccessful)
                        {
                            await dialogManager.ShowDialog("Success", $"{title} was fully submitted across multiple requests.", "OK", "", "", XamlRoot);

                            FileMgr.DeleteFile("draft.json");
                            ((App)Application.Current).UpdateHomeContent(PushRequestResult);
                            Frame.Navigate(typeof(Home), new object[] { loginResult, username, password, PushRequestResult });
                        }
                    }
                }
            }
            else
            {
                try
                {
                    ShowSubmissionProgressBar();
                    int result = await PushRequestAsync(title, date, hours, desc, true);
                    HideSubmissionProgressBar();
                    if (result == 1)
                    {
                        // Request submitted successfully
                        await dialogManager.ShowDialog("Success", $"{title} was just submitted for {hours} eHours", "OK", "", "", XamlRoot);
                        UpdateUIAfterSubmission();

                        FileMgr.DeleteFile("draft.json");
                        ((App)Application.Current).UpdateHomeContent(PushRequestResult);
                        Frame.Navigate(
                            typeof(Home),
                            new object[]
                            {
                                loginResult,
                                username,
                                password,
                                PushRequestResult
                            }
                        );
                    }
                    else if (result == -1)
                    {
                        // Too many login attempts
                        await dialogManager.ShowErrorDialog("Too many login attempts. Please try again later.", false, XamlRoot);
                    }
                    else if (result == -2)
                    {
                        // Invalid credentials
                        await dialogManager.ShowDialog("Incorrect Credentials", $"Your credentials for the user {username} are incorrect. Your progress has been saved. Please log in again.", "OK", "", "", XamlRoot);
                        Frame.Navigate(
                            typeof(Login),
                            new object[]
                            {
                                false
                            }
                        );
                    }
                }
                catch (Exception ex)
                {
                    FileMgr.LogError(ex.Message);
                    await dialogManager.ShowDialog("Error", $"An error occurred when submitting {title}.", "OK", "", "", XamlRoot);
                }
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError(ex.Message);
            HideSubmissionProgressBar();
            try
            {
                await dialogManager.ShowErrorDialog($"An error occurred when submitting {title}.", false, XamlRoot);
            }
            catch (Exception ex2)
            {
                FileMgr.LogError(ex2.Message);
            }
        }
        finally
        {
            HideSubmissionProgressBar();
            SubmitButton.IsEnabled = true;
        }
    }

    private void UpdateUIAfterSubmission()
    {
        // Clear form fields
        eventTitle.Text = "";
        eventDate.SelectedDate = null;
        NumericTextBox.Text = "0";
        eventBody.Text = "";

        // Clear images
        selectedImages.Clear();
        ImageDisplayItems.Clear();
        UpdateImageUI();

        FileMgr.Log("done");

        // Save the empty draft
        SaveDraft();
    }

    private MultipartFormDataContent CreateMultipartFormDataContent(
        string title,
        string formattedDate,
        string hours,
        string desc,
        bool includeImages = true
    )
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent(formattedDate), "activityDate" },
            { new StringContent(hours), "hours" },
            { new StringContent(desc), "description" },
        };
        if (includeImages)
        {
            foreach (var imagePath in selectedImages)
            {
                var imageContent = new ByteArrayContent(File.ReadAllBytes(imagePath))
                {
                    Headers =
                {
                    ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(GetImageMimeType(imagePath)),
                },
                };
                content.Add(imageContent, "files[]", Path.GetFileName(imagePath));
            }
        }

        return content;
    }

    private static string GetImageMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".heic" => "image/heic",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/jpeg" // Default fallback
        };
    }

    //Log in again
    private async Task<bool> LogInAgain()
    {
        FileMgr.Log("Attempting login via HourSyncCore");
        loginResult = await HourSyncCore.Login(username, password);
        if (string.IsNullOrEmpty(loginResult.Error) || !string.IsNullOrEmpty(loginResult.PhpSessionId))
        {
            var homeResult = await HourSyncCore.GetRequestsPage(loginResult.PhpSessionId);
            ((App)Application.Current).LoggedIn(loginResult, username, password, homeResult, false);
            return true;
        }
        else
        {
            return false;
        }
    }

    //Drafts
    private void KeyUp_SaveDraft(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) =>
        SaveDraft();

    //number only textbox
    private void NumericTextBox_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        // Get the current text in the TextBox
        string currentText = sender.Text;

        // Check if the text is valid numeric
        if (!IsNumeric(currentText))
        {
            // Remove the last character if it's not numeric
            sender.TextChanging -= NumericTextBox_TextChanging; // Temporarily detach the event
            sender.Text = RemoveLastCharacter(currentText);
            sender.SelectionStart = sender.Text.Length; // Set cursor to the end
            sender.TextChanging += NumericTextBox_TextChanging; // Reattach the event
        }
    }

    private static bool IsNumeric(string text)
    {
        // Check if the text is numeric
        return double.TryParse(text, out _);
    }

    private static string RemoveLastCharacter(string text)
    {
        return text.Length > 0 ? text[..^1] : text;
    }

    private async void Delete_Draft(object _, RoutedEventArgs __)
    {
        ContentDialogResult result = await dialogManager.ShowDialog("Confirm", "Are you sure you want to delete the draft?", "No", "Yes", "Open Draft", XamlRoot);
        if (result == ContentDialogResult.Primary)
        {
            FileMgr.DeleteFile("draft.json");
            DraftIsCorruptStack.Visibility = Visibility.Collapsed;
            DraftDeletedSuccessfully.Visibility = Visibility.Visible;
            _ = AutoLoadDraft();
        }
        else if (result == ContentDialogResult.Secondary)
        {
            OpenDraft_Click(null, null);
        }
    }

    private void SaveDraft(string filename = "")
    {
        if (filename?.Length == 0)
        {
            filename = eventTitle.Text.ToLower();
            filename = filename.Replace(" ", "-");
            filename = Utils.ToSafeFilename(filename);
            filename += ".json";
        }

        DraftManager.SaveDraft(
            eventTitle.Text,
            eventDate.SelectedDate ?? DateTimeOffset.Now,
            NumericTextBox.Text,
            eventBody.Text,
            selectedImages,
            "drafts/" + filename
        );

        FileMgr.WriteToFile("lastDraft.txt", "drafts/" + filename);
    }

    private bool AutoLoadDraft()
    {
        DraftResult result;
        string lastDraft = FileMgr.ReadFromFile("lastDraft.txt");
        if (lastDraft == null)
        {
            result = DraftManager.LoadDraft();
        }
        else
        {
            result = DraftManager.LoadDraft(lastDraft);
        }
        if (result.Status == DraftLoadStatus.Loaded)
        {
            var draft = result.Draft;
            eventTitle.Text = draft.Title;
            eventDate.SelectedDate = draft.Date;
            NumericTextBox.Text = draft.Hours;
            eventBody.Text = draft.Description;
            selectedImages = [.. draft.ImagePaths];

            // Update display collection
            ImageDisplayItems.Clear();
            foreach (var imagePath in selectedImages)
            {
                var displayItem = new ImageDisplayItem
                {
                    ImagePath = imagePath,
                    FileName = Path.GetFileName(imagePath)
                };
                ImageDisplayItems.Add(displayItem);
            }

            UpdateImageUI();
            DraftLoadedSuccessfully.Visibility = Visibility.Visible;
            UpdatePresence();
            return true;
        }
        else if (result.Status == DraftLoadStatus.Corrupt)
        {
            DraftIsCorruptStack.Visibility = Visibility.Visible;
            return false;
        }
        return false;
    }
    private void OnWindowSizeChanged(double windowWidth)
    {
        //System.Diagnostics.Trace.WriteLine($"{windowWidth}");
        // Update the UI based on window width
        if (windowWidth < 1300 && windowWidth > 900)
        {
            MainGrid.ColumnDefinitions[0].Width = new GridLength(2.5, GridUnitType.Star);
            MainScroller.Margin = new Thickness(36);
            if (!isAiEnabled)
            {
                MainGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Star);
            }

            popOut.Visibility = Visibility.Visible;
        }
        else if (windowWidth < 900)
        {
            MainGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
            if (!isAiEnabled)
            {
                MainGrid.ColumnDefinitions[1].Width = new GridLength(0, GridUnitType.Star);
                MainScroller.Margin = new Thickness(36);
            }
            else
            {
                MainScroller.Margin = new Thickness(36, 36, 20, 36);
            }

            popOut.Visibility = Visibility.Visible;
        }
        else
        {
            MainGrid.ColumnDefinitions[0].Width = new GridLength(3.5, GridUnitType.Star);
            if (!isAiEnabled)
            {
                MainGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            }
            popOut.Visibility = Visibility.Collapsed;
        }
    }

    private Window browserWindow;

    private void PopOutBrowser(object _, RoutedEventArgs __)
    {
        // First, remove the webview from its current parent
        if (webView.Parent is Panel currentParent)
        {
            currentParent.Children.Remove(webView);
        }

        // Create new window with a simple Grid container
        var browserContainer = new Grid();
        browserContainer.Children.Add(webView);

        browserWindow = new Window
        {
            Content = browserContainer,
            Title = "HourSync AI Companion"
        };

        browserWindow.Closed += BrowserWindow_Closed;
        browserWindow.Activate();

        browserWindow.ExtendsContentIntoTitleBar = true;
    }

    private void BrowserWindow_Closed(object sender, WindowEventArgs args)
    {
        // Move webview back to original location
        if (browserWindow?.Content is Grid grid && grid.Children.Contains(webView))
        {
            grid.Children.Remove(webView);

            // Find the Grid that's in Column 1 of MainGrid (the right column)
            var rightColumnGrid = MainGrid.Children.OfType<Grid>().FirstOrDefault(g => Grid.GetColumn(g) == 1);

            if (rightColumnGrid != null)
            {
                // Set it back to the middle row (Grid.Row="1")
                Grid.SetRow(webView, 1);
                rightColumnGrid.Children.Add(webView);
            }
            else
            {
                // Fallback: add it directly to MainGrid with proper positioning
                Grid.SetColumn(webView, 1);
                Grid.SetRow(webView, 0); // Since MainGrid doesn't have explicit rows defined
                MainGrid.Children.Add(webView);
            }
        }
        browserWindow = null;
    }

    private async Task ConvertHeicFiles(List<StorageFile> heicFiles)
    {
        if (isConversionActive) return;
        isConversionActive = true;

        conversionProgressBar = new ProgressBar
        {
            IsIndeterminate = false,
            Maximum = heicFiles.Count,
            Value = 0,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 300,
            Height = 20,
        };

        conversionDialog = new ContentDialog
        {
            Title = "Converting",
            CloseButtonText = null,
            PrimaryButtonText = null,
            Content = conversionProgressBar,
            XamlRoot = XamlRoot,
        };

        for (int i = 0; i < heicFiles.Count; i++)
        {
            var file = heicFiles[i];
            string outputPath = Path.Combine(
                Path.GetDirectoryName(file.Path),
                Path.GetFileNameWithoutExtension(file.Path) + ".HourSync.png"
            );

            try
            {
                using (var image = new MagickImage(file.Path))
                {
                    image.Write(outputPath, MagickFormat.Png);
                }

                selectedImages.Add(outputPath);
                var displayItem = new ImageDisplayItem
                {
                    ImagePath = outputPath,
                    FileName = Path.GetFileName(outputPath)
                };
                ImageDisplayItems.Add(displayItem);
            }
            catch (Exception ex)
            {

                await dialogManager.ShowDialog(
                    "Conversion Error",
                    $"Failed to convert {file.Name}: {ex.Message}",
                    "OK",
                    "",
                    "",
                    XamlRoot
                );
            }

            conversionProgressBar.Value = i + 1;
        }

        conversionDialog.Hide();
        isConversionActive = false;

        UpdateImageUI();
        SaveDraft();
    }

    private void SaveToOneDrive_Click(object _, RoutedEventArgs __)
    {
        // todo save to onedrive
        var filename = eventTitle.Text.ToLower();
        filename = filename.Replace(" ", "-");
        filename = Utils.ToSafeFilename(filename);
        SaveDraft($"%onedrive%/HourSync/{filename}.json");
        SaveToOneDriveText.Text = "Saved!";

        SymbolIcon check = new SymbolIcon(Symbol.Accept);
        CheckBoxSavedToOnedrive.Children.Add(check);

        _ = Task.Run(async () =>
        {
            await Task.Delay(2000);
            DispatcherQueue.TryEnqueue(() =>
            {
                SaveToOneDriveText.Text = "Save to OneDrive";
                CheckBoxSavedToOnedrive.Children.Remove(check);
            });
        });
    }

    private void LoadDraft_Click(object sender, RoutedEventArgs _)
    {
        MenuFlyout contextMenu = new()
        {
            Placement = FlyoutPlacementMode.BottomEdgeAlignedLeft,
            OverlayInputPassThroughElement = sender as UIElement
        };

        var recents = DraftManager.GetRecentDrafts();

        var recentsSubMenu = new MenuFlyoutSubItem
        {
            Text = $"Recent Drafts ({recents.Count})",
            IsEnabled = recents.Count > 0
        };

        foreach (var draft in recents.Take(10))
        {
            var item = new MenuFlyoutItem { Text = DraftManager.LoadDraft(draft).Draft.Title };
            item.Click += (_, _) => LoadDraft(draft);
            recentsSubMenu.Items.Add(item);
        }

        var other = new MenuFlyoutItem { Text = "Other" };
        other.Click += async (_, _) =>
        {
            var picker = new FileOpenPicker
            {
                ViewMode = PickerViewMode.Thumbnail,
                SuggestedStartLocation = PickerLocationId.DocumentsLibrary
            };

            var app = (App)Application.Current;


            var hwnd = WindowNative.GetWindowHandle(app.m_window);


            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

            picker.FileTypeFilter.Add(".json");
            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                LoadDraft(file.Path);
            }
        };

        contextMenu.Items.Add(recentsSubMenu);
        contextMenu.Items.Add(other);
        contextMenu.ShowAt(sender as FrameworkElement);
    }
    private async void LoadDraft(string draftPath)
    {
        var draft = DraftManager.LoadDraft(draftPath).Draft;

        eventTitle.Text = draft.Title;
        eventDate.SelectedDate = draft.Date;
        NumericTextBox.Text = draft.Hours;
        eventBody.Text = draft.Description;
        selectedImages = [.. draft.ImagePaths];

        // Update display collection
        ImageDisplayItems.Clear();

        List<string> notAvailableImages = [];
        List<string> availableImages = [];
        foreach (var imagePath in selectedImages)
        {
            string resolvedPath = imagePath;
            if (imagePath.StartsWith('%'))
            {
                resolvedPath = Environment.ExpandEnvironmentVariables(imagePath);
            }
            if (!Path.IsPathRooted(resolvedPath))
            {
                string draftDir = Path.GetDirectoryName(draftPath);
                resolvedPath = Path.GetFullPath(Path.Combine(draftDir, resolvedPath));
            }
            if (!File.Exists(resolvedPath))
            {
                notAvailableImages.Add(imagePath);
                continue;
            }
            availableImages.Add(resolvedPath);
            var displayItem = new ImageDisplayItem
            {
                ImagePath = resolvedPath,
                FileName = Path.GetFileName(resolvedPath)
            };
            ImageDisplayItems.Add(displayItem);
        }
        selectedImages = availableImages;
        if (notAvailableImages.Count > 0)
        {
            await dialogManager.ShowDialog("Unavailable Images", $"The following images are not available because they do not exist on this device:\n{string.Join("\n - ", notAvailableImages)}", "OK", "", "", XamlRoot);
        }

        UpdateImageUI();
        SaveDraft();
    }

    private async void NumericTextBox_LostFocus(object _, RoutedEventArgs __)
    {
        if (double.TryParse(NumericTextBox.Text, out double value))
        {
            if (value > 99.75)
            {
                await dialogManager.ShowDialog("Requests more than 100 eHours", "Submitting requests more than 100 eHours is not officially supported. HourSync will attempt to make multiple requests to fulfill the entire " + NumericTextBox.Text + " eHours.\r\n\r\nMake sure to obtain permission to submit your request for more than 100 eHours from your " + loginResult.StudentAcademy + " teacher before submitting.", "Cancel", "I understand", "", XamlRoot);
            }
        }
    }

    private async void TripCalculator(object sender, RoutedEventArgs e)
    {
        var title = LSGS("TripCalculatorTitle");
        var body = new StackPanel();

        //two date selectors, a check to include start or end date in calculation, an input for sleeping time, and an optional input for how many hours a day the user was on the trip.

        var dateSelectorPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var startDatePicker = new CalendarDatePicker { Header = LSGS("RequestMakerStartDateHeader"), Margin = new Thickness(0, 0, 10, 0) };
        var endDatePicker = new CalendarDatePicker { Header = LSGS("RequestMakerEndDateHeader") };
        if (eventDate.SelectedDate != null)
        {
            FileMgr.Log("Selected Date is NOT null");
            startDatePicker.Date = eventDate.SelectedDate;
            DateTimeOffset endDate = (DateTimeOffset)eventDate.SelectedDate;
            //add 5 days to the end date for the default value
            endDate = endDate.AddDays(5);
            endDatePicker.Date = endDate;
        }
        dateSelectorPanel.Children.Add(startDatePicker);
        dateSelectorPanel.Children.Add(endDatePicker);
        body.Children.Add(dateSelectorPanel);

        var includeContainer = new StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(0, 0, 0, 10) };
        var includeBoxes = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        var includeStartCheckBox = new CheckBox { Content = LSGS("RequestMakerIncludeStartDate"), Margin = new Thickness(0, 0, 10, 0) };
        var includeEndCheckBox = new CheckBox { Content = LSGS("RequestMakerIncludeEndDate") };
        var includeExplanation = new TextBlock { Text = LSGS("RequestMakerIncludesExplanation"), TextWrapping=TextWrapping.WrapWholeWords };
        includeBoxes.Children.Add(includeStartCheckBox);
        includeBoxes.Children.Add(includeEndCheckBox);

        includeContainer.Children.Add(includeBoxes);
        includeContainer.Children.Add(includeExplanation);
        body.Children.Add(includeContainer);

        var sleepingTimeInput = new TextBox { Header = LSGS("RequestMakerSleepingTimeHeader"), PlaceholderText = $"{LSGS("GenericEgText")}, 8", Text = "8", Margin = new Thickness(0, 0, 0, 10) };
        body.Children.Add(sleepingTimeInput);

        var dailyHoursInput = new TextBox { Header = LSGS("RequestMakerDailyHoursHeader"), PlaceholderText = $"{LSGS("GenericEgText")}, 8", Margin = new Thickness(0, 0, 0, 10) };
        body.Children.Add(dailyHoursInput);

        var previewCalcResult = new TextBlock { Text = $"{LSGS("RequestMakerTotalHours")}: 0", Margin = new Thickness(0, 10, 0, 0) };
        body.Children.Add(previewCalcResult);

        var calculationResult = 0.0;
        String parseDayAsTh(int day)
        {
            if (LSGS("LocalizationDoesStNdRdTh") == "0")
            {
                return LSGS("LocalizationDoesntDoStNdRdThExtension") == "NONE"
                    ? day.ToString()
                    : $"{day}{LSGS("LocalizationDoesntDoStNdRdThExtension")}";
            }

            if (day < 0 || day > 31)
            {
                return "";
            }

            var dayArray = day.ToString().Split("");
            int[] sts = [1, 21, 31];
            int[] nds = [2, 22];
            int[] rds = [3, 23];
            return sts.Contains(day) ? $"{day}st" : nds.Contains(day) ? $"{day}nd" : rds.Contains(day) ? $"{day}rd" : $"{day}th";
        }
        void UpdatePreview()
        {
            if (startDatePicker.Date.HasValue && endDatePicker.Date.HasValue)
            {
                var startDate = startDatePicker.Date.Value.DateTime;
                var endDate = endDatePicker.Date.Value.DateTime;
                if (endDate < startDate)
                {
                    previewCalcResult.Text = LSGS("RequestMakerEDBeforeSD");
                    return;
                }
                int totalDays = (endDate - startDate).Days + 1; // +1 to include the start date
                FileMgr.Log("Between the start and end date is " + totalDays);
                bool includeStartDate = includeStartCheckBox.IsChecked == true ? true : false;
                bool includeEndDate = includeEndCheckBox.IsChecked == true ? true : false;
                string includeStartDateText = "";
                string includeEndDateText = "";
                if (!includeStartDate)
                {
                    FileMgr.Log("Subtracting a day because we're NOT including the start date");
                    totalDays--;
                }
                else
                {
                    FileMgr.Log("Including the start date");
                    if (includeEndDate)
                    {
                        includeStartDateText = LocalizationService.PrepareStatement("RequestMakerISDTBoth", parseDayAsTh(startDate.Day));
                    }
                    else
                    {
                        includeStartDateText = LocalizationService.PrepareStatement("RequestMakerISDTOnly", parseDayAsTh(startDate.Day));
                    }
                }
                if (!includeEndDate)
                {
                    FileMgr.Log("Subtracting a day because we're NOT including the end date");
                    totalDays--;
                }
                else
                {
                    FileMgr.Log("Including the end date");
                    if (includeStartDate)
                    {
                        includeEndDateText = LocalizationService.PrepareStatement("RequestMakerIEDTBoth", parseDayAsTh(endDate.Day));
                    }
                    else
                    {
                        includeEndDateText = LocalizationService.PrepareStatement("RequestMakerIEDTOnly", parseDayAsTh(endDate.Day));
                    }
                }
                double sleepingHours = 0;
                double dailyHours = 0;
                if (sleepingTimeInput.IsEnabled)
                {
                    double.TryParse(sleepingTimeInput.Text, out sleepingHours);
                }
                else
                {
                    double.TryParse(dailyHoursInput.Text, out dailyHours);
                }
                FileMgr.Log("Sleeping for " + sleepingHours + " hours");
                if (dailyHoursInput.Text.Trim() == "") dailyHours = 24;
                FileMgr.Log("Daily Hours: " + dailyHours + " hours");
                double totalHours = totalDays * (dailyHours - sleepingHours);
                FileMgr.Log("In total awake for " + totalDays + "*" + (dailyHours - sleepingHours) + " hours a day, equals " + totalHours);
                previewCalcResult.Text = $"{LSGS("RequestMakerTotalHours")}: {totalHours} {includeStartDateText}{includeEndDateText}";
                calculationResult = totalHours;
            }
            else
            {
                previewCalcResult.Text = $"{LSGS("RequestMakerTotalHours")}: 0";
            }
        }
        startDatePicker.DateChanged += (_, _) => UpdatePreview();
        endDatePicker.DateChanged += (_, _) => UpdatePreview();
        includeStartCheckBox.Checked += (_, _) => UpdatePreview();
        includeStartCheckBox.Unchecked += (_, _) => UpdatePreview();
        includeEndCheckBox.Checked += (_, _) => UpdatePreview();
        includeEndCheckBox.Unchecked += (_, _) => UpdatePreview();
        sleepingTimeInput.TextChanged += (_, _) =>
        {
            if (sleepingTimeInput.Text != "")
            {
                dailyHoursInput.IsEnabled = false;
            }
            else
            {
                dailyHoursInput.IsEnabled = true;
            }
            UpdatePreview();
        };
        dailyHoursInput.TextChanged += (_, _) =>
        {
            if (dailyHoursInput.Text != "")
            {
                sleepingTimeInput.IsEnabled = false;
            }
            else
            {
                sleepingTimeInput.IsEnabled = true;
            }
            UpdatePreview();
        };

        var result = await dialogManager.ShowDialog(title, body, "Cancel", "Use Result", "", XamlRoot);
        if (result == ContentDialogResult.Primary)
        {
            NumericTextBox.Text = calculationResult.ToString(CultureInfo.InvariantCulture);
        }
    }

    private string LSGS(string query)
    {
        try
        {
            return LocalizationService.GetString(query);
        }
        catch (Exception ex)
        {
            FileMgr.LogError("Exception in LSGS with query " + query + ": " + ex.Message);
            return query;
        }
    }
}

public class ImageDisplayItem
{
    public string ImagePath
    {
        get; set;
    }
    public string FileName
    {
        get; set;
    }
}
