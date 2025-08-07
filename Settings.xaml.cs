#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Newtonsoft.Json;

namespace HourSync;
public sealed partial class Settings : Page
{
    private Dictionary<string, object> settingsCache = FileMgr.LoadSettings();

    public Settings()
    {
        InitializeComponent();
        InitializeSettings();
    }

    private List<SettingDefinition> settings = new();

    private void InitializeSettings()
    {
        try
        {
            var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dataPath = Path.Combine(localAppDataPath, "HourSync");
            var settingsPath = Path.Combine(dataPath, "settings.json");

            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settingsDictionary = JsonConvert.DeserializeObject<Dictionary<string, SettingDefinition>>(json);
                settings = settingsDictionary.Values.ToList();
            }
            else
            {
                settings = new List<SettingDefinition>();
            }

            // Bind the settings list to the ListView
            SettingsPanel.ItemsSource = settings;
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error initializing settings: {ex.Message}");
        }
    }


    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is HyperlinkButton helpButton)
            {
                var setting = (SettingDefinition)helpButton.DataContext;
                var teachingTip = (TeachingTip)helpButton.FindName("TeachingTip");
                teachingTip.Target = helpButton;
                teachingTip.IsOpen = true;
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error: {ex.Message}");
        }
    }

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is ToggleSwitch toggle && toggle.DataContext is SettingDefinition setting)
            {
                SaveSetting(setting.Key, toggle.IsOn);
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error: {ex.Message}");
        }
    }


    private void SaveSetting(string key, object value)
    {
        try
        {
            // Find the setting in the list and update its value
            var setting = settings.Find(s => s.Key == key);
            if (setting != null)
            {
                if (value is bool boolValue)
                {
                    setting.IsEnabled = boolValue;
                }
                else if (value is Option selectedOption)
                {
                    setting.SelectedValue = selectedOption; // Save the entire Option object
                }

                // Update the settingsCache to reflect the new structure
                settingsCache[key] = setting;

                // Serialize the updated settingsCache back to the JSON file
                var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dataPath = Path.Combine(localAppDataPath, "HourSync");
                var settingsPath = Path.Combine(dataPath, "settings.json");

                var json = JsonConvert.SerializeObject(settingsCache, Formatting.Indented);
                File.WriteAllText(settingsPath, json);
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error saving setting: {ex.Message}");
        }
    }



    private void DropdownItem_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is SettingDefinition setting)
            {
                // Get the selected Option object from the ComboBox
                if (comboBox.SelectedItem is Option selectedOption)
                {
                    setting.SelectedValue = selectedOption;
                    comboBox.SelectedIndex = setting.Options.IndexOf(selectedOption);

                    // Save the updated setting to the settings file
                    SaveSetting(setting.Key, setting.SelectedValue);
                }
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error: {ex.Message}");
        }
    }


    private bool? RetrieveSetting(string key)
    {
        try
        {
            FileMgr.Log($"Retrieving {key} from settings.json");
            if (settingsCache.TryGetValue(key, out var value) && value is SettingDefinition setting)
            {
                FileMgr.Log($"Retrieved {key}.enabled from settings.json: {setting.IsEnabled}");
                return setting.IsEnabled;
            }
            FileMgr.Log($"Retrieved {key} from settings.json: nothing was found");
            return null;
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error retrieving setting: {ex.Message}");
            return null;
        }
    }

    private async void ComboBox_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            if (sender is ComboBox comboBox && comboBox.DataContext is SettingDefinition setting)
            {
                if (setting.Options == null) return;
                FileMgr.Log(setting.SelectedValue.Key);
                await Task.Delay(100); // Replaced Thread.Sleep with Task.Delay to fix CS4008  
                // Set the selected item in the ComboBox
                var selectedOption = setting.Options.FirstOrDefault(o => o.Key == setting.SelectedValue.Key);
                if (selectedOption != null)
                {
                    comboBox.SelectedItem = selectedOption;
                    comboBox.SelectedIndex = setting.Options.IndexOf(selectedOption);
                }
                else
                {
                    FileMgr.Log($"Selected option not found in the list: {setting.SelectedValue.Key}");
                }
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error: {ex.Message}");
        }
    }
}

public class SettingDefinition
{
    public string Key { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string Type { get; set; } // "bool" or "select"
    public bool IsEnabled { get; set; } = true;
    public bool DefaultValue { get; set; } = true;
    public List<Option> Options { get; set; } // For dropdown menus
    public Option SelectedValue { get; set; } // Changed from string to Option
}

public class Option
{
    public string FriendlyName
    {
        get; set;
    }
    public string Key
    {
        get; set;
    }
}
public partial class BoolVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString() == "bool" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}
public partial class SelectVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value?.ToString() == "select" ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}