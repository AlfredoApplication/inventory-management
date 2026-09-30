using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace LoginAppFramework
{
    public partial class WorkerListWindow : Window
    {
        private readonly WorkerListWindowViewModel _viewModel;
        private readonly DispatcherTimer _selectionTimer;

        private WorkerViewModel _selectedWorker;
        private bool isDetailPanelOpen;

        public WorkerListWindow()
        {
            InitializeComponent();

            _viewModel = new WorkerListWindowViewModel();
            DataContext = _viewModel;

            _selectionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(150)
            };
            _selectionTimer.Tick += SelectionTimer_Tick;
        }

        private async void Window_Loaded(object _, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(20);

            try
            {
                if (string.IsNullOrEmpty(SessionManager.CurrentUserConnectionString))
                {
                    MessageBox.Show(
                        "Aktiv istifadəçi sessiyası tapılmadı. Giriş ekranına qaytarılırsınız.",
                        "Sessiya Xətası",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    NavigationManager.RestartApplication();
                    return;
                }

                _viewModel.Refresh();
                WorkerAssetManager.OnDetailPanelClosed += DetailControl_PanelClosed;
                WorkerAssetManager.OnAssetDoubleClicked += Manager_AssetDoubleClicked;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"İşçilər yüklənərkən xəta baş verdi:\n\n{ex.Message}",
                    "Yükləmə Xətası",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                ApplyRoleBasedPermissions();
            }
        }

        private async void Manager_AssetDoubleClicked(object sender, Asset asset)
        {
            if (asset == null) return;

            await NavigationManager.GoToAssetWindow(this, asset.Id);
        }

        private void DetailControl_PanelClosed(object sender, EventArgs e)
            => WorkersDataGrid.SelectedItem = null;

        private void OpenDetailPanel()
        {
            double targetWidth = GetResponsiveDetailPanelWidth();

            var animation = new DoubleAnimation(
                DetailPanelContainer.ActualWidth,
                targetWidth,
                TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            DetailPanelContainer.BeginAnimation(WidthProperty, animation);
            isDetailPanelOpen = true;
        }

        private double GetResponsiveDetailPanelWidth()
        {
            double available = Math.Max(ActualWidth, 1000);
            return Math.Clamp(available * 0.42, 480, 680);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!isDetailPanelOpen)
                return;

            DetailPanelContainer.BeginAnimation(WidthProperty, null);
            DetailPanelContainer.Width = GetResponsiveDetailPanelWidth();
        }

        private void CloseDetailPanel()
        {
            var animation = new DoubleAnimation(
                0,
                TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };

            DetailPanelContainer.BeginAnimation(WidthProperty, animation);
            isDetailPanelOpen = false;
        }

        private async void SyncWorkersButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show(
                    "Bu əməliyyat üçün icazəniz yoxdur.",
                    "Giriş Qadağandır",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var confirmResult = MessageBox.Show(
                "İşçi siyahısı uzaq HR məlumat bazası ilə sinxronizasiya ediləcək.\n\nDavam etmək istəyirsiniz?",
                "Sinxronizasiyanı Təsdiq Et",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            SyncWorkersButton.IsEnabled = false;
            LoadingMessage.Text = "Sinxronizasiya edilir...";
            LoadingOverlay.Visibility = Visibility.Visible;

            try
            {
                int affectedRows = await _viewModel.SynchronizeAsync();
                RefreshViewModelPreservingSelection();

                NotificationService.Success(
                    this,
                    $"Sinxronizasiya tamamlandı. {affectedRows} qeyd yeniləndi.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Sinxronizasiya zamanı xəta baş verdi:\n{ex.Message}",
                    "Xəta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                SyncWorkersButton.IsEnabled = true;
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        private void RefreshViewModelPreservingSelection()
        {
            var selectedIds = WorkersDataGrid.SelectedItems
                .Cast<WorkerViewModel>()
                .Select(vm => vm.GetModel().Id)
                .ToHashSet();

            _viewModel.Refresh();

            if (selectedIds.Count == 0)
            {
                UpdateDetailView();
                return;
            }

            WorkersDataGrid.SelectedItems.Clear();
            foreach (var item in _viewModel.VisibleWorkers.Where(vm =>
                selectedIds.Contains(vm.GetModel().Id)))
            {
                WorkersDataGrid.SelectedItems.Add(item);
            }
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
            _viewModel.SetSelectedCount(selectedCount);

            if (selectedCount > 1)
            {
                if (isDetailPanelOpen) CloseDetailPanel();

                _selectedWorker = null;
                return;
            }

            if (selectedCount == 1)
            {
                var newlySelectedWorker = WorkersDataGrid.SelectedItem as WorkerViewModel;

                if (_selectedWorker != newlySelectedWorker)
                {
                    _selectedWorker = newlySelectedWorker;
                    UpdateDetailView();

                    if (!isDetailPanelOpen)
                        OpenDetailPanel();
                }

                return;
            }

            if (isDetailPanelOpen) CloseDetailPanel();
            _selectedWorker = null;
        }

        private void BulkDeactivateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show(
                    "Bu əməliyyat üçün icazəniz yoxdur.",
                    "Giriş Qadağandır",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var selected = WorkersDataGrid.SelectedItems
                .Cast<WorkerViewModel>()
                .ToList();

            if (selected.Count == 0) return;

            var result = MessageBox.Show(
                $"Seçilmiş {selected.Count} işçini qeyri-aktiv etmək istəyirsiniz?",
                "Deaktivləşdirməni Təsdiq Et",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            int count = _viewModel.SetActiveState(
                selected.Select(vm => vm.GetModel().Id),
                false);

            RefreshViewModelPreservingSelection();
            NotificationService.Success(this, $"{count} işçi qeyri-aktiv edildi.");
        }

        private void BulkActivateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show(
                    "Bu əməliyyat üçün icazəniz yoxdur.",
                    "Giriş Qadağandır",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var selected = WorkersDataGrid.SelectedItems
                .Cast<WorkerViewModel>()
                .ToList();

            if (selected.Count == 0) return;

            var result = MessageBox.Show(
                $"Seçilmiş {selected.Count} işçini aktiv etmək istəyirsiniz?",
                "Aktivləşdirməni Təsdiq Et",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            int count = _viewModel.SetActiveState(
                selected.Select(vm => vm.GetModel().Id),
                true);

            RefreshViewModelPreservingSelection();
            NotificationService.Success(this, $"{count} işçi aktiv edildi.");
        }

        private void UpdateDetailView()
            => WorkerAssetManager.SetWorker(_selectedWorker, AppData.GetAssets());

        private void AddWorkerButton_Click(object _, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show(
                    "Bu əməliyyat üçün icazəniz yoxdur.",
                    "Giriş Qadağandır",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var addWindow = new AddEditWorkerWindow { Owner = this };
            if (addWindow.ShowDialog() == true)
            {
                _viewModel.SaveWorker(addWindow.Worker);
                RefreshViewModelPreservingSelection();
            }
        }

        private void Manager_EditWorker(object sender, Worker worker)
        {
            if (worker == null) return;

            var editWindow = new AddEditWorkerWindow(worker) { Owner = this };
            if (editWindow.ShowDialog() == true)
            {
                _viewModel.SaveWorker(editWindow.Worker);
                RefreshViewModelPreservingSelection();
            }
        }

        private void Manager_DeleteWorker(object sender, Worker worker)
        {
            if (worker == null) return;

            var result = MessageBox.Show(
                $"'{worker.per_adiper_soyadi}' adlı işçini həmişəlik silmək istəyirsiniz?",
                "Silməni Təsdiq Et",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            _viewModel.DeleteWorker(worker);
            RefreshViewModelPreservingSelection();
        }

        private void WorkersDataGrid_MouseDoubleClick(object _, MouseButtonEventArgs e)
        {
            if (WorkersDataGrid.SelectedItem is not WorkerViewModel selectedWorker) return;

            var detailWindow = new WorkerDetailWindow(
                selectedWorker.GetModel(),
                AppData.GetAssets())
            {
                Owner = this
            };

            detailWindow.OnWorkerUpdated += RefreshViewModelPreservingSelection;
            detailWindow.ShowDialog();
        }

        private void AssetManager_AssetAssignmentChanged(object _, EventArgs e)
            => RefreshViewModelPreservingSelection();

        private void ClearFiltersButton_Click(object _, RoutedEventArgs e)
        {
            _viewModel.ClearFilters();
        }

        private void ToggleFilterPanelButton_Click(object sender, RoutedEventArgs e)
        {
            bool isVisible = FilterSidebar.Visibility == Visibility.Visible;
            SetFilterPanelVisibility(!isVisible);
        }

        private void SetFilterPanelVisibility(bool isVisible)
        {
            FilterSidebar.Visibility =
                isVisible ? Visibility.Visible : Visibility.Collapsed;

            FilterColumn.Width =
                isVisible ? new GridLength(260) : new GridLength(0);

            FilterPanelToggleButton.Content =
                isVisible ? "Filtrləri Gizlət" : "Filtrləri Göstər";
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SetFilterPanelVisibility(true);
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape && isDetailPanelOpen)
            {
                WorkersDataGrid.SelectedItems.Clear();
                _selectedWorker = null;
                CloseDetailPanel();
                e.Handled = true;
            }
        }

        private void MenuButton_Click(object _, RoutedEventArgs e)
            => SharedNavigationMenu.Open();

        private void ApplyRoleBasedPermissions()
        {
            bool canEdit = SessionManager.CanEdit();

            SyncWorkersButton.IsEnabled = canEdit;
            AddWorkerButton.IsEnabled = canEdit;
            BulkDeactivateButton.IsEnabled = canEdit;
            BulkActivateButton.IsEnabled = canEdit;

            WorkerAssetManager.SetReadOnlyMode(!canEdit);
        }
    }
}
