using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using HourSyncCoreLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HourSync.Services;

/// <summary>
/// Manages local metrics storage. Metrics are stored locally in JSON format,
/// organized by year-month (YYYY-MM). This class handles all read/write operations
/// to the local metrics file.
/// </summary>
public class LocalMetricsStorage
{
    private readonly string _metricsFilePath;
    private readonly object _fileLock = new object();

    public LocalMetricsStorage()
    {
        string appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HourSync"
        );

        if (!Directory.Exists(appDataFolder))
            Directory.CreateDirectory(appDataFolder);

        _metricsFilePath = Path.Combine(appDataFolder, "metrics.json");

        // Initialize file if it doesn't exist
        if (!File.Exists(_metricsFilePath))
        {
            File.WriteAllText(_metricsFilePath, "{}");
        }
    }

    /// <summary>
    /// Get or create the metrics object for the current month (YYYY-MM format)
    /// </summary>
    private string GetCurrentMonthKey()
    {
        return DateTime.Now.ToString("yyyy-MM");
    }

    /// <summary>
    /// Increment a metric counter for the current month
    /// </summary>
    public void IncrementMetric(string metricName)
    {
        lock (_fileLock)
        {
            try
            {
                string json = File.ReadAllText(_metricsFilePath);
                var metricsData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json) 
                    ?? new Dictionary<string, Dictionary<string, int>>();

                string monthKey = GetCurrentMonthKey();

                if (!metricsData.ContainsKey(monthKey))
                {
                    metricsData[monthKey] = new Dictionary<string, int>
                    {
                        { "app_opens", 0 },
                        { "auto_logins", 0 },
                        { "images_viewed", 0 },
                        { "session_time_seconds", 0 }
                    };
                }

                if (!metricsData[monthKey].ContainsKey(metricName))
                {
                    metricsData[monthKey][metricName] = 0;
                }

                metricsData[monthKey][metricName]++;

                string updatedJson = JsonConvert.SerializeObject(metricsData, Formatting.Indented);
                File.WriteAllText(_metricsFilePath, updatedJson);

                FileMgr.Log($"Incremented local metric '{metricName}' for {monthKey}");
            }
            catch (Exception ex)
            {
                FileMgr.LogError($"Error incrementing local metric: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Add to a metric (for time tracking)
    /// </summary>
    public void AddToMetric(string metricName, int value)
    {
        lock (_fileLock)
        {
            try
            {
                string json = File.ReadAllText(_metricsFilePath);
                var metricsData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
                    ?? new Dictionary<string, Dictionary<string, int>>();

                string monthKey = GetCurrentMonthKey();

                if (!metricsData.ContainsKey(monthKey))
                {
                    metricsData[monthKey] = new Dictionary<string, int>
                    {
                        { "app_opens", 0 },
                        { "auto_logins", 0 },
                        { "images_viewed", 0 },
                        { "session_time_seconds", 0 }
                    };
                }

                if (!metricsData[monthKey].ContainsKey(metricName))
                {
                    metricsData[monthKey][metricName] = 0;
                }

                metricsData[monthKey][metricName] += value;

                string updatedJson = JsonConvert.SerializeObject(metricsData, Formatting.Indented);
                File.WriteAllText(_metricsFilePath, updatedJson);

                FileMgr.Log($"Added {value} to local metric '{metricName}' for {monthKey}");
            }
            catch (Exception ex)
            {
                FileMgr.LogError($"Error adding to local metric: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Get all metrics for a specific month (YYYY-MM format)
    /// </summary>
    public Dictionary<string, int> GetMetricsForMonth(string yearMonth)
    {
        lock (_fileLock)
        {
            try
            {
                string json = File.ReadAllText(_metricsFilePath);
                var metricsData = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
                    ?? new Dictionary<string, Dictionary<string, int>>();

                if (metricsData.ContainsKey(yearMonth))
                {
                    return metricsData[yearMonth];
                }

                return new Dictionary<string, int>
                {
                    { "app_opens", 0 },
                    { "auto_logins", 0 },
                    { "images_viewed", 0 },
                    { "session_time_seconds", 0 }
                };
            }
            catch (Exception ex)
            {
                FileMgr.LogError($"Error reading local metrics: {ex.Message}");
                return new Dictionary<string, int>();
            }
        }
    }

    /// <summary>
    /// Get all metrics data (entire file)
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> GetAllMetrics()
    {
        lock (_fileLock)
        {
            try
            {
                string json = File.ReadAllText(_metricsFilePath);
                return JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, int>>>(json)
                    ?? new Dictionary<string, Dictionary<string, int>>();
            }
            catch (Exception ex)
            {
                FileMgr.LogError($"Error reading all metrics: {ex.Message}");
                return new Dictionary<string, Dictionary<string, int>>();
            }
        }
    }

    /// <summary>
    /// Get metrics for the current month
    /// </summary>
    public Dictionary<string, int> GetCurrentMonthMetrics()
    {
        return GetMetricsForMonth(GetCurrentMonthKey());
    }
}
