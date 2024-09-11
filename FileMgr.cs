#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace HourSync;

public static class FileMgr
{
    private static readonly string appDataFolder = "eHours";
    public static string ReadFromFile(string filename)
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
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
        string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "eHours", "log.txt");
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
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            var dataPath = Path.Combine(localAppDataPath, "eHours");

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
        catch (Exception ex)
        {
            ((App)App.Current).m_window.Close();
            return false;
        }
    }
}