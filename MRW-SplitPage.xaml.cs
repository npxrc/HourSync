using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using static HourSync.RequestMaker;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HourSync;

public sealed partial class MRW_SplitPage : UserControl
{
    private readonly MultiRequestSettings _settings;

    private string LSGS(string query)
    {
        try
        {
            return LocalizationService.GetString(query);
        }
        catch
        {
            return query;
        }
    }

    public MRW_SplitPage(MultiRequestSettings settings)
    {
        _settings = settings;
        InitializeComponent();

        EvenlyRadio.Checked += (s, e) => { 
            _settings.SplitStyle = SplitStyle.Evenly;
            _settings.MaxHoursPerRequest = 99.75;
            MaxHoursInput.IsEnabled = false;
            updateExampleText();
        };
        FillRadio.Checked += (s, e) => { 
            _settings.SplitStyle = SplitStyle.Fill; 
            _settings.MaxHoursPerRequest = 99.75;
            MaxHoursInput.IsEnabled = false;
            updateExampleText();
        };
        MaxPerRequestRadio.Checked += (s, e) => { 
            _settings.SplitStyle = SplitStyle.MaxLimit;
            _settings.MaxHoursPerRequest = double.TryParse(MaxHoursInput.Text, out double result) ? result : 75;
            MaxHoursInput.IsEnabled = true;
            updateExampleText();
        };
        MaxHoursInput.TextChanged += (s, e) =>
        {
            if (_settings.SplitStyle == SplitStyle.MaxLimit)
            {
                _settings.MaxHoursPerRequest = double.TryParse(MaxHoursInput.Text, out double result) ? result : 1;
                if (_settings.MaxHoursPerRequest < 1)
                {
                    _settings.MaxHoursPerRequest = 1;
                    MaxHoursInput.Text = "1";
                } else if (_settings.MaxHoursPerRequest > 99.75)
                {
                    _settings.MaxHoursPerRequest = 99.75;
                    MaxHoursInput.Text = "99.75";
                }
                updateExampleText();
            }
        };

        void updateExampleText()
        {
            var calculated = CalculateHourSplits(_settings.ExampleHours, _settings.SplitStyle, _settings.MaxHoursPerRequest);
            List<double> unique = new();
            List<double> timesUsed = new();
            foreach (double i in calculated)
            {
                if (!unique.Contains(i))
                {
                    unique.Add(i);
                    timesUsed.Add(1);
                }
                else
                {
                    //get index
                    double timesNonuniqueUsed = timesUsed[unique.IndexOf(i)];
                    timesNonuniqueUsed++;
                    timesUsed[unique.IndexOf(i)] = timesNonuniqueUsed;
                }
            }
            List<string> uniqueTextStrings = new();
            for (int i = 0; i < unique.Count; i++)
            {
                uniqueTextStrings.Add($"{timesUsed[i]} {(timesUsed[i]==1 ? LSGS("GenericRequest") : LSGS("GenericRequests"))} @ {unique[i]}{LSGS("GenericHrs")}");
            }
            splitStyleExampleText.Text = string.Join(", ", uniqueTextStrings);
        }

        updateExampleText();

        Loaded += MRW_SplitPage_Loaded;
    }

    private void MRW_SplitPage_Loaded(object _, RoutedEventArgs __)
    {
        if (_settings.ExampleHours % 99.75 < 5)
        {
            FillRadio.IsChecked = false;
            FillRadio.Content = LSGS("MRW-SplitPage-FillRadioNotRecommended");
            EvenlyRadio.IsChecked = true; // recommend choosing that because otherwise a request that's 99.75 and a request for 0.25 eHours seems really petty
            EvenlyRadio.Content = LSGS("MRW-SplitPage-EvenlyRadioRecommended");
        }
        FileMgr.Log($"{_settings.ExampleHours}");
        FileMgr.Log($"{_settings.ExampleHours % 99.75}");
    }
}
