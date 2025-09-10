#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using HourSyncCoreLib;
using HtmlAgilityPack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;

namespace HourSync;

public sealed partial class RequestViewer : Window
{
    /*
    TODO: Combine ImageViewer, BrowserView, and this page into one file

    Baby steps to achieve:
    - Set up a frame
    - Move this code into a page
    - Navigate to the main Request itself when the window is activated
    - Allow user to click image viewer or browser view
    - when clicked, navigate to the viewers and put a back button which navigates frame backwards so state is saved
     */

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

    private string initialRequestBody;

    private DialogService dialogManager = new();

    public RequestViewer(
        string id,
        string phpSessionId,
        string nameOfAcademy,
        string nameOfEvent,
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
        eventName = nameOfEvent.Split('\n')[0];
        FileMgr.Log(nameOfEvent);
        eventTitle.Text = nameOfEvent;
        this.status = status;
        this.username = username;
        this.password = password;

        FileMgr.Log("Running PostAsync()");
        _ = PostAsync();

        Closed += (_, __) =>
        {
            if (isEditing)
            {
                MainWindow _mainWindow = (MainWindow)((App)Application.Current).m_window;
                _mainWindow.ClosedEditor(id, this);
            }
        };
        SetupMinimumWindowSize();
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

            pendingStatus.Text = status.ToString();
            openBrowserButton.IsEnabled = true;
            if (status != HourSyncCore.Status.Returned)
            {
                progressBar.Value = 100;
                if (status == HourSyncCore.Status.Denied)
                {
                    progressBar.ShowError = true;
                }
                else if (status == HourSyncCore.Status.Pending)
                {
                    progressBar.ShowPaused = true;

                    editReqButton.IsEnabled = true;
                    delReqButton.IsEnabled = true;
                }
            }
            else if (status == HourSyncCore.Status.Returned)
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
            ContentDialogResult result = ContentDialogResult.None;
#pragma warning restore RCS1118 // Mark local variable as const
            if (!bypassDelReqDialog)
            {
                await dialog.ShowAsync();
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

                    if (responseString.Contains("See your current eHours"))
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
        Close();
    }

    private async void EditReq(object _, RoutedEventArgs __)
    {
        MainWindow _mainWindow = (MainWindow)((App)Application.Current).m_window;
        bool res = _mainWindow.NowEditingViewer(id, this);
        if (res)
        {
            isEditing = true;
            EditEnabled.Visibility = Visibility.Visible;
            eventBody.IsReadOnly = false;
            eventBody.Focus(FocusState.Keyboard);
            editingControls.Visibility = Visibility.Visible;
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
            HourSyncCore.UpdatedRequest request = new()
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

                waitForDelete.CloseButtonClick += (_, __) => Close();
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

            MainWindow _mainWindow = (MainWindow)((App)Application.Current).m_window;
            _mainWindow.ClosedEditor(id, this);
        }
    }

    private void OpenBrowser(object _, RoutedEventArgs __)
    {
        BrowserView browser = new(phpSessionId);
        browser.Activate();
    }

    // Set min size

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int GWL_WNDPROC = -4;

    private IntPtr _hwnd;
    private WNDPROC _newWndProc;
    private IntPtr _oldWndProc;

    // Minimum window size in pixels
    private int MIN_WIDTH = 410;
    private int MIN_HEIGHT = 500;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WNDPROC(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WNDPROC dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    public void SetupMinimumWindowSize()
    {
        // Get the window handle
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Create our window procedure delegate
        _newWndProc = new WNDPROC(WindowProc);

        // Subclass the window
        _oldWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, _newWndProc);
    }

    public IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_GETMINMAXINFO:
                // Handle the minimum window size
                var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                minMaxInfo.ptMinTrackSize.x = MIN_WIDTH;
                minMaxInfo.ptMinTrackSize.y = MIN_HEIGHT;
                Marshal.StructureToPtr(minMaxInfo, lParam, true);
                return IntPtr.Zero;
        }

        // Call the original window procedure for all other messages
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }
}
