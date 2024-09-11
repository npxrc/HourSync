#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
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
                throw new ArgumentException("Incorrect number of parameters passed to RequestMaker page. Parameters Length was " + parameters.Length);
            }
        }
        else
        {
            // Handle the case where parameters are not in the expected format
            throw new ArgumentException("Parameters passed to RequestMaker page are not in the expected format.");
        }
        eventTitle.KeyUp += KeyUp_SaveDraft;
        eventDate.SelectedDateChanged += (sender, e) => KeyUp_SaveDraft(sender, null);
        eventBody.KeyUp += KeyUp_SaveDraft;
        Loaded += OnPageLoaded;
    }
    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnPageLoaded;
        LoadDraft();
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
            XamlRoot = XamlRoot
        };
        ContentDialogResult result = await dialog.ShowAsync();

        // Handle the result
        if (result == ContentDialogResult.Primary)
        {
            // User clicked Yes
            await PostRequestAsync(eventTitle.Text, eventDate.Date.ToString(), numberOfHoursRequested.Value.ToString(), eventBody.Text);

            eventTitle.Text = "";
            eventDate.SelectedDate = null;
            numberOfHoursRequested.Value = 0;
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
            XamlRoot = XamlRoot
        };
        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            eventTitle.Text = "";
            eventDate.SelectedDate = null;
            numberOfHoursRequested.Value = 0;
            eventBody.Text = "";
            DraftDeletedSuccessfully.Visibility = Visibility.Visible;
            DeleteFile("draft.json");
            LoadDraft();
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
                _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            }

            var response = await _client.PostAsync("https://academyendorsement.olatheschools.com/Student/makeRequest.php", content);
            var responseString = await response.Content.ReadAsStringAsync();

            await new ContentDialog
            {
                Title = "Success",
                Content = $"{title} was just submitted for ${hours} eHours.",
                PrimaryButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();

            // Instead of navigating immediately, update the UI on this page
            UpdateUIAfterSubmission();

            // Optional: Navigate after a short delay to ensure UI updates are visible
            DeleteFile("draft.json");
            ((App)Application.Current).UpdateHomeContent(responseString);
            Frame.Navigate(typeof(Home), new object[] { username, password, phpSessionId, nameOfPerson, nameOfAcademy, responseString, _cookieContainer, _handler, _client });
        }
        catch (Exception ex)
        {
            LogError(ex.Message);
            await new ContentDialog
            {
                Title = "Error",
                Content = $"An error occurred when submitting {title}.",
                PrimaryButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
    }

    private void UpdateUIAfterSubmission()
    {
        // Clear form fields
        eventTitle.Text = "";
        eventDate.SelectedDate = null;
        numberOfHoursRequested.Value = 0;
        eventBody.Text = "";
        filesSelectedTextBlock.Text = "Selected Files:";
        selectedImages.Clear();

        Log("done");

        // Save the empty draft
        SaveDraft();
    }

    private MultipartFormDataContent CreateMultipartFormDataContent(string title, string formattedDate, string hours, string desc)
    {
        var content = new MultipartFormDataContent
    {
        { new StringContent(title), "title" },
        { new StringContent(formattedDate), "activityDate" },
        { new StringContent(hours), "hours" },
        { new StringContent(desc), "description" }
    };

        foreach (var imagePath in selectedImages)
        {
            var imageContent = new ByteArrayContent(File.ReadAllBytes(imagePath))
            {
                Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") }
            };
            content.Add(imageContent, "img[]", Path.GetFileName(imagePath));
        }

        return content;
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
                Hours = (int)numberOfHoursRequested.Value,
                Description = eventBody.Text,
                ImagePaths = new List<string>(selectedImages)
            };

            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataPath = Path.Combine(localAppDataPath, "eHours");
            var draftFilePath = Path.Combine(dataPath, "draft.json");

            var json = JsonConvert.SerializeObject(draft, Newtonsoft.Json.Formatting.Indented);
            File.WriteAllText(draftFilePath, json);
        }
        catch (Exception)
        {
            var dialog = new ContentDialog
            {
                Title = "Save Error",
                Content = "An error occurred when saving your draft.\r\nIt may be a good idea to also save your request elsewhere.",
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
        }
    }
    private async void LoadDraft()
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataPath = Path.Combine(localAppDataPath, "eHours");
            var draftFilePath = Path.Combine(dataPath, "draft.json");

            if (File.Exists(draftFilePath))
            {
                if (ReadFromFile("draft.json").Length <= 92){
                    DraftLoadedSuccessfully.Visibility = Visibility.Collapsed;
                    DraftIsCorruptStack.Visibility = Visibility.Visible;
                    DraftIsCorrupt.CloseButtonClick += OnDraftIsCorruptClose;
                    return; 
                }
                var json = File.ReadAllText(draftFilePath);
                var draft = JsonConvert.DeserializeObject<Draft>(json);

                eventTitle.Text = draft.Title;
                eventDate.SelectedDate = draft.Date;
                numberOfHoursRequested.Value = draft.Hours;
                eventBody.Text = draft.Description;
                selectedImages = new List<string>(draft.ImagePaths);

                // Update image labels
                var stringOfFileNames = "Selected Files:";
                foreach (var imagePath in selectedImages)
                {
                    // Handle each file
                    stringOfFileNames += (", " + imagePath);
                }
                filesSelectedTextBlock.Text = stringOfFileNames;

                DraftLoadedSuccessfully.Visibility = Visibility.Visible;
            }
        }
        catch (Exception)
        {
            DraftLoadedSuccessfully.Visibility = Visibility.Collapsed;
            var dialog = new ContentDialog
            {
                Title = "Load Draft Error",
                Content = "An error occurred loading a previous draft.",
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            DeleteFile("draft.json");
        }
    }

    private void OnDraftIsCorruptClose(InfoBar sender, object args)
    {
        DraftIsCorruptStack.Visibility = Visibility.Collapsed;
    }
    private void KeyUp_SaveDraft(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) => SaveDraft();

    private async void OpenDraft_Click(object sender, RoutedEventArgs e)
    {
        var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataPath = Path.Combine(localAppDataPath, "eHours");
        var filePath = Path.Combine(dataPath, "draft.json");
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Open Draft Error",
                Content = "An error occurred when opening the draft. What is wrong with your computer lil bro?",
                PrimaryButtonText = "OK",
                XamlRoot = XamlRoot
            };
            await dialog.ShowAsync();
            Log("An exception occurred at " + DateTime.Now + " when opening the draft. Exception: " + ex.Message);
        }
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
            XamlRoot = XamlRoot
        };
        ContentDialogResult result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            DeleteFile("draft.json");
            DraftIsCorruptStack.Visibility = Visibility.Collapsed;
            DraftDeletedSuccessfully.Visibility = Visibility.Visible;
            LoadDraft();
        }
        else if (result == ContentDialogResult.Secondary)
        {
            OpenDraft_Click(null, null);
        }
    }
    private static readonly string appDataFolder = "eHours";
    public static string ReadFromFile(string filename)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataPath = Path.Combine(localAppDataPath, appDataFolder);
            var filePath = Path.Combine(dataPath, filename);

            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
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
    public static void WriteToFile(string filename, string content)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataPath = Path.Combine(localAppDataPath, appDataFolder);
            var filePath = Path.Combine(dataPath, filename);

            Directory.CreateDirectory(dataPath);
            File.WriteAllText(filePath, content);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    public static void Log(string toLog)
    {
        string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eHours", "log.txt");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
            File.AppendAllText(logFilePath, $"\r\n{toLog}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    public static bool DeleteFile(string filename)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var dataPath = Path.Combine(localAppDataPath, "eHours");

            var filePath = Path.Combine(dataPath, filename);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            else
            {
                Console.WriteLine($"File not found: {filePath}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting file: {ex.Message}");
            return false;
        }
    }
    public static bool LogError(string message)
    {
        try
        {
            Log("An exception occurred at " + DateTime.Now + ". Exception: " + message);
            return true;
        }
        catch (Exception ex)
        {
            ((App)App.Current).m_window.Close();
            return false;
        }
    }
}
public class Draft
{
    public string Title
    {
        get; set;
    }
    public DateTimeOffset? Date
    {
        get; set;
    }
    public int Hours
    {
        get; set;
    }
    public string Description
    {
        get; set;
    }
    public List<string> ImagePaths { get; set; } = [];
}
