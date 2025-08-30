#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Newtonsoft.Json;
using Windows.ApplicationModel;

namespace HourSync;
public sealed partial class Settings : Page
{
    private Dictionary<string, SettingDefinition> settingsCache = FileMgr.LoadSettings();

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
            FileMgr.LogError($"Error with opening help button: {ex.Message}");
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
            FileMgr.LogError($"Error with toggle switch toggling: {ex.Message}");
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
            FileMgr.LogError($"Error with dropdown item click: {ex.Message}");
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
        if (sender is ComboBox comboBox && comboBox.DataContext is SettingDefinition setting)
        {
            if (setting.Type == "bool" || setting.Options == null) return;
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
                FileMgr.Log($"A selected option was not found in a list.");
            }
        }
    }

    private async void ClearLog(object sender, object e)
    {
        var confirm = new ContentDialog
        {
            Title = "Confirm",
            Content = "Click \"Clear\" to confirm you would like to clear the log. These help for diagnostic purposes and are stored in your AppData folder.",
            PrimaryButtonText = "Clear",
            SecondaryButtonText = "Open Log",
            CloseButtonText = "Cancel",
            XamlRoot = XamlRoot
        };

        var resources = Application.Current.Resources;

        // Same idea as {ThemeResource SystemControlHighlightAccentBrush}
        var accentBrush =
            resources["SystemControlHighlightAccentBrush"] as Brush
            ?? resources["AccentFillColorDefaultBrush"] as Brush; // WinUI 3 token on newer builds

        // Try to get the stock Button style to preserve template, corner radius, states, etc.
        resources.TryGetValue("ButtonStyle", out var baseObj);
        var baseButtonStyle = baseObj as Style;

        var closeButtonStyle = new Style(typeof(Button));
        if (baseButtonStyle != null)
            closeButtonStyle.BasedOn = baseButtonStyle;

        closeButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, accentBrush));

        // If we couldn't base on the stock style, apply the theme corner radius token
        if (baseButtonStyle == null &&
            resources.TryGetValue("ControlCornerRadius", out var radiusObj) &&
            radiusObj is CornerRadius cr)
        {
            closeButtonStyle.Setters.Add(new Setter(Button.CornerRadiusProperty, cr));
        }

        confirm.CloseButtonStyle = closeButtonStyle;

        var res = await confirm.ShowAsync();
        if (res == ContentDialogResult.Primary)
        {
            FileMgr.WriteToFile("log.txt", "Log cleared on " + DateTime.Now + "\r\n----------");
        }
        else if (res == ContentDialogResult.Secondary)
        {
            var localAppDataPath = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData
            );
            var dataPath = Path.Combine(localAppDataPath, "HourSync");
            var filePath = Path.Combine(dataPath, "log.txt");
            try
            {
                Process.Start(new ProcessStartInfo { FileName = filePath, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    Title = "Open Draft Error",
                    Content =
                        "An error occurred when opening the log. Take your laptop to tech at this point brochacho.",
                    PrimaryButtonText = "OK",
                    XamlRoot = XamlRoot,
                };
                await dialog.ShowAsync();
                FileMgr.Log(
                    "An exception occurred at "
                        + DateTime.Now
                        + " when opening the log. Exception: "
                        + ex.Message
                );
            }
        }
    }

    private void GetMachineType(object sender, RoutedEventArgs e)
    {
        _ = new ContentDialog()
        {
            Title = "Device Information",
            Content = VmChecker.GetMachineType().Trim(),
            XamlRoot = XamlRoot,
            CloseButtonText = "OK"
        }.ShowAsync();
    }

    private void AboutHourSync(object sender, RoutedEventArgs e)
    {
        _ = new ContentDialog()
        {
            Title = "About HourSync - " + Package.Current.Id.Version,
            Content = ""
        };
    }
}

public class SettingDefinition
{
    // Explicit default constructor so Json.NET never complains
    public SettingDefinition()
    {
        Key = string.Empty;
        Title = string.Empty;
        Description = string.Empty;
        Type = string.Empty;
        Options = new List<Option>();
        SelectedValue = null;
        IsEnabled = true;
        DefaultValue = true;
    }

    [JsonConstructor]
    public SettingDefinition(
        string Key,
        string Title,
        string Description,
        string Type,
        bool IsEnabled,
        bool DefaultValue,
        List<Option> Options,
        Option SelectedValue)
    {
        this.Key = Key ?? string.Empty;
        this.Title = Title ?? string.Empty;
        this.Description = Description ?? string.Empty;
        this.Type = Type ?? string.Empty;
        this.IsEnabled = IsEnabled;
        this.DefaultValue = DefaultValue;
        this.Options = Options ?? new List<Option>();
        this.SelectedValue = SelectedValue;
    }

    public string Key
    {
        get; set;
    }
    public string Title
    {
        get; set;
    }
    public string Description
    {
        get; set;
    }
    public string Type
    {
        get; set;
    } // "bool" or "select"
    public bool IsEnabled
    {
        get; set;
    }
    public bool DefaultValue
    {
        get; set;
    }
    public List<Option> Options
    {
        get; set;
    }
    public Option SelectedValue
    {
        get; set;
    }
}

public class Option
{
    // Explicit default constructor
    public Option()
    {
        FriendlyName = string.Empty;
        Key = string.Empty;
    }

    [JsonConstructor]
    public Option(string FriendlyName, string Key)
    {
        this.FriendlyName = FriendlyName ?? string.Empty;
        this.Key = Key ?? string.Empty;
    }

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