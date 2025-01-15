#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web;
using HtmlAgilityPack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;

namespace HourSync;
public sealed partial class Home : Page
{
    private string username;
    private string password;
    private string phpSessionId;
    private string nameOfPerson;
    private string nameOfAcademy;
    private string getresp;
    private CookieContainer _cookieContainer;
    private HttpClientHandler _handler;
    private HttpClient _client;
    private HtmlAgilityPack.HtmlDocument doc = new();
    private List<EHourRequest> ReturnedRequests = [];
    private List<EHourRequest> PendingRequests = [];
    private List<EHourRequest> AcceptedRequests = [];
    private List<EHourRequest> DeniedRequests = [];

    public Home()
    {
        InitializeComponent();
        Loaded += Home_Loaded;
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
                getresp = parameters[5] as string;
                _cookieContainer = parameters[6] as CookieContainer;
                _handler = parameters[7] as HttpClientHandler;
                _client = parameters[8] as HttpClient;

                doc.LoadHtml(getresp);

                StudentName.Text = nameOfPerson;
                StudentAcademy.Text = nameOfAcademy;
            }
            else
            {
                // Handle the case where parameters are missing or incorrect
                throw new ArgumentException("Incorrect number of parameters passed to Home page. Parameters Length was " + parameters.Length);
            }
        }
        else
        {
            // Handle the case where parameters are not in the expected format
            throw new ArgumentException("Parameters passed to Home page are not in the expected format.");
        }
    }


    private void Home_Loaded(object sender, RoutedEventArgs e)
    {
        ParseProgressTo200();
        ParseEHourRequests();
        CreateLayout();
    }

    private void ParseProgressTo200()
    {
        StudentEHourProgress.Value = 0;
        var barval = doc.DocumentNode.SelectSingleNode("//div[@class='bar2']").InnerText.Split('/')[0];
        double.TryParse(barval, NumberStyles.Any, CultureInfo.InvariantCulture, out double percent);
        var percentto300 = ((percent / 3) >= 100 ? 100 : percent / 3);
        var percentto400 = ((percent / 4) >= 100 ? 100 : percent / 4);
        percent = ((percent / 2) >= 100 ? 100 : percent / 2);
        StudentEHourProgress.Value = percent;
        ToolTipService.SetToolTip(StudentEHourProgress, $"You are {percent}% to endorsing, {percentto300}% to endorsing with Honours, and {percentto400}% to endorsing with High Honours.");
        progressToEndorsementText.Text = $"{percent}%";
        ToolTipService.SetToolTip(progressToEndorsementText, $"You are {percent}% to endorsing, {percentto300}% to endorsing with Honours, and {percentto400}% to endorsing with High Honours.");
    }

    private void ParseEHourRequests()
    {
        StudentEHours.Text = CleanText(doc.DocumentNode.SelectSingleNode("//table[@id='HourCount']").InnerText);

        var separators = doc.DocumentNode.SelectNodes("//table[@id='eHourRequests']//tr[not(@class)]");
        if (separators != null && separators.Count >= 5)
        {
            // Extract contents for each category
            var contentMap = new Dictionary<string, string>
        {
            { "ReturnedRequests", GetContentBetween(separators[1], separators[2]) },
            { "PendingRequests", GetContentBetween(separators[2], separators[3]) },
            { "AcceptedRequests", GetContentBetween(separators[3], separators[4]) },
            { "DeniedRequests", GetContentFrom(separators[4]) }
        };

            // Process each category
            foreach (var kvp in contentMap)
            {
                if (kvp.Value.Length >= 10)
                {
                    ProcessRequests(kvp.Value, kvp.Key);
                }
            }
        }
        else
        {
            FileMgr.Log("Insufficient separators found.");
        }
    }

    // Helper methods
    private static string GetContentBetween(HtmlNode startNode, HtmlNode endNode)
    {
        var content = new StringBuilder();
        var currentNode = startNode.NextSibling;

        while (currentNode != null && currentNode != endNode)
        {
            content.Append(currentNode.OuterHtml);
            currentNode = currentNode.NextSibling;
        }

        return content.ToString();
    }

    private static string GetContentFrom(HtmlNode startNode)
    {
        var content = new StringBuilder();
        var currentNode = startNode.NextSibling;

        while (currentNode != null)
        {
            content.Append(currentNode.OuterHtml);
            currentNode = currentNode.NextSibling;
        }

        return content.ToString();
    }

    private void ProcessRequests(string htmlContent, string requestType)
    {
        doc.LoadHtml(htmlContent);
        var rows = doc.DocumentNode.SelectNodes("//tr[@class='entry']");

        if (rows != null)
        {
            foreach (var row in rows)
            {
                var buttonNode = row.SelectSingleNode(".//button[@name='ehours_request_descr']");
                var value = buttonNode.GetAttributeValue("value", string.Empty);
                var description = HttpUtility.HtmlDecode(buttonNode.InnerText.Trim());

                var tdNodes = row.SelectNodes(".//td");
                if (tdNodes != null && tdNodes.Count >= 3)
                {
                    var hours = tdNodes[1].InnerText.Trim();
                    var date = tdNodes[2].InnerText.Trim();

                    var request = new EHourRequest
                    {
                        Value = value,
                        Description = description,
                        Hours = hours,
                        Date = date
                    };

                    // Add to the appropriate list
                    switch (requestType)
                    {
                        case "ReturnedRequests":
                            ReturnedRequests.Add(request);
                            break;
                        case "PendingRequests":
                            PendingRequests.Add(request);
                            break;
                        case "AcceptedRequests":
                            AcceptedRequests.Add(request);
                            break;
                        case "DeniedRequests":
                            DeniedRequests.Add(request);
                            break;
                    }
                }
            }
        }
    }

    private static string CleanText(string text)
    {
        string trimmedText = text.Trim();
        var lines = trimmedText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var trimmedLines = lines.Select(line => line.Trim());
        return string.Join(Environment.NewLine, trimmedLines);
    }

    private void CreateLayout()
    {
        foreach (var request in ReturnedRequests) { CreateButton(request, "returned"); }
        foreach (var request in PendingRequests)  { CreateButton(request, "pending");  }
        foreach (var request in AcceptedRequests) { CreateButton(request, "accepted"); }
        foreach (var request in DeniedRequests)   { CreateButton(request, "denied");   }
    }
    private void CreateButton(EHourRequest request, string type)
    {
        Button requestButton = new Button
        {
            Content = $"{request.Description}\nHours: {request.Hours}\nDate: {request.Date}",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Left,
            Height = 80,
            Margin = new Thickness(0, 0, 0, 10),
            Tag = request.Value
        };
        requestButton.Click += RequestButton_Click;

        switch (type) {
            case "returned":
                ReturnedReqsPanel.Children.Add(requestButton);
                break;
            case "pending":
                PendingReqsPanel.Children.Add(requestButton);
                break;
            case "accepted":
                AcceptedReqsPanel.Children.Add(requestButton);
                break;
            case "denied":
                DeniedReqsPanel.Children.Add(requestButton);
                break;
        }
            
    }

    private MainWindow _mainWindow;
    private void RequestButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow = (MainWindow)((App)Application.Current).m_window;
        Button clickedButton = (Button)sender;
        string value = (string)clickedButton.Tag;
        string evtName = (string)clickedButton.Content;
        string status = "";
        _mainWindow.OpenRequestViewer(value, phpSessionId, evtName, _cookieContainer, _handler, _client, nameOfAcademy, status, username, password);
    }

    private async void Logout_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            Title = "Confirm Logout",
            Content = "Are you sure you want to log out?",
            PrimaryButtonText = "Yes",
            SecondaryButtonText = "No",
            XamlRoot = XamlRoot
        };

        ContentDialogResult result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            ((App)Application.Current).LoggedOut();
        }
    }

    private void SortDateOtoN(object sender, RoutedEventArgs e)
    {
        Sort("date", "old");
    }
    private void SortDateNtoO(object sender, RoutedEventArgs e)
    {
        Sort("date", "new");
    }
    private void SortNameAtoZ(object sender, RoutedEventArgs e)
    {
        Sort("name", "a");
    }
    private void SortNameZtoA(object sender, RoutedEventArgs e)
    {
        Sort("name", "z");
    }
    private void SortHoursLtoH(object sender, RoutedEventArgs e)
    {
        Sort("hours", "low");
    }
    private void SortHoursHtoL(object sender, RoutedEventArgs e)
    {
        Sort("hours", "high");
    }

    private void Sort(string way, string from) // sorting will come soon i promise :D
    {
        switch (way)
        {
            case "date":
                switch (from)
                {
                    case "old":

                        break;
                    case "new":

                        break;
                }
                break;
            case "name":
                switch (from)
                {
                    case "a":

                        break;
                    case "z":

                        break;
                }
                break;
            case "hours":
                switch (from)
                {
                    case "low":

                        break;
                    case "high":

                        break;
                }
                break;
        }
    }
}

public class EHourRequest
{
    public string Value
    {
        get; set;
    }
    public string Description
    {
        get; set;
    }
    public string Hours
    {
        get; set;
    }
    public string Date
    {
        get; set;
    }
}