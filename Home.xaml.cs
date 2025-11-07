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
using HourSyncCoreLib;
using HtmlAgilityPack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HourSync;

public sealed partial class Home : Page
{
    private LoginResult loginResult;
    private string username;
    private string password;
    private string getresp;
    private HtmlDocument doc = new();

    // Change these to ObservableCollection for UI binding
    public ObservableCollection<EHourRequest> ReturnedRequests = [];
    public ObservableCollection<EHourRequest> PendingRequests = [];
    public ObservableCollection<EHourRequest> AcceptedRequests = [];
    public ObservableCollection<EHourRequest> DeniedRequests = [];

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

        var parsed = HourSyncCore.ParseRequests(getresp);  // static call from the DLL

        // Convert Lists to ObservableCollections
        PopulateObservableCollection(ReturnedRequests, parsed.Returned ?? []);
        PopulateObservableCollection(PendingRequests, parsed.Pending ?? []);
        PopulateObservableCollection(AcceptedRequests, parsed.Accepted ?? []);
        PopulateObservableCollection(DeniedRequests, parsed.Denied ?? []);

        // Keep master lists as regular Lists
        AllReturned = (parsed.Returned ?? []).ToList();
        AllPending = (parsed.Pending ?? []).ToList();
        AllAccepted = (parsed.Accepted ?? []).ToList();
        AllDenied = (parsed.Denied ?? []).ToList();

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
    private void PopulateObservableCollection(ObservableCollection<EHourRequest> target, List<EHourRequest> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
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

        foreach (var request in AcceptedRequests)
        {
            acceptedHours += double.TryParse(request.Hours, out var hrs) ? hrs : 0;
            CreateMenuItem(request);
        }
        foreach (var request in DeniedRequests)
        {
            deniedHours += double.TryParse(request.Hours, out var hrs) ? hrs : 0;
            CreateMenuItem(request);
        }
        foreach (var request in PendingRequests)
        {
            pendingHours += double.TryParse(request.Hours, out var hrs) ? hrs : 0;
            CreateMenuItem(request);
        }
        foreach (var request in ReturnedRequests)
        {
            returnedHours += double.TryParse(request.Hours, out var hrs) ? hrs : 0;
            CreateMenuItem(request);
        }
        string pending = "";
        if (pendingHours > 0)
        {
            pending = $"Pending Hours: {pendingHours}\r\n";
        }
        StudentEHours.Text = $"Accepted Hours: {acceptedHours}\r\n{pending}Accept Rate: {(acceptedHours / (returnedHours + deniedHours + acceptedHours)) * 100}% ({acceptedHours}/{returnedHours + deniedHours + acceptedHours})";

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

        // Find the request by value - now search in ObservableCollections
        var request = ReturnedRequests
            .Concat(PendingRequests)
            .Concat(AcceptedRequests)
            .Concat(DeniedRequests)
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
        Func<EHourRequest, object> keySelector = way switch
        {
            "date" => request => DateTime.Parse(request.Date),
            "name" => request => request.Description,
            "hours" => request => decimal.Parse(request.Hours),
            _ => request => request.Description
        };

        bool ascending = from == "old" || from == "a" || from == "low";

        // Use the ApplySort method to sort ObservableCollections
        ApplySort(ReturnedRequests, keySelector, !ascending);
        ApplySort(PendingRequests, keySelector, !ascending);
        ApplySort(AcceptedRequests, keySelector, !ascending);
        ApplySort(DeniedRequests, keySelector, !ascending);
    }

    private void ApplySort(ObservableCollection<EHourRequest> collection,
                           Func<EHourRequest, object> keySelector,
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

        ReplaceCollection(ReturnedRequests, searchResults["returned"]);
        ReplaceCollection(PendingRequests, searchResults["pending"]);
        ReplaceCollection(AcceptedRequests, searchResults["accepted"]);
        ReplaceCollection(DeniedRequests, searchResults["denied"]);
    }

    private void ReloadAllRequests()
    {
        ReplaceCollection(ReturnedRequests, AllReturned);
        ReplaceCollection(PendingRequests, AllPending);
        ReplaceCollection(AcceptedRequests, AllAccepted);
        ReplaceCollection(DeniedRequests, AllDenied);
    }

    private void ReplaceCollection(ObservableCollection<EHourRequest> target,
                                   IEnumerable<EHourRequest> items)
    {
        target.Clear();
        foreach (var item in items)
        {
            target.Add(item);
        }
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
}