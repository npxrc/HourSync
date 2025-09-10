using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

namespace HourSync;
internal static class Utils
{
    public static string IsNewerVersion(string fetchedVersion, string currentVersion)
    {
        // Handle empty or null version strings
        if (string.IsNullOrWhiteSpace(fetchedVersion) || fetchedVersion.Split('.').Length != 3)
        {
            return "error";
        }

        var fetchedVersionParts = fetchedVersion.Split('.');
        var currentVersionParts = currentVersion.Split('.');

        // Check if both version parts have exactly 3 elements (major, minor, patch)
        if (fetchedVersionParts.Length != 3 || currentVersionParts.Length != 3)
        {
            return "error";
        }

        // Loop through each version part
        for (var i = 0; i < 3; i++)
        {
            try
            {
                var fetchedPart = int.Parse(fetchedVersionParts[i]);
                var currentPart = int.Parse(currentVersionParts[i]);

                if (fetchedPart > currentPart)
                {
                    return "true"; // Newer version
                }
                else if (fetchedPart < currentPart)
                {
                    return "false"; // Current version is newer or equal
                }
            }
            catch (FormatException)
            {
                return "error"; // Return error if there is a format issue in version parsing
            }
        }

        return "false"; // Both versions are the same
    }
    public static string FormatTimeAgo(DateTime inputDateTime)
    {
        var now = DateTime.Now;
        var diff = now - inputDateTime;

        var roundedMinutes = diff.Seconds >= 30
            ? (int)Math.Round(diff.TotalMinutes)
            : (int)Math.Floor(diff.TotalMinutes);

        var days = (int)diff.TotalDays;
        var hours = (int)diff.TotalHours % 24;
        var minutes = roundedMinutes % 60;

        if (days == 0 && hours == 0 && minutes == 0)
        {
            return "Submitted just now.";
        }

        var result = "Submitted ";
        if (days > 0)
        {
            result += $"{days} day{(days > 1 ? "s" : "")}, ";
        }

        if (hours > 0 || days > 0)
        {
            result += $"{hours} hour{(hours > 1 ? "s" : "")}, ";
        }

        result += $"{minutes} minute{(minutes > 1 ? "s" : "")} ago.";

        return result;
    }
    public static string JoinDateParts(string[] dateArray)
    {
        return string.Join(":", dateArray.Skip(1));
    }
    public static string FixMojibake(string text)
    {
        return text.Replace("â??", "'")
                   .Replace("â€™", "'")
                   .Replace("â€œ", "\"")
                   .Replace("â€\u009d", "\"")
                   .Replace("â€\"", "–")
                   .Replace("â€”", "—");
    }
    public static Encoding GetEncodingFromCharset(string? charset)
    {
        return charset switch
        {
            "iso-8859-1" or "latin1" => Encoding.GetEncoding("ISO-8859-1"),
            "windows-1252" => Encoding.GetEncoding("windows-1252"),
            _ => Encoding.UTF8
        };
    }

}
// Add this context class for source generation
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, SettingDefinition>))]
[JsonSerializable(typeof(SettingDefinition))]
[JsonSerializable(typeof(Option))]
[JsonSerializable(typeof(List<Option>))]
public partial class SettingsJsonContext : JsonSerializerContext;

// Updated classes for System.Text.Json
public class SettingDefinition
{
    public SettingDefinition()
    {
        Key = string.Empty;
        Title = string.Empty;
        Description = string.Empty;
        Type = string.Empty;
        Options = [];
        SelectedValue = null;
        IsEnabled = true;
        DefaultValue = true;
    }

    [JsonConstructor]
    public SettingDefinition(
        string key,
        string title,
        string description,
        string type,
        bool isEnabled,
        bool defaultValue,
        List<Option> options,
        Option? selectedValue)
    {
        Key = key ?? string.Empty;
        Title = title ?? string.Empty;
        Description = description ?? string.Empty;
        Type = type ?? string.Empty;
        IsEnabled = isEnabled;
        DefaultValue = defaultValue;
        Options = options ?? [];
        SelectedValue = selectedValue;
    }

    [JsonPropertyName("Key")]
    public string Key
    {
        get; set;
    }

    [JsonPropertyName("Title")]
    public string Title
    {
        get; set;
    }

    [JsonPropertyName("Description")]
    public string Description
    {
        get; set;
    }

    [JsonPropertyName("Type")]
    public string Type
    {
        get; set;
    }

    [JsonPropertyName("IsEnabled")]
    public bool IsEnabled
    {
        get; set;
    }

    [JsonPropertyName("DefaultValue")]
    public bool DefaultValue
    {
        get; set;
    }

    [JsonPropertyName("Options")]
    public List<Option> Options
    {
        get; set;
    }

    [JsonPropertyName("SelectedValue")]
    public Option? SelectedValue
    {
        get; set;
    }
}

public class Option
{
    public Option()
    {
        FriendlyName = string.Empty;
        Key = string.Empty;
    }

    [JsonConstructor]
    public Option(string friendlyName, string key)
    {
        FriendlyName = friendlyName ?? string.Empty;
        Key = key ?? string.Empty;
    }

    [JsonPropertyName("FriendlyName")]
    public string FriendlyName
    {
        get; set;
    }

    [JsonPropertyName("Key")]
    public string Key
    {
        get; set;
    }
}
