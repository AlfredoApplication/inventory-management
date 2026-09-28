using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace LoginAppFramework
{
    public partial class AssetWindow : Window
    {
        private readonly AssetListWindowViewModel _viewModel;
        private AssetCheckableViewModel _selectedAssetVM;
        private bool isMenuOpen = false;
        private readonly int? _assetIdToSelectOnLoad;
        private readonly DispatcherTimer _selectionTimer;

        public AssetWindow()
        {
            InitializeComponent();

            _viewModel = new AssetListWindowViewModel();
            DataContext = _viewModel;

            _assetIdToSelectOnLoad = null;
            _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _selectionTimer.Tick += SelectionTimer_Tick;
        }

        public AssetWindow(int assetIdToSelect = -1)
        {
            InitializeComponent();

            _viewModel = new AssetListWindowViewModel();
            DataContext = _viewModel;

            _assetIdToSelectOnLoad = assetIdToSelect > 0 ? assetIdToSelect : null;
            _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _selectionTimer.Tick += SelectionTimer_Tick;
        }

        private async void Window_Loaded(object _, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(20);

            _viewModel.Refresh();
            InitializeColumnFilters();

            if (_assetIdToSelectOnLoad.HasValue)
            {
                SelectAssetById(_assetIdToSelectOnLoad.Value);
            }
            else
            {
                UpdateDetailView();
            }

            UpdateUserDisplay();

            AssetDetailControl.OnAssetModified += (s, asset) =>
                DetailControl_EditAsset(s, new AssetCheckableViewModel(asset));
            AssetDetailControl.OnAssetDeleted += (s, asset) =>
                DetailControl_DeleteAsset(s, new AssetCheckableViewModel(asset));
            AssetDetailControl.OnAssignmentChanged += DetailControl_AssignmentChanged;
            AssetDetailControl.OnDetailPanelClosed += DetailControl_PanelClosed;

            LoadingOverlay.Visibility = Visibility.Collapsed;
            ApplyRoleBasedPermissions();
        }

        #region Action Buttons (QR, Import, Export)

        private void DetailControl_PanelClosed(object sender, EventArgs e)
        {
            AssetsDataGrid.SelectedItem = null;
            if (isDetailPanelOpen) CloseDetailPanel();
        }

        private void GenerateBarcodesButton_Click(object sender, RoutedEventArgs e)
        {
            // 5. Seçilmiş yox, İŞARƏLƏNMİŞ vəsaitləri götürün
            var checkedAssets = _viewModel.AllAssets
                .Where(vm => vm.IsChecked)
                .Select(vm => vm.Asset)
                .Where(a => !string.IsNullOrEmpty(a.VesaitinKodu))
                .ToList();

            if (!checkedAssets.Any())
            {
                MessageBox.Show("QR kod yaratmaq üçün ən azı bir vəsait işarələyin (vəsaitin kodu boş olmamalıdır).", "Vəsait İşarələnməyib", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var printWindow = new PrintQrCodesWindow(checkedAssets) { Owner = this };
            printWindow.ShowDialog();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var assetsToExport = _viewModel.VisibleAssets
                .Select(vm => vm.Asset)
                .ToList();

            if (assetsToExport.Count == 0)
            {
                MessageBox.Show("Export üçün heç bir vəsait tapılmadı.", "Boş Siyahı", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook|*.xlsx",
                Title = "Excel Faylını Yadda Saxla",
                FileName = $"Vesaitler_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() != true) return;

            try
            {
                _viewModel.Export(saveFileDialog.FileName);
                MessageBox.Show("Məlumatlar uğurla Excel faylına export edildi.", "Export Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export zamanı xəta baş verdi: {ex.Message}", "Xəta", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Title = "Import üçün Excel faylı seçin",
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };

            if (openFileDialog.ShowDialog() != true) return;

            AssetImportBatch batch;
            try
            {
                batch = _viewModel.ParseImport(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel faylı oxunarkən xəta baş verdi:\n\n{ex.Message}", "Import Xətası", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (batch.Assets.Count == 0)
            {
                string message = batch.Errors.Count > 0
                    ? $"İmport üçün etibarlı vəsait tapılmadı.\n\n{string.Join("\n", batch.Errors.Take(10))}"
                    : "İmport üçün etibarlı vəsait tapılmadı.";

                MessageBox.Show(message, "Import Başa Çatdı", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dbWorkers = AppData.GetWorkers();
            var confirmedMappings = new Dictionary<string, Worker>(StringComparer.OrdinalIgnoreCase);
            var requestedNames = batch.RequestedWorkerNames.Values
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var mappingViewModels = requestedNames
                .Select(name => new ImportMappingViewModel(name, dbWorkers))
                .ToList();

            foreach (var vm in mappingViewModels.Where(vm => !vm.IsUnmapped))
                confirmedMappings[vm.ExcelUserName] = vm.SelectedDbWorker;

            var unmappedNames = mappingViewModels
                .Where(vm => vm.IsUnmapped)
                .Select(vm => vm.ExcelUserName)
                .ToList();

            if (unmappedNames.Count > 0)
            {
                var mappingWindow = new ImportMappingWindow(unmappedNames, dbWorkers) { Owner = this };
                if (mappingWindow.ShowDialog() != true)
                {
                    MessageBox.Show("Import ləğv edildi.");
                    return;
                }

                foreach (var mapping in mappingWindow.ConfirmedMappings)
                {
                    if (mapping.Value?.Id > 0)
                        confirmedMappings[mapping.Key] = mapping.Value;
                }
            }

            foreach (var asset in batch.Assets)
            {
                if (batch.RequestedWorkerNames.TryGetValue(asset, out string workerName) &&
                    confirmedMappings.TryGetValue(workerName, out Worker worker))
                {
                    asset.AssignWorker(worker);
                }
                else
                {
                    asset.ClearWorkerAssignment();
                }
            }

            string summary = $"{batch.Assets.Count} yeni vəsait importa hazırdır.";
            if (batch.Errors.Count > 0)
                summary += $"\n\n{batch.Errors.Count} sətir xətaya görə ötürüldü.";
            summary += "\n\nBu vəsaitləri verilənlər bazasında yadda saxlamaq istəyirsiniz?";

            if (MessageBox.Show(summary, "Importu Təsdiq Et", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                _viewModel.ImportAssets(batch.Assets);
                MessageBox.Show($"{batch.Assets.Count} vəsait uğurla import edildi.", "Import Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Verilənlər bazasına yadda saxlayarkən kritik xəta baş verdi.\n\nXəta: {ex.InnerException?.Message ?? ex.Message}", "Verilənlər Bazası Xətası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                RefreshDataAndSelection();
            }
        }

        #endregion

        #region Bulk Action Methods

        // Bütün Bulk... metodlarını yeniləyin
        private void BulkDeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanDelete())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var checkedAssets = _viewModel.AllAssets.Where(vm => vm.IsChecked).Select(vm => vm.Asset).ToList();
            if (!checkedAssets.Any()) return;

            var result = MessageBox.Show($"İşarələnmiş {checkedAssets.Count} vəsaiti həmişəlik silməyə əminsinizmi?", "Toplu Silməni Təsdiq Et", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.DeleteCheckedAssets();
                RefreshDataAndSelection();
            }
        }

        private void BulkEditButton_Click(object sender, RoutedEventArgs e)
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

            var checkedAssets = _viewModel.GetCheckedAssets();
            if (checkedAssets.Count == 0) return;

            var editWindow = new BulkEditWindow(checkedAssets.Count)
            {
                Owner = this
            };

            if (editWindow.ShowDialog() != true) return;

            try
            {
                var result = _viewModel.ApplyBulkChanges(editWindow.Changes);

                RefreshDataAndSelection();

                if (result.UpdatedCount > 0)
                {
                    MessageBox.Show(
                        $"{result.UpdatedCount} vəsait uğurla dəyişdirildi.",
                        "Tamamlandı",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }

                if (result.SkippedStatusAssets.Count > 0)
                {
                    string skippedNames = string.Join(
                        "\n",
                        result.SkippedStatusAssets.Select(a => $"- {a.VesaitinAdi}"));

                    MessageBox.Show(
                        $"XƏBƏRDARLIQ: {result.SkippedStatusAssets.Count} vəsaitin statusu dəyişdirilmədi, çünki onlar işçiyə təhkim olunub və 'İstifadədədir' statusunda qalmalıdırlar:\n\n{skippedNames}",
                        "Status Dəyişikliyi Ötürüldü",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                if (result.UpdatedCount == 0 &&
                    result.SkippedStatusAssets.Count == 0)
                {
                    MessageBox.Show(
                        "Heç bir dəyişiklik edilmədi.",
                        "Məlumat",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Məlumat bazasına yadda saxlayarkən xəta baş verdi: {ex.Message}",
                    "Xəta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BulkAssignButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var checkedAssets = _viewModel.AllAssets.Where(vm => vm.IsChecked).Select(vm => vm.Asset).ToList();
            if (!checkedAssets.Any())
            {
                MessageBox.Show("Təhkim etmək üçün ən azı bir vəsait işarələyin.", "Vəsait Seçilməyib", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectWindow = new SelectWorkerWindow(_viewModel.Workers.ToList()) { Owner = this };
            if (selectWindow.ShowDialog() == true)
            {
                Worker newWorker = selectWindow.SelectedWorker;

                try
                {
                    _viewModel.AssignCheckedAssets(newWorker);
                    RefreshDataAndSelection();

                    MessageBox.Show($"{checkedAssets.Count} vəsait {newWorker.per_adiper_soyadi} adlı işçiyə uğurla təhkim edildi.",
                                    "Təhkim Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Toplu təhkim zamanı xəta baş verdi:\n\n{ex.Message}",
                                    "Xəta", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        #endregion

        #region Filtering and Data Logic

        private bool isDetailPanelOpen = false;

        private void RefreshDataAndSelection(int? assetIdToSelect = null)
        {
            _viewModel.Refresh();
            InitializeColumnFilters();

            if (assetIdToSelect.HasValue)
            {
                SelectAssetById(assetIdToSelect.Value);
            }
            else
            {
                _selectedAssetVM = null;
                UpdateDetailView();
            }
        }

        private void AddAssetButton_Click(object _, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var newAsset = new Asset();
            var addWindow = new AddEditAssetWindow(newAsset) { Owner = this };
            if (addWindow.ShowDialog() == true) { RefreshDataAndSelection(addWindow.Asset.Id); }
        }

        private void DetailControl_EditAsset(object _, AssetCheckableViewModel assetVMToEdit)
        {
            var editWindow = new AddEditAssetWindow(assetVMToEdit.Asset) { Owner = this };
            if (editWindow.ShowDialog() == true) { RefreshDataAndSelection(assetVMToEdit.Asset.Id); }
        }

        private void DetailControl_DeleteAsset(object _, AssetCheckableViewModel assetVMToDelete)
        {
            if (MessageBox.Show($"'{assetVMToDelete.Asset.VesaitinAdi}' adlı vəsaiti HƏMİŞƏLİK SİLMƏK istədiyinizə əminsinizmi?", "Silməni Təsdiq Et", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            {
                _viewModel.DeleteAsset(assetVMToDelete.Asset);
                _selectedAssetVM = null;
                RefreshDataAndSelection();
            }
        }

        private void DetailControl_AssignmentChanged(object _, EventArgs e) => RefreshDataAndSelection(_selectedAssetVM?.Asset.Id);


        private void AssetsDataGrid_SelectionChanged(object _, SelectionChangedEventArgs e)
        {
            // Detal panelinin açılması üçün sətir seçimi hələ də işləyir
            _selectionTimer.Stop();
            _selectionTimer.Start();
        }

        private void SelectionTimer_Tick(object sender, EventArgs e)
        {
            _selectionTimer.Stop();

            if (AssetsDataGrid.SelectedItems.Count == 1)
            {
                var newlySelectedAssetVM = AssetsDataGrid.SelectedItem as AssetCheckableViewModel;

                // Əgər seçilmiş element dəyişibsə və ya detal paneli bağlıdırsa, açın
                if (_selectedAssetVM != newlySelectedAssetVM || !isDetailPanelOpen)
                {
                    _selectedAssetVM = newlySelectedAssetVM;
                    UpdateDetailView();
                    if (!isDetailPanelOpen) OpenDetailPanel();
                }
            }
            else
            {
                // Çoxlu seçim və ya heç bir seçim yoxdursa, detal panelini bağlayın
                _selectedAssetVM = null;
                if (isDetailPanelOpen) CloseDetailPanel();
            }
        }

        private void ClearFiltersButton_Click(object _, RoutedEventArgs e) => _viewModel.ClearFilters();


        private void UpdateDetailView() => AssetDetailControl.DisplayAsset(_selectedAssetVM?.Asset, _viewModel.Workers.ToList());

        public void SelectAssetById(int assetId)
        {
            var assetToSelect = (AssetsDataGrid.ItemsSource as IEnumerable<AssetCheckableViewModel>)?.FirstOrDefault(vm => vm.Asset.Id == assetId);
            if (assetToSelect != null)
            {
                AssetsDataGrid.SelectedItem = assetToSelect;
                AssetsDataGrid.ScrollIntoView(assetToSelect);
                _selectedAssetVM = assetToSelect;
                UpdateDetailView();
                if (!isDetailPanelOpen) OpenDetailPanel();
            }
            else
            {
                if (isDetailPanelOpen) CloseDetailPanel();
            }
        }

        #endregion

        #region Menu Handlers
        private void OpenDetailPanel()
        {
            var animation = new DoubleAnimation(0, 450, TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            DetailPanelContainer.BeginAnimation(WidthProperty, animation);
            isDetailPanelOpen = true;
        }

        private void CloseDetailPanel()
        {
            var animation = new DoubleAnimation(0, TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            DetailPanelContainer.BeginAnimation(WidthProperty, animation);
            isDetailPanelOpen = false;
        }
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child); if (parentObject == null) return null; T parent = parentObject as T; return parent ?? FindParent<T>(parentObject);
        }

        private void CopyCellContent_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem || menuItem.Parent is not ContextMenu contextMenu || contextMenu.PlacementTarget is not DependencyObject target) return;

            if (FindParent<DataGridCell>(target) is DataGridCell cell)
            {
                if (cell.DataContext is AssetCheckableViewModel viewModel && cell.Column is DataGridBoundColumn column)
                {
                    BindingBase bindingBase = column.ClipboardContentBinding ?? column.Binding;
                    if (bindingBase is Binding binding)
                    {
                        // Get the actual Asset object from the ViewModel
                        var asset = viewModel.Asset;
                        // Use reflection to get the property value from the Asset object
                        PropertyInfo propertyInfo = typeof(Asset).GetProperty(binding.Path.Path);
                        if (propertyInfo != null)
                        {
                            object value = propertyInfo.GetValue(asset);
                            string textToCopy = value?.ToString() ?? string.Empty;
                            if (!string.IsNullOrEmpty(textToCopy) && textToCopy != "-" && textToCopy != "(Boşdur)")
                            {
                                try
                                {
                                    Clipboard.SetText(textToCopy);
                                }
                                catch (Exception ex)
                                {
                                    MessageBox.Show($"Kopyalana bilmədi: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
        }
        private void CloseTheMenu() { isMenuOpen = false; MenuOverlay.Visibility = Visibility.Collapsed; (FindResource("CloseMenu") as Storyboard)?.Begin(); }
        private void MenuButton_Click(object _, RoutedEventArgs e) { if (isMenuOpen) CloseTheMenu(); else { isMenuOpen = true; UserSwitchPopup.IsOpen = false; MenuOverlay.Visibility = Visibility.Visible; (FindResource("OpenMenu") as Storyboard)?.Begin(); } }
        private void CloseMenuButton_Click(object _, RoutedEventArgs e) => CloseTheMenu();
        private void MenuOverlay_MouseDown(object _, MouseButtonEventArgs e) => CloseTheMenu();
        private void UpdateUserDisplay() { if (SessionManager.CurrentUser != null) { UserProfileIcon.Text = SessionManager.CurrentUser.ProfilePicture; UserProfileName.Text = SessionManager.CurrentUser.FullName; } }
        private async void DashboardButton_Click(object _, RoutedEventArgs e) { await NavigationManager.GoToDashboard(); }
        private async void UsersButton_Click(object _, RoutedEventArgs e) { await NavigationManager.GoToWorkerListWindow(); }
        private async void HistoryLogButton_Click(object _, RoutedEventArgs e) { await NavigationManager.GoToHistoryLogWindow(); }
        private async void LifecycleReportButton_Click(object _, RoutedEventArgs e) { await NavigationManager.GoToLifecycleReportWindow(); }
        private void UserProfileButton_Click(object _, RoutedEventArgs e) => UserSwitchPopup.IsOpen = true;
        private void SwitchUserButton_Click(object _, RoutedEventArgs e) => ReturnToLogin();
        private void LogoutButton_Click(object _, RoutedEventArgs e) => ReturnToLogin();
        private void ReturnToLogin() { NavigationManager.RestartApplication(); }
        #endregion

        #region Column Filters
        private void InitializeColumnFilters()
        {
            InitializeColumnFilter(VesaitinKoduFilter, "VesaitinKodu");
            InitializeColumnFilter(VesaitinAdiFilter, "VesaitinAdi");
            InitializeColumnFilter(KateqoriyaFilter, "Kateqoriya");
            InitializeColumnFilter(WorkerFilter, "Worker");
            InitializeColumnFilter(DepartmentFilter, "Department");
            InitializeColumnFilter(YerleshmeYeriFilter, "YerleshmeYeri");
            InitializeColumnFilter(EraziFilter, "Erazi");
        }

        private void InitializeColumnFilter(ColumnFilterControl control, string key)
        {
            var filter = _viewModel.GetColumnFilter(key);
            if (filter == null) return;

            control.InitializeFilter(filter);
            control.FilterApplied -= OnColumnFilterApplied;
            control.FilterApplied += OnColumnFilterApplied;
        }

        private void OnColumnFilterApplied(object sender, ColumnFilterEventArgs e)
            => _viewModel.ReapplyFilters();

        private void ApplyRoleBasedPermissions()
        {
            bool canEdit = SessionManager.CanEdit();

            // Disable editing buttons for read-only users
            AddAssetButton.IsEnabled = canEdit;
            BulkDeleteButton.IsEnabled = canEdit;
            BulkEditButton.IsEnabled = canEdit;
            BulkAssignButton.IsEnabled = canEdit;
            ImportButton.IsEnabled = canEdit;

            // QR and Export remain enabled (read-only users can use these)
            // GenerateBarcodesButton.IsEnabled stays true
            // ExportButton.IsEnabled stays true

            // Pass permission to detail control
            AssetDetailControl.SetReadOnlyMode(!canEdit);
        }
        #endregion

        // Bu mövcud SelectionChanged metodunu tamamilə silin
        // private void AssetsDataGrid_SelectionChanged(...
    }
}