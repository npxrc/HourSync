using Microsoft.UI.Xaml;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Media.Protection.PlayReady;
using System;
using System.IO;
using HtmlAgilityPack;
using System.Reflection.Metadata;
using System.Web;
using Microsoft.UI.Xaml.Controls;
using System.Linq;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Dispatching;
using CommunityToolkit.WinUI;
using Microsoft.UI.Xaml.Media;
using Windows.Storage.Streams;
using Windows.Storage;
using Windows.Storage.Pickers;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HourSync;
public sealed partial class RequestViewer : Window
{
    private string id;
    private string phpSessionId;
    private string eventName;
    private CookieContainer cookieContainer;
    private HttpClientHandler handler;
    private HttpClient client;
    private HtmlDocument doc = new();
    public RequestViewer(string idOfItem, string phpSessionId, string eventName, CookieContainer cookieContainer, HttpClientHandler handler, HttpClient client)
    {
        this.InitializeComponent();
        Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
        micaBackdrop.Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base;
        this.SystemBackdrop = micaBackdrop;
        ExtendsContentIntoTitleBar = true;
        this.Title = "Request Viewer";

        this.id = idOfItem;
        this.phpSessionId = phpSessionId;
        this.eventName = eventName;
        this.cookieContainer = cookieContainer;
        this.handler = handler;
        this.client = client;

        Log("Running PostAsync()");
        PostAsync();
    }
    private async Task PostAsync()
    {
        var values = new Dictionary<string, string>
        {
            { "ehours_request_descr", id }
        };

        var content = new FormUrlEncodedContent(values);

        Uri uri = new Uri("https://academyendorsement.olatheschools.com/");
        cookieContainer.Add(uri, new Cookie("PHPSESSID", phpSessionId));

        if (!client.DefaultRequestHeaders.Contains("User-Agent"))
        {
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
        }

        var response = await client.PostAsync("https://academyendorsement.olatheschools.com/Student/eHourDescription.php", content);
        var responseString = await response.Content.ReadAsStringAsync();
        doc.LoadHtml(responseString);

        var whiteTextNodes = doc.DocumentNode.SelectNodes("//*[@class='whitetext']");
        if (whiteTextNodes != null && whiteTextNodes.Count >= 2)
        {
            Log("Found the things");
            string reqdHrs = HttpUtility.HtmlDecode(whiteTextNodes[0].InnerText);
            string dateSubmitted = HttpUtility.HtmlDecode(whiteTextNodes[1].InnerText);
            string desc = HttpUtility.HtmlDecode(doc.DocumentNode.SelectSingleNode("//textarea[@id='description']")?.InnerText);

            eventTitle.Text = eventName.Split('\n')[0];
            reqdEHourCount.Text = reqdHrs.Split(':')[1].Split(' ')[1];
            if (doc.DocumentNode.SelectSingleNode("//*[@id='Delete']").InnerHtml.Length > 1)
            {
                pendingStatus.Text = "Pending";
                progressBar.ShowPaused = true;
            }
            else
            {
                pendingStatus.Text = "Accepted";
                progressBar.IsIndeterminate = false;
                progressBar.Value = 100;
            }
            var dateArray = dateSubmitted.Split(':');
            string dateFinalText = "";
            for (int i = 1; i < dateArray.Length; i++)
            {
                if (i == 1)
                {
                    dateFinalText += dateArray[i];
                }
                else
                {
                    dateFinalText += (":" + dateArray[i]);
                }
            }
            dateSubtd.Text = dateFinalText;

            Log(dateFinalText.TrimStart());

            try
            {
                // Parse the input string to a DateTime object
                DateTime inputDateTime = DateTime.ParseExact(dateFinalText.TrimStart(), "yyyy-MM-dd HH:mm:ss", null);

                // Get the current time
                DateTime currentDateTime = DateTime.Now;

                // Calculate the time difference
                TimeSpan timeDifference = currentDateTime - inputDateTime;

                // Determine if we need to round the minutes
                int roundedMinutes;
                if (timeDifference.Seconds >= 30)
                {
                    roundedMinutes = (int)Math.Round(timeDifference.TotalMinutes);
                }
                else
                {
                    roundedMinutes = (int)Math.Floor(timeDifference.TotalMinutes);
                }

                // Calculate the components of the time difference
                int days = (int)timeDifference.TotalDays;
                int hours = (int)timeDifference.TotalHours % 24;
                int minutes = roundedMinutes % 60;

                // Format the output based on the components
                string result = "Submitted ";
                if (days > 0)
                {
                    result += $"{days} day{(days > 1 ? "s" : "")}, ";
                }
                if (hours > 0 || days > 0)
                {
                    result += $"{hours} hour{(hours > 1 ? "s" : "")}, ";
                }
                if (minutes > 0 || hours > 0 || days > 0)
                {
                    result += $"{minutes} minute{(minutes > 1 ? "s" : "")} ago.";
                }
                else
                {
                    result = "Submitted just now.";
                }

                // Output the result
                submittedTimeAgoText.Text = result;
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Error parsing date: {ex.Message}");
            }

            eventBody.Text = desc;

            Log("Adding images to carousel from HTML");
            try
            {
                AddImagesToCarouselFromDoc();
            }
            catch (Exception ex)
            {
                Log($"Exception in AddImagesToCarouselFromDoc: {ex.Message}");
            }
        }
        else if (whiteTextNodes != null)
        {
            Log("Not null");
        }
        else
        {
            Log("Null");
        }
    }

