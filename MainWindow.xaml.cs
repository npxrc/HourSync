// MainWindow.xaml.cs
using System.Net;
using System.Net.Http;
using Microsoft.UI.Xaml;

namespace HourSync
{
    public sealed partial class MainWindow : Window
    {
        private RequestViewer _requestViewer;
        public NetworkMonitor _networkMonitor;
        public MainWindow()
        {
            InitializeComponent();
            Microsoft.UI.Xaml.Media.MicaBackdrop micaBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
            {
                Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.BaseAlt
            };
            SystemBackdrop = micaBackdrop;
            ExtendsContentIntoTitleBar = true;
            Title = "HourSync";

            Closed += Closing;
            Activated += WindowActivated;
        }

        private void WindowActivated(object sender, WindowActivatedEventArgs args)
        {
            _networkMonitor = new NetworkMonitor(this);
        }

        private void Closing(object sender, WindowEventArgs args)
        {
            _requestViewer?.Close();
        }

        public void OpenRequestViewer(string idOfItem, string phpSessionId, string eventName, CookieContainer cookieContainer, HttpClientHandler handler, HttpClient client, string nameOfAcademy)
        {
            _requestViewer = new RequestViewer(idOfItem, phpSessionId, eventName, cookieContainer, handler, client, nameOfAcademy);
            _requestViewer.Activate();
        }
    }
}