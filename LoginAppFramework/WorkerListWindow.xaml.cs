using System;
using System.Collections.Generic;
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
        private int? _workerIdToSelectOnLoad;
        private DataGridPersonalizationController _gridPersonalization;
        private bool isDetailPanelOpen;

        private static readonly IReadOnlyDictionary<string, string> WorkerColumnLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["Name"] = "Tam Ad",
                ["Position"] = "Vəzifə",
                ["Department"] = "Departament",
                ["IsActive"] = "Status",
                ["AssignedAssetsCount"] = "Vəsaitlər"
            };

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

            _gridPersonalization = new DataGridPersonalizationController(
                WorkersDataGrid,
                ColumnsButton,
                "workers-grid",
                WorkerColumnLabels);
        }

        private async void Window_Loaded(object _, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(20);

            try
            {
                if (string.IsNullOrEmpty(SessionManager.CurrentUserConnectionString))
                {
                    DialogService.Error(
                        this,
                        "Sessiya Xətası",
                        "Aktiv istifadəçi sessiyası tapılmadı. Giriş ekranına qaytarılırsınız.");

                    NavigationManager.RestartApplication();
                    return;
                }

                _viewModel.Refresh();

                if (_workerIdToSelectOnLoad.HasValue)
                    SelectWorkerById(_workerIdToSelectOnLoad.Value);

                WorkerAssetManager.OnDetailPanelClosed += DetailControl_PanelClosed;
                WorkerAssetManager.OnAssetDoubleClicked += Manager_AssetDoubleClicked;
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Yükləmə Xətası",
                    $"İşçilər yüklənərkən xəta baş verdi:\n\n{ex.Message}");
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
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            bool confirmResult = DialogService.Confirm(
                this,
                "Sinxronizasiyanı Təsdiq Et",
                "İşçi siyahısı uzaq HR məlumat bazası ilə sinxronizasiya ediləcək.\n\nDavam etmək istəyirsiniz?",
                "Sinxronizasiya et",
                "Ləğv et");

            if (!confirmResult) return;

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
                NotificationService.Error(
                    this,
                    $"Sinxronizasiya zamanı xəta baş verdi:\n{ex.Message}");
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
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            var selected = WorkersDataGrid.SelectedItems
                .Cast<WorkerViewModel>()
                .ToList();

            if (selected.Count == 0) return;

            bool result = DialogService.Confirm(
                this,
                "Deaktivləşdirməni Təsdiq Et",
                $"Seçilmiş {selected.Count} işçini qeyri-aktiv etmək istəyirsiniz?",
                "Deaktiv et",
                "Ləğv et");

            if (!result) return;

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
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            var selected = WorkersDataGrid.SelectedItems
                .Cast<WorkerViewModel>()
                .ToList();

            if (selected.Count == 0) return;

            bool result = DialogService.Confirm(
                this,
                "Aktivləşdirməni Təsdiq Et",
                $"Seçilmiş {selected.Count} işçini aktiv etmək istəyirsiniz?",
                "Aktiv et",
                "Ləğv et");

            if (!result) return;

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
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
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

            bool result = DialogService.Confirm(
                this,
                "Silməni Təsdiq Et",
                $"'{worker.per_adiper_soyadi}' adlı işçini həmişəlik silmək istəyirsiniz?",
                "İşçini sil",
                "Ləğv et",
                destructive: true);

            if (!result) return;

            _viewModel.DeleteWorker(worker);
            RefreshViewModelPreservingSelection();
        }

        private void WorkersDataGrid_MouseDoubleClick(object _, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source ||
                FindParent<DataGridRow>(source) == null ||
                WorkersDataGrid.SelectedItem is not WorkerViewModel selectedWorker)
            {
                return;
            }

            OpenWorkerDetails(selectedWorker);
            e.Handled = true;
        }

        private static T FindParent<T>(DependencyObject child)
            where T : DependencyObject
        {
            if (child == null)
                return null;

            DependencyObject parent = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parent == null)
                return null;

            return parent is T typed ? typed : FindParent<T>(parent);
        }

        private WorkerViewModel GetWorkerFromContext(object sender)
            => (sender as FrameworkElement)?.DataContext as WorkerViewModel;

        private void OpenWorkerDetails(WorkerViewModel workerVm)
        {
            if (workerVm == null)
                return;

            WorkersDataGrid.SelectedItem = workerVm;
            WorkersDataGrid.ScrollIntoView(workerVm);
            _selectedWorker = workerVm;
            UpdateDetailView();

            if (!isDetailPanelOpen)
                OpenDetailPanel();
        }

        private void OpenWorkerDetailsContext_Click(object sender, RoutedEventArgs e)
            => OpenWorkerDetails(GetWorkerFromContext(sender));

        private void EditWorkerContext_Click(object sender, RoutedEventArgs e)
        {
            var workerVm = GetWorkerFromContext(sender);
            if (workerVm == null)
                return;

            if (!SessionManager.CanEdit())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            Manager_EditWorker(sender, workerVm.GetModel());
        }

        private void CopyWorkerNameContext_Click(object sender, RoutedEventArgs e)
        {
            var worker = GetWorkerFromContext(sender)?.GetModel();
            CopyWorkerText(worker?.per_adiper_soyadi, "İşçi adı");
        }

        private void CopyWorkerCodeContext_Click(object sender, RoutedEventArgs e)
        {
            var worker = GetWorkerFromContext(sender)?.GetModel();
            CopyWorkerText(worker?.per_kod, "İşçi kodu");
        }

        private void CopyWorkerText(string value, string label)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                NotificationService.Info(
                    this,
                    $"{label} boşdur.",
                    title: "Kopyalama");
                return;
            }

            try
            {
                Clipboard.SetText(value);
                NotificationService.Success(this, $"{label} kopyalandı.");
            }
            catch (Exception ex)
            {
                NotificationService.Error(
                    this,
                    $"Kopyalama zamanı xəta baş verdi: {ex.Message}");
            }
        }

        private void ToggleWorkerActiveContext_Click(object sender, RoutedEventArgs e)
        {
            var workerVm = GetWorkerFromContext(sender);
            if (workerVm == null)
                return;

            if (!SessionManager.CanEdit())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            var worker = workerVm.GetModel();
            bool nextState = !worker.IsActive;
            _viewModel.SetActiveState(new[] { worker.Id }, nextState);
            RefreshViewModelPreservingSelection();

            NotificationService.Success(
                this,
                nextState
                    ? "İşçi aktiv edildi."
                    : "İşçi qeyri-aktiv edildi.");
        }

        public void NavigateToWorker(int workerId)
        {
            if (workerId <= 0)
                return;

            _workerIdToSelectOnLoad = workerId;

            if (IsLoaded)
                SelectWorkerById(workerId);
        }

        private void SelectWorkerById(int workerId)
        {
            var visible = _viewModel.VisibleWorkers
                .FirstOrDefault(vm => vm.GetModel().Id == workerId);

            if (visible != null)
            {
                OpenWorkerDetails(visible);
                return;
            }

            var worker = AppData.GetWorkers()
                .FirstOrDefault(item => item.Id == workerId);

            if (worker == null)
                return;

            var assignedAssets = AppData.GetAssets()
                .Where(asset => asset.WorkerId == workerId)
                .ToList();

            _selectedWorker = new WorkerViewModel(worker, assignedAssets);
            WorkersDataGrid.SelectedItem = null;
            UpdateDetailView();

            if (!isDetailPanelOpen)
                OpenDetailPanel();
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
