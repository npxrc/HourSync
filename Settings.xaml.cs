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
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel;
using Windows.UI.Text;

namespace HourSync;
public sealed partial class Settings : Page
{
    private Dictionary<string, SettingDefinition> settingsCache = FileMgr.LoadSettings();

    public Settings()
    {
        InitializeComponent();
        InitializeSettings();
    }

    private List<SettingDefinition> settings = [];

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
                // Use the generated type info
                var settingsDictionary = JsonSerializer.Deserialize(json, SettingsJsonContext.Default.DictionaryStringSettingDefinition);
                settings = [.. settingsDictionary.Values];
            }
            else
            {
                settings = [];
            }

            SettingsPanel.ItemsSource = settings;
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error initializing settings: {ex.Message}");
        }
    }

    // Also update your SaveSetting method
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
                    setting.SelectedValue = selectedOption;
                }

                // Update the settingsCache to reflect the new structure
                settingsCache[key] = setting;

                // Serialize using source generation
                var localAppDataPath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                var dataPath = Path.Combine(localAppDataPath, "HourSync");
                var settingsPath = Path.Combine(dataPath, "settings.json");

                var json = JsonSerializer.Serialize(settingsCache, SettingsJsonContext.Default.DictionaryStringSettingDefinition);
                File.WriteAllText(settingsPath, json);
            }
        }
        catch (Exception ex)
        {
            FileMgr.LogError($"Error saving setting: {ex.Message}");
        }
    }


    private void HelpButton_Click(object sender, RoutedEventArgs __)
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

    private void ToggleSwitch_Toggled(object sender, RoutedEventArgs __)
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


    private void DropdownItem_Click(object sender, RoutedEventArgs __)
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

    private async void ComboBox_Loaded(object sender, RoutedEventArgs __)
    {
        if (sender is ComboBox comboBox && comboBox.DataContext is SettingDefinition setting)
        {
            if (setting.Type == "bool" || setting.Options == null)
            {
                return;
            }

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
                FileMgr.Log("A selected option was not found in a list.");
            }
        }
    }

    private async void ClearLog(object _, object __)
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
        {
            closeButtonStyle.BasedOn = baseButtonStyle;
        }

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

    private ProgressBar waitProgressBar = new()
    {
        IsIndeterminate = true,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        Width = 200, // Set width as needed
        Height = 20, // Set height as needed
    };
    private ContentDialog waitForInfo = new()
    {
        Title = "Loading",
        CloseButtonText = null,
        PrimaryButtonText = null, // Ensure there's no default button
    };

    bool isLoadingBarOpen = false;
    private async void ShowLoadingProgressBarAsync()
    {
        // Initialize and configure the ContentDialog
        waitProgressBar = new()
        {
            IsIndeterminate = true,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 200, // Set width as needed
            Height = 20, // Set height as needed
        };

        waitForInfo = new()
        {
            Title = "Loading",
            PrimaryButtonText = null,
            CloseButtonText = "Cancel",
            Content = waitProgressBar,

            // Ensure the ContentDialog is set to the correct XamlRoot
            XamlRoot = XamlRoot,
        };

        // Show the ContentDialog asynchronously
        isLoadingBarOpen = true;
        await waitForInfo.ShowAsync();
        waitForInfo.Closing += (_, __) => isLoadingBarOpen = false;
    }

    private async void GetMachineType(object _, RoutedEventArgs __)
    {
        ShowLoadingProgressBarAsync();
        string info = await VmChecker.GetMachineType();
        info = info.Trim();
        waitForInfo.Title = "Device Information";
        waitForInfo.Content = info;
        waitForInfo.CloseButtonText = "OK";
    }

    private async void AboutHourSync(object _, RoutedEventArgs __)
    {
        var notesPath = Path.Combine(AppContext.BaseDirectory, "releaseNotes.json");
        var notesJson = File.ReadAllText(notesPath);

        var releaseNotes = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(notesJson);

        var package = Package.Current.Id.Version;
        var versionString = $"{package.Major}.{package.Minor}.{package.Revision}";

        var richText = new RichTextBlock();

        if (releaseNotes.TryGetValue(versionString, out var currentNotes))
        {
            var currentHeading = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
            currentHeading.Inlines.Add(new Run { Text = $"What's new in {versionString}", FontWeight = new FontWeight(550), FontSize = 18 });
            richText.Blocks.Add(currentHeading);

            var currentParagraph = new Paragraph();
            foreach (var note in currentNotes)
            {
                currentParagraph.Inlines.Add(new Run { Text = $"• {note}\n" });
            }
            richText.Blocks.Add(currentParagraph);
        }
        else
        {
            var missing = new Paragraph();
            missing.Inlines.Add(new Run { Text = $"No release notes found for {versionString}" });
            richText.Blocks.Add(missing);
        }

        // Archive of past updates
        foreach (var kvp in releaseNotes.OrderByDescending(r => r.Key))
        {
            if (kvp.Key == versionString) continue; // Skip current

            var heading = new Paragraph { Margin = new Thickness(0, 10, 0, 5) };
            heading.Inlines.Add(new Run { Text = $"Version {kvp.Key}", FontWeight = new FontWeight(550), FontSize = 16 });
            richText.Blocks.Add(heading);

            var para = new Paragraph();
            foreach (var note in kvp.Value)
            {
                para.Inlines.Add(new Run { Text = $"• {note}\n" });
            }
            richText.Blocks.Add(para);
        }

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = "Release Notes",
            FontSize = 25,
            FontWeight = new FontWeight(600),
            Margin = new Thickness(0, 0, 0, 10)
        });
        stack.Children.Add(new ScrollViewer
        {
            Content = richText,
            VerticalScrollBarVisibility = ScrollBarVisibility.Visible
        });

        var dialog = new ContentDialog
        {
            Content = stack,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot
        };

        await dialog.ShowAsync();
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