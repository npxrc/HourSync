#pragma warning disable IDE0007 // Use implicit type
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using Windows.Storage.Pickers;
using Windows.Storage;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using WinRT.Interop;
using System.Xml;
using Newtonsoft.Json;
using System.Reflection.Emit;
using HtmlAgilityPack;

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
        this.Loaded += OnPageLoaded;
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e){
        this.Loaded -= OnPageLoaded;
        LoadDraft();
    }

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
                stringOfFileNames += (", "+file.Name);
                selectedImages.Add(file.Path);
            }
            filesSelectedTextBlock.Text = stringOfFileNames;
        }
    }
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
        catch (Exception ex)
        {
            var dialog = new ContentDialog()
            {
                Title = "Save Error",
                Content = "An error occurred when saving your draft.\r\nIt may be a good idea to also save your request elsewhere.",
                PrimaryButtonText = "Continue",
                CloseButtonText = "Cancel"
            };
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }
    }
    
    //Button clicks
    private async void SubmitButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog()
        {
            Title = "Confirm",
            Content = "Ready to submit? Click 'Continue' to proceed.",
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel"
        };
        dialog.XamlRoot = this.XamlRoot;
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
        var dialog = new ContentDialog()
        {
            Title = "Confirm",
            Content = "Are you sure you want to clear the form?",
            PrimaryButtonText = "Continue",
            CloseButtonText = "Cancel"
        };
        dialog.XamlRoot = this.XamlRoot;
        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            eventTitle.Text = "";
            eventDate.SelectedDate = null;
            numberOfHoursRequested.Value = 0;
            eventBody.Text = "";
            WriteToFile("draft.json", "");
        }
    }

    //POST request
    private async Task PostRequestAsync(string title, string date, string hours, string desc)
    {
        if (title.Length < 3)
        {
            var dialog = new ContentDialog()
            {
                Title = "Confirmation",
                Content = $"Is your title really {title}...",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No"
            };
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var yourloss = new ContentDialog()
                {
                    Title = "Information",
                    Content = "Your loss, not mine.",
                    PrimaryButtonText = "OK"
                };
                yourloss.XamlRoot = this.XamlRoot;
                await yourloss.ShowAsync();
            } else
            {
                var goodjobturningaround = new ContentDialog()
                {
                    Title = "Information",
                    Content = "That's what I thought, go fix it.",
                    PrimaryButtonText = "OK"
                };
                goodjobturningaround.XamlRoot = this.XamlRoot;
                await goodjobturningaround.ShowAsync();
                return;
            }
        }
        if (desc.Length < 20)
        {
            var dialog = new ContentDialog()
            {
                Title = "Confirmation",
                Content = "Are you sure your academy instructor will accept this with such a short description?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No"
            };
            dialog.XamlRoot = this.XamlRoot;
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                var yourloss = new ContentDialog()
                {
                    Title = "Information",
                    Content = "Your loss, not mine.",
                    PrimaryButtonText = "OK"
                };
                yourloss.XamlRoot = this.XamlRoot;
                await yourloss.ShowAsync();
                return;
            }
            else
            {
                var goodjobturningaround = new ContentDialog()
                {
                    Title = "Information",
                    Content = "That's what I thought, go fix it.",
                    PrimaryButtonText = "OK"
                };
                goodjobturningaround.XamlRoot = this.XamlRoot;
                await goodjobturningaround.ShowAsync();
                return;
            }
        }

        try
        {
            string formattedDate = DateTime.Parse(date).ToString("yyyy-MM-dd");

            // Prepare the content for the POST request
            var content = new MultipartFormDataContent();
            content.Add(new StringContent(title), "title");
            content.Add(new StringContent(formattedDate), "activityDate");
            content.Add(new StringContent(hours), "hours");
            content.Add(new StringContent(desc), "description");

            // Add images
            foreach (var imagePath in selectedImages)
            {
                var imageContent = new ByteArrayContent(File.ReadAllBytes(imagePath));
                imageContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
                content.Add(imageContent, "img[]", Path.GetFileName(imagePath));
                Console.WriteLine(imagePath);
            }

            // Ensure the PHPSESSID cookie is set for the domain
            Uri uri = new Uri("https://academyendorsement.olatheschools.com/");
            _cookieContainer.Add(uri, new Cookie("PHPSESSID", phpSessionId));

            // Set User-Agent if not already set
            if (!_client.DefaultRequestHeaders.Contains("User-Agent"))
            {
                _client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
            }
            // Send the POST request
            var response = await _client.PostAsync("https://academyendorsement.olatheschools.com/Student/makeRequest.php", content);
            var responseString = await response.Content.ReadAsStringAsync();

            // Handle response (optional)
            var dialog = new ContentDialog()
            {
                Title = "Success",
                Content = $"\"{title}\" was submitted for {hours} eHours just now.",
                PrimaryButtonText = "OK"
            };
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();

            Frame.Navigate(typeof(Home), new object[] { username, password, phpSessionId, nameOfPerson, nameOfAcademy, responseString, _cookieContainer, _handler, _client });
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog()
            {
                Title = "Error",
                Content = "An error occurred when submitting your request.",
                PrimaryButtonText = "OK"
            };
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }
    }

    //Load Draft
    private async void LoadDraft()
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataPath = Path.Combine(localAppDataPath, "eHours");
            var draftFilePath = Path.Combine(dataPath, "draft.json");

            if (File.Exists(draftFilePath))
            {
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

                var dialog = new ContentDialog()
                {
                    Title = "Draft Loaded",
                    Content = "A previous draft has been loaded. Click \"Clear\" to delete the draft.",
                    PrimaryButtonText = "OK"
                };
                dialog.XamlRoot = this.XamlRoot;
                await dialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog()
            {
                Title = "Load Draft Error",
                Content = "An error occurred loading a previous draft.",
                PrimaryButtonText = "OK"
            };
            dialog.XamlRoot = this.XamlRoot;
            await dialog.ShowAsync();
        }
    }

    private void KeyUp_SaveDraft(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e) => SaveDraft();

    private string ReadFromFile(string filename)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var dataPath = Path.Combine(localAppDataPath, "eHours");

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

            var dataPath = Path.Combine(localAppDataPath, "eHours");

            var filePath = Path.Combine(dataPath, filename);

            Directory.CreateDirectory(dataPath);

            File.WriteAllText(filePath, towrite);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }
    private void Log(string toLog)
    {
        WriteToFile("log.txt", (ReadFromFile("log.txt") + $"\r\n{toLog}"));
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
    public List<string> ImagePaths { get; set; } = new List<string>();
}
