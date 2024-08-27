// MainWindow.xaml.cs
using Microsoft.UI.Xaml;
using System;
using System.Net;
using System.Net.Http;

namespace HourSync
{
    public sealed partial class MainWindow : Window
    {
        private RequestViewer _requestViewer;

        public MainWindow()
        {
            this.InitializeComponent();
            Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop();
            micaBackdrop.Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt;
            this.SystemBackdrop = micaBackdrop;
            ExtendsContentIntoTitleBar = true;
            this.Title = "HourSync";

            this.Closed += Closing;
        }

        private void Closing(object sender, WindowEventArgs args)
        {
            _requestViewer?.Close();
        }

        public void OpenRequestViewer(string idOfItem, string phpSessionId, string eventName, CookieContainer cookieContainer, HttpClientHandler handler, HttpClient client)
        {
            _requestViewer = new RequestViewer(idOfItem, phpSessionId, eventName, cookieContainer, handler, client);
            _requestViewer.Activate();
        }
    }
}