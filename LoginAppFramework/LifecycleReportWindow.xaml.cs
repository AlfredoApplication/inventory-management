using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace LoginAppFramework
{
    public partial class LifecycleReportWindow : Window
    {
        private List<Asset> _allAssets;
        private bool isMenuOpen;
        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        public LifecycleReportWindow()
        {
            InitializeComponent();
            AssetReportListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeader_Click));
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(20);

            await Task.Run(() =>
            {
                _allAssets = AppData.GetAssets();
            });

            ApplyFilters();
            UpdateUserDisplay();
        }

        private void ApplyFilters()
        {
            if (_allAssets == null) return;

            IEnumerable<Asset> filteredAssets = _allAssets;

            // 1. Apply search text filter
            string searchText = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredAssets = filteredAssets.Where(a =>
                    (a.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.SerialNumber?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.AssignedUser?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }

            // 2. Apply the new RadioButton lifecycle filter
            if (ActiveAssetsFilterButton.IsChecked == true)
            {
                filteredAssets = filteredAssets.Where(a => !a.IsEndOfLife);
                TitleTextBlock.Text = "İstifadə Müddəti Davam Edən Vəsaitlər";
            }
            else if (ExpiredAssetsFilterButton.IsChecked == true)
            {
                filteredAssets = filteredAssets.Where(a => a.IsEndOfLife);
                TitleTextBlock.Text = "İstifadə Müddəti Bitmiş Vəsaitlər";
            }
            else // AllAssetsFilterButton is checked
            {
                TitleTextBlock.Text = "Bütün Vəsaitlər";
            }

            // 3. Apply the new default sorting logic
            var results = filteredAssets
                .OrderBy(a => a.IsEndOfLife)
                .ThenBy(a => a.EndOfLifeDate)
                .ToList();

            // 4. Update the UI
            AssetReportListView.ItemsSource = results;
            CountTextBlock.Text = $"{results.Count} element tapıldı";
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void AssetReportListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AssetReportListView.SelectedItem is Asset selectedAsset)
            {
                var assetWindow = new AssetWindow(selectedAsset.Id);
                assetWindow.Show();
                this.Close();
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader { Role: not GridViewColumnHeaderRole.Padding } headerClicked)
            {
                var direction = (headerClicked == _lastHeaderClicked && _lastDirection == ListSortDirection.Ascending) ?
                                               ListSortDirection.Descending :
                                               ListSortDirection.Ascending;

                if (headerClicked.Column.DisplayMemberBinding is Binding { Path.Path: var sortBy })
                {
                    Sort(sortBy, direction);
                }

                _lastHeaderClicked = headerClicked;
                _lastDirection = direction;
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(AssetReportListView.ItemsSource);
            if (dataView == null) return;

            // Clear existing sorts and apply the new one
            dataView.SortDescriptions.Clear();
            SortDescription sd = new(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private void CloseTheMenu() { isMenuOpen = false; MenuOverlay.Visibility = Visibility.Collapsed; (FindResource("CloseMenu") as Storyboard)?.Begin(); }
        private void MenuButton_Click(object sender, RoutedEventArgs e) { if (isMenuOpen) CloseTheMenu(); else { isMenuOpen = true; UserSwitchPopup.IsOpen = false; MenuOverlay.Visibility = Visibility.Visible; (FindResource("OpenMenu") as Storyboard)?.Begin(); } }
        private void CloseMenuButton_Click(object sender, RoutedEventArgs e) => CloseTheMenu();
        private void MenuOverlay_MouseDown(object sender, MouseButtonEventArgs e) => CloseTheMenu();
        private void DashboardButton_Click(object sender, RoutedEventArgs e) { new DashboardWindow().Show(); this.Close(); }
        private void UsersButton_Click(object sender, RoutedEventArgs e) { new WorkerListWindow().Show(); this.Close(); }
        private void AssetsButton_Click(object sender, RoutedEventArgs e) { new AssetWindow().Show(); this.Close(); }
        private void HistoryLogButton_Click(object sender, RoutedEventArgs e) { new HistoryLogWindow().Show(); this.Close(); }
        private void LifecycleReportButton_Click(object sender, RoutedEventArgs e) => CloseTheMenu();
        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationManager.GoToReportsWindow();
        }
        private void UpdateUserDisplay()
        {
            if (SessionManager.CurrentUser != null)
            {
                // This method no longer tries to change the icon, only the name.
                UserProfileName.Text = SessionManager.CurrentUser.FullName;
            }
        }
        private void UserProfileButton_Click(object sender, RoutedEventArgs e) => UserSwitchPopup.IsOpen = true;
        private void ReturnToLogin()
        {
            NavigationManager.RestartApplication();
        }
        private void SwitchUserButton_Click(object sender, RoutedEventArgs e) => ReturnToLogin();
        private void LogoutButton_Click(object sender, RoutedEventArgs e) => ReturnToLogin();
    }
}