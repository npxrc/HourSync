#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
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

namespace HourSync;

public sealed partial class RequestViewer : Window
{
    private bool isEditing = false;

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
    private HourSyncCore.Status status;
    private HtmlDocument doc = new();

    private string username;
    private string password;

    private int logInAgainAttempts = 0;

    public RequestViewer(
        string id,
        string phpSessionId,
        string nameOfAcademy,
        string eventName,
        HourSyncCore.Status status,
        string username,
        string password
    )
    {
        InitializeComponent();
        Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt,
        };
        SystemBackdrop = micaBackdrop;
        ExtendsContentIntoTitleBar = true;
        Title = "Request Viewer";

        this.id = id;
        this.phpSessionId = phpSessionId;
        this.nameOfAcademy = nameOfAcademy;
        this.eventName = eventName.Split('\n')[0];
        this.status = status;
        this.username = username;
        this.password = password;

        FileMgr.Log("Running PostAsync()");
        _ = PostAsync();

        Closed += async (s, e) =>
        {
            if (isEditing)
            {
                MainWindow _mainWindow = (MainWindow)((App)Application.Current).m_window;
                _mainWindow.ClosedEditor(id, this);
            }
        };
    }

    private async Task<ContentDialogResult> ShowDialog(string title, string content, string closeButtonText = "", string primaryButtonText = "", string secondaryButtonText = "")
    {
        return await new ContentDialog { Title = title, Content = content, CloseButtonText = closeButtonText.Length > 0 ? closeButtonText : null, PrimaryButtonText = primaryButtonText.Length > 0 ? primaryButtonText : null, SecondaryButtonText = secondaryButtonText.Length > 0 ? secondaryButtonText : null, XamlRoot = RootGrid.XamlRoot }.ShowAsync();
    }

    private async Task PostAsync()
    {
        FileMgr.Log("Getting request via HourSyncCore");
        HourSyncCore.FetchedEHourRequest response = await HourSyncCore.GetRequest(phpSessionId, id, true);

        if (response.Success && response.LoggedIn)
        {
            FileMgr.Log("Success and logged in");

            eventTitle.Text = eventName;
            reqdEHourCount.Text = response.RequestedHours;

            progressBar.IsIndeterminate = false;

            if (status == HourSyncCore.Status.Accepted)
            {
                pendingStatus.Text = "Accepted";
                progressBar.Value = 100;
            }
            else if (status == HourSyncCore.Status.Denied)
            {
                pendingStatus.Text = "Denied";
                progressBar.Value = 100;
                progressBar.ShowError = true;
            }
            else if (status == HourSyncCore.Status.Pending)
            {
                pendingStatus.Text = "Pending";
                progressBar.Value = 100;
                progressBar.ShowPaused = true;

                editReqButton.IsEnabled = true;
                delReqButton.IsEnabled = true;
            }
            else if (status == HourSyncCore.Status.Returned)
            {
                pendingStatus.Text = "Returned";
                progressBar.IsIndeterminate = true; // should already be
            }

            eventBody.Text = response.Body;
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
                ((App)Application.Current).Exit();
                Close();
            }
            bool isLoggedInAgain = await LogInAgain();
            logInAgainAttempts++;
            if (isLoggedInAgain)
            {
                await PostAsync();
            }
            else
            {
                await new ContentDialog()
                {
                    Title = "Incorrect credentials",
                    Content = $"Your credentials for the user {username} are incorrect. Please log in again.",
                    PrimaryButtonText = "OK",
                    XamlRoot = RootGrid.XamlRoot,
                }.ShowAsync();
            }
        }
        else
        {
            FileMgr.LogError($"Success: {response.Success}, Logged In: {response.LoggedIn}");
            FileMgr.LogError($"{response.Error}");

            await ShowDialog("Error", "An error occurred. Please check the log for more information", "Okay");
            Close();
        }
    }

    private async Task<bool> LogInAgain()
    {
        FileMgr.Log("Attempting login via HourSyncCore");
        var loginResult = await HourSyncCore.Login(username, password);
        if (string.IsNullOrEmpty(loginResult.Error) || !string.IsNullOrEmpty(phpSessionId))
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

    private void LoadImage(string ImagePath)
    {
        // Create a base URL for relative paths
        string baseUrl = "https://academyendorsement.olatheschools.com/";

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

    private async void ShowDeleteProgressBar()
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
            Title = "Deleting",
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

    private async void DelReq(object sender, RoutedEventArgs e)
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
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
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

                    FileMgr.WriteToFile("delreq.txt", responseString);
                    afterDelReqResp = responseString;
                    waitForDeleteProgressBar.IsIndeterminate = false;
                    waitForDeleteProgressBar.Value = 100;
                    waitForDelete.Title = "Deleted succesfully";
                    waitForDelete.CloseButtonText = "Close";
                    waitForDelete.CloseButtonClick += GoBackToHomeAndUpdate;
                }
                catch (Exception ex)
                {
                    FileMgr.Log(
                        "An exception occurred at "
                            + DateTime.Now
                            + " when deleting request "
                            + eventName
                            + ". Exception: "
                            + ex.Message
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
                _ = ShowDialog("Cannot Delete", "You cannot delete this request because it has already been accepted or denied by your academy instructor. Please contact the instructor of the "
                        + nameOfAcademy
                        + " for further instructions.", "OK");
            }
            catch (Exception ex)
            {
                FileMgr.Log(ex.Message);
                _ = ShowDialog("Cannot Delete", "You cannot delete this request because it has already been accepted or denied by your academy instructor. Please contact the instructor of the "
                        + nameOfAcademy
                        + " for further instructions.", "OK");
            }
        }
    }

    private void GoBackToHomeAndUpdate(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ((App)App.Current).GoToHomeAfterDel(afterDelReqResp);
        Close();
    }

    private async void EditReq(object sender, RoutedEventArgs e)
    {
        MainWindow _mainWindow = (MainWindow)((App)Application.Current).m_window;
        bool res = _mainWindow.NowEditingViewer(id, this);
        if (res)
        {
            isEditing = true;
            EditEnabled.Visibility = Visibility.Visible;
            eventBody.IsReadOnly = false;
            eventBody.Focus(FocusState.Keyboard);
        }
        else
        {
            var contentRes = await ShowDialog("Max Editors Reached", "This request is already open in another editor. Please close that editor or continue working there.", "Close", "Go to Existing Editor");
            if (contentRes == ContentDialogResult.Primary)
            {
                _mainWindow.FocusEditor(id);
            }
        }
    }

    private void Button_Click(object sender, RoutedEventArgs e)
    {

    }
}
