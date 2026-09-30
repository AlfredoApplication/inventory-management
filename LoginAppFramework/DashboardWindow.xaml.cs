using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LiveCharts;
using LiveCharts.Wpf;

namespace LoginAppFramework
{
    public partial class DashboardWindow : Window, INavigationRefreshable
    {
        private List<Asset> _allAssets;
        private List<Asset> _dashboardAssets = new();
        private List<Worker> _allWorkers;
        private readonly DashboardViewModel _viewModel;

        private enum DashboardPeriod
        {
            All,
            Year,
            Month
        }

        private DashboardPeriod _selectedPeriod =
            DashboardPeriod.All;

        public DashboardWindow()
        {
            InitializeComponent();
            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;

            NotificationCenterService.Changed +=
                NotificationCenterService_Changed;

            Closed += (_, _) =>
            {
                NotificationCenterService.Changed -=
                    NotificationCenterService_Changed;
            };
        }

        private async void Window_Loaded(object _, RoutedEventArgs e)
            => await LoadDashboardDataAsync(showLoading: true);

        public Task RefreshForNavigationAsync()
            => LoadDashboardDataAsync(showLoading: false);

        private async Task LoadDashboardDataAsync(bool showLoading)
        {
            if (showLoading)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                await Task.Delay(20);
            }

            try
            {
                await Task.Run(() =>
                {
                    _allAssets = AppData.GetAssets();
                    _allWorkers = AppData.GetWorkers();
                });

                AlertService.Refresh(_allAssets);
                ApplyDashboardPeriod();
                UpdateAlertSummary();
                ApplyResponsiveDashboardLayout();
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Yükləmə Xətası",
                    $"İdarə paneli yüklənərkən xəta baş verdi:\n\n{ex.Message}");
            }
            finally
            {
                if (showLoading)
                    LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void DashboardPeriodComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (DashboardPeriodComboBox?.SelectedItem is not ComboBoxItem item ||
                item.Tag is not string tag)
            {
                return;
            }

            _selectedPeriod = tag switch
            {
                "Year" => DashboardPeriod.Year,
                "Month" => DashboardPeriod.Month,
                _ => DashboardPeriod.All
            };

            ApplyDashboardPeriod();
        }

        private void ApplyDashboardPeriod()
        {
            if (_allAssets == null ||
                _allWorkers == null ||
                _viewModel == null)
            {
                return;
            }

            DateTime today = DateTime.Today;

            var filtered = _selectedPeriod switch
            {
                DashboardPeriod.Year => _allAssets
                    .Where(asset =>
                        asset.PurchaseDate > DateTime.MinValue &&
                        asset.PurchaseDate.Year == today.Year)
                    .ToList(),

                DashboardPeriod.Month => _allAssets
                    .Where(asset =>
                        asset.PurchaseDate > DateTime.MinValue &&
                        asset.PurchaseDate.Year == today.Year &&
                        asset.PurchaseDate.Month == today.Month)
                    .ToList(),

                _ => _allAssets.ToList()
            };

            _dashboardAssets = filtered;

            _viewModel.LoadAllData(
                _dashboardAssets,
                _allWorkers);

            if (DashboardPeriodSummaryText != null)
            {
                string periodText = _selectedPeriod switch
                {
                    DashboardPeriod.Year => $"{today.Year}",
                    DashboardPeriod.Month => today.ToString("MMMM yyyy"),
                    _ => "Bütün dövr"
                };

                DashboardPeriodSummaryText.Text =
                    $"{periodText} • {filtered.Count} vəsait";
            }
        }

        private void UpdateAlertSummary()
        {
            if (WarrantyAlertCountText == null ||
                MaintenanceAlertCountText == null ||
                NotificationUnreadCountText == null)
            {
                return;
            }

            int warranty = AlertService.WarrantyAlertCount;
            int maintenance = AlertService.MaintenanceAlertCount;
            int unread = NotificationCenterService.GetUnreadCount();

            WarrantyAlertCountText.Text =
                $"Zəmanət: {warranty}";

            MaintenanceAlertCountText.Text =
                $"Texniki xidmət: {maintenance}";

            NotificationUnreadCountText.Text =
                unread == 0
                    ? "Bildiriş yoxdur"
                    : $"Bildirişlər: {unread}";

            WarrantyAlertButton.IsEnabled = warranty > 0;
            MaintenanceAlertButton.IsEnabled = maintenance > 0;
        }

        private void NotificationCenterService_Changed(
            object sender,
            EventArgs e)
        {
            if (Dispatcher.CheckAccess())
                UpdateAlertSummary();
            else
                Dispatcher.BeginInvoke(
                    new Action(UpdateAlertSummary));
        }

        private void WarrantyAlertButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var assets = AlertService
                .GetWarrantyAssets(_allAssets)
                .ToList();

            if (assets.Count == 0)
                return;

            var window = new FilteredAssetsWindow(
                "Zəmanət xəbərdarlıqları",
                assets)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void MaintenanceAlertButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var assets = AlertService
                .GetMaintenanceAssets(_allAssets)
                .ToList();

            if (assets.Count == 0)
                return;

