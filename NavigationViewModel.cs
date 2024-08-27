using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Controls;

namespace HourSync
{
    public class NavigationViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private ObservableCollection<NavigationViewItem> _menuItems;
        public ObservableCollection<NavigationViewItem> MenuItems
        {
            get => _menuItems;
            set
            {
                _menuItems = value;
                OnPropertyChanged();
            }
        }

        private NavigationViewItem _selectedItem;
        public NavigationViewItem SelectedItem
        {
            get => _selectedItem;
            set
            {
                _selectedItem = value;
                OnPropertyChanged();
            }
        }

        public NavigationViewModel()
        {
            MenuItems = new ObservableCollection<NavigationViewItem>
            {
                new(){ Content = "Login", Tag = "login", Icon = new SymbolIcon(Symbol.Contact) },
                new(){ Content = "Home", Tag = "home", Icon = new SymbolIcon(Symbol.Home) },
                new(){ Content = "Create Submission", Tag = "create", Icon = new SymbolIcon(Symbol.NewWindow) }
            };
            RefreshMenuItems(isLoggedIn: false);
        }

        public void RefreshMenuItems(bool isLoggedIn)
        {
            foreach (var item in MenuItems)
            {
                switch (item.Tag.ToString())
                {
                    case "login":
                        item.IsEnabled = !isLoggedIn;
                        break;
                    case "home":
                    case "create":
                        item.IsEnabled = isLoggedIn;
                        break;
                }
            }

            if (isLoggedIn)
            {
                SelectedItem = MenuItems.FirstOrDefault(item => item.Tag.ToString() == "home");
            }
            else
            {
                SelectedItem = MenuItems.FirstOrDefault(item => item.Tag.ToString() == "login");
            }

            // Force a refresh of the MenuItems collection
            OnPropertyChanged(nameof(MenuItems));
        }
    }
}