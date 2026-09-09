#pragma warning disable IDE1006 // Naming Styles
using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using static HourSync.RequestMaker;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace HourSync;

public sealed partial class MultiRequestControl : UserControl
{
    public MultiRequestSettings Settings { get; } = new();
    public List<GeneratedRequest> GeneratedRequests = null;
    private List<string> _pages = ["Title", "Split", "Images", "Review"];
    private int _currentPageIndex = 0;
    private Action _callback;
    public MultiRequestControl(string exampleTitle, double exampleHours, string body, Action callback)
    {
        InitializeComponent();
        Settings.ExampleTitle = exampleTitle;
        Settings.ExampleHours= exampleHours;
        Settings.ExampleBody = body;
        _callback = callback;
        _currentPageIndex = 0;

        // display the first page
        NavigateTo(_currentPageIndex);
        FileMgr.Log("Setting to first page");
    }
    private void NavigateTo(int index)
    {
        _currentPageIndex = index;
        DisplayPage(index);
        UpdateControls(index);
    }
    MRW_ReviewPage review = null;
    private void DisplayPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= _pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        // Clear the current content
        switch (_pages[pageIndex])
        {
            case "Title":
                PageContent.Content = new MRW_TitlePage(Settings);
                break;
            case "Split":
                PageContent.Content = new MRW_SplitPage(Settings);
                break;
            case "Images":
                PageContent.Content = new MRW_ImagesPage(Settings);
                break;
            case "Review":
                review = new MRW_ReviewPage(Settings);
                FileMgr.Log("Set review var");
                PageContent.Content = review;
                break;
        }
    }
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
    private void UpdateControls(int pageIndex)
    {
        FileMgr.Log("Updating controls for page index: " + pageIndex);
        FileMgr.Log("Max allowed page index: " + (_pages.Count - 1));
        RequestButtonsPanel.Children.Clear();
        if (pageIndex > 0)
        {
            Button button = new()
            {
                Content = LSGS("GenericBackText")
            };
            button.Click += (_, _) => NavigateTo(_currentPageIndex-1);
            RequestButtonsPanel.Children.Add(button);
        }
        else
        {
            Button button = new()
            {
                Content = LSGS("GenericCancelText")
            };
            button.Click += (_, _) =>
            {
                _callback?.Invoke();
            };
            RequestButtonsPanel.Children.Add(button);
        }
        if (pageIndex < _pages.Count-1)
        {
            Button button = new()
            {
                Content = LSGS("GenericContinueText")
            };
            button.Click += (_, _) => NavigateTo(_currentPageIndex + 1);
            var resources = Application.Current.Resources;

            var accentBrush =
                resources["SystemControlHighlightAccentBrush"] as Brush
                ?? resources["AccentFillColorDefaultBrush"] as Brush;

            resources.TryGetValue("ButtonStyle", out var baseObj);
            var baseButtonStyle = baseObj as Style;

            var continueButtonStyle = new Style(typeof(Button));
            if (baseButtonStyle != null)
            {
                continueButtonStyle.BasedOn = baseButtonStyle;
            }

            continueButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, accentBrush));

            if (baseButtonStyle == null &&
                resources.TryGetValue("ControlCornerRadius", out var radiusObj) &&
                radiusObj is CornerRadius cr)
            {
                continueButtonStyle.Setters.Add(new Setter(Button.CornerRadiusProperty, cr));
            }

            button.Style = continueButtonStyle;
            button.Margin = new Thickness(5, 0, 0, 0);
            RequestButtonsPanel.Children.Add(button);
        }
        else if (pageIndex == _pages.Count - 1)
        {
            Button button = new()
            {
                Content = LSGS("GenericFinishText")
            };
            var resources = Application.Current.Resources;

            button.Click+= (_, _) =>
            {
                if (review != null)
                {
                    FileMgr.Log("Review is NOT null; setting our generatedrequests to review.requests");
                    GeneratedRequests = review.requests;
                }
                _callback?.Invoke();
            };

            var accentBrush =
                resources["SystemControlHighlightAccentBrush"] as Brush
                ?? resources["AccentFillColorDefaultBrush"] as Brush;

            resources.TryGetValue("ButtonStyle", out var baseObj);
            var baseButtonStyle = baseObj as Style;

            var continueButtonStyle = new Style(typeof(Button));
            if (baseButtonStyle != null)
            {
                continueButtonStyle.BasedOn = baseButtonStyle;
            }

            continueButtonStyle.Setters.Add(new Setter(Button.BackgroundProperty, accentBrush));

            if (baseButtonStyle == null &&
                resources.TryGetValue("ControlCornerRadius", out var radiusObj) &&
                radiusObj is CornerRadius cr)
            {
                continueButtonStyle.Setters.Add(new Setter(Button.CornerRadiusProperty, cr));
            }

            button.Style = continueButtonStyle;
            button.Margin = new Thickness(5, 0, 0, 0);
            RequestButtonsPanel.Children.Add(button);
        }
        else
        {
            FileMgr.LogError("Unexpected page index in UpdateControls: " + pageIndex); 
        }
    }

}
