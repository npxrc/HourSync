#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable CA1861 // Avoid constant arrays as arguments
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web;
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
    private List<EHourRequest> eHourRequests = [];

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
        ParseEHourRequests();
        CreateLayout();
        ParseProgressTo200();
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

                    eHourRequests.Add(new EHourRequest
                    {
                        Value = value,
                        Description = description,
                        Hours = hours,
                        Date = date
                    });
                }
            }
        }

        StudentEHours.Text = CleanText(doc.DocumentNode.SelectSingleNode("//table[@id='HourCount']").InnerText);
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
        RequestsPanel.Children.Clear();

        foreach (var request in eHourRequests)
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
            RequestsPanel.Children.Add(requestButton);
        }
    }

    private MainWindow _mainWindow;
    private void RequestButton_Click(object sender, RoutedEventArgs e)
    {
        _mainWindow = (MainWindow)((App)Application.Current).m_window;
        Button clickedButton = (Button)sender;
        string value = (string)clickedButton.Tag;
        string evtName = (string)clickedButton.Content;
        _mainWindow.OpenRequestViewer(value, phpSessionId, evtName, _cookieContainer, _handler, _client, nameOfAcademy);
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