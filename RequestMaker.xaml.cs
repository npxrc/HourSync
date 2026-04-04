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
using ImageMagick; // Add this using directive for Magick.NET
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using WinRT.Interop;

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
                    "Convert HEIC Files",
                    $"Some files could not be added without converting them. Would you like to convert them to PNG?\n\n{fileList}",
                    "No",
                    "Yes",
                    "",
                    XamlRoot
                );

                if (result == ContentDialogResult.Primary) // Yes
                {
                    await ConvertHeicFiles(heicFiles);
                }
            }

            UpdateImageUI();
            SaveDraft(); // Save draft when images are added
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
            var stringOfFileNames = $"Selected Files: {string.Join(", ", selectedImages.Select(Path.GetFileName))}";
            filesSelectedTextBlock.Text = stringOfFileNames;
            ImagePreviewSection.Visibility = Visibility.Visible;
        }
        else
        {
            filesSelectedTextBlock.Text = "No files selected.";
            ImagePreviewSection.Visibility = Visibility.Collapsed;
        }
    }

    private async void SubmitButton_Click(object _, RoutedEventArgs __)
    {
        ContentDialogResult result = await dialogManager.ShowDialog("Confirm", "Ready to submit? Click 'Submit' to proceed.", "Cancel", "Submit", "", XamlRoot);

        // Handle the result
        if (result == ContentDialogResult.Primary)
        {
            // User clicked Yes
            await PostRequestAsync(
                eventTitle.Text,
                eventDate.Date.ToString(),
                NumericTextBox.Text,
                eventBody.Text
            );

            // Get the raw event title text
            string rawText = eventTitle.Text;

            // Define a list of invalid characters for file names
            char[] invalidChars = Path.GetInvalidFileNameChars();

            // Remove invalid characters from the event title
            string cleanedText = new string([.. rawText.Where(c => !invalidChars.Contains(c))]);

            SaveDraft("drafts/" + cleanedText + ".json");

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

    //POST request
    private async Task PostRequestAsync(string title, string date, string hours, string desc)
    {
        SubmitButton.IsEnabled = false;
        try
        {
            string formattedDate = DateTime.Parse(date).ToString("yyyy-MM-dd");
            var content = CreateMultipartFormDataContent(title, formattedDate, hours, desc);

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
                await dialogManager.ShowDialog("Success", $"{title} was just submitted for {hours} eHours", "OK", "", "", XamlRoot);

                UpdateUIAfterSubmission();

                FileMgr.DeleteFile("draft.json");
                ((App)Application.Current).UpdateHomeContent(responseString);
                Frame.Navigate(
                    typeof(Home),
                    new object[]
                    {
                        loginResult,
                        username,
                        password,
                        responseString
                    }
                );
            }
            else
            {
                FileMgr.Log("Null, logging in again.");
                if (logInAgainAttempts >= 3)
                {
                    FileMgr.LogError("Too many log in attempts reached, check the code.");
                    throw new Exception("Too many log in attempts reached, check the code.");
                }
                bool isLoggedInAgain = await LogInAgain();
                logInAgainAttempts++;
                if (isLoggedInAgain)
                {
                    await PostRequestAsync(title, date, hours, desc);
                }
                else
                {
                    await dialogManager.ShowDialog("Incorrect Credentials", $"Your credentials for the user {username} are incorrect. Please log in again.", "OK", "", "", XamlRoot);
                    SubmitButton.IsEnabled = true;
                }
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError(ex.Message);
            await dialogManager.ShowDialog("Error", $"An error occurred when submitting {title}.", "OK", "", "", XamlRoot);
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
        string desc
    )
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(title), "title" },
            { new StringContent(formattedDate), "activityDate" },
            { new StringContent(hours), "hours" },
            { new StringContent(desc), "description" },
        };

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

    private async void NumericTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(NumericTextBox.Text, out int value))
        {
            if (value > 99.75)
            {
                await dialogManager.ShowDialog("Requests more than 100 eHours", "Submitting requests more than 100 eHours is not officially supported. HourSync will attempt to make multiple requests to fulfill the entire " + NumericTextBox.Text + " eHours.\r\n\r\nMake sure to obtain permission to submit your request for more than 100 eHours from your " + loginResult.StudentAcademy + " teacher before submitting.", "Cancel", "I understand", "", XamlRoot);
            }
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
