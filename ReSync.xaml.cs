using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using HourSyncCoreLib;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace HourSync;

public sealed partial class ReSync : Page
{
    private readonly List<EHourRequest> Accepted = ((App)Application.Current).Accepted;
    private readonly List<EHourRequest> Returned = ((App)Application.Current).Returned;
    private readonly List<EHourRequest> Pending = ((App)Application.Current).Pending;
    private readonly List<EHourRequest> Denied = ((App)Application.Current).Denied;

    public ReSync()
    {
        InitializeComponent();

        InitializeWebView();
    }

    private async void InitializeWebView()
    {
        try
        {
            await HighlightWebView.EnsureCoreWebView2Async();
            FileMgr.Log("WebView2 initialized successfully");

            HighlightWebView.CoreWebView2.NavigationCompleted += (s, e) =>
            {
                FileMgr.Log($"Navigation completed: {e.IsSuccess}, URI: {HighlightWebView.Source}");
                if (!e.IsSuccess)
                {
                    FileMgr.Log($"Navigation failed with status: {e.WebErrorStatus}");
                }
            };

            HighlightWebView.CoreWebView2.WebMessageReceived += (_, e) =>
            {
                try
                {
                    using var message = JsonDocument.Parse(e.WebMessageAsJson);
                    var root = message.RootElement;

                    if (!root.TryGetProperty("type", out var typeElement) ||
                        typeElement.GetString() != "openRequest")
                    {
                        return;
                    }

                    if (!root.TryGetProperty("requestId", out var requestIdElement))
                    {
                        return;
                    }

                    var requestId = requestIdElement.GetString();
                    if (string.IsNullOrWhiteSpace(requestId))
                    {
                        return;
                    }

                    OpenRequestViewer(requestId);
                }
                catch (Exception ex)
                {
                    FileMgr.Log($"Failed to handle WebView message: {ex.Message}");
                }
            };

            var appPath = Path.GetFullPath(AppContext.BaseDirectory);
            HighlightWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                "appassets.hoursync",
                appPath,
                Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);

            var uri = new Uri("https://appassets.hoursync/highlight-html/highlight.html");
            FileMgr.Log($"Attempting to load: {uri}");
            HighlightWebView.Source = uri;
            HighlightWebView.CoreWebView2.DOMContentLoaded += async (_, __) =>
            {
                var currentYear = DateTime.Now.Year;
                var allRequestsList = GetAllRequests();
                var yearsAvailable = GetAvailableYears(allRequestsList);

                ReSyncData data = new()
                {
                    AllRequests = [allRequestsList],
                    CurrentYear = currentYear,
                    StudentName = ((App)Application.Current).LoginResult.StudentName,
                    YearsAvailable = yearsAvailable
                };

                await Task.Delay(500);
                var dataToBrowser = JsonSerializer.Serialize(data);
                var browserArgument = JsonSerializer.Serialize(dataToBrowser);
                await HighlightWebView.ExecuteScriptAsync($"receiveData({browserArgument});");
            };
        }
        catch (Exception ex)
        {
            FileMgr.Log($"WebView2 initialization failed: {ex.Message}");
        }
    }

    private List<EHourRequest> GetAllRequests()
    {
        return Accepted
            .Concat(Returned)
            .Concat(Pending)
            .Concat(Denied)
            .ToList();
    }

    private static int[] GetAvailableYears(IEnumerable<EHourRequest> requests)
    {
        return requests
            .Select(r => DateTime.TryParse(r.Date, out var date) ? date.Year : 0)
            .Where(y => y > 0)
            .Distinct()
            .OrderByDescending(y => y)
            .ToArray();
    }

    private void OpenRequestViewer(string requestId)
    {
        var app = (App)Application.Current;
        var request = GetAllRequests().FirstOrDefault(r => r.Value == requestId);

        if (request == null)
        {
            FileMgr.Log($"Request not found for id: {requestId}");
            return;
        }

        if (app.m_window is not MainWindow mainWindow)
        {
            FileMgr.Log("Main window not available to open request viewer.");
            return;
        }

        mainWindow.OpenRequestViewer(
            requestId,
            app.LoginResult.PhpSessionId,
            app.LoginResult.StudentAcademy,
            request.Description,
            request.State,
            app.Username,
            app.Password);
    }
}
public record ReSyncData
{
    /// <summary>
    /// The years that ReSync is available for the current student
    /// </summary>
    public required int[] YearsAvailable
    {
        get; init;
    }

    /// <summary>
    /// The current year
    /// </summary>
    public required int CurrentYear
    {
        get; init;
    }

    /// <summary>
    /// Every request the student has submitted
    /// </summary>
    public required List<EHourRequest>[] AllRequests
    {
        get; init;
    }

    /// <summary>
    /// The name of the student
    /// </summary>
    public required string StudentName
    {
        get; init;
    }
}
