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
            // Get the path to the settings.json file
            string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string settingsFilePath = Path.Combine(appDataPath, "HourSync", "settings.json");

            if (!File.Exists(settingsFilePath))
            {
                FileMgr.Log("settings.json file not found.");
                return "NOSETTINGSFILE";
            }

            // Read and parse the JSON file
            string jsonContent = await File.ReadAllTextAsync(settingsFilePath);
            if (jsonContent.Length < 5)
            {
                return "NOSETTINGSFILE";
            }
            var settings = JObject.Parse(jsonContent);

            // Traverse the JSON object using the key path
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

    public static Dictionary<string, object> LoadSettings()
    {
        try
        {
            var json = ReadFromFile(settingsFileName);
            if (string.IsNullOrEmpty(json))
            {
                return [];
            }

            return JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                ?? [];
        }
        catch (Exception ex)
        {
            Log($"Error loading settings: {ex.Message}");
            return [];
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
}
