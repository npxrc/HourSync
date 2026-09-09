using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using HourSyncCoreLib;
using Newtonsoft.Json;

namespace HourSync.Services;

/// <summary>
/// Manages metrics tracking with local storage and optional remote syncing.
/// Metrics are always stored locally. Syncing to remote API is opt-in only.
/// </summary>
public class MetricsSyncManager
{
    private static readonly HttpClient _client = new();
    private readonly string _userId;
    private readonly string _apiBaseUrl;
    private readonly LocalMetricsStorage _localStorage;
    private bool _syncEnabled = false;

    private const string API_BASE = "https://api.hoursync.net";

    public MetricsSyncManager(string userId, string apiBaseUrl = API_BASE)
    {
        _userId = userId;
        _apiBaseUrl = apiBaseUrl;
        _localStorage = new LocalMetricsStorage();
    }

    /// <summary>
    /// Check if user has enabled metrics syncing (reads from FileMgr settings)
    /// </summary>
    public async Task LoadSyncSettingAsync()
    {
        try
        {
            var syncSetting = await FileMgr.GetSettingValueAsync("metrics.sync-enabled");
            _syncEnabled = syncSetting?.ToString()?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;
            FileMgr.Log($"Metrics sync enabled: {_syncEnabled}");
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error loading metrics sync setting: {ex.Message}");
            _syncEnabled = false;
        }
    }

    /// <summary>
    /// Set whether metrics syncing is enabled
    /// </summary>
    public async Task SetSyncEnabledAsync(bool enabled)
    {
        try
        {
            _syncEnabled = enabled;
            var appSettings = FileMgr.LoadAppSettings();
            appSettings["metrics.sync-enabled"] = enabled.ToString();
            FileMgr.SaveAppSettings(appSettings);
            FileMgr.Log($"Set metrics sync enabled to: {enabled}");
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error saving metrics sync setting: {ex.Message}");
        }
    }

    /// <summary>
    /// Track app open - stored locally always
    /// </summary>
    public void TrackAppOpen()
    {
        _localStorage.IncrementMetric("app_opens");
        FileMgr.Log("Tracked app open (local)");
    }

    /// <summary>
    /// Track session time - stored locally always
    /// </summary>
    public void TrackSessionTime(int sessionSeconds)
    {
        _localStorage.AddToMetric("session_time_seconds", sessionSeconds);
        FileMgr.Log($"Tracked session time: {sessionSeconds}s (local)");
    }

    /// <summary>
    /// Track auto-login - stored locally always
    /// </summary>
    public void TrackAutoLogin()
    {
        _localStorage.IncrementMetric("auto_logins");
        FileMgr.Log("Tracked auto-login (local)");
    }

    /// <summary>
    /// Track image viewed - stored locally always
    /// </summary>
    public void TrackImageViewed()
    {
        _localStorage.IncrementMetric("images_viewed");
        FileMgr.Log("Tracked image viewed (local)");
    }

    /// <summary>
    /// Get current month's metrics from local storage
    /// </summary>
    public Dictionary<string, int> GetCurrentMonthMetrics()
    {
        return _localStorage.GetCurrentMonthMetrics();
    }

    /// <summary>
    /// Get all metrics from local storage
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> GetAllMetrics()
    {
        return _localStorage.GetAllMetrics();
    }

    /// <summary>
    /// Get metrics for a specific month (YYYY-MM format)
    /// </summary>
    public Dictionary<string, int> GetMetricsForMonth(string yearMonth)
    {
        return _localStorage.GetMetricsForMonth(yearMonth);
    }

    /// <summary>
    /// Sync all metrics to remote API (if enabled)
    /// Call this when app closes
    /// </summary>
    public async Task SyncMetricsOnCloseAsync()
    {
        if (!_syncEnabled)
        {
            FileMgr.Log("Metrics sync disabled, skipping sync on close");
            return;
        }

        try
        {
            FileMgr.Log("Starting metrics sync on app close...");
            var allMetrics = _localStorage.GetAllMetrics();

            if (allMetrics.Count == 0)
            {
                FileMgr.Log("No metrics to sync");
                return;
            }

            // Send all metrics to API
            await SendMetricsToApiAsync(allMetrics);
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error syncing metrics on close: {ex.Message}");
        }
    }

    /// <summary>
    /// Manually trigger a sync (can be called from Settings page)
    /// </summary>
    public async Task ManualSyncAsync()
    {
        if (!_syncEnabled)
        {
            FileMgr.Log("Metrics sync not enabled");
            return;
        }

        try
        {
            FileMgr.Log("Starting manual metrics sync...");
            var allMetrics = _localStorage.GetAllMetrics();
            await SendMetricsToApiAsync(allMetrics);
            FileMgr.Log("Manual metrics sync completed successfully");
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error during manual metrics sync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Send metrics to the remote API
    /// Endpoint: POST /metrics/sync
    /// </summary>
    private async Task SendMetricsToApiAsync(Dictionary<string, Dictionary<string, int>> metricsData)
    {
        try
        {
            var payload = new
            {
                userId = _userId,
                metrics = metricsData,
                syncedAt = DateTime.UtcNow.ToString("O")
            };

            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            string endpoint = $"{_apiBaseUrl}/metrics/sync";
            var response = await _client.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                FileMgr.Log($"Successfully synced metrics to {endpoint}");
            }
            else
            {
                FileMgr.LogError($"Metrics sync failed with status {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error sending metrics to API: {ex.Message}");
        }
    }
}
