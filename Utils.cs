using System;
using System.Linq;
using System.Text;

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
        for (int i = 0; i < 3; i++)
        {
            try
            {
                int fetchedPart = int.Parse(fetchedVersionParts[i]);
                int currentPart = int.Parse(currentVersionParts[i]);

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
        DateTime now = DateTime.Now;
        TimeSpan diff = now - inputDateTime;

        int roundedMinutes = diff.Seconds >= 30
            ? (int)Math.Round(diff.TotalMinutes)
            : (int)Math.Floor(diff.TotalMinutes);

        int days = (int)diff.TotalDays;
        int hours = (int)diff.TotalHours % 24;
        int minutes = roundedMinutes % 60;

        if (days == 0 && hours == 0 && minutes == 0)
            return "Submitted just now.";

        string result = "Submitted ";
        if (days > 0) result += $"{days} day{(days > 1 ? "s" : "")}, ";
        if (hours > 0 || days > 0) result += $"{hours} hour{(hours > 1 ? "s" : "")}, ";
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
