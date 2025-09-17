#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable IDE0007 // Use implicit type
#pragma warning disable IDE0044 // Add readonly modifier
#pragma warning disable IDE0052 // Remove unread private members
using System;
using System.Runtime.InteropServices;
using HourSyncCoreLib;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Animation;

namespace HourSync;

public sealed partial class RequestManager : Window
{
    /*
    TODO: Combine ImageViewer, BrowserView, and this page into one file

    Baby steps to achieve:
    - Set up a frame
    - Move this code into a page
    - Navigate to the main Request itself when the window is activated
    - Allow user to click image viewer or browser view
    - when clicked, navigate to the viewers and put a back button which navigates frame backwards so state is saved
     */

    private RequestPage editingPage; // used to tell mainwindow that editing has been cancelled if the user closes this window
    private bool isEditing = false;

    public RequestManager(
        string id,
        string phpSessionId,
        string nameOfAcademy,
        string nameOfEvent,
        Status status,
        string username,
        string password
    )
    {
        InitializeComponent();
        Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
        {
            Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt,
        };
        SystemBackdrop = micaBackdrop;
        ExtendsContentIntoTitleBar = true;
        Title = "Request Viewer";

        string eventName = nameOfEvent.Split('\n')[0];

        Closed += (_, __) =>
        {
            if (isEditing && editingPage != null)
            {
                MainWindow _mainWindow = (MainWindow)((App)Application.Current).m_window;
                _mainWindow.ClosedEditor(id, editingPage);
            }
        };
        SetupMinimumWindowSize();

        var context = new RequestContext
        {
            Id = id,
            PhpSessionId = phpSessionId,
            AcademyName = nameOfAcademy,
            EventName = eventName,
            Status = status,
            Username = username,
            Password = password
        };

        FileMgr.Log("Navigating to requestpage with context");
        mainFrame.Navigate(typeof(RequestPage), context);

        var page = (RequestPage)mainFrame.Content;

        page.RequestClosed += (_, __) => Close();
        page.EditStarted += (s, e) => HandleStartEdit((RequestPage)s);
        page.EditCancelled += (_, __) => HandleStopEdit();
        page.BrowserOpenEvent += (_, e) => OpenBrowser(e.PhpSessionId);
    }

    private void HandleStartEdit(RequestPage sender)
    {
        editingPage = sender;
        isEditing = true;
    }

    private void HandleStopEdit()
    {
        editingPage = null;
        isEditing = false;
    }

    public void OpenBrowser(string phpSessionId)
    {
        var page = (RequestPage)mainFrame.Content;
        page.BrowserOpenEvent -= (_, e) => OpenBrowser(e.PhpSessionId);

        mainFrame.Navigate(typeof(BrowserView), phpSessionId, new SlideNavigationTransitionInfo() { Effect = SlideNavigationTransitionEffect.FromRight });

        var newPage = (BrowserView)mainFrame.Content;
        newPage.GoBack += (_, __) => HandleGoBack();
    }

    private void HandleGoBack()
    {
        mainFrame.GoBack();
        if (mainFrame.Content.GetType() == typeof(RequestPage))
        {
            var page = (RequestPage)mainFrame.Content;
            page.BrowserOpenEvent += (_, e) => OpenBrowser(e.PhpSessionId);
        }
        else
        {
            //it SHOULD always be a requestpage, but i did BS this together since i thought that rewriting the whole requestviewer structure would be a smart idea...
        }
    }

    // Set min size

    private const int WM_GETMINMAXINFO = 0x0024;
    private const int GWL_WNDPROC = -4;

    private IntPtr _hwnd;
    private WNDPROC _newWndProc;
    private IntPtr _oldWndProc;

    // Minimum window size in pixels
    private int MIN_WIDTH = 410;
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

    public void SetupMinimumWindowSize()
    {
        // Get the window handle
        _hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        // Create our window procedure delegate
        _newWndProc = new WNDPROC(WindowProc);

        // Subclass the window
        _oldWndProc = SetWindowLongPtr(_hwnd, GWL_WNDPROC, _newWndProc);
    }

    public IntPtr WindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
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
}
public class RequestContext
{
    public string Id
    {
        get; set;
    }
    public string PhpSessionId
    {
        get; set;
    }
    public string AcademyName
    {
        get; set;
    }
    public string EventName
    {
        get; set;
    }
    public Status Status
    {
        get; set;
    }
    public string Username
    {
        get; set;
    }
    public string Password
    {
        get; set;
    }
}
