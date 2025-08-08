#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HourSync;

public class FileMgr
{
    private static readonly string settingsFileName = "settings.json";

    public static void SaveSettings(Dictionary<string, object> settings)
    {
        try
        {
            var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
            WriteToFile(settingsFileName, json);
        }
        catch (Exception ex)
        {
            Log($"Error saving settings: {ex.Message}");
        }
    }

    public static async Task<object> GetSettingValueAsync(string keyPath)
    {
        try
        {
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsFilePath = Path.Combine(appDataPath, "HourSync", "settings.json");

            if (!File.Exists(settingsFilePath))
            {
                FileMgr.Log("settings.json file not found.");
                return "NOSETTINGSFILE";
            }

            string jsonContent = await File.ReadAllTextAsync(settingsFilePath);
            if (jsonContent.Length < 5)
            {
                return "NOSETTINGSFILE";
            }
            var settings = JObject.Parse(jsonContent);

            var tokens = keyPath.Split('.');
            JToken currentToken = settings;
            foreach (var token in tokens)
            {
                currentToken = currentToken[token];
                if (currentToken == null)
                {
                    FileMgr.Log($"Key '{keyPath}' not found in settings.json.");
                    return "NOTFOUND";
                }
            }

            return currentToken.ToObject<object>();
        }
        catch (Exception ex)
        {
            FileMgr.Log($"Error reading settings.json: {ex.Message}");
            return null;
        }
    }

    // Existing LoadSettings now returns Dictionary<string, SettingDefinition>
    public static Dictionary<string, SettingDefinition> LoadSettings()
    {
        try
        {
            var json = ReadFromFile(settingsFileName);
            if (string.IsNullOrEmpty(json))
            {
                return new Dictionary<string, SettingDefinition>();
            }

            return JsonConvert.DeserializeObject<Dictionary<string, SettingDefinition>>(json)
                ?? new Dictionary<string, SettingDefinition>();
        }
        catch (Exception ex)
        {
            Log($"Error loading settings: {ex.Message}");
            return new Dictionary<string, SettingDefinition>();
        }
    }

    private static readonly string appDataFolder = "HourSync";

    public static string ReadFromFile(string filename)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );
            var dataPath = Path.Combine(localAppDataPath, appDataFolder);
            var filePath = Path.Combine(dataPath, filename);

            if (File.Exists(filePath))
            {
                return File.ReadAllText(filePath);
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

    public static void WriteToFile(string filename, string content)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );
            var dataPath = Path.Combine(localAppDataPath, appDataFolder);
            var filePath = Path.Combine(dataPath, filename);

            Directory.CreateDirectory(dataPath);
            File.WriteAllText(filePath, content);
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public static void Log(string toLog)
    {
        string logFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "HourSync",
            "log.txt"
        );
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(logFilePath));
            File.AppendAllText(logFilePath, $"\r\n{toLog}");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public static bool DeleteFile(string filename)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );

            var dataPath = Path.Combine(localAppDataPath, "HourSync");

            var filePath = Path.Combine(dataPath, filename);

            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                return true;
            }
            else
            {
                Console.WriteLine($"File not found: {filePath}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting file: {ex.Message}");
            return false;
        }
    }

    public static bool LogError(string message)
    {
        try
        {
            Log("An exception occurred at " + DateTime.Now + ". Exception: " + message);
            return true;
        }
        catch (Exception)
        {
            ((App)App.Current).m_window.Close();
            return false;
        }
    }

    // New method for merging downloaded settings with on-device settings
    public static void MergeSettings(Dictionary<string, SettingDefinition> downloadedSettings)
    {
        // Load on-device settings
        var localSettings = LoadSettings();
        // Build new merged dictionary
        var merged = new Dictionary<string, SettingDefinition>();

        // For each downloaded setting...
        foreach (var kvp in downloadedSettings)
        {
            string key = kvp.Key;
            var downloadedSetting = kvp.Value;

            if (localSettings.TryGetValue(key, out var localSetting))
            {
                // If the setting type is "select", compare options.
                if (downloadedSetting.Type == "select")
                {
                    if (!AreOptionsEqual(downloadedSetting.Options, localSetting.Options))
                    {
                        // Update options from downloaded
                        localSetting.Options = downloadedSetting.Options;
                        // Ensure selected value is valid
                        if (localSetting.SelectedValue == null ||
                            !OptionExists(localSetting.SelectedValue, downloadedSetting.Options))
                        {
                            // Set to downloaded default (first option) if available.
                            localSetting.SelectedValue = downloadedSetting.Options != null &&
                                                         downloadedSetting.Options.Count > 0
                                ? downloadedSetting.Options[0]
                                : null;
                        }
                    }
                    // If options are the same (ignoring SelectedValue), no further changes.
                }
                // For non-select types, leave the on-device value intact.
                merged[key] = localSetting;
            }
            else
            {
                // Key missing locally: add downloaded setting.
                merged[key] = downloadedSetting;
            }
        }

        // Remove keys that are present on-device but not in downloaded settings
        // (i.e. only keys in merged are written)
        SaveSettings(ConvertForSaving(merged));
    }

    // Helper to compare options list (order and key/friendly name)
    private static bool AreOptionsEqual(List<Option> a, List<Option> b)
    {
        if (a == null && b == null)
            return true;
        if (a == null || b == null)
            return false;
        if (a.Count != b.Count)
            return false;
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Key != b[i].Key || a[i].FriendlyName != b[i].FriendlyName)
                return false;
        }
        return true;
    }

    // Helper to verify if an option exists by key.
    private static bool OptionExists(Option option, List<Option> options)
    {
        if (option == null || options == null)
            return false;
        foreach (var opt in options)
        {
            if (opt.Key == option.Key)
                return true;
        }
        return false;
    }

    // Helper method: convert Dictionary<string, SettingDefinition> into Dictionary<string, object>
    // for saving.
    private static Dictionary<string, object> ConvertForSaving(Dictionary<string, SettingDefinition> dict)
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in dict)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }
}