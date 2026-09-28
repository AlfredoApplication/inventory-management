using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LiveCharts;
using LiveCharts.Wpf;

namespace LoginAppFramework
{
    public partial class DashboardWindow : Window
    {
        private List<Asset> _allAssets;
        private List<Worker> _allWorkers;
        private readonly DashboardViewModel _viewModel;
        private bool isMenuOpen = false;

        public DashboardWindow()
        {
            InitializeComponent();
            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;
        }

        private async void Window_Loaded(object _, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(20);
            await Task.Run(() =>
            {
                _allAssets = AppData.GetAssets();
                _allWorkers = AppData.GetWorkers();
            });

            _viewModel.LoadAllData(_allAssets, _allWorkers);

            UpdateUserDisplay();
            ApplyRoleBasedPermissions();
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void PieChart_DataClick(object sender, ChartPoint chartPoint)
        {
            if (chartPoint.SeriesView is PieSeries series)
            {
                ShowFilteredAssetsByParentCategory(series.Title);
            }
        }

        private void TopAssetsChart_DataClick(object sender, ChartPoint chartPoint)
        {
            int clickedIndex = (int)chartPoint.X;
            if (_viewModel.TopAssetsFullNames != null && clickedIndex < _viewModel.TopAssetsFullNames.Length)
            {
                string assetName = _viewModel.TopAssetsFullNames[clickedIndex];
                ShowFilteredAssetsByName(assetName);
            }
        }

        private void DepartmentsChart_DataClick(object sender, ChartPoint chartPoint)
        {
            int clickedIndex = (int)chartPoint.Y;
            if (_viewModel.DepartmentsLabels != null && clickedIndex < _viewModel.DepartmentsLabels.Length)
            {
                string departmentName = _viewModel.DepartmentsLabels[clickedIndex];
                ShowFilteredAssetsByDepartment(departmentName);
            }
        }

        private void LegendItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is LegendItemViewModel legendItem)
            {
                ShowFilteredAssetsByParentCategory(legendItem.CategoryName);
            }
        }

        private void ShowFilteredAssetsByParentCategory(string mainCategoryName)
        {
            if (string.IsNullOrEmpty(mainCategoryName) || _allAssets == null) return;

            var categoryTree = AppData.GetHierarchicalCategories();
            List<string> categoriesToFilter = new List<string>();
            var mainCategoryNode = categoryTree.FirstOrDefault(c => c.Name.Equals(mainCategoryName, StringComparison.OrdinalIgnoreCase));

            if (mainCategoryNode != null)
            {
                void GetAllSubCategoryNames(DeviceCategory category)
                {
                    categoriesToFilter.Add(category.Name);
                    foreach (var sub in category.Subcategories)
                    {
                        GetAllSubCategoryNames(sub);
                    }
                }
                GetAllSubCategoryNames(mainCategoryNode);
            }
            else
            {
                categoriesToFilter.Add(mainCategoryName);
            }

            var filteredAssets = _allAssets
                .Where(asset => categoriesToFilter.Contains(asset.Kateqoriya ?? "Naməlum"))
                .ToList();

            // --- THE FIX IS HERE ---
            string title = $"Kateqoriya Qrupu: {mainCategoryName}";
            var filteredWindow = new FilteredAssetsWindow(title, filteredAssets) { Owner = this };
            filteredWindow.ShowDialog();
        }

        private void ShowFilteredAssetsByName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName) || _allAssets == null) return;

            var filteredAssets = _allAssets
                .Where(asset => asset.VesaitinAdi.Equals(assetName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // --- THE FIX IS HERE ---
            string title = $"Vəsait Adı: {assetName}";
            var filteredWindow = new FilteredAssetsWindow(title, filteredAssets) { Owner = this };
            filteredWindow.ShowDialog();
        }

        private void ShowFilteredAssetsByDepartment(string departmentName)
        {
            if (string.IsNullOrEmpty(departmentName) || _allAssets == null) return;

            var filteredAssets = _allAssets
                .Where(asset => asset.BolmeShobeDepartment != null && asset.BolmeShobeDepartment.Equals(departmentName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // --- THE FIX IS HERE ---
            string title = $"Departament: {departmentName}";
            var filteredWindow = new FilteredAssetsWindow(title, filteredAssets) { Owner = this };
            filteredWindow.ShowDialog();
        }

        private async void AssetsButton_Click(object _, RoutedEventArgs e) => await NavigationManager.GoToAssetWindow();
        private async void UsersButton_Click(object _, RoutedEventArgs e) => await NavigationManager.GoToWorkerListWindow();
        private async void HistoryLogButton_Click(object _, RoutedEventArgs e) => await NavigationManager.GoToHistoryLogWindow();
        private async void LifecycleReportButton_Click(object _, RoutedEventArgs e) => await NavigationManager.GoToLifecycleReportWindow();
        private void CloseTheMenu() { isMenuOpen = false; MenuOverlay.Visibility = Visibility.Collapsed; (FindResource("CloseMenu") as Storyboard)?.Begin(); }
        private void MenuButton_Click(object _, RoutedEventArgs e) { if (isMenuOpen) CloseTheMenu(); else { isMenuOpen = true; UserSwitchPopup.IsOpen = false; MenuOverlay.Visibility = Visibility.Visible; (FindResource("OpenMenu") as Storyboard)?.Begin(); } }
        private void CloseMenuButton_Click(object _, RoutedEventArgs e) => CloseTheMenu();
        private void MenuOverlay_MouseDown(object _, MouseButtonEventArgs e) => CloseTheMenu();
        private void DashboardButton_Click(object _, RoutedEventArgs e) => CloseTheMenu();
        private void UpdateUserDisplay()
        {
            if (SessionManager.CurrentUser != null)
            {
                UserProfileName.Text = SessionManager.CurrentUser.FullName;
                // Display role instead of "Onlayn"
                UserRoleDisplay.Text = SessionManager.CurrentUser.Role == "Admin" ? "Admin" : "Yalnız Baxış";
            }
        }
        private void UserProfileButton_Click(object _, RoutedEventArgs e) => UserSwitchPopup.IsOpen = true;
        private void ReturnToLogin() { NavigationManager.RestartApplication(); }
        private void SwitchUserButton_Click(object _, RoutedEventArgs e) => ReturnToLogin();
        private void LogoutButton_Click(object _, RoutedEventArgs e) => ReturnToLogin();

        private void ApplyRoleBasedPermissions()
        {
            // Show User Management button only to Admins
            if (SessionManager.IsAdmin())
            {
                UserManagementButton.Visibility = Visibility.Visible;
            }
            else
            {
                UserManagementButton.Visibility = Visibility.Collapsed;
            }
        }

        private void UserManagementButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanManageUsers())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var userManagementWindow = new UserManagementWindow { Owner = this };
            userManagementWindow.ShowDialog();
            CloseTheMenu();
        }
    }
}