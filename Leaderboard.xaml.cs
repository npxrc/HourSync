using System;
using System.ComponentModel;
using HourSyncCoreLib;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;

namespace HourSync;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, string language)
        => (value is Visibility v && v == Visibility.Visible);
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
    }

    private async void FetchLeaderboard()
    {
        try
        {
            IsLoading = true;
            leaderboardData = await HourSyncCore.FetchLeaderboard(((App)Application.Current).PhpSessionId);
        }
        finally
        {
            IsLoading = false;
            Bindings.Update();
        }
    }
}
