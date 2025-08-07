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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Newtonsoft.Json;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace HourSync;

public sealed partial class RequestMaker : Page
{
    private List<string> selectedImages = [];
    private string username;
    private string password;
    private string phpSessionId;
    private string nameOfPerson;
    private string nameOfAcademy;
    private CookieContainer _cookieContainer;
    private HttpClientHandler _handler;
    private HttpClient _client;

    public RequestMaker()
    {
        InitializeComponent();
        Loaded += loaded;

        eventTitle.KeyUp += (sender, e) =>
        {
            UpdatePresence();
        };
        NumericTextBox.KeyUp += (sender, e) =>
        {
            UpdatePresence();
        };
    }

    private async void loaded(object sender, RoutedEventArgs e)
    {
        await webView.EnsureCoreWebView2Async(null);
        webView.CoreWebView2.Navigate("https://www.chatgpt.com"); // Example webpage
    }

    private void UpdatePresence()
    {
        FileMgr.Log("Updating presence");
        string anyHours = "to log";
        try
        {
            if (NumericTextBox != null && NumericTextBox.Text.Length > 0)
            {
                anyHours = $"{NumericTextBox.Text} hours for";
            }

            if (eventTitle != null && eventTitle.Text.Length > 0)
            {
                FileMgr.Log("Setting presence to `"+ $"Requesting {anyHours} \"{eventTitle.Text}\" `");
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
            if (parameters.Length >= 9)
            {
                username = parameters[0] as string;
                password = parameters[1] as string;
                phpSessionId = parameters[2] as string;
                nameOfPerson = parameters[3] as string;
                nameOfAcademy = parameters[4] as string;
                _cookieContainer = parameters[6] as CookieContainer;
                _handler = parameters[7] as HttpClientHandler;
                _client = parameters[8] as HttpClient;
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
        eventDate.SelectedDateChanged += (sender, e) => KeyUp_SaveDraft(sender, null);
        eventBody.KeyUp += KeyUp_SaveDraft;
        Loaded += OnPageLoaded;
    }

    private async void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnPageLoaded;
        bool res = await LoadDraft();
        if (res == true)
        {
            SaveDraft();
        }
    }

    //Button clicks
    private async void ImageUpload_Click(object sender, RoutedEventArgs e)
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

        // Open the picker and get the files
        var files = await openPicker.PickMultipleFilesAsync();

        if (files.Count > 0)
        {
            // Do something with the files
            var stringOfFileNames = "Selected Files:";
            foreach (StorageFile file in files)
            {
                // Handle each file
                stringOfFileNames += (", " + file.Name);
                selectedImages.Add(file.Path);
            }
            filesSelectedTextBlock.Text = stringOfFileNames;
        }
    }

    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Confirm",
            Content = "Ready to submit? Click 'Continue' to proceed.",
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        ContentDialogResult result = await dialog.ShowAsync();

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

            eventTitle.Text = "";
            eventDate.SelectedDate = null;
            NumericTextBox.Text = "0";
            eventBody.Text = "";
            SaveDraft();
        }
    }

    private async void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Confirm",
            Content = "Are you sure you want to clear the form?",
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot,
        };
        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            eventTitle.Text = "";
            eventDate.SelectedDate = null;
            NumericTextBox.Text = "0";
            eventBody.Text = "";
            DraftDeletedSuccessfully.Visibility = Visibility.Visible;
            FileMgr.DeleteFile("draft.json");
            LoadDraft();
        }
    }

    private async void OpenDraft_Click(object sender, RoutedEventArgs e)
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
            var dialog = new ContentDialog
            {
                Title = "Open Draft Error",
                Content =
                    "An error occurred when opening the draft. Take your laptop to tech at this point brochacho.",
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
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
        try
        {
            string formattedDate = DateTime.Parse(date).ToString("yyyy-MM-dd");
            var content = CreateMultipartFormDataContent(title, formattedDate, hours, desc);

            Uri uri = new("https://academyendorsement.olatheschools.com/");
            _cookieContainer.Add(uri, new Cookie("PHPSESSID", phpSessionId));

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
                await new ContentDialog
                {
                    Title = "Success",
                    Content = $"{title} was just submitted for {hours} eHours.",
                    PrimaryButtonText = "OK",
                    XamlRoot = XamlRoot,
                }.ShowAsync();

                // Instead of navigating immediately, update the UI on this page
                UpdateUIAfterSubmission();

                // Optional: Navigate after a short delay to ensure UI updates are visible
                FileMgr.DeleteFile("draft.json");
                ((App)Application.Current).UpdateHomeContent(responseString);
                Frame.Navigate(
                    typeof(Home),
                    new object[]
                    {
                        username,
                        password,
                        phpSessionId,
                        nameOfPerson,
                        nameOfAcademy,
                        responseString,
                        _cookieContainer,
                        _handler,
                        _client,
                    }
                );
            }
            else
            {
                FileMgr.Log("Null, logging in again.");
                bool isLoggedInAgain = await LogInAgain();
                if (isLoggedInAgain)
                {
                    await PostRequestAsync(title, date, hours, desc);
                }
                else
                {
                    await new ContentDialog()
                    {
                        Title = "Incorrect credentials",
                        Content =
                            $"Your credentials for the user {username} are incorrect. Please log in again.",
                        PrimaryButtonText = "OK",
                        XamlRoot = XamlRoot,
                    }.ShowAsync();
                }
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError(ex.Message);
            await new ContentDialog
            {
                Title = "Error",
                Content = $"An error occurred when submitting {title}.",
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot,
            }.ShowAsync();
        }
    }

    private void UpdateUIAfterSubmission()
    {
        // Clear form fields
        eventTitle.Text = "";
        eventDate.SelectedDate = null;
        NumericTextBox.Text = "0";
        eventBody.Text = "";
        filesSelectedTextBlock.Text = "Selected Files:";
        selectedImages.Clear();

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
                    ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg"),
                },
            };
            content.Add(imageContent, "img[]", Path.GetFileName(imagePath));
        }

        return content;
    }

    //Log in again
    private async Task<bool> LogInAgain()
    {
        FileMgr.Log("Running await Post()");
        var resp = await Post();
        FileMgr.Log("POSTed");

        resp = resp.Replace("\n", "");
        if (resp.Contains("<h2>Welcome to your"))
        {
            FileMgr.Log("Successful login");
            nameOfAcademy = resp.Split(
                    ["<h2>Welcome to your "],
                    StringSplitOptions.None
                )[1]
                .Split([" Endorsement"], StringSplitOptions.None)[0];
            nameOfPerson = resp.Split(["Tracking, "], StringSplitOptions.None)[1]
                .Split(["</h2>"], StringSplitOptions.None)[0];

            FileMgr.Log("Getting home page");
            var getresp = await Get(
                "https://academyendorsement.olatheschools.com/Student/studentEHours.php"
            );
            FileMgr.Log("Successfully got home");
            ((App)Application.Current).GetRespOnLogin = getresp;
            return true;
        }
        else
        {
            return false;
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

    //Drafts
    private async void SaveDraft()
    {
        try
        {
            Draft draft = new Draft
            {
                Title = eventTitle.Text,
                Date = eventDate.Date,
                Hours = (string)NumericTextBox.Text,
                Description = eventBody.Text,
                ImagePaths = [.. selectedImages],
            };

            var localAppDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );
            var dataPath = Path.Combine(localAppDataPath, "HourSync");
            var draftFilePath = Path.Combine(dataPath, "draft.json");

            var json = JsonConvert.SerializeObject(draft, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(draftFilePath, json);
        }
        catch (Exception)
        {
            var dialog = new ContentDialog
            {
                Title = "Save Error",
                Content =
                    "An error occurred when saving your draft.\r\nIt may be a good idea to also save your request elsewhere.",
                PrimaryButtonText = "Okay",
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
        }
    }

    private async Task<bool> LoadDraft()
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );
            var dataPath = Path.Combine(localAppDataPath, "HourSync");
            var draftFilePath = Path.Combine(dataPath, "draft.json");

            if (File.Exists(draftFilePath))
            {
                if (FileMgr.ReadFromFile("draft.json").Length <= 92)
                {
                    DraftLoadedSuccessfully.Visibility = Visibility.Collapsed;
                    DraftIsCorruptStack.Visibility = Visibility.Visible;
                    DraftIsCorrupt.CloseButtonClick += OnDraftIsCorruptClose;
                    return false;
                }
                var json = File.ReadAllText(draftFilePath);
                var draft = JsonConvert.DeserializeObject<Draft>(json);

                eventTitle.Text = draft.Title;
                eventDate.SelectedDate = draft.Date;
                NumericTextBox.Text = draft.Hours;
                eventBody.Text = draft.Description;
                selectedImages = [.. draft.ImagePaths];

                // Update image labels
                var stringOfFileNames = "Selected Files:";
                foreach (var imagePath in selectedImages)
                {
                    // Handle each file
                    stringOfFileNames += (", " + imagePath);
                }
                filesSelectedTextBlock.Text = stringOfFileNames;

                DraftLoadedSuccessfully.Visibility = Visibility.Visible;

                UpdatePresence();
                return true;
            }
            return false;
        }
        catch (Exception)
        {
            DraftLoadedSuccessfully.Visibility = Visibility.Collapsed;
            var dialog = new ContentDialog
            {
                Title = "Load Draft Error",
                Content = "An error occurred loading a previous draft.",
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot,
            };
            await dialog.ShowAsync();
            FileMgr.DeleteFile("draft.json");
            return false;
        }
    }

    private void OnDraftIsCorruptClose(InfoBar sender, object args)
    {
        DraftIsCorruptStack.Visibility = Visibility.Collapsed;
    }

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

    private async void Delete_Draft(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Confirmation",
            Content = "Are you sure you want to delete the draft?",
            PrimaryButtonText = "Yes",
            SecondaryButtonText = "Open Draft",
            CloseButtonText = "No",
            XamlRoot = XamlRoot,
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            FileMgr.DeleteFile("draft.json");
            DraftIsCorruptStack.Visibility = Visibility.Collapsed;
            DraftDeletedSuccessfully.Visibility = Visibility.Visible;
            LoadDraft();
        }
        else if (result == ContentDialogResult.Secondary)
        {
            OpenDraft_Click(null, null);
        }
    }
}

public class Draft
{
    public string Title { get; set; }
    public DateTimeOffset? Date { get; set; }
    public string Hours { get; set; }
    public string Description { get; set; }
    public List<string> ImagePaths { get; set; } = [];
}
