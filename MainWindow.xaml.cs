#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
#pragma warning disable CA1861 // Avoid constant arrays as arguments
#pragma warning disable CsWinRT1029 // Class not trimming / AOT compatible
// MainWindow.xaml.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using HourSyncCoreLib;
using Microsoft.UI.Xaml;

namespace HourSync;

public sealed partial class MainWindow : Window
{
    private const int WM_GETMINMAXINFO = 0x0024;
    private const int GWL_WNDPROC = -4;

    private IntPtr _hwnd;
    private WNDPROC _newWndProc;
    private IntPtr _oldWndProc;

    // Minimum window size in pixels
    private int MIN_WIDTH = 670;
    private int MIN_HEIGHT = 500;

    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr WNDPROC(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WNDPROC dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    private RequestViewer _requestViewer;

    public MainWindow()
    {
        InitializeComponent();

        // Your existing initialization code
        Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt,
        };
        SystemBackdrop = micaBackdrop;
        ExtendsContentIntoTitleBar = true;
        Title = "HourSync";

        // Set up window subclassing for minimum size
        SetupMinimumWindowSize();

        Closed += Closing;
        SizeChanged += MainWindow_SizeChanged;
    }
    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs e)
    {
        if (((App)Application.Current).isOnHome && ((App)Application.Current).homePage != null)
        {
            ((App)Application.Current).homePage.OnWindowSizeChanged(e.Size.Width);
        }
    }

    private void SetupMinimumWindowSize()
    {
        // Get the window handle
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Create our window procedure delegate
        _newWndProc = new WNDPROC(WindowProc);

        // Subclass the window
        _oldWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, _newWndProc);
    }

    private IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_GETMINMAXINFO:
                // Handle the minimum window size
                var minMaxInfo = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                minMaxInfo.ptMinTrackSize.x = MIN_WIDTH;
                minMaxInfo.ptMinTrackSize.y = MIN_HEIGHT;
                Marshal.StructureToPtr(minMaxInfo, lParam, true);
                return IntPtr.Zero;
        }

        // Call the original window procedure for all other messages
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    private void Closing(object sender, WindowEventArgs args)
    {
        // Restore original window procedure before closing
        if (_hwnd != IntPtr.Zero && _oldWndProc != IntPtr.Zero)
        {
            SetWindowLongPtr(_hwnd, GWL_WNDPROC, new WNDPROC((h, m, w, l) => CallWindowProc(_oldWndProc, h, m, w, l)));
        }

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
