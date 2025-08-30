#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible
// MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using HourSyncCoreLib;
using Microsoft.UI.Xaml;

namespace HourSync;

public sealed partial class MainWindow : Window
{
    private RequestViewer _requestViewer;

    public MainWindow()
    {
        InitializeComponent();
        Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt,
        };
        SystemBackdrop = micaBackdrop;
        ExtendsContentIntoTitleBar = true;
        Title = "HourSync";

        Closed += Closing;
    }

    private void Closing(object sender, WindowEventArgs args)
    {
        _requestViewer?.Close();
        ((App)Application.Current).client.Dispose();
    }

    public void OpenRequestViewer(
        string id,
        string phpSessionId,
        string nameOfAcademy,
        string eventName,
        HourSyncCore.Status status,
        string username,
        string password
    )
    {
        _requestViewer = new RequestViewer(
            id,
            phpSessionId,
            nameOfAcademy,
            eventName,
            status,
            username,
            password
        );
        _requestViewer.Activate();
    }

    // One dictionary is enough.
    private Dictionary<string, RequestViewer> openedEditors = [];

    public bool NowEditingViewer(string id, RequestViewer viewer)
    {
        // If already editing this ID, don’t allow a second editor.
        if (openedEditors.TryGetValue(id, out var value))
        {
            return value == viewer;
        }

        // Otherwise, register it.
        openedEditors[id] = viewer;
        return true;
    }
    public void ClosedEditor(string id, RequestViewer viewer)
    {
        FileMgr.Log("Closing window " + id);
        if (openedEditors.TryGetValue(id, out var existingViewer))
        {
            // Only remove if the instance matches what we expect
            if (existingViewer == viewer)
            {
                openedEditors.Remove(id);
            }
        }
    }

    public void FocusEditor(string id)
    {
        try
        {
            if (openedEditors.TryGetValue(id, out RequestViewer viewer))
            {
                viewer.AppWindow.Show(false);
                viewer.Activate();
            }
        }
        catch (Exception)
        {
            FileMgr.LogError("Window does not exist");
        }
    }
}
