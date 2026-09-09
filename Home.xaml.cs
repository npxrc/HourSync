#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible

//Home.xaml.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using HourSyncCoreLib;
using HtmlAgilityPack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HourSync;

public sealed partial class Home : Page, System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

    public string TotalReturnedCount => ReturnedRequests.Sum(g => g.SubRequests.Count).ToString();
    public string TotalPendingCount => PendingRequests.Sum(g => g.SubRequests.Count).ToString();
    public string TotalAcceptedCount => AcceptedRequests.Sum(g => g.SubRequests.Count).ToString();
    public string TotalDeniedCount => DeniedRequests.Sum(g => g.SubRequests.Count).ToString();

    private void NotifyCountChanges()
    {
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TotalReturnedCount)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TotalPendingCount)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TotalAcceptedCount)));
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(TotalDeniedCount)));
    }

    private LoginResult loginResult;
    private string username;
    private string password;
    private string getresp;
    private HtmlDocument doc = new();

    // Change these to ObservableCollection for UI binding
    public ObservableCollection<EHourRequestGroup> ReturnedRequests = [];
    public ObservableCollection<EHourRequestGroup> PendingRequests = [];
    public ObservableCollection<EHourRequestGroup> AcceptedRequests = [];
    public ObservableCollection<EHourRequestGroup> DeniedRequests = [];

    // Master lists that never change unless you actually fetch new data
    private List<EHourRequest> AllReturned = [];
    private List<EHourRequest> AllPending = [];
    private List<EHourRequest> AllAccepted = [];
    private List<EHourRequest> AllDenied = [];

    private DialogService dialogManager = new();

    public Home()
    {
        InitializeComponent();
        Loaded += Home_Loaded;

        ((App)Application.Current).m_window.SizeChanged += (s, e) => { OnWindowSizeChanged(e.Size.Width); };
    }

    protected async override void OnNavigatedTo(NavigationEventArgs e)
    {
        var time = DateTime.Now;
        FileMgr.Log("Ended at " + time.ToString());
        FileMgr.Log("Elapsed time: " + (time - ((App)Application.Current).startTime).ToString());
        base.OnNavigatedTo(e);
        if (e.Parameter is object[] parameters)
        {
            // Check the number of parameters
            if (parameters.Length >= 4)
            {
                loginResult = (LoginResult)parameters[0];
                username = parameters[1] as string;
                password = parameters[2] as string;
                getresp = parameters[3] as string;

                if (getresp.Length < 1)
                {
                    FileMgr.LogError("An error occurred with loading the eHours response.");
                    await dialogManager.ShowErrorDialog("The eHour requests list is not in the expected format.", true, XamlRoot);
                }
                else
                {
                    FileMgr.Log("Writing to getresp");
                    FileMgr.WriteToFile("getresp.html", getresp);
                    doc.LoadHtml(getresp);
                }

                StudentName.Text = loginResult.StudentName;
                StudentAcademy.Text = loginResult.StudentAcademy;
            }
            else
            {
                // Handle the case where parameters are missing or incorrect
                await dialogManager.ShowErrorDialog("Incorrect number of parameters passed to Home page. Parameters Length was "
                        + parameters.Length, true, XamlRoot);
            }
        }
        else
        {
            // Handle the case where parameters are not in the expected format
            await dialogManager.ShowErrorDialog("Incorrect number of parameters passed to Home page. Parameter type was " + e.Parameter.GetType() + ".", true, XamlRoot);
        }

        ((App)Application.Current).UpdatePresence("home", $"Signed in as {loginResult.StudentName}");
        ((App)Application.Current).homePage = this;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ((App)Application.Current).homePage = null;
    }

    private async void Home_Loaded(object _, RoutedEventArgs __)
    {
        ParseProgressTo200();

        var unformatted = HourSyncCore.ParseRequests(getresp);  // static call from the DLL

        var acceptedFormatted = new List<EHourRequest>();
        var deniedFormatted = new List<EHourRequest>();
        var pendingFormatted = new List<EHourRequest>();
        var returnedFormatted = new List<EHourRequest>();

        for (int i=0; i<unformatted.Accepted.Count; i++)
        {
            acceptedFormatted.Add(new EHourRequest()
            {
                Date = LocalizationService.FormatLocalizedDate(unformatted.Accepted[i].Date),
                Description = unformatted.Accepted[i].Description,
                Hours = LocalizationService.FormatNumber(unformatted.Accepted[i].Hours),
                Value = unformatted.Accepted[i].Value,
                State = unformatted.Accepted[i].State,
                DateTime = unformatted.Accepted[i].DateTime
            });
        }

        for (int i = 0; i < unformatted.Denied.Count; i++)
        {
            deniedFormatted.Add(new EHourRequest()
            {
                Date = LocalizationService.FormatLocalizedDate(unformatted.Denied[i].Date),
                Description = unformatted.Denied[i].Description,
                Hours = LocalizationService.FormatNumber(unformatted.Denied[i].Hours),
                Value = unformatted.Denied[i].Value,
                State = unformatted.Denied[i].State,
                DateTime = unformatted.Denied[i].DateTime
            });
        }

        for (int i = 0; i < unformatted.Pending.Count; i++)
        {
            pendingFormatted.Add(new EHourRequest()
            {
                Date = LocalizationService.FormatLocalizedDate(unformatted.Pending[i].Date),
                Description = unformatted.Pending[i].Description,
                Hours = LocalizationService.FormatNumber(unformatted.Pending[i].Hours),
                Value = unformatted.Pending[i].Value,
                State = unformatted.Pending[i].State,
                DateTime = unformatted.Pending[i].DateTime
            });
        }

        for (int i = 0; i < unformatted.Returned.Count; i++)
        {
            returnedFormatted.Add(new EHourRequest()
            {
                Date = LocalizationService.FormatLocalizedDate(unformatted.Returned[i].Date),
                Description = unformatted.Returned[i].Description,
                Hours = LocalizationService.FormatNumber(unformatted.Returned[i].Hours),
                Value = unformatted.Returned[i].Value,
                State = unformatted.Returned[i].State,
                DateTime = unformatted.Returned[i].DateTime
            });
        }

        // Convert Lists to ObservableCollections
        PopulateObservableCollection(ReturnedRequests, returnedFormatted);
        PopulateObservableCollection(PendingRequests, pendingFormatted);
        PopulateObservableCollection(AcceptedRequests, acceptedFormatted);
        PopulateObservableCollection(DeniedRequests, deniedFormatted);

        // Keep master lists as regular Lists
        AllReturned = returnedFormatted;
        AllPending = pendingFormatted;
        AllAccepted = acceptedFormatted;
        AllDenied = deniedFormatted;

        ((App)Application.Current).SetRequests(AllReturned, AllPending, AllAccepted, AllDenied);

        CreateLayout();

        try
        {
            var sortBy = await FileMgr.GetSettingValueAsync("sortBy.SelectedValue.Key");
            if (sortBy != null && sortBy is string)
            {
                FileMgr.Log(sortBy.ToString());
                int length = sortBy.ToString().Length;
                //split sortBy by "-"

                if (sortBy.ToString().Split('-')[0] == sortBy.ToString())
                {
                    FileMgr.LogError("Wrong length dumbass");
                }
                else
                {
                    var way = sortBy.ToString().Split('-')[0];
                    var from = sortBy.ToString().Split('-')[1];

                    if (way != "date" && way != "name" && way != "hours" && from != "old" && from != "new" && from != "a" && from != "z" && from != "low" && from != "high")
                    {
                        FileMgr.Log($"Invalid sortBy value: {way}, {from}");
                        return;
                    }
                    else
                    {
                        FileMgr.Log($"Sorting by: {way}, {from}");
                        // Call the Sort method with the parsed values
                        Sort(way, from);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            FileMgr.Log($"Error retrieving sortBy setting: {ex.Message}");
        }
    }

    // Helper method to populate ObservableCollection from List
    private void PopulateObservableCollection(ObservableCollection<EHourRequestGroup> target, List<EHourRequest> source)
    {
        target.Clear();
        var groups = GroupRequests(source);
        foreach (var group in groups)
        {
            target.Add(group);
        }
        NotifyCountChanges();
    }

    private List<EHourRequestGroup> GroupRequests(List<EHourRequest> requests)
    {
        var groups = new List<EHourRequestGroup>();
        if (requests == null || requests.Count == 0) return groups;

        var sorted = requests.OrderBy(r => r.DateTime).ToList();
        var regex = new System.Text.RegularExpressions.Regex(@"(?i)\s*(?:\[\d+\]|(?:Submission|Part|Pt\.?|Request)\s*\d+|\d+)\s*$");

        foreach (var req in sorted)
        {
            string baseDesc = regex.Replace(req.Description, "").Trim();
            if (string.IsNullOrWhiteSpace(baseDesc)) baseDesc = req.Description;

            var existingGroup = groups.LastOrDefault(g =>
                g.BaseDescription == baseDesc &&
                Math.Abs((req.DateTime - g.LatestDateTime).TotalHours) <= 2.0);

            if (existingGroup != null)
            {
                existingGroup.SubRequests.Add(req);
                existingGroup.TotalHours += TryParseHours(req.Hours);
                if (req.DateTime > existingGroup.LatestDateTime)
                {
                    existingGroup.LatestDateTime = req.DateTime;
                    existingGroup.DisplayDate = req.Date;
                }
            }
            else
            {
                var newGroup = new EHourRequestGroup
                {
                    BaseDescription = baseDesc,
                    TotalHours = TryParseHours(req.Hours),
                    LatestDateTime = req.DateTime,
                    DisplayDate = req.Date
                };
                newGroup.SubRequests.Add(req);
                groups.Add(newGroup);
            }
        }

        // Return to newest-first to match portal defaults
        groups.Reverse();
        return groups;
    }

    private double TryParseHours(string hoursStr)
    {
        if (double.TryParse(hoursStr.Replace(",", "."), CultureInfo.InvariantCulture, out double h))
            return h;
        return 0;
    }

    private async void ParseProgressTo200()
    {
        try
        {
            StudentEHourProgress.Value = 0;
            var barval = doc.DocumentNode.SelectSingleNode("//div[@class='bar2']").InnerText.Split('/')[
                0
            ];
            double.TryParse(barval, NumberStyles.Any, CultureInfo.InvariantCulture, out double numberOfHours);

            var percentto200 = Math.Round((numberOfHours / 2.0) >= 100 ? 100 : numberOfHours / 2.0, 2);
            var percentto300 = Math.Round((numberOfHours / 3.0) >= 100 ? 100 : numberOfHours / 3.0, 2);
            var percentto400 = Math.Round((numberOfHours / 4.0) >= 100 ? 100 : numberOfHours / 4.0, 2);

            var selectedProgress = await FileMgr.GetSettingValueAsync("endorsementSelection.SelectedValue.FriendlyName");
            if (selectedProgress != null && selectedProgress is string)
            {
                FileMgr.Log(selectedProgress as string);
                double.TryParse(selectedProgress as string, NumberStyles.Any, CultureInfo.InvariantCulture, out double selectedProgressNumber);
                FileMgr.Log($"{(numberOfHours / selectedProgressNumber) * 100}");
                numberOfHours = ((numberOfHours / (selectedProgressNumber / 100)) >= 100 ? 100 : (numberOfHours / (selectedProgressNumber / 100)));
            }
            else
            {
                numberOfHours = percentto200;
            }

            StudentEHourProgress.Value = numberOfHours;
            ToolTipService.SetToolTip(
                StudentEHourProgress,
                $"You are {percentto200}% to endorsing, {percentto300}% to endorsing with Honours, and {percentto400}% to endorsing with High Honours."
            );
            progressToEndorsementText.Text = $"{numberOfHours}%";
            ToolTipService.SetToolTip(
                progressToEndorsementText,
                $"You are {percentto200}% to endorsing, {percentto300}% to endorsing with Honours, and {percentto400}% to endorsing with High Honours."
            );
        }
        catch (Exception ex)
        {
            FileMgr.LogError("Unable to parse progress to 200: " + ex.Message);
            StudentEHourProgress.Value = 0;
            ToolTipService.SetToolTip(
                StudentEHourProgress,
                "An error occurred when processing your percent to Endorsement."
            );
            progressToEndorsementText.Text = "0%";
            ToolTipService.SetToolTip(
                progressToEndorsementText,
                "An error occurred when processing your percent to Endorsement."
            );
        }
    }

    private void CreateLayout()
    {
        double acceptedHours = 0;
        double deniedHours = 0;
        double pendingHours = 0;
        double returnedHours = 0;

        static double parseToEnUsDouble(string hours) => double.Parse(hours.Replace(",", "."), CultureInfo.InvariantCulture);

        foreach (var group in AcceptedRequests)
        {
            foreach (var request in group.SubRequests)
            {
                acceptedHours += parseToEnUsDouble(request.Hours);
                CreateMenuItem(request);
            }
        }
        foreach (var group in DeniedRequests)
        {
            foreach (var request in group.SubRequests)
            {
                deniedHours += parseToEnUsDouble(request.Hours);
                CreateMenuItem(request);
            }
        }
        foreach (var group in PendingRequests)
        {
            foreach (var request in group.SubRequests)
            {
                pendingHours += parseToEnUsDouble(request.Hours);
                CreateMenuItem(request);
            }
        }
        foreach (var group in ReturnedRequests)
        {
            foreach (var request in group.SubRequests)
            {
                returnedHours += parseToEnUsDouble(request.Hours);
                CreateMenuItem(request);
            }
        }
        string pending = "";
        if (pendingHours > 0)
        {
            pending = $"{LocalizationService.GetString("Home.PendingHoursText")}: {LocalizationService.FormatNumber(pendingHours.ToString())}\r\n";
        }
        StudentEHours.Text = $"{LocalizationService.GetString("Home.AcceptedHoursText")}: {LocalizationService.FormatNumber(acceptedHours.ToString())}\r\n{pending}{LocalizationService.GetString("Home.AcceptRateText")}: {LocalizationService.FormatNumber((Math.Round((acceptedHours / (returnedHours + deniedHours + acceptedHours)) * 100, 2)).ToString(),0)}% ({LocalizationService.FormatNumber(acceptedHours.ToString())}/{LocalizationService.FormatNumber((returnedHours + deniedHours + acceptedHours).ToString())})";

        ((App)Application.Current).NavigationViewModel.MenuItems[1].MenuItems.Clear();
    }

    private void CreateMenuItem(EHourRequest request)
    {
        NavigationViewItem item = new() { Tag = request.Value, Content = $"{request.Description}", Name = $"{request.Description} - {request.Date}" };
        item.Tapped += (sender, _) =>
        {
            var clickedItem = sender as NavigationViewItem;
            var content = clickedItem?.Content as string;
            if (clickedItem?.Tag is string value)
            {
                _mainWindow = (MainWindow)((App)Application.Current).m_window;
                _mainWindow.OpenRequestViewer(
                    value,
                    loginResult.PhpSessionId,
                    loginResult.StudentAcademy,
                    content,
                    request.State,
                    username,
                    password
                );
                ((App)Application.Current).NavigationView.SelectedItem = ((App)Application.Current).NavigationViewModel.MenuItems[1];
            }
        };
        ((App)Application.Current).NavigationViewModel.MenuItems[1].MenuItems.Add(item);

        // Sort the items alphabetically by Content
        var menu = ((App)Application.Current).NavigationViewModel.MenuItems[1].MenuItems;

        var sorted = menu
            .OrderBy(x => (x as NavigationViewItem)?.Content?.ToString())
            .ToList();

        // Clear and re-add in sorted order
        menu.Clear();
        foreach (var i in sorted)
        {
            menu.Add(i);
        }
    }

    private MainWindow _mainWindow;

    private void RequestButton_Click(object sender, RoutedEventArgs __)
    {
        _mainWindow = (MainWindow)((App)Application.Current).m_window;
        Button clickedButton = (Button)sender;
        string value = (string)clickedButton.Tag;

        var request = AllReturned
            .Concat(AllPending)
            .Concat(AllAccepted)
            .Concat(AllDenied)
            .FirstOrDefault(r => r.Value == value);

        _mainWindow.OpenRequestViewer(
            value,
            loginResult.PhpSessionId,
            loginResult.StudentAcademy,
            request.Description,
            request.State,
            username,
            password
        );
    }
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
    private void HandleRightClick(object sender, RoutedEventArgs e)
    {
        Button clickedButton = (Button)sender;
        string value = (string)clickedButton.Tag;
        var request = AllReturned
            .Concat(AllPending)
            .Concat(AllAccepted)
            .Concat(AllDenied)
            .FirstOrDefault(r => r.Value == value);

        // Create context menu
        MenuFlyout contextMenu = new MenuFlyout();

        MenuFlyoutItem openItem = new MenuFlyoutItem { Text = LocalizationService.GetString("OpenRequest") };
        openItem.Click += (s, args) =>
        {
            _mainWindow = (MainWindow)((App)Application.Current).m_window;
            _mainWindow.OpenRequestViewer(
                value,
                loginResult.PhpSessionId,
                loginResult.StudentAcademy,
                request.Description,
                request.State,
                username,
                password
            );
        };

        MenuFlyoutItem deleteRequest = new MenuFlyoutItem { Text = LocalizationService.GetString("DeleteRequest"), IsEnabled = request.State == Status.Pending};
        deleteRequest.Click += async(_, __) =>
        {
            if (request.State == Status.Pending) //redundant but just in case
            {
                ContentDialogResult res = await dialogManager.ShowDialog(LocalizationService.GetString("ConfirmDeleteTitle"), LocalizationService.PrepareStatement("ConfirmDeleteMessage", request.Description.Split('\n')[0]), LocalizationService.GetString("No"), LocalizationService.GetString("Yes"), null, XamlRoot);
                if (res == ContentDialogResult.Primary)
                {
                    try
                    {
                        ShowDeleteProgressBar();
                        var values = new Dictionary<string, string> { { "del", request.Value } };

                        var content = new FormUrlEncodedContent(values);

                        Uri uri = new Uri("https://academyendorsement.olatheschools.com/");
                        cookieContainer.Add(uri, new Cookie("PHPSESSID", loginResult.PhpSessionId));

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
                            waitForDelete.Hide();
                            await dialogManager.ShowErrorDialog("You are not logged in. Please log in again.", false, XamlRoot);
                        }

                        FileMgr.WriteToFile("delreq.txt", responseString);
                        waitForDeleteProgressBar.IsIndeterminate = false;
                        waitForDeleteProgressBar.Value = 100;
                        waitForDelete.Title = "Deleted Succesfully";
                        waitForDelete.CloseButtonText = "Close";
                        waitForDelete.CloseButtonClick += (_, __) => ((App)App.Current).GoToHomeAfterDel(responseString);
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
        };

        contextMenu.Items.Add(openItem);
            contextMenu.Items.Add(deleteRequest);

        contextMenu.ShowAt(clickedButton);
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
            XamlRoot = XamlRoot,
        };

        // Show the ContentDialog asynchronously
        await waitForDelete.ShowAsync();
    }

    private async void Logout_Click(object _, RoutedEventArgs __)
    {
        ContentDialogResult result = await dialogManager.ShowDialog("Confirm Logout", "Are you sure you want to log out?", "No", "Yes", "", XamlRoot);

        if (result == ContentDialogResult.Primary)
        {
            ((App)Application.Current).NavigationViewModel.MenuItems[1].MenuItems.Clear();
            ((App)Application.Current).LoggedOut();
        }
    }

    private void SortDateOtoN(object _, RoutedEventArgs __)
    {
        Sort("date", "old");
    }

    private void SortDateNtoO(object _, RoutedEventArgs __)
    {
        Sort("date", "new");
    }

    private void SortNameAtoZ(object _, RoutedEventArgs __)
    {
        Sort("name", "a");
    }

    private void SortNameZtoA(object _, RoutedEventArgs __)
    {
        Sort("name", "z");
    }

    private void SortHoursLtoH(object _, RoutedEventArgs __)
    {
        Sort("hours", "low");
    }

    private void SortHoursHtoL(object _, RoutedEventArgs __)
    {
        Sort("hours", "high");
    }

    private void Sort(string way, string from)
    {
        Func<EHourRequestGroup, object> keySelector = way switch
        {
            "date" => group => group.LatestDateTime,
            "name" => group => group.BaseDescription,
            "hours" => group => group.TotalHours,
            _ => group => group.BaseDescription
        };

        bool ascending = from == "old" || from == "a" || from == "low";

        // Use the ApplySort method to sort ObservableCollections
        ApplySort(ReturnedRequests, keySelector, !ascending);
        ApplySort(PendingRequests, keySelector, !ascending);
        ApplySort(AcceptedRequests, keySelector, !ascending);
        ApplySort(DeniedRequests, keySelector, !ascending);
    }

    private void ApplySort(ObservableCollection<EHourRequestGroup> collection,
                           Func<EHourRequestGroup, object> keySelector,
                           bool descending)
    {
        var sorted = descending
            ? collection.OrderByDescending(keySelector).ToList()
            : collection.OrderBy(keySelector).ToList();

        collection.Clear();
        foreach (var item in sorted)
            collection.Add(item);
    }

    private void Search(object sender, TextChangedEventArgs _)
    {
        var text = ((TextBox)sender).Text;

        if (string.IsNullOrWhiteSpace(text))
        {
            ReloadAllRequests();
            return;
        }

        FileMgr.Log("Searching for " + text);

        var searchResults = Searcher.CombinedSearch.SearchAllRequestsCombined(
            text,
            AllReturned,
            AllPending,
            AllAccepted,
            AllDenied,
            maxDistance: 2
        );

        FileMgr.Log("Found " + (searchResults["returned"].Count + searchResults["pending"].Count + +searchResults["accepted"].Count + +searchResults["denied"].Count) + " results");

        PopulateObservableCollection(ReturnedRequests, searchResults["returned"].ToList());
        PopulateObservableCollection(PendingRequests, searchResults["pending"].ToList());
        PopulateObservableCollection(AcceptedRequests, searchResults["accepted"].ToList());
        PopulateObservableCollection(DeniedRequests, searchResults["denied"].ToList());
    }

    private void ReloadAllRequests()
    {
        PopulateObservableCollection(ReturnedRequests, AllReturned);
        PopulateObservableCollection(PendingRequests, AllPending);
        PopulateObservableCollection(AcceptedRequests, AllAccepted);
        PopulateObservableCollection(DeniedRequests, AllDenied);
    }

    public void OnWindowSizeChanged(double windowWidth)
    {
        // Update the UI based on window width
        if (windowWidth < 1008)
        {
            // Single column layout
            RequestsGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            RequestsGrid.ColumnDefinitions[1].Width = new GridLength(0);
        }
        else
        {
            // Two column layout
            RequestsGrid.ColumnDefinitions[0].Width = new GridLength(2, GridUnitType.Star);
            RequestsGrid.ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
        }
    }


    //year in review button

    private void OpenReSync_Click(object _, RoutedEventArgs __)
    {
        Frame.Navigate(typeof(ReSync));
    }

    private async void ReloadHomeContent_Click(object _, RoutedEventArgs __)
    {
        // disable button to prevent double clicking
        ReloadHomeButton.IsEnabled = false;

        //use hoursynccore to refetch home page
        var homerefetch = await HourSyncCore.GetRequestsPage(loginResult.PhpSessionId);
        if (string.IsNullOrWhiteSpace(homerefetch))
        {
            await dialogManager.ShowErrorDialog("Failed to reload content. Please try again later.", false, XamlRoot);
            return;
        }
        
        HourSyncCore.ParseRequests(homerefetch);

        // Update the getresp and doc with the new content
        getresp = homerefetch;

        // Clear existing data
        ReturnedRequests.Clear();
        PendingRequests.Clear();
        AcceptedRequests.Clear();
        DeniedRequests.Clear();

        // call homeloaded
        Home_Loaded(null, null);

        //re-enable button
        ReloadHomeButton.IsEnabled = true;
    }

    private void HandleRightClick()
    {

    }
}

public class EHourRequestGroup
{
    public string BaseDescription { get; set; }
    public double TotalHours { get; set; }
    public string FormattedTotalHours => LocalizationService.FormatNumber(TotalHours.ToString(CultureInfo.InvariantCulture));
    public string DisplayDate { get; set; }
    public DateTime LatestDateTime { get; set; }
    public ObservableCollection<EHourRequest> SubRequests { get; set; } = new();

    public bool IsGroup => SubRequests.Count > 1;
    public bool IsSingle => SubRequests.Count == 1;
    public EHourRequest SingleRequest => SubRequests.FirstOrDefault();
}

public partial class HomeBoolToVisConverter : Microsoft.UI.Xaml.Data.IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => (value is Visibility v && v == Visibility.Visible);
}