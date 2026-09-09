using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HourSync;

internal static class DraftManager
{
    public static void SaveDraft(string title, DateTimeOffset date, string hours, string description, List<string> imagePaths, string filename = "draft.json")
    {
        // Create a proper copy of the image paths list
        List<string> draftImagePaths = [.. imagePaths];

        var draft = new Draft
        {
            Title = title,
            Date = date.Date,
            Hours = hours,
            Description = description,
            ImagePaths = draftImagePaths,
        };

        var localAppDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        var dataPath = Path.Combine(localAppDataPath, "HourSync");

        if (filename.StartsWith("%onedrive%"))
        {
            dataPath = "%onedrive%/HourSync/drafts/";
            filename = Path.GetFileName(filename);


            if (draftImagePaths.Count != 0)
            {
                List<string> oneDrivePaths = [];

                foreach (var path in draftImagePaths)
                {
                    var onedrivePath = $"%onedrive%/HourSync/draftImages/{Path.GetFileName(path)}";
                    oneDrivePaths.Add(onedrivePath);

                    onedrivePath = Environment.ExpandEnvironmentVariables(onedrivePath);

                    // Create directory and copy the file
                    Directory.CreateDirectory(Path.GetDirectoryName(onedrivePath));
                    File.Copy(path, onedrivePath, overwrite: true);
                }

                draft.ImagePaths = oneDrivePaths;
            }
        }

        dataPath = Environment.ExpandEnvironmentVariables(dataPath);
        Directory.CreateDirectory(dataPath);
        var draftFilePath = Path.Combine(dataPath, filename);

        Directory.CreateDirectory(Path.GetDirectoryName(draftFilePath));

        var json = JsonConvert.SerializeObject(draft, Newtonsoft.Json.Formatting.Indented);
        File.WriteAllText(draftFilePath, json);
    }

    public static DraftResult LoadDraft(string filename = "draft.json")
    {
        var localAppDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        var dataPath = Path.Combine(localAppDataPath, "HourSync");
        var draftFilePath = Path.Combine(dataPath, filename);

        if (File.Exists(draftFilePath))
        {
            if (FileMgr.ReadFromFile(filename).Length <= 92)
            {
                return new DraftResult(DraftLoadStatus.Corrupt, null);
            }
            return new DraftResult(DraftLoadStatus.Loaded, JsonConvert.DeserializeObject<Draft>(FileMgr.ReadFromFile(filename)));
        }
        return new DraftResult(DraftLoadStatus.None, null);
    }
    public static bool DeleteDraft()
    {
        var localAppDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        var dataPath = Path.Combine(localAppDataPath, "HourSync");
        var draftFilePath = Path.Combine(dataPath, "draft.json");

        if (File.Exists(draftFilePath))
        {
            File.Delete(draftFilePath);
            return true;
        }
        return false;
    }

    public static List<string> GetRecentDrafts()
    {
        var drafts = new List<string>();

        var localAppDataPath = Environment.GetFolderPath(
            Environment.SpecialFolder.LocalApplicationData
        );
        var dataPath = Path.Combine(localAppDataPath, "HourSync");

        if (Directory.Exists(dataPath))
        {
            var files = Directory.GetFiles(Path.Combine(dataPath, "drafts"), "*.json");

            foreach (var file in files)
            {
                var fileName = Path.GetFileName(file);

                if (fileName.Equals("settings.json", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var content = FileMgr.ReadFromFile(fileName);

                if (!string.IsNullOrEmpty(content) && content.Length > 92)
                {
                    drafts.Add(fileName);
                }
            }
        }

        FileMgr.Log("Found drafts: " + string.Join(", ", drafts) +
                    "\nThis amounts to " + drafts.Count);

        return drafts;
    }

}
#nullable enable
public record DraftResult(DraftLoadStatus Status, Draft? Draft);
#nullable disable
public enum DraftLoadStatus
{
    Loaded,
    Corrupt,
    None
}
public class Draft
{
    public string Title
    {
        get; set;
    }
    public DateTimeOffset? Date
    {
        get; set;
    }
    public string Hours
    {
        get; set;
    }
    public string Description
    {
        get; set;
    }
    public List<string> ImagePaths { get; set; } = [];
}