    private async void AddImagesToCarouselFromDoc()
    {
        Log("Loading images from HTML");

        // Create a base URL for relative paths
        string baseUrl = "https://academyendorsement.olatheschools.com/";

        // Select all image nodes
        var imgNodes = doc.DocumentNode.SelectNodes("//img");
        if (imgNodes != null)
        {
            foreach (var imgNode in imgNodes)
            {
                var src = imgNode.GetAttributeValue("src", string.Empty);

                // If src contains "../", it needs to be fixed
                if (src.StartsWith("../"))
                {
                    src = baseUrl + src.Substring(3);
                }

                // Create a BitmapImage
                var bitmapImage = new BitmapImage(new Uri(src));

                // Create an Image control
                var img = new Image
                {
                    MaxWidth = 600,
                    MaxHeight = 450,
                    Source = bitmapImage,
                    Margin = new Thickness(0, 0, 5, 0)
                };

                // Attach click event handler
                img.Tapped += (sender, e) => OnImageTapped(src);

                // Add to ImagePanel
                ImagePanel.Children.Add(img);
            }
        }
        else
        {
            Log("No images found on the page.");
        }
    }

    private void OnImageTapped(string imageUrl)
    {
        ImageViewer imageViewer = new(imageUrl);
        imageViewer.Title = "Viewing Image - "+imageUrl.Split('/')[imageUrl.Split('/').Length - 1];
        imageViewer.Activate();
    }

    private string ReadFromFile(string filename)
    {
        try
        {
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string dataPath = Path.Combine(localAppDataPath, "eHours");

            string filePath = Path.Combine(dataPath, filename);

            if (File.Exists(filePath))
            {
                string textFromFile = File.ReadAllText(filePath);
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
    private void WriteToFile(string filename, string toWrite)
    {
        try
        {
            string localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            string dataPath = Path.Combine(localAppDataPath, "eHours");

            if (!Directory.Exists(dataPath))
            {
                Directory.CreateDirectory(dataPath);
            }

            string filePath = Path.Combine(dataPath, filename);

            string textToWrite = toWrite;
            File.WriteAllText(filePath, textToWrite);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }
    private void Log(string toLog)
    {
        WriteToFile("log.txt", (ReadFromFile("log.txt") + $"\r\n{toLog}"));
    }
}
