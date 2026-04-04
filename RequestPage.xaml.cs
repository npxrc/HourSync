using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HourSyncCoreLib;
using HtmlAgilityPack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HourSync;
public sealed partial class RequestPage : Page
{
    private string id;
    private string phpSessionId;
    private string eventName;
    private static readonly CookieContainer cookieContainer = new();
    private static readonly HttpClientHandler handler = new()
    {
        CookieContainer = cookieContainer,
        AllowAutoRedirect = true,
    };
    private static readonly HttpClient client = new(handler)
    {
        DefaultRequestHeaders =
        {
            {
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
            },
        },
    };
    private string nameOfAcademy;
    private Status status;
    private HtmlDocument doc = new();

    private string username;
    private string password;

    private int logInAgainAttempts = 0;

    private string initialRequestBody;

    private readonly DialogService dialogManager = new();

    public event EventHandler RequestClosed;
    public event EventHandler<RequestEditEventArgs> EditStarted;
    public event EventHandler EditCancelled;
    public event EventHandler<OpenBrowserEventArgs> BrowserOpenEvent;

    public RequestPage()
    {
        InitializeComponent();
    }

    protected async override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);

        if (e.Parameter.GetType() == typeof(RequestContext))
        {
            var ctx = (RequestContext)e.Parameter;
            id = ctx.Id;
            phpSessionId = ctx.PhpSessionId;
            nameOfAcademy = ctx.AcademyName;
            eventName = ctx.EventName;
            status = ctx.Status;
            username = ctx.Username;
            password = ctx.Password;

            // Now that everything is initialized, safe to start.
            await PostAsync();
        }
        else
        {
            throw new ArgumentException();
        }
    }

    private async Task PostAsync()
    {
        FileMgr.Log("Getting request via HourSyncCore");
        FetchedEHourRequest response = await HourSyncCore.GetRequest(phpSessionId, id, true);
        doc.LoadHtml(response.Html);

        if (response.Success && response.LoggedIn)
        {
            FileMgr.Log("Success and logged in");

            eventTitle.Text = eventName;
            reqdEHourCount.Text = response.RequestedHours;

            progressBar.IsIndeterminate = false;

            pendingStatus.Text = status.ToString();
            openBrowserButton.IsEnabled = true;
            if (status != Status.Returned)
            {
                progressBar.Value = 100;
                if (status == Status.Denied)
                {
                    progressBar.ShowError = true;
                }
                else if (status == Status.Pending)
                {
                    progressBar.ShowPaused = true;

                    editReqButton.IsEnabled = true;
                    delReqButton.IsEnabled = true;
                }
            }
            else if (status == Status.Returned)
            {
                progressBar.IsIndeterminate = true;
            }

            eventBody.Text = response.Body;
            initialRequestBody = response.Body;

            foreach (string ImagePath in response.Images)
            {
                LoadImage(ImagePath);
            }

            dateSubtd.Text = response.Date.ToString();
            submittedTimeAgoText.Text = Utils.FormatTimeAgo(response.Date);
        }
        else if (!response.LoggedIn)
        {
            FileMgr.Log("Null, logging in again.");
            if (logInAgainAttempts >= 2)
            {
                FileMgr.LogError("Too many log in attempts reached, check the code.");
                throw new Exception("Too many log in attempts reached, check the code.");
            }
            bool isLoggedInAgain = await LogInAgain();
            logInAgainAttempts++;
            if (isLoggedInAgain)
            {
                await PostAsync();
            }
            else
            {
                await dialogManager.ShowDialog("Incorrect Credentials", $"Your credentials for the user {username} are incorrect. Please log in again.", "OK");
            }
        }
        else
        {
            FileMgr.LogError($"Success: {response.Success}, Logged In: {response.LoggedIn}");
            FileMgr.LogError($"{response.Error}");

            await dialogManager.ShowDialog("Error", "An error occurred. Please check the log for more information", "Okay");

        }
    }

    private async Task<bool> LogInAgain()
    {
        FileMgr.Log("Attempting login via HourSyncCore");
        var loginResult = await HourSyncCore.Login(username, password);
        if (string.IsNullOrEmpty(loginResult.Error) || !string.IsNullOrEmpty(phpSessionId))
        {
            phpSessionId = loginResult.PhpSessionId;
            var homeResult = await HourSyncCore.GetRequestsPage(loginResult.PhpSessionId);
            ((App)Application.Current).LoggedIn(loginResult, username, password, homeResult, false);
            return true;
        }
        else
        {
            return false;
        }
    }

    private void LoadImage(string ImagePath)
    {
        // Create a base URL for relative paths
        const string baseUrl = "https://academyendorsement.olatheschools.com/";

        // If src contains "../", it needs to be fixed
        if (ImagePath.StartsWith("../"))
        {
            ImagePath = string.Concat(baseUrl, ImagePath.AsSpan(3));
        }

        // Create a BitmapImage
        var bitmapImage = new BitmapImage(new Uri(ImagePath));

        // Create an Image control
        var img = new Image
        {
            MaxWidth = 600,
            MaxHeight = 450,
            Source = bitmapImage,
            Margin = new Thickness(0, 0, 5, 0),
        };

        // Attach click event handler
        img.Tapped += (_, __) => OnImageTapped(ImagePath);

        ImageMainContainer.Visibility = Visibility.Visible;
        // Add to ImagePanel
        ImagePanel.Children.Add(img);

    }

    private static void OnImageTapped(string imageUrl)
    {
        ImageViewer imageViewer = new(imageUrl)
        {
            Title = "Viewing Image - " + imageUrl.Split('/')[^1],
        };
        imageViewer.Activate();
    }

    private ProgressBar waitForDeleteProgressBar = new()
    {
        IsIndeterminate = true,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Width = 200, // Set width as needed
        Height = 20, // Set height as needed
    };
    private ContentDialog waitForDelete = new()
    {
        Title = "Deleting",
        CloseButtonText = null,
        PrimaryButtonText = null, // Ensure there's no default button
    };

    private async void ShowDeleteProgressBar(string title = "Deleting")
    {
        // Initialize and configure the ContentDialog
        waitForDeleteProgressBar = new()
        {
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 200, // Set width as needed
            Height = 20, // Set height as needed
        };

        waitForDelete = new()
        {
            Title = title,
            CloseButtonText = null,
            PrimaryButtonText = null, // Ensure there's no default button
            Content = waitForDeleteProgressBar,

            // Ensure the ContentDialog is set to the correct XamlRoot
            XamlRoot = RootGrid.XamlRoot,
        };

        // Show the ContentDialog asynchronously
        await waitForDelete.ShowAsync();
    }

    private string afterDelReqResp = "";

    private bool bypassDelReqDialog = false;
    private async void DelReq(object _, RoutedEventArgs __)
    {
        if (doc.DocumentNode.SelectSingleNode("//*[@id='Delete']").InnerHtml.Length > 1)
        {
            var dialog = new ContentDialog()
            {
                Title = "Confirm Delete",
                Content = $"Are you sure you would like to delete {eventName.Split('\n')[0]}?",
                PrimaryButtonText = "Yes",
                CloseButtonText = "No",
                XamlRoot = RootGrid.XamlRoot,
            };
#pragma warning disable RCS1118 // Mark local variable as const
            ContentDialogResult result = ContentDialogResult.Primary;
#pragma warning restore RCS1118 // Mark local variable as const
            if (!bypassDelReqDialog)
            {
                result = await dialog.ShowAsync();
            }
            if (bypassDelReqDialog || result == ContentDialogResult.Primary)
            {
                try
                {
                    ShowDeleteProgressBar();
                    var values = new Dictionary<string, string> { { "del", id } };

                    var content = new FormUrlEncodedContent(values);

                    Uri uri = new Uri("https://academyendorsement.olatheschools.com/");
                    cookieContainer.Add(uri, new Cookie("PHPSESSID", phpSessionId));

                    if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                    {
                        client.DefaultRequestHeaders.Add(
                            "User-Agent",
                            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36"
                        );
                    }

                    var response = await client.PostAsync(
                        "https://academyendorsement.olatheschools.com/deleteRequest.php",
                        content
                    );
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (!responseString.Contains("See your current eHours"))
                    {
                        var loggedIn = await LogInAgain();
                        if (loggedIn)
                        {
                            bypassDelReqDialog = true;
                            DelReq(null, null);
                            bypassDelReqDialog = false;
                        }
                        else
                        {
                            waitForDelete.Hide();
                            await dialogManager.ShowDialog("Incorrect Credentials", $"Your credentials for the user {username} are incorrect. Please log in again.", "OK");
                        }
                    }

                    FileMgr.WriteToFile("delreq.txt", responseString);
                    afterDelReqResp = responseString;
                    waitForDeleteProgressBar.IsIndeterminate = false;
                    waitForDeleteProgressBar.Value = 100;
                    waitForDelete.Title = "Deleted Succesfully";
                    waitForDelete.CloseButtonText = "Close";
                    waitForDelete.CloseButtonClick += GoBackToHomeAndUpdate;
                }
                catch (Exception ex)
                {
                    FileMgr.LogError(
                        "Error while deleting request: " + ex.Message
                    );
                    waitForDeleteProgressBar.ShowError = true;
                    waitForDelete.Title = "Error Deleting. Check the log for more info.";
                    waitForDelete.CloseButtonText = "Close";
                }
            }
        }
        else
        {
            try
            {
                _ = dialogManager.ShowDialog("Cannot Delete", "You cannot delete this request because it has already been accepted or denied by your academy instructor. Please contact the instructor of the "
                        + nameOfAcademy
                        + " for further instructions.", "OK");
            }
            catch (Exception ex)
            {
                FileMgr.Log(ex.Message);
                _ = dialogManager.ShowDialog("Cannot Delete", "You cannot delete this request because it has already been accepted or denied by your academy instructor. Please contact the instructor of the "
                        + nameOfAcademy
                        + " for further instructions.", "OK");
            }
        }
    }

    private void GoBackToHomeAndUpdate(ContentDialog _, ContentDialogButtonClickEventArgs __)
    {
        ((App)App.Current).GoToHomeAfterDel(afterDelReqResp);
        RequestClosed?.Invoke(this, null);
    }

    private async void EditReq(object _, RoutedEventArgs __)
    {
        MainWindow _mainWindow = (MainWindow)((App)Application.Current).m_window;
        bool res = _mainWindow.NowEditingViewer(id, this);
        if (res)
        {
            EditEnabled.Visibility = Visibility.Visible;
            eventBody.IsReadOnly = false;
            eventBody.Focus(FocusState.Keyboard);
            editingControls.Visibility = Visibility.Visible;

            EditStarted?.Invoke(this, null);
        }
        else
        {
            var contentRes = await dialogManager.ShowDialog("Max Editors Reached", "This request is already open in another editor. Please close that editor or continue working there.", "Close", "Go to Existing Editor");
            if (contentRes == ContentDialogResult.Primary)
            {
                _mainWindow.FocusEditor(id);
            }
        }
    }

    private bool bypassUpdateReqDialog = false;
    private async void UpdateReqClick(object _, RoutedEventArgs __)
    {
        ContentDialogResult result = ContentDialogResult.None;
        if (!bypassUpdateReqDialog)
        {
            result = await dialogManager.ShowDialog("Confirm Edit", "Are you sure you want to update this request?", "No", "Update");
        }

        if (bypassUpdateReqDialog || result == ContentDialogResult.Primary)
        {
            ShowDeleteProgressBar("Updating");
            UpdatedRequest request = new()
            {
                NewContent = eventBody.Text,
                State = status,
                Value = id
            };
            var response = await HourSyncCore.EditRequest(phpSessionId, request);
            if (response.Success)
            {
                waitForDeleteProgressBar.IsIndeterminate = false;
                waitForDeleteProgressBar.Value = 100;
                waitForDelete.Title = "Updated Succesfully";
                waitForDelete.CloseButtonText = "Close";

                MainWindow _mainWindow = (MainWindow)((App)Application.Current).m_window;
                _mainWindow.ClosedEditor(id, this);

                ((App)Application.Current).UpdateHomeContent(response.HtmlResponse);

                //waitForDelete.CloseButtonClick += (_, __) => Close();
                //TODO : grab parent and close it
            }
            else if (!response.LoggedIn)
            {
                var loggedIn = await LogInAgain();
                if (loggedIn)
                {
                    bypassUpdateReqDialog = true;
                    UpdateReqClick(null, null);
                    bypassUpdateReqDialog = false;
                }
                else
                {
                    waitForDelete.Hide();
                    await dialogManager.ShowDialog("Incorrect Credentials", $"Your credentials for the user {username} are incorrect. Please log in again.", "OK");
                }
            }
            else if (response.Error != null)
            {
                FileMgr.LogError("Error while updating a request:" + response.Error);
                await dialogManager.ShowDialog("Error", "An error occurred, check the log for more info.", "OK");
            }
            else
            {
                FileMgr.LogError("No error was provided, but editing was unsuccessful.");
                await dialogManager.ShowDialog("Error", "Something else went wrong but we are unable to determine the cause. Try again using the official portal.", "OK");
            }
        }
    }

    private async void CancelEdit(object _, RoutedEventArgs __)
    {
        var resp = await dialogManager.ShowDialog("Confirm", "Are you sure you want to stop editing? Your edits will be discarded.", "No", "Discard");
        if (resp == ContentDialogResult.Primary)
        {
            eventBody.IsReadOnly = true;
            eventBody.Text = initialRequestBody;

            EditCancelled?.Invoke(this, null);
            var _mainWindow = ((App)Application.Current).m_window;
            _mainWindow.ClosedEditor(id, this);
        }
    }

    private void OpenBrowser(object _, RoutedEventArgs __)
    {
        FileMgr.Log("OpenBrowser invoked");
        BrowserOpenEvent?.Invoke(this, new OpenBrowserEventArgs(phpSessionId));
    }
}

public class RequestEditEventArgs(string requestId) : EventArgs
{
    public string RequestId
    {
        get;
    } = requestId;
}

public class OpenBrowserEventArgs(string sessID) : EventArgs
{
    public string PhpSessionId
    {
        get;
    } = sessID;
}