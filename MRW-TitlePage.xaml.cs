using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using static HourSync.RequestMaker;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HourSync;

public sealed partial class MRW_TitlePage : UserControl
{
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
    private readonly MultiRequestSettings _settings;
    public MRW_TitlePage(MultiRequestSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        var localizationWarning = LSGS("MRW-TitlePage-LW");
        if (localizationWarning != "NONE")
        {
            LocalizationWarning.Text = localizationWarning;
        }
        else
        {
            LocalizationWarning.Visibility = Visibility.Collapsed;
        }

        NumberRadio.Checked += (s, e) => { _settings.TitleStyle = TitleStyle.Number; updateExampleText(); };
        PartRadio.Checked += (s, e) => { _settings.TitleStyle = TitleStyle.Part; updateExampleText();};
        FractionRadio.Checked += (s, e) => { _settings.TitleStyle = TitleStyle.Fraction; updateExampleText();};
        BracketsRadio.Checked += (s, e) => { _settings.TitleStyle = TitleStyle.Brackets; updateExampleText();};
        void updateExampleText() { 
            titleStyleExampleText.Text = _settings.TitleStyle switch
            {
                TitleStyle.Number => $"{LSGS("GenericExampleText")}: {_settings.ExampleTitle} - 1",
                TitleStyle.Part => $"{LSGS("GenericExampleText")}: {_settings.ExampleTitle} - Part 1",
                TitleStyle.Fraction => $"{LSGS("GenericExampleText")}: {_settings.ExampleTitle} - 1/5",
                TitleStyle.Brackets => $"{LSGS("GenericExampleText")}: {_settings.ExampleTitle} [1]",
                _ => $"{LSGS("GenericExampleText")}: {_settings.ExampleTitle} - 1"
            };
        }

        updateExampleText();
    }
}
