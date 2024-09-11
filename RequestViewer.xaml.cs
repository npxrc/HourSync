#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using HtmlAgilityPack;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Media.Protection.PlayReady;

namespace HourSync;
public sealed partial class RequestViewer : Window
{
    private string id;
    private string phpSessionId;
    private string eventName;
    private CookieContainer cookieContainer;
    private HttpClientHandler handler;
    private HttpClient client;
    private string nameOfAcademy;
    private HtmlDocument doc = new();

    public RequestViewer(string idOfItem, string phpSessionId, string eventName, CookieContainer cookieContainer, HttpClientHandler handler, HttpClient client, string nameOfAcademy)
    {
        InitializeComponent();
        Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
        };
        SystemBackdrop = micaBackdrop;
        ExtendsContentIntoTitleBar = true;
        Title = "Request Viewer";

        id = idOfItem;
        this.phpSessionId = phpSessionId;
        this.eventName = eventName;
        this.cookieContainer = cookieContainer;
        this.handler = handler;
        this.client = client;
        this.nameOfAcademy = nameOfAcademy;

        FileMgr.Log("Running PostAsync()");
        _ = PostAsync();
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

        //var responseString = ReadFromFile("eHourReq.html");

        doc.LoadHtml(responseString);

        var whiteTextNodes = doc.DocumentNode.SelectNodes("//*[@class='whitetext']");
        if (whiteTextNodes != null && whiteTextNodes.Count >= 2)
        {
            FileMgr.Log("Found the things");
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

            FileMgr.Log(dateFinalText.TrimStart());

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

            FileMgr.Log("Adding images to carousel from HTML");
            try
            {
                AddImagesToCarouselFromDoc();
            }
            catch (Exception ex)
            {
                FileMgr.Log($"Exception in AddImagesToCarouselFromDoc: {ex.Message}");
            }
        }
        else if (whiteTextNodes != null)
        {
            FileMgr.Log("Not null");
        }
        else
        {
            FileMgr.Log("Null");
        }
    }

    private void AddImagesToCarouselFromDoc()
    {
        FileMgr.Log("Loading images from HTML");

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
                    src = string.Concat(baseUrl, src.AsSpan(3));
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
            FileMgr.Log("No images found on the page.");
        }
    }

    private static void OnImageTapped(string imageUrl)
    {
        ImageViewer imageViewer = new(imageUrl)
        {
            Title = "Viewing Image - " + imageUrl.Split('/')[^1]
        };
        imageViewer.Activate();
    }
    private ProgressBar waitForDeleteProgressBar = new()
    {
        IsIndeterminate = true,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Width = 200, // Set width as needed
        Height = 20 // Set height as needed
    };
    private ContentDialog waitForDelete = new()
    {
        Title = "Deleting",
        CloseButtonText = null,
        PrimaryButtonText = null // Ensure there's no default button
    };
    private async void ShowLoginProgressBarAsync()
    {
        // Initialize and configure the ContentDialog
        waitForDeleteProgressBar = new()
        {
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 200, // Set width as needed
            Height = 20 // Set height as needed
        };

        waitForDelete = new()
        {
            Title = "Deleting",
            CloseButtonText = null,
            PrimaryButtonText = null, // Ensure there's no default button
            Content = waitForDeleteProgressBar,

            // Ensure the ContentDialog is set to the correct XamlRoot
            XamlRoot = RootGrid.XamlRoot
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
                XamlRoot = RootGrid.XamlRoot
            };
            ContentDialogResult result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                try
                {
                    ShowLoginProgressBarAsync();
                    var values = new Dictionary<string, string>
                    {
                        { "del", id }
                    };

                    var content = new FormUrlEncodedContent(values);

                    Uri uri = new Uri("https://academyendorsement.olatheschools.com/");
                    //_cookieContainer.Add(uri, new Cookie("PHPSESSID", phpSessionId));

                    if (!client.DefaultRequestHeaders.Contains("User-Agent"))
                    {
                        client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
                    }

                    var response = await client.PostAsync("https://academyendorsement.olatheschools.com/deleteRequest.php", content);
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
                    FileMgr.Log("An exception occurred at " + DateTime.Now + " when deleting request " + eventName + ". Exception: " + ex.Message);
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
                await new ContentDialog()
                {
                    Title = "Cannot Delete",
                    Content = "You cannot delete this request because it has already been accepted or denied by your academy instructor. Please contact the instructor of the " + nameOfAcademy + " for further instructions.",
                    PrimaryButtonText = "OK",
                    XamlRoot = RootGrid.XamlRoot
                }.ShowAsync();
            } catch(Exception ex)
            {
                FileMgr.Log(ex.Message);
                await new ContentDialog()
                {
                    Title = "Cannot Delete",
                    Content = "You cannot delete this request because it has already been accepted or denied by your academy instructor. Please contact the instructor of the " + nameOfAcademy + " for further instructions.",
                    PrimaryButtonText = "OK",
                    XamlRoot = RootGrid.XamlRoot
                }.ShowAsync();
            }
        }
    }

    private void GoBackToHomeAndUpdate(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ((App)App.Current).GoToHomeAfterDel(afterDelReqResp);
        this.Close();
    }
}
