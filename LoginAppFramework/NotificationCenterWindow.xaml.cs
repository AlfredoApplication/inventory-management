using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class NotificationCenterWindow : Window
    {
        public NotificationCenterWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            NotificationCenterService.Changed += NotificationCenterService_Changed;
            ReloadItems();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            NotificationCenterService.Changed -= NotificationCenterService_Changed;
        }

        private void NotificationCenterService_Changed(
            object sender,
            EventArgs e)
        {
            if (Dispatcher.CheckAccess())
                ReloadItems();
            else
                Dispatcher.BeginInvoke(new Action(ReloadItems));
        }

        private void ReloadItems()
        {
            var items = NotificationCenterService
                .GetItems()
                .Where(item =>
                    UnreadOnlyCheckBox.IsChecked != true ||
                    (!item.IsRead && !item.IsResolved))
                .ToList();

            NotificationsListView.ItemsSource = items;

            int unread = NotificationCenterService.GetUnreadCount();
            UnreadCountTextBlock.Text =
                unread == 0
                    ? "Yeni bildiriş yoxdur"
                    : $"{unread} yeni";

            CountTextBlock.Text = $"{items.Count} bildiriş";

            NotificationsListView.Visibility =
                items.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            EmptyState.Visibility =
                items.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void FilterChanged(
            object sender,
            RoutedEventArgs e)
            => ReloadItems();

        private void NotificationsListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (NotificationsListView.SelectedItem is not NotificationCenterItem item)
                return;

            if (!item.IsRead)
                NotificationCenterService.MarkRead(item.Id);
        }

        private async void NotificationsListView_MouseDoubleClick(
            object sender,
            MouseButtonEventArgs e)
        {
            if (NotificationsListView.SelectedItem is NotificationCenterItem item)
                await OpenItemAsync(item);
        }

        private async void OpenNotificationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag is NotificationCenterItem item)
                await OpenItemAsync(item);
        }

        private async Task OpenItemAsync(NotificationCenterItem item)
        {
            if (item == null)
                return;

            NotificationCenterService.MarkRead(item.Id);

            if (item.TargetType != NavigationTargetType.Asset ||
                item.TargetId <= 0)
            {
                return;
            }

            Window owner = Owner;
            Close();

            await NavigationManager.GoToAssetWindow(
                owner,
                item.TargetId);
        }

        private void MarkAllReadButton_Click(
            object sender,
            RoutedEventArgs e)
            => NotificationCenterService.MarkAllRead();

        private void ClearReadButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!DialogService.Confirm(
                    this,
                    "Oxunmuş Bildirişləri Təmizlə",
                    "Oxunmuş bildirişləri tarixçədən silmək istəyirsiniz?",
                    "Təmizlə",
                    "Ləğv et"))
            {
                return;
            }

            NotificationCenterService.ClearRead();
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            Close();
            e.Handled = true;
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
            => Close();
    }
}
