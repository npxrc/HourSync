using System.Collections.Generic;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using static HourSync.RequestMaker;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HourSync;
public sealed partial class MRW_ReviewPage : UserControl
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
    public List<GeneratedRequest> requests = [];
    public MRW_ReviewPage(MultiRequestSettings settings)
    {
        _settings = settings;
        InitializeComponent();
        requests = GenerateRequests(_settings.ExampleTitle, _settings.ExampleBody, _settings.ExampleHours, _settings);
        GenerateSettingsString(settings);
    }
    private void GenerateSettingsString(MultiRequestSettings settings)
    {
        //$"• 
        var titleModeString = $"• {LSGS("MRW-ReviewPage-TitleStyleText")}: ";
        switch (_settings.TitleStyle)
        {
            case TitleStyle.Number:
                titleModeString+=LSGS("MRW-ReviewPage-Number");
                break;
            case TitleStyle.Part:
                titleModeString+=LSGS("MRW-ReviewPage-Part");
                break;
            case TitleStyle.Fraction:
                titleModeString+=LSGS("MRW-ReviewPage-Fraction");
                break;
            case TitleStyle.Brackets:
                titleModeString+=LSGS("MRW-ReviewPage-Brackets");
                break;
        }
        var titleModeContent = new Run() { Text = titleModeString};

        var splitModeString = $"• {LSGS("MRW-ReviewPage-SplitModeText")}: ";
        switch (_settings.SplitStyle)
        {
            case SplitStyle.Fill:
                splitModeString+=LSGS("MRW-SplitPage-FillRadio.Content");
                break;
            case SplitStyle.MaxLimit:
                splitModeString+=LSGS("MRW-SplitPage-MaxPerRequest.Content")+" ("+_settings.MaxHoursPerRequest+LSGS("GenericHrs")+")";
                break;
            case SplitStyle.Evenly:
                splitModeString+=LSGS("MRW-SplitPage-EvenlyRadio.Content");
                break;
        }
        var splitModeContent = new Run() { Text = splitModeString};

        var imageModeString = $"• {LSGS("MRW-ReviewPage-ImageModeText")}: ";
        switch (_settings.ImageMode)
        {
            case ImageMode.All:
                imageModeString += LSGS("MRW-ImagePage-AllRequestsRadio.Content");
                break;
            case ImageMode.FirstOnly:
                imageModeString += LSGS("MRW-ImagePage-FirstRequestOnlyRadio.Content");
                break;
        }
        var imageModeContent = new Run() { Text = imageModeString };

        var text = new TextBlock();
        text.Inlines.Add(titleModeContent);
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(splitModeContent);
        text.Inlines.Add(new LineBreak());
        text.Inlines.Add(imageModeContent);

        SelectedSettingsPanel.Children.Add(text);
    }
}
