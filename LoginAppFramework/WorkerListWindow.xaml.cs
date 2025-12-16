using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LoginAppFramework
{
    public partial class WorkerListWindow : Window
    {
        private List<WorkerViewModel> _allWorkerViewModels;
        private WorkerViewModel _selectedWorker;
        private bool isMenuOpen = false;
        private WorkerFilterViewModel _filterViewModel;
        private readonly DispatcherTimer _selectionTimer;
        private bool isDetailPanelOpen = false;

        public WorkerListWindow()
        {
            InitializeComponent();
            _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _selectionTimer.Tick += SelectionTimer_Tick;
        }

        private async void Window_Loaded(object _, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(20);
            if (string.IsNullOrEmpty(SessionManager.CurrentUserConnectionString))
            {
                MessageBox.Show("FATAL ERROR: No user session found. Returning to login.", "Session Error", MessageBoxButton.OK, MessageBoxImage.Error);
                NavigationManager.RestartApplication();
                return;
            }
            await Task.Run(() => AppData.LoadAllData());
            LoadAllData();
            _filterViewModel = new WorkerFilterViewModel(AppData.GetWorkers());
            _filterViewModel.FilterChanged += ApplyFilters;
            FilterPanel.DataContext = _filterViewModel;
            SummaryPanel.DataContext = _filterViewModel;
            ApplyFilters();
            UpdateUserDisplay();
            WorkerAssetManager.OnDetailPanelClosed += DetailControl_PanelClosed;
            WorkerAssetManager.OnAssetDoubleClicked += Manager_AssetDoubleClicked;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }

        private void Manager_AssetDoubleClicked(object sender, Asset e)
        {
            if (e != null)
            {
                var assetWindow = new AssetWindow(e.Id);
                assetWindow.Show();
                this.Close();
            }
        }

        private void DetailControl_PanelClosed(object sender, EventArgs e)
        {
            WorkersDataGrid.SelectedItem = null;
        }

        private void OpenDetailPanel()
        {
            var animation = new DoubleAnimation(0, 650, TimeSpan.FromMilliseconds(250)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            DetailPanelContainer.BeginAnimation(WidthProperty, animation);
            isDetailPanelOpen = true;
        }

        private void CloseDetailPanel()
        {
            var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            DetailPanelContainer.BeginAnimation(WidthProperty, animation);
            isDetailPanelOpen = false;
        }

        private void LoadAllData()
        {
            var allWorkers = AppData.GetWorkers();
            var allAssets = AppData.GetAssets();
            if (allWorkers == null || allAssets == null) { _allWorkerViewModels = new List<WorkerViewModel>(); return; }
            var assetsByUser = allAssets.Where(a => a.WorkerId.HasValue).ToLookup(a => a.WorkerId.Value);
            _allWorkerViewModels = allWorkers.Select(w => new WorkerViewModel(w, assetsByUser[w.Id].ToList())).ToList();
        }

        private void ApplyFilters()
        {
            if (_filterViewModel == null || _allWorkerViewModels == null) return;
            IEnumerable<WorkerViewModel> filtered = _allWorkerViewModels;
            if (ActiveFilterButton.IsChecked == true) { filtered = filtered.Where(w => w.IsActive); }
            else if (InactiveFilterButton.IsChecked == true) { filtered = filtered.Where(w => !w.IsActive); }
            string searchText = _filterViewModel.SearchText;
            var culture = CultureInfo.CurrentCulture;
            var compareOptions = CompareOptions.IgnoreCase;
            if (!string.IsNullOrWhiteSpace(searchText)) { filtered = filtered.Where(w => (w.Name != null && culture.CompareInfo.IndexOf(w.Name, searchText, compareOptions) >= 0)); }
            var selectedDepts = _filterViewModel.DepartmentOptions.Where(o => o.IsChecked).Select(o => o.Value).ToList();
            if (selectedDepts.Any()) { filtered = filtered.Where(w => w.Department != null && selectedDepts.Contains(w.Department)); }

            var results = filtered.ToList();
            WorkersDataGrid.ItemsSource = results;

            _filterViewModel.UpdateFinancialSummary(results);

            NoResultsTextBlock.Visibility = results.Any() ? Visibility.Collapsed : Visibility.Visible;
            UpdateDetailView();
            UpdateActiveFilterTags();
        }

        private async void SyncWorkersButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show("This will synchronize with the remote HR database.\n\nContinue?", "Confirm Sync", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmResult != MessageBoxResult.Yes) return;
            SyncWorkersButton.IsEnabled = false;
            LoadingMessage.Text = "Synchronizing...";
            LoadingOverlay.Visibility = Visibility.Visible;
            try
            {
                int affectedRows = await DataAccess.SynchronizeWorkersFromRemoteAsync();
                RefreshAllDataAndFilters();
                MessageBox.Show($"Sync complete. {affectedRows} records affected.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Sync Failed:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                SyncWorkersButton.IsEnabled = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshAllDataAndFilters()
        {
            var selectedItems = WorkersDataGrid.SelectedItems.Cast<WorkerViewModel>().ToList();
            AppData.LoadAllData();
            LoadAllData();
            _filterViewModel = new WorkerFilterViewModel(AppData.GetWorkers());
            _filterViewModel.FilterChanged += ApplyFilters;
            FilterPanel.DataContext = _filterViewModel;
            ApplyFilters();
            if (selectedItems.Any())
            {
                var currentItems = WorkersDataGrid.ItemsSource as List<WorkerViewModel>;
                if (currentItems != null)
                {
                    WorkersDataGrid.SelectedItems.Clear();
                    foreach (var itemToReselect in selectedItems)
                    {
                        var foundItem = currentItems.FirstOrDefault(i => i.GetModel().Id == itemToReselect.GetModel().Id);
                        if (foundItem != null) { WorkersDataGrid.SelectedItems.Add(foundItem); }
                    }
                }
            }
            else { UpdateDetailView(); }
        }

        private void WorkersDataGrid_SelectionChanged(object _, SelectionChangedEventArgs e)
        {
            _selectionTimer.Stop();
            _selectionTimer.Start();
        }

        private void SelectionTimer_Tick(object sender, EventArgs e)
        {
            _selectionTimer.Stop();
            int selectedCount = WorkersDataGrid.SelectedItems.Count;
            if (selectedCount > 1)
            {
                if (isDetailPanelOpen) CloseDetailPanel();
                BulkActionPanel.Visibility = Visibility.Visible;
                SelectionCountText.Text = $"{selectedCount} items selected";
                _selectedWorker = null;
            }
            else if (selectedCount == 1)
            {
                BulkActionPanel.Visibility = Visibility.Collapsed;
                var newlySelectedWorker = WorkersDataGrid.SelectedItem as WorkerViewModel;
                if (_selectedWorker != newlySelectedWorker)
                {
                    _selectedWorker = newlySelectedWorker;
                    UpdateDetailView();
                    if (!isDetailPanelOpen) OpenDetailPanel();
                }
            }
            else
            {
                if (isDetailPanelOpen) CloseDetailPanel();
                BulkActionPanel.Visibility = Visibility.Collapsed;
                _selectedWorker = null;
            }
        }

        private void BulkDeactivateButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedVMs = WorkersDataGrid.SelectedItems.Cast<WorkerViewModel>().ToList();
            if (!selectedVMs.Any()) return;
            var result = MessageBox.Show($"Are you sure you want to DEACTIVATE {selectedVMs.Count} workers?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                int count = 0;
                foreach (var vm in selectedVMs) { var worker = vm.GetModel(); if (worker.IsActive) { worker.IsActive = false; DataAccess.SaveWorker(worker); count++; } }
                RefreshAllDataAndFilters();
                MessageBox.Show($"{count} workers deactivated.", "Complete");
            }
        }

        private void BulkActivateButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedVMs = WorkersDataGrid.SelectedItems.Cast<WorkerViewModel>().ToList();
            if (!selectedVMs.Any()) return;
            var result = MessageBox.Show($"Are you sure you want to ACTIVATE {selectedVMs.Count} workers?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                int count = 0;
                foreach (var vm in selectedVMs) { var worker = vm.GetModel(); if (!worker.IsActive) { worker.IsActive = true; DataAccess.SaveWorker(worker); count++; } }
                RefreshAllDataAndFilters();
                MessageBox.Show($"{count} workers activated.", "Complete");
            }
        }

        private void StatusFilter_Changed(object sender, RoutedEventArgs e) => ApplyFilters();
        private void UpdateDetailView() => WorkerAssetManager.SetWorker(_selectedWorker, AppData.GetAssets());
        private void UpdateActiveFilterTags()
        {
            if (_filterViewModel == null) return;
            ActiveFiltersPanel.Children.Clear();
            var activeFilters = new List<string>();
            if (ActiveFilterButton.IsChecked == true) activeFilters.Add("Status: Active"); else if (InactiveFilterButton.IsChecked == true) activeFilters.Add("Status: Inactive"); else activeFilters.Add("Status: All");
            if (!string.IsNullOrWhiteSpace(_filterViewModel.SearchText)) { activeFilters.Add($"Search: '{_filterViewModel.SearchText}'"); }
            var selectedDepts = _filterViewModel.DepartmentOptions.Where(o => o.IsChecked).Select(o => o.Value).ToList();
            if (selectedDepts.Any()) { activeFilters.Add($"Dept: {string.Join(", ", selectedDepts)}"); }
            foreach (var filterText in activeFilters) { ActiveFiltersPanel.Children.Add(new Border { Background = Brushes.LightGray, CornerRadius = new CornerRadius(10), Margin = new Thickness(2), Padding = new Thickness(8, 3, 8, 3), Child = new TextBlock { Text = filterText, Foreground = Brushes.Black } }); }
        }
        private void AddWorkerButton_Click(object _, RoutedEventArgs e) { var addWindow = new AddEditWorkerWindow { Owner = this }; if (addWindow.ShowDialog() == true) { DataAccess.SaveWorker(addWindow.Worker); RefreshAllDataAndFilters(); } }
        private void Manager_EditWorker(object sender, Worker worker) { if (worker == null) return; var editWindow = new AddEditWorkerWindow(worker) { Owner = this }; if (editWindow.ShowDialog() == true) { DataAccess.SaveWorker(editWindow.Worker); RefreshAllDataAndFilters(); } }
        private void Manager_DeleteWorker(object sender, Worker worker) { if (worker == null) return; var result = MessageBox.Show($"Are you sure you want to permanently delete '{worker.per_adiper_soyadi}'?", "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning); if (result == MessageBoxResult.Yes) { DataAccess.DeleteWorker(worker); RefreshAllDataAndFilters(); } }
        private void WorkersDataGrid_MouseDoubleClick(object _, MouseButtonEventArgs e) { if (WorkersDataGrid.SelectedItem is WorkerViewModel selectedWorkerVM) { var detailWindow = new WorkerDetailWindow(selectedWorkerVM.GetModel(), AppData.GetAssets()) { Owner = this }; detailWindow.OnWorkerUpdated += RefreshAllDataAndFilters; detailWindow.ShowDialog(); } }
        private void AssetManager_AssetAssignmentChanged(object _, EventArgs e) => RefreshAllDataAndFilters();
        private void ClearFiltersButton_Click(object _, RoutedEventArgs e) { ActiveFilterButton.IsChecked = true; _filterViewModel.Clear(); }

        private void CloseTheMenu() { isMenuOpen = false; MenuOverlay.Visibility = Visibility.Collapsed; (FindResource("CloseMenu") as Storyboard)?.Begin(); }
        private void MenuButton_Click(object _, RoutedEventArgs e) { if (isMenuOpen) CloseTheMenu(); else { isMenuOpen = true; UserSwitchPopup.IsOpen = false; MenuOverlay.Visibility = Visibility.Visible; (FindResource("OpenMenu") as Storyboard)?.Begin(); } }
        private void CloseMenuButton_Click(object _, RoutedEventArgs e) => CloseTheMenu();
        private void MenuOverlay_MouseDown(object _, MouseButtonEventArgs e) => CloseTheMenu();
        private void UpdateUserDisplay() { if (SessionManager.CurrentUser != null) { UserProfileIcon.Text = SessionManager.CurrentUser.ProfilePicture; UserProfileName.Text = SessionManager.CurrentUser.FullName; } }
        private async void DashboardButton_Click(object _, RoutedEventArgs e) { await NavigationManager.GoToDashboard(); }
        private void UsersButton_Click(object _, RoutedEventArgs e) { CloseTheMenu(); }
        private async void AssetsButton_Click(object _, RoutedEventArgs e) { await NavigationManager.GoToAssetWindow(); }
        private async void HistoryLogButton_Click(object _, RoutedEventArgs e) { await NavigationManager.GoToHistoryLogWindow(); }
        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationManager.GoToReportsWindow();
        }
        private async void LifecycleReportButton_Click(object _, RoutedEventArgs e) { await NavigationManager.GoToLifecycleReportWindow(); }
        private void UserProfileButton_Click(object _, RoutedEventArgs e) => UserSwitchPopup.IsOpen = true;
        private void SwitchUserButton_Click(object _, RoutedEventArgs e) => ReturnToLogin();
        private void LogoutButton_Click(object _, RoutedEventArgs e) => ReturnToLogin();
        private void ReturnToLogin() { NavigationManager.RestartApplication(); }
    }
}