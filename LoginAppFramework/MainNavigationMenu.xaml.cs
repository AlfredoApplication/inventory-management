using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class MainNavigationMenu : UserControl
    {
        private IInputElement _returnFocus;
        public static readonly DependencyProperty CurrentPageProperty =
            DependencyProperty.Register(
                nameof(CurrentPage),
                typeof(string),
                typeof(MainNavigationMenu),
                new PropertyMetadata(string.Empty, OnCurrentPageChanged));

        public string CurrentPage
        {
            get => (string)GetValue(CurrentPageProperty);
            set => SetValue(CurrentPageProperty, value);
        }

        public MainNavigationMenu()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                UpdateUserDisplay();
                UpdateActiveButton();
                UpdateNotificationBadge();
                NotificationCenterService.Changed +=
                    NotificationCenterService_Changed;
            };

            Unloaded += (_, _) =>
            {
                NotificationCenterService.Changed -=
                    NotificationCenterService_Changed;
            };
        }

        public void Open()
        {
            _returnFocus = Keyboard.FocusedElement;

            UpdateUserDisplay();
            UpdateActiveButton();
            UpdateNotificationBadge();
            UserPopup.IsOpen = false;
            Visibility = Visibility.Visible;

            Dispatcher.BeginInvoke(
                new Action(() => GetActiveButton().Focus()));
        }

        public void Close()
        {
            UserPopup.IsOpen = false;
            Visibility = Visibility.Collapsed;

            if (_returnFocus is UIElement element)
                element.Focus();

            _returnFocus = null;
        }

        private Button GetActiveButton()
        {
            return (CurrentPage ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "workers" => WorkersButton,
                "assets" => AssetsButton,
                "history" => HistoryButton,
                "lifecycle" => LifecycleButton,
                "reports" => ReportsButton,
                _ => DashboardButton
            };
        }

        private Window OwnerWindow => Window.GetWindow(this);

        private static void OnCurrentPageChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is MainNavigationMenu menu && menu.IsLoaded)
                menu.UpdateActiveButton();
        }

        private void NotificationCenterService_Changed(
            object sender,
            EventArgs e)
        {
            if (Dispatcher.CheckAccess())
                UpdateNotificationBadge();
            else
                Dispatcher.BeginInvoke(
                    new Action(UpdateNotificationBadge));
        }

        private void UpdateNotificationBadge()
        {
            if (NotificationBadge == null ||
                NotificationCountText == null)
            {
                return;
            }

            int unread =
                NotificationCenterService.GetUnreadCount();

            NotificationCountText.Text =
                unread > 99
                    ? "99+"
                    : unread.ToString();

            NotificationBadge.Visibility =
                unread > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void UpdateUserDisplay()
        {
            var user = SessionManager.CurrentUser;
            UserProfileName.Text =
                string.IsNullOrWhiteSpace(user?.FullName)
                    ? user?.Username ?? "İstifadəçi"
                    : user.FullName;

            UserRoleText.Text =
                user?.Role == "Admin"
                    ? "Admin"
                    : "Yalnız Baxış";

            UserManagementButton.Visibility =
                SessionManager.CanManageUsers()
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            RecycleBinButton.Visibility =
                SessionManager.CanDelete()
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void UpdateActiveButton()
        {
            var normal = Brushes.Transparent;
            var active = (Brush)FindResource("PrimaryBrushHover");

            DashboardButton.Background = normal;
            WorkersButton.Background = normal;
            AssetsButton.Background = normal;
            HistoryButton.Background = normal;
            LifecycleButton.Background = normal;
            ReportsButton.Background = normal;

            switch ((CurrentPage ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "dashboard":
                    DashboardButton.Background = active;
                    break;
                case "workers":
                    WorkersButton.Background = active;
                    break;
                case "assets":
                    AssetsButton.Background = active;
                    break;
                case "history":
                    HistoryButton.Background = active;
                    break;
                case "lifecycle":
                    LifecycleButton.Background = active;
                    break;
                case "reports":
                    ReportsButton.Background = active;
                    break;
            }
        }

        private async Task NavigateAsync(
            string targetPage,
            Func<Window, Task> navigate)
        {
            var owner = OwnerWindow;

            if (string.Equals(
                    CurrentPage,
                    targetPage,
                    StringComparison.OrdinalIgnoreCase))
            {
                Close();
                return;
            }

            Close();
            await navigate(owner);
        }

        private async void DashboardButton_Click(object sender, RoutedEventArgs e)
            => await NavigateAsync(
                "Dashboard",
                window => NavigationManager.GoToDashboard(window));

        private void NotificationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var owner = OwnerWindow;
            Close();
            NavigationManager.GoToNotificationCenter(owner);
        }

        private void GlobalSearchButton_Click(object sender, RoutedEventArgs e)
        {
            var owner = OwnerWindow;
            Close();
            NavigationManager.GoToGlobalSearch(owner);
        }

        private async void WorkersButton_Click(object sender, RoutedEventArgs e)
            => await NavigateAsync(
                "Workers",
                window => NavigationManager.GoToWorkerListWindow(window));

        private async void AssetsButton_Click(object sender, RoutedEventArgs e)
            => await NavigateAsync(
                "Assets",
                window => NavigationManager.GoToAssetWindow(window));

        private async void RecycleBinButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var owner = OwnerWindow;
            Close();
            await NavigationManager.GoToRecycleBin(owner);
        }

        private async void HistoryButton_Click(object sender, RoutedEventArgs e)
            => await NavigateAsync(
                "History",
                window => NavigationManager.GoToHistoryLogWindow(window));

        private async void LifecycleButton_Click(object sender, RoutedEventArgs e)
            => await NavigateAsync(
                "Lifecycle",
                window => NavigationManager.GoToLifecycleReportWindow(window));

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            var owner = OwnerWindow;
            Close();
            NavigationManager.GoToReportsWindow(owner);
        }

        private void UserManagementButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanManageUsers())
            {
                DialogService.Warning(
                    OwnerWindow,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            var owner = OwnerWindow;
            Close();

            var window = new UserManagementWindow
            {
                Owner = owner
            };
            window.ShowDialog();
        }

        private void UserProfileButton_Click(object sender, RoutedEventArgs e)
            => UserPopup.IsOpen = !UserPopup.IsOpen;

        private void SwitchUserButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
            NavigationManager.SwitchUser();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            bool confirm = DialogService.Confirm(
                OwnerWindow,
                "Çıxış",
                "Proqramdan çıxmaq istəyirsiniz?",
                "Çıxış et",
                "Ləğv et");

            if (confirm)
                NavigationManager.ExitApplication();
        }

        private void MainNavigationMenu_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            if (UserPopup.IsOpen)
            {
                UserPopup.IsOpen = false;
            }
            else
            {
                Close();
            }

            e.Handled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();

        private void Overlay_MouseDown(object sender, MouseButtonEventArgs e)
            => Close();
    }
}
