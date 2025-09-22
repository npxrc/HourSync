using System;
using System.ComponentModel;
using HourSyncCoreLib;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;

namespace HourSync;

public partial class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => (value is Visibility v && v == Visibility.Visible);
}

public partial class NameMatchConverter : IValueConverter
{
    private bool IsDarkTheme()
    {
        var uiSettings = new UISettings();
        var color = uiSettings.GetColorValue(UIColorType.Background);
        return color.R < 128 && color.G < 128 && color.B < 128;
    }

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is string studentName && !string.IsNullOrEmpty(studentName))
        {
            var app = Application.Current as App;
            var currentUserName = app?.NameOfPerson;

            if (!string.IsNullOrEmpty(currentUserName) && IsNameMatch(currentUserName, studentName))
            {
                if (IsDarkTheme())
                {
                    return new SolidColorBrush(Colors.LightBlue);
                }
                return new SolidColorBrush(Colors.DarkBlue);
            }
        }

        if (IsDarkTheme())
        {
            return new SolidColorBrush(Colors.White);
        }
        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }

    private static bool IsNameMatch(string currentUserName, string leaderboardName)
    {
        var leaderParts = leaderboardName.Split(',');
        if (leaderParts.Length != 2)
            return false;

        var firstName = leaderParts[1].Trim();
        var lastName = leaderParts[0].Trim();

        var newLeaderName = $"{firstName} {lastName}";

        return currentUserName == newLeaderName;
    }
}

public sealed partial class Leaderboard : Page, INotifyPropertyChanged
{
    private LeaderboardData _leaderboardData;

    public LeaderboardData leaderboardData
    {
        get => _leaderboardData;
        set
        {
            _leaderboardData = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(leaderboardData)));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    private bool _isLoading = true;
    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            _isLoading = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsLoading)));
        }
    }


    public Leaderboard()
    {
        InitializeComponent();
        FetchLeaderboard();
        DataContext = this;

        var uiSettings = new UISettings();
        uiSettings.ColorValuesChanged += OnSystemThemeChanged;
    }
    private void OnSystemThemeChanged(UISettings _, object __)
    {
        // Use dispatcher to update UI thread
        DispatcherQueue.TryEnqueue(() => ApplyTheme());
    }

    private void ApplyTheme()
    {
        var saved = leaderboardData;
        leaderboardData = new LeaderboardData();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(leaderboardData)));
        FileMgr.Log("Invoked change, restoring");
        leaderboardData = saved;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(leaderboardData)));
        FileMgr.Log("Restored");
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        var uiSettings = new UISettings();
        uiSettings.ColorValuesChanged -= OnSystemThemeChanged;

        base.OnNavigatedFrom(e);
    }

    private async void FetchLeaderboard()
    {
        try
        {
            IsLoading = true;
            leaderboardData = await HourSyncCore.GetLeaderboard(((App)Application.Current).PhpSessionId);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