            var window = new FilteredAssetsWindow(
                "Texniki xidmət vaxtı çatan vəsaitlər",
                assets)
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void NotificationCenterButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            NavigationManager.GoToNotificationCenter(this);
            UpdateAlertSummary();
        }

        private void Window_SizeChanged(
            object sender,
            SizeChangedEventArgs e)
            => ApplyResponsiveDashboardLayout();

        private void ApplyResponsiveDashboardLayout()
        {
            if (DashboardLayoutGrid == null ||
                KpiGrid == null ||
                ChartsGrid == null)
            {
                return;
            }

            bool compact = ActualWidth < 1180;

            if (!compact)
            {
                DashboardChartsColumn.Width =
                    new GridLength(3.5, GridUnitType.Star);

                DashboardKpiColumn.Width =
                    new GridLength(1, GridUnitType.Star);

                DashboardTopRow.Height =
                    new GridLength(1, GridUnitType.Star);

                DashboardBottomRow.Height =
                    new GridLength(1, GridUnitType.Star);

                Grid.SetRow(ChartsGrid, 0);
                Grid.SetColumn(ChartsGrid, 0);
                Grid.SetRowSpan(ChartsGrid, 2);
                Grid.SetColumnSpan(ChartsGrid, 1);
                ChartsGrid.Margin =
                    new Thickness(0, 0, 15, 0);

                Grid.SetRow(KpiGrid, 0);
                Grid.SetColumn(KpiGrid, 1);
                Grid.SetRowSpan(KpiGrid, 2);
                Grid.SetColumnSpan(KpiGrid, 1);
                KpiGrid.Margin = new Thickness(0);

                KpiGrid.ColumnDefinitions[0].Width =
                    new GridLength(1, GridUnitType.Star);

                KpiGrid.ColumnDefinitions[1].Width =
                    new GridLength(0);

                KpiGrid.RowDefinitions[0].Height =
                    new GridLength(1, GridUnitType.Star);
                KpiGrid.RowDefinitions[1].Height =
                    new GridLength(1.5, GridUnitType.Star);
                KpiGrid.RowDefinitions[2].Height =
                    new GridLength(1, GridUnitType.Star);
                KpiGrid.RowDefinitions[3].Height =
                    new GridLength(1, GridUnitType.Star);

                SetKpiCard(
                    TotalAssetsCard,
                    0,
                    0,
                    new Thickness(0, 0, 0, 7.5));

                SetKpiCard(
                    TotalPurchaseCard,
                    1,
                    0,
                    new Thickness(0, 7.5, 0, 7.5));

                SetKpiCard(
                    CurrentValueCard,
                    2,
                    0,
                    new Thickness(0, 7.5, 0, 7.5));

                SetKpiCard(
                    DepreciationCard,
                    3,
                    0,
                    new Thickness(0, 7.5, 0, 0));

                return;
            }

            DashboardChartsColumn.Width =
                new GridLength(1, GridUnitType.Star);

            DashboardKpiColumn.Width =
                new GridLength(0);

            DashboardTopRow.Height =
                new GridLength(
                    ActualWidth < 900 ? 250 : 210);

            DashboardBottomRow.Height =
                new GridLength(1, GridUnitType.Star);

            Grid.SetRow(KpiGrid, 0);
            Grid.SetColumn(KpiGrid, 0);
            Grid.SetRowSpan(KpiGrid, 1);
            Grid.SetColumnSpan(KpiGrid, 2);
            KpiGrid.Margin =
                new Thickness(0, 0, 0, 12);

            Grid.SetRow(ChartsGrid, 1);
            Grid.SetColumn(ChartsGrid, 0);
            Grid.SetRowSpan(ChartsGrid, 1);
            Grid.SetColumnSpan(ChartsGrid, 2);
            ChartsGrid.Margin = new Thickness(0);

            KpiGrid.ColumnDefinitions[0].Width =
                new GridLength(1, GridUnitType.Star);

            KpiGrid.ColumnDefinitions[1].Width =
                new GridLength(1, GridUnitType.Star);

            KpiGrid.RowDefinitions[0].Height =
                new GridLength(1, GridUnitType.Star);
            KpiGrid.RowDefinitions[1].Height =
                new GridLength(1, GridUnitType.Star);
            KpiGrid.RowDefinitions[2].Height =
                new GridLength(0);
            KpiGrid.RowDefinitions[3].Height =
                new GridLength(0);

            SetKpiCard(
                TotalAssetsCard,
                0,
                0,
                new Thickness(0, 0, 6, 6));

            SetKpiCard(
                TotalPurchaseCard,
                0,
                1,
                new Thickness(6, 0, 0, 6));

            SetKpiCard(
                CurrentValueCard,
                1,
                0,
                new Thickness(0, 6, 6, 0));

            SetKpiCard(
                DepreciationCard,
                1,
                1,
                new Thickness(6, 6, 0, 0));
        }

        private static void SetKpiCard(
            FrameworkElement card,
            int row,
            int column,
            Thickness margin)
        {
            Grid.SetRow(card, row);
            Grid.SetColumn(card, column);
            card.Margin = margin;
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
            if (string.IsNullOrEmpty(mainCategoryName) || _dashboardAssets == null) return;

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
            if (string.IsNullOrEmpty(departmentName) || _dashboardAssets == null) return;

            var filteredAssets = _dashboardAssets
                .Where(asset => asset.Department != null && asset.Department.Equals(departmentName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // --- THE FIX IS HERE ---
            string title = $"Departament: {departmentName}";
            var filteredWindow = new FilteredAssetsWindow(title, filteredAssets) { Owner = this };
            filteredWindow.ShowDialog();
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
                e.Key == Key.K)
            {
                NavigationManager.GoToGlobalSearch(this);
                e.Handled = true;
            }
        }

        private void MenuButton_Click(object _, RoutedEventArgs e)
            => SharedNavigationMenu.Open();
    }
}