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
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;
using Windows.UI.Text;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Windows.AppLifecycle;
using Windows.ApplicationModel.Core;

namespace HourSync;
public sealed partial class Settings : Page
{
    private Dictionary<string, SettingDefinition> settingsCache = FileMgr.LoadSettings();
    private bool isLanguageSelectorReady;

    private string settingsOptions = "{\r\n  \"aiCompanion\": {\r\n    \"Key\": \"aiCompanion\",\r\n    \"Title\": \"AI Companion\",\r\n    \"Description\": \"The AI companion which you want to use. Only works if AI features are enabled.\",\r\n    \"Type\": \"select\",\r\n    \"IsEnabled\": true,\r\n    \"DefaultValue\": true,\r\n    \"Options\": [\r\n      {\r\n        \"FriendlyName\": \"ChatGPT\",\r\n        \"Key\": \"chatgpt\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Google Gemini\",\r\n        \"Key\": \"gemini\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Claude\",\r\n        \"Key\": \"claude\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"DeepSeek\",\r\n        \"Key\": \"deepseek\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Microsoft Copilot\",\r\n        \"Key\": \"copilot\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Duck AI\",\r\n        \"Key\": \"duck\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Perplexity\",\r\n        \"Key\": \"perplexity\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Grok\",\r\n        \"Key\": \"grok\"\r\n      }\r\n    ],\r\n    \"SelectedValue\": {\r\n      \"FriendlyName\": \"ChatGPT\",\r\n      \"Key\": \"chatgpt\"\r\n    }\r\n  },\r\n  \"endorsementSelection\": {\r\n    \"Key\": \"endorsementSelection\",\r\n    \"Title\": \"Endorsement Level Selection\",\r\n    \"Description\": \"Select whether you want to endorse, endorse with honours, or high honours.\",\r\n    \"Type\": \"select\",\r\n    \"IsEnabled\": true,\r\n    \"DefaultValue\": true,\r\n    \"Options\": [\r\n      {\r\n        \"FriendlyName\": \"200\",\r\n        \"Key\": \"two\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"300\",\r\n        \"Key\": \"three\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"400\",\r\n        \"Key\": \"four\"\r\n      }\r\n    ],\r\n    \"SelectedValue\": {\r\n      \"FriendlyName\": \"200\",\r\n      \"Key\": \"two\"\r\n    }\r\n  },\r\n  \"sortBy\": {\r\n    \"Key\": \"sortBy\",\r\n    \"Title\": \"Sort Order\",\r\n    \"Description\": \"The default way that the app sorts requests. If unset, defaults to last used.\",\r\n    \"Type\": \"select\",\r\n    \"IsEnabled\": true,\r\n    \"DefaultValue\": true,\r\n    \"Options\": [\r\n      {\r\n        \"FriendlyName\": \"Date (Old to New)\",\r\n        \"Key\": \"date-old\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Date (New to Old)\",\r\n        \"Key\": \"date-new\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Name (A to Z)\",\r\n        \"Key\": \"name-a\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Name (Z to A)\",\r\n        \"Key\": \"name-z\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Hours (Low to High)\",\r\n        \"Key\": \"hours-low\"\r\n      },\r\n      {\r\n        \"FriendlyName\": \"Hours (High to Low)\",\r\n        \"Key\": \"hours-high\"\r\n      }\r\n    ],\r\n    \"SelectedValue\": {\r\n      \"FriendlyName\": \"Date (Old to New)\",\r\n      \"Key\": \"date-old\"\r\n    }\r\n  },\r\n  \"aiEnabled\": {\r\n    \"Key\": \"aiEnabled\",\r\n    \"Title\": \"AI Companion\",\r\n    \"Description\": \"Enables an AI companion window in the submission creator\",\r\n    \"Type\": \"bool\",\r\n    \"IsEnabled\": true,\r\n    \"DefaultValue\": false,\r\n    \"Options\": null,\r\n    \"SelectedValue\": null\r\n  },\r\n  \"autologin\": {\r\n    \"Key\": \"autologin\",\r\n    \"Title\": \"Auto Login\",\r\n    \"Description\": \"Automatically logs you into the app\",\r\n    \"Type\": \"bool\",\r\n    \"IsEnabled\": true,\r\n    \"DefaultValue\": true,\r\n    \"Options\": null,\r\n    \"SelectedValue\": null\r\n  }\r\n}";

    public Settings()
    {
        InitializeComponent();
        InitializeSettings();
        InitializeLanguageSelector();
    }

    private List<SettingDefinition> settings = [];

    private void InitializeLanguageSelector()
    {
        var savedLanguage = LocalizationService.GetSavedLanguage();

        foreach (var item in LanguageSelector.Items)
        {
            if (item is ComboBoxItem comboBoxItem && comboBoxItem.Tag is string tag)
            {
                if (string.Equals(tag, savedLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    LanguageSelector.SelectedItem = comboBoxItem;
                    break;
                }
            }
        }

        if (LanguageSelector.SelectedItem == null && LanguageSelector.Items.Count > 0)
        {
            LanguageSelector.SelectedIndex = 0;
        }

        isLanguageSelectorReady = true;
    }

    private async void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!isLanguageSelectorReady)
        {
            return;
        }

        if (LanguageSelector.SelectedItem is not ComboBoxItem selectedItem || selectedItem.Tag is not string selectedLanguage)
        {
            return;
        }

        var existingLanguage = LocalizationService.GetSavedLanguage();
        if (string.Equals(existingLanguage, selectedLanguage, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LocalizationService.SetLanguagePreference(selectedLanguage);

        var restartDialog = new ContentDialog
        {
            Title = LocalizationService.GetString("Language.RestartDialog.Title"),
            Content = LocalizationService.GetString("Language.RestartDialog.Content"),
            PrimaryButtonText = LocalizationService.GetString("Language.RestartDialog.Primary"),
            CloseButtonText = LocalizationService.GetString("Language.RestartDialog.Close"),
            XamlRoot = XamlRoot,
        };

        var result = await restartDialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            AppRestartFailureReason restartError = AppInstance.Restart("");
            if (restartError != AppRestartFailureReason.RestartPending) { return; }
            if (restartDialog.Visibility == Visibility.Visible)
            {
                restartDialog.Hide();
                DialogService dialogmgr = new DialogService();
                await dialogmgr.ShowErrorDialog(LocalizationService.GetString("GenericErrorMessage"), true, XamlRoot);
            }
        }
    }

    private void InitializeSettings()
    {
        try
        {

            var settingsDictionary = JsonSerializer.Deserialize(settingsOptions, SettingsJsonContext.Default.DictionaryStringSettingDefinition);
            settings = [.. settingsDictionary.Values];
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
                FileMgr.Log("Saving value for " + setting.Key);
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
            Title = LocalizationService.GetString("ClearLogTitleText"),
            Content = LocalizationService.GetString("ClearLogContentText"),
            PrimaryButtonText = LocalizationService.GetString("ClearLogPrimaryText"),
            SecondaryButtonText = LocalizationService.GetString("ClearLogSecondaryText"),
            CloseButtonText = LocalizationService.GetString("GenericCancelText"),
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
                    Title = LocalizationService.GetString("OpenLogErrorTitle"),
                    Content = LocalizationService.GetString("OpenLogErrorContent"),
                    PrimaryButtonText = LocalizationService.GetString("GenericOKText"),
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
        Title = LocalizationService.GetString("GenericLoadingText"),
        CloseButtonText = null,
        PrimaryButtonText = null, // Ensure there's no default button
    };

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
            Title = LocalizationService.GetString("GenericLoadingText"),
            PrimaryButtonText = null,
            CloseButtonText = LocalizationService.GetString("GenericCancelText"),
            Content = waitProgressBar,

            // Ensure the ContentDialog is set to the correct XamlRoot
            XamlRoot = XamlRoot,
        };

        // Show the ContentDialog asynchronously
        await waitForInfo.ShowAsync();
    }

    private async void GetMachineType(object _, RoutedEventArgs __)
    {
        ShowLoadingProgressBarAsync();
        string info = await VmChecker.GetMachineType();
        info = info.Trim();
        waitForInfo.Title = LocalizationService.GetString("SettingsDeviceInfoPopupTitle");
        waitForInfo.Content = info;
        waitForInfo.CloseButtonText = LocalizationService.GetString("GenericOKText");
    }

    private async void AboutHourSync(object _, RoutedEventArgs __)
    {
        FileMgr.Log("Getting app directory");
        var notesPath = Path.Combine(AppContext.BaseDirectory, "releaseNotes.json");
        var notesJson = File.ReadAllText(notesPath);

        FileMgr.Log("Notes path: " + notesPath);
        var releaseNotes = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(notesJson);
        FileMgr.Log("Done serializing release notes");
        if (releaseNotes == null)
        {
            FileMgr.Log("Release notes are null");
            return;
        }

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        FileMgr.Log("Assembly version: " + version.ToString());
        var versionString = $"{version.Major}.{version.Minor}.{version.Revision}";
        FileMgr.Log("Version string: " + versionString);

        var richText = new RichTextBlock();

        if (releaseNotes.TryGetValue(versionString, out var currentNotes))
        {
            var currentHeading = new Paragraph { Margin = new Thickness(0, 0, 0, 10) };
            currentHeading.Inlines.Add(new Run { Text = LocalizationService.PrepareStatement("WhatsNewInVersionText", versionString), FontWeight = new FontWeight(550), FontSize = 18 });
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
            missing.Inlines.Add(new Run { Text = $"{LocalizationService.PrepareStatement("NoNotesFoundText", versionString)}." });
            richText.Blocks.Add(missing);
        }

        // Archive of past updates
        foreach (var kvp in releaseNotes.OrderByDescending(r => r.Key))
        {
            if (kvp.Key == versionString) continue; // Skip current

            var heading = new Paragraph { Margin = new Thickness(0, 10, 0, 5) };
            heading.Inlines.Add(new Run { Text = LocalizationService.PrepareStatement("GenericVersionString", kvp.Key), FontWeight = new FontWeight(550), FontSize = 16 });
            richText.Blocks.Add(heading);

            var para = new Paragraph();
            foreach (var note in kvp.Value)
            {
                para.Inlines.Add(new Run { Text = $"• {note}\n" });
            }
            richText.Blocks.Add(para);
        }

        var container = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            MaxHeight = 800
        };

        var grid = new Grid()
        {
            Padding = new Thickness(0, 0, 20, 0)
        };

        // Define rows for the grid
        grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Auto) });
        grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(1, GridUnitType.Star) });

        // Add title to the first row
        var titleTextBlock = new TextBlock
        {
            Text = LocalizationService.GetString("SettingsReleaseNotesTitle"),
            FontSize = 25,
            FontWeight = new FontWeight(600),
            Margin = new Thickness(0, 0, 0, 10)
        };
        Grid.SetRow(titleTextBlock, 0);
        grid.Children.Add(titleTextBlock);

        // Add release notes to the second row
        Grid.SetRow(richText, 1);
        grid.Children.Add(richText);

        container.Content = grid;

        FileMgr.Log("Showing dialog");
        var dialog = new ContentDialog
        {
            Content = container,
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