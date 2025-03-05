using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;

namespace HourSync;
public sealed partial class Settings : Page
{
    public Settings()
    {
        InitializeComponent();
        ((App)Application.Current).UpdatePresence("settings", "");
    }

    private void SaveSetting(string key, object value)
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        localSettings.Values[key] = value;
    }
    private object RetrieveSetting(string key)
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings.Values.ContainsKey(key))
        {
            return localSettings.Values[key];
        }
        return null;
    }
}