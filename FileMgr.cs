#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HourSync;

public static partial class FileMgr
{
    private const string settingsFileName = "settings.json";

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
                return [];
            }

            return JsonConvert.DeserializeObject<Dictionary<string, SettingDefinition>>(json)
                ?? [];
        }
        catch (Exception ex)
        {
            Log($"Error loading settings: {ex.Message}");
            return [];
        }
    }

    private const string appDataFolder = "HourSync";

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
            if (DriveRegex().IsMatch(filename) || filename.StartsWith('%'))
            {
                var directory = Environment.ExpandEnvironmentVariables(Path.GetDirectoryName(filename));
                Directory.CreateDirectory(directory);

                var filePath = Path.Combine(directory, Path.GetFileName(filename));
                File.WriteAllText(filePath, content);
            }
            else
            {
                var localAppDataPath = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData
                );
                var dataPath = Path.Combine(localAppDataPath, appDataFolder);
                var filePath = Path.Combine(dataPath, filename);

                Directory.CreateDirectory(dataPath);
                File.WriteAllText(filePath, content);
            }
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
            if (System.Diagnostics.Debugger.IsAttached)
            {
                System.Diagnostics.Trace.WriteLine($"{DateTime.Now} - {toLog}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    public static void StartLogSession()
    {
        string logExists = ReadFromFile("log.txt");
        if (logExists != null)
        {
            Log("----------\r\nLogging started for session " + DateTime.Now);
            return;
        }
        Log("                             HourSync Log\r\nThese logs help for diagnostic purposes and can be deleted at any time.\r\n       Be sure to also check Windows Event Viewer (eventvwr.msc)");
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

    public static void MergeSettings(Dictionary<string, SettingDefinition> downloadedSettings)
    {
        var localSettings = LoadSettings();
        var merged = new Dictionary<string, SettingDefinition>();

        foreach (var kvp in downloadedSettings)
        {
            string key = kvp.Key;
            var downloadedSetting = kvp.Value;

            if (localSettings.TryGetValue(key, out var localSetting))
            {
                if (downloadedSetting.Type == "select")
                {
                    if (!AreOptionsEqual(downloadedSetting.Options, localSetting.Options))
                    {
                        localSetting.Options = downloadedSetting.Options;
                        if (localSetting.SelectedValue == null ||
                            !OptionExists(localSetting.SelectedValue, downloadedSetting.Options))
                        {
                            localSetting.SelectedValue = downloadedSetting.Options?.Count > 0
                                ? downloadedSetting.Options[0]
                                : null;
                        }
                    }
                }
                merged[key] = localSetting;
            }
            else
            {
                merged[key] = downloadedSetting;
            }
        }

        SaveSettings(ConvertForSaving(merged));
    }

    private static bool AreOptionsEqual(List<Option> a, List<Option> b)
    {
        if (a == null && b == null)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        if (a.Count != b.Count)
        {
            return false;
        }

        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].Key != b[i].Key || a[i].FriendlyName != b[i].FriendlyName)
            {
                return false;
            }
        }
        return true;
    }

    private static bool OptionExists(Option option, List<Option> options)
    {
        if (option == null || options == null)
        {
            return false;
        }

        foreach (var opt in options)
        {
            if (opt.Key == option.Key)
            {
                return true;
            }
        }
        return false;
    }

    private static Dictionary<string, object> ConvertForSaving(Dictionary<string, SettingDefinition> dict)
    {
        var result = new Dictionary<string, object>();
        foreach (var kvp in dict)
        {
            result[kvp.Key] = kvp.Value;
        }
        return result;
    }

    public static string ParseAndWriteSettingsJson(string rawJson)
    {
        using JsonDocument doc = JsonDocument.Parse(rawJson);
        var selectSettings = new Dictionary<string, object>();
        var otherSettings = new Dictionary<string, object>();

        foreach (var docElement in doc.RootElement.GetProperty("documents").EnumerateArray())
        {
            var fields = docElement.GetProperty("fields");
            var key = fields.GetProperty("key").GetProperty("stringValue").GetString();
            var type = fields.GetProperty("type").GetProperty("stringValue").GetString();

            var setting = new Dictionary<string, object>
            {
                ["Key"] = key,
                ["Title"] = fields.GetProperty("title").GetProperty("stringValue").GetString(),
                ["Description"] = fields.GetProperty("description").GetProperty("stringValue").GetString(),
                ["Type"] = type,
                ["IsEnabled"] = true,
                ["DefaultValue"] = fields.TryGetProperty("defaultValue", out var defValProp) && defValProp.TryGetProperty("booleanValue", out var boolVal)
                        ? boolVal.GetBoolean()
                        : (object)null,
                ["Options"] = null,
                ["SelectedValue"] = null,
            };

            if (fields.TryGetProperty("options", out var optionsProp) && optionsProp.ValueKind == JsonValueKind.Object)
            {
                if (optionsProp.TryGetProperty("arrayValue", out var arrayVal) && arrayVal.TryGetProperty("values", out var valuesArray))
                {
                    var opts = new List<Dictionary<string, string>>();
                    foreach (var option in valuesArray.EnumerateArray())
                    {
                        var fieldsMap = option.GetProperty("mapValue").GetProperty("fields");

                        var friendlyName = fieldsMap.GetProperty("FriendlyName").GetProperty("stringValue").GetString();
                        var keyName = fieldsMap.TryGetProperty("Key", out var k)
                                        ? k.GetProperty("stringValue").GetString()
                                      : fieldsMap.TryGetProperty("key", out var k2)
                                        ? k2.GetProperty("stringValue").GetString()
                                      : null;

                        opts.Add(new Dictionary<string, string>
                        {
                            ["FriendlyName"] = friendlyName,
                            ["Key"] = keyName,
                        });
                    }

                    setting["Options"] = opts;
                    if (opts.Count > 0)
                    {
                        setting["SelectedValue"] = opts[0];
                    }
                }
            }

            if (type == "select")
            {
                selectSettings[key] = setting;
            }
            else
            {
                otherSettings[key] = setting;
            }
        }

        var finalSettings = new Dictionary<string, object>();
        foreach (var kvp in selectSettings)
        {
            finalSettings[kvp.Key] = kvp.Value;
        }

        foreach (var kvp in otherSettings)
        {
            finalSettings[kvp.Key] = kvp.Value;
        }

#pragma warning disable CA1869 // Cache and reuse 'JsonSerializerOptions' instances
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver() // Enable reflection-based serialization
        };
#pragma warning restore CA1869 // Cache and reuse 'JsonSerializerOptions' instances

        return (string)System.Text.Json.JsonSerializer.Serialize(finalSettings, options);
    }

    //Appsettings.json to replace Applicationdata.localsettings or whatever
    //since that crashed the app

    private const string appSettingsFileName = "appsettings.json";

    public static void SaveAppSettings(Dictionary<string, object> appSettings)
    {
        try
        {
            var json = JsonConvert.SerializeObject(appSettings, Formatting.Indented);
            WriteToFile(appSettingsFileName, json);
        }
        catch (Exception ex)
        {
            LogError($"Error saving app settings: {ex.Message}");
        }
    }

    public static Dictionary<string, object> LoadAppSettings()
    {
        try
        {
            var json = ReadFromFile(appSettingsFileName);
            if (string.IsNullOrEmpty(json))
            {
                return [];
            }

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(json) ?? [];
        }
        catch (Exception ex)
        {
            LogError($"Error loading app settings: {ex.Message}");
            return [];
        }
    }

    [GeneratedRegex(@"^[A-Za-z]:\\")]
    private static partial Regex DriveRegex();
}