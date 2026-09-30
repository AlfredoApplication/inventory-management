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
        private int? _assetIdToSelectOnLoad;
        private readonly DispatcherTimer _selectionTimer;
        private DataGridPersonalizationController _gridPersonalization;
        private bool _suppressSelectionChange;

        private static readonly IReadOnlyDictionary<string, string> AssetColumnLabels =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["VesaitinKodu"] = "Vəsaitin Kodu",
                ["VesaitinAdi"] = "Vəsaitin Adı",
                ["ITAvadanliqlarininSeriyaNomresi"] = "Seriya Nömrəsi",
                ["Kateqoriya"] = "Kateqoriya",
                ["Status"] = "Status",
                ["Worker.per_adiper_soyadi"] = "Təhkim Olunan Əməkdaş",
                ["Worker.pgk_gorev_adi"] = "Vəzifəsi",
                ["Worker.pdp_adi"] = "Bölmə/Şöbə/Departament",
                ["YerleshmeYeri"] = "Yerləşmə Yeri",
                ["Erazi"] = "Ərazi"
            };

        public AssetWindow()
        {
            InitializeComponent();

            _viewModel = new AssetListWindowViewModel();
            DataContext = _viewModel;

            _assetIdToSelectOnLoad = null;
            _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _selectionTimer.Tick += SelectionTimer_Tick;
            InitializeGridPersonalization();
        }

        public AssetWindow(int assetIdToSelect = -1)
        {
            InitializeComponent();

            _viewModel = new AssetListWindowViewModel();
            DataContext = _viewModel;

            _assetIdToSelectOnLoad = assetIdToSelect > 0 ? assetIdToSelect : null;
            _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _selectionTimer.Tick += SelectionTimer_Tick;
            InitializeGridPersonalization();
        }

        private void InitializeGridPersonalization()
        {
            _gridPersonalization = new DataGridPersonalizationController(
                AssetsDataGrid,
                ColumnsButton,
                "assets-grid",
                AssetColumnLabels);
        }

        private async void Window_Loaded(object _, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(20);

            try
            {
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

                AssetDetailControl.OnAssetModified += (s, asset) =>
                    DetailControl_EditAsset(s, new AssetCheckableViewModel(asset));
                AssetDetailControl.OnAssetDeleted += (s, asset) =>
                    DetailControl_DeleteAsset(s, new AssetCheckableViewModel(asset));
                AssetDetailControl.OnAssignmentChanged += DetailControl_AssignmentChanged;
                AssetDetailControl.OnDetailPanelClosed += DetailControl_PanelClosed;
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Yükləmə Xətası",
                    $"Vəsaitlər yüklənərkən xəta baş verdi:\n\n{ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
                ApplyRoleBasedPermissions();
            }
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
                NotificationService.Info(
                    this,
                    "Barkod yaratmaq üçün ən azı bir vəsait işarələyin (vəsaitin kodu boş olmamalıdır).",
                    title: "Vəsait İşarələnməyib");
                return;
            }

            var printWindow = new PrintQrCodesWindow(checkedAssets) { Owner = this };
            printWindow.ShowDialog();
        }

        private async void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var assetsToExport = _viewModel.VisibleAssets
                .Select(vm => vm.Asset)
                .ToList();

            if (assetsToExport.Count == 0)
            {
                NotificationService.Info(
                    this,
                    "Export üçün heç bir vəsait tapılmadı.",
                    title: "Boş Siyahı");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook|*.xlsx",
                Title = "Excel Faylını Yadda Saxla",
                FileName = $"Vesaitler_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveFileDialog.ShowDialog() != true) return;

            ExportButton.IsEnabled = false;
            OperationProgressOverlay.Show(
                "Excel faylı yaradılır...",
                $"{assetsToExport.Count} vəsait export üçün hazırlanır.");

            var progress = new Progress<OperationProgressInfo>(
                value => OperationProgressOverlay.Report(value));

            try
            {
                await Task.Run(
                    () => _viewModel.Export(
                        saveFileDialog.FileName,
                        progress));

                NotificationService.Success(
                    this,
                    $"{assetsToExport.Count} vəsait Excel faylına export edildi.");
            }
            catch (Exception ex)
            {
                NotificationService.Error(
                    this,
                    $"Export zamanı xəta baş verdi: {ex.Message}");
            }
            finally
            {
                OperationProgressOverlay.Hide();
                ExportButton.IsEnabled = true;
            }
        }

        private async void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            var openFileDialog = new OpenFileDialog
            {
                Title = "Import üçün Excel faylı seçin",
                Filter = "Excel Files (*.xlsx)|*.xlsx"
            };

            if (openFileDialog.ShowDialog() != true) return;

            AssetImportBatch batch;
            ImportButton.IsEnabled = false;
            OperationProgressOverlay.Show(
                "Excel faylı oxunur...",
                "Import məlumatları yoxlanılır.");

            var parseProgress = new Progress<OperationProgressInfo>(
                value => OperationProgressOverlay.Report(value));

            try
            {
                batch = await Task.Run(
                    () => _viewModel.ParseImport(
                        openFileDialog.FileName,
                        parseProgress));
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Import Xətası",
                    $"Excel faylı oxunarkən xəta baş verdi:\n\n{ex.Message}");
                return;
            }
            finally
            {
                OperationProgressOverlay.Hide();
                ImportButton.IsEnabled = true;
            }

            if (batch.Assets.Count == 0)
            {
                string message = batch.Errors.Count > 0
                    ? $"İmport üçün etibarlı vəsait tapılmadı.\n\n{string.Join("\n", batch.Errors.Take(10))}"
                    : "İmport üçün etibarlı vəsait tapılmadı.";

                DialogService.Info(
                    this,
                    "Import Başa Çatdı",
                    message);
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
                    return;

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

            if (!DialogService.Confirm(
                    this,
                    "Importu Təsdiq Et",
                    summary,
                    "Import et",
                    "Ləğv et"))
                return;

            ImportButton.IsEnabled = false;
            OperationProgressOverlay.Show(
                "Vəsaitlər import edilir...",
                $"{batch.Assets.Count} vəsait verilənlər bazasına yazılır.");

            OperationProgressOverlay.Report(
                new OperationProgressInfo(
                    "Vəsaitlər import edilir...",
                    0,
                    batch.Assets.Count,
                    "Verilənlər bazasına yazılır."));

            try
            {
                await Task.Run(() => _viewModel.ImportAssets(batch.Assets));

                OperationProgressOverlay.Report(
                    new OperationProgressInfo(
                        "Import tamamlandı",
                        batch.Assets.Count,
                        batch.Assets.Count,
                        $"{batch.Assets.Count} vəsait yadda saxlanıldı."));

                NotificationService.Success(
                    this,
                    $"{batch.Assets.Count} vəsait import edildi.");
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Verilənlər Bazası Xətası",
                    $"Verilənlər bazasına yadda saxlayarkən kritik xəta baş verdi.\n\nXəta: {ex.InnerException?.Message ?? ex.Message}");
            }
            finally
            {
                OperationProgressOverlay.Hide();
                ImportButton.IsEnabled = true;
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
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            var checkedAssets = _viewModel.AllAssets.Where(vm => vm.IsChecked).Select(vm => vm.Asset).ToList();
            if (!checkedAssets.Any()) return;

            bool result = DialogService.Confirm(
                this,
                "Toplu Silməni Təsdiq Et",
                $"İşarələnmiş {checkedAssets.Count} vəsaiti həmişəlik silməyə əminsinizmi?",
                "Seçilmişləri sil",
                "Ləğv et",
                destructive: true);

            if (!result)
                return;

            _viewModel.DeleteCheckedAssets();
            RefreshDataAndSelection();
        }

        private void BulkEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
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
                    NotificationService.Success(
                        this,
                        $"{result.UpdatedCount} vəsait dəyişdirildi.");
                }

                if (result.SkippedStatusAssets.Count > 0)
                {
                    string skippedNames = string.Join(
                        "\n",
                        result.SkippedStatusAssets.Select(a => $"- {a.VesaitinAdi}"));

                    DialogService.Warning(
                        this,
                        "Status Dəyişikliyi Ötürüldü",
                        $"{result.SkippedStatusAssets.Count} vəsaitin statusu dəyişdirilmədi, çünki onlar işçiyə təhkim olunub və 'İstifadədədir' statusunda qalmalıdırlar:\n\n{skippedNames}");
                }

                if (result.UpdatedCount == 0 &&
                    result.SkippedStatusAssets.Count == 0)
                {
                    NotificationService.Success(
                        this,
                        "Dəyişiklik üçün yeni dəyər seçilməyib.");
                }
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Xəta",
                    $"Məlumat bazasına yadda saxlayarkən xəta baş verdi: {ex.Message}");
            }
        }

        private void BulkAssignButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            var checkedAssets = _viewModel.AllAssets.Where(vm => vm.IsChecked).Select(vm => vm.Asset).ToList();
            if (!checkedAssets.Any())
            {
                NotificationService.Info(
                    this,
                    "Təhkim etmək üçün ən azı bir vəsait işarələyin.",
                    title: "Vəsait Seçilməyib");
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

                    NotificationService.Success(
                        this,
                        $"{checkedAssets.Count} vəsait {newWorker.per_adiper_soyadi} adlı işçiyə təhkim edildi.");
                }
                catch (Exception ex)
                {
                    DialogService.Error(
                        this,
                        "Xəta",
                        $"Toplu təhkim zamanı xəta baş verdi:\n\n{ex.Message}");
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
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
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
            bool confirm = DialogService.Confirm(
                this,
                "Silməni Təsdiq Et",
                $"'{assetVMToDelete.Asset.VesaitinAdi}' adlı vəsaiti həmişəlik silmək istədiyinizə əminsinizmi?",
                "Vəsaiti sil",
                "Ləğv et",
                destructive: true);

            if (!confirm)
                return;

            _viewModel.DeleteAsset(assetVMToDelete.Asset);
            _selectedAssetVM = null;
            RefreshDataAndSelection();
        }

        private void DetailControl_AssignmentChanged(object _, EventArgs e) => RefreshDataAndSelection(_selectedAssetVM?.Asset.Id);


        private void ShowAssetDetails(AssetCheckableViewModel assetVm)
        {
            if (assetVm == null)
                return;

            AssetsDataGrid.SelectedItem = assetVm;
            AssetsDataGrid.ScrollIntoView(assetVm);
            _selectedAssetVM = assetVm;
            UpdateDetailView();

            if (!isDetailPanelOpen)
                OpenDetailPanel();
        }

        private void AssetsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source ||
                FindParent<DataGridRow>(source) == null ||
                AssetsDataGrid.SelectedItem is not AssetCheckableViewModel assetVm)
            {
                return;
            }

            ShowAssetDetails(assetVm);
            e.Handled = true;
        }

        private AssetCheckableViewModel GetAssetFromContext(object sender)
            => (sender as FrameworkElement)?.DataContext as AssetCheckableViewModel;

        private void OpenAssetDetailsContext_Click(object sender, RoutedEventArgs e)
            => ShowAssetDetails(GetAssetFromContext(sender));

        private void EditAssetContext_Click(object sender, RoutedEventArgs e)
        {
            var assetVm = GetAssetFromContext(sender);
            if (assetVm == null)
                return;

            if (!SessionManager.CanEdit())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            DetailControl_EditAsset(sender, assetVm);
        }

        private void CopyAssetCodeContext_Click(object sender, RoutedEventArgs e)
        {
            var asset = GetAssetFromContext(sender)?.Asset;
            CopyTextToClipboard(asset?.VesaitinKodu, "Vəsait kodu");
        }

        private void CopyAssetSerialContext_Click(object sender, RoutedEventArgs e)
        {
            var asset = GetAssetFromContext(sender)?.Asset;
            CopyTextToClipboard(asset?.ITAvadanliqlarininSeriyaNomresi, "Seriya nömrəsi");
        }

        private void CopyTextToClipboard(string value, string label)
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
                NotificationService.Success(
                    this,
                    $"{label} kopyalandı.");
            }
            catch (Exception ex)
            {
                NotificationService.Error(
                    this,
                    $"Kopyalama zamanı xəta baş verdi: {ex.Message}");
            }
        }

        private void AssignAssetContext_Click(object sender, RoutedEventArgs e)
        {
            var assetVm = GetAssetFromContext(sender);
            if (assetVm == null)
                return;

            if (!SessionManager.CanEdit())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            var workers = AppData.GetWorkers()
                .Where(worker => worker.IsActive)
                .ToList();

            if (workers.Count == 0)
            {
                NotificationService.Info(
                    this,
                    "Təhkim ediləcək aktiv işçi tapılmadı.",
                    title: "İşçi Yoxdur");
                return;
            }

            var selectWindow = new SelectWorkerWindow(workers)
            {
                Owner = this
            };

            if (selectWindow.ShowDialog() != true ||
                selectWindow.SelectedWorker == null)
            {
                return;
            }

            AppServices.Assets.Assign(
                assetVm.Asset,
                selectWindow.SelectedWorker,
                "Vəsait siyahısı");

            RefreshDataAndSelection(assetVm.Asset.Id);
            NotificationService.Success(
                this,
                $"Vəsait {selectWindow.SelectedWorker.per_adiper_soyadi} adlı işçiyə təhkim edildi.");
        }

        private void PrintAssetBarcodeContext_Click(object sender, RoutedEventArgs e)
        {
            var asset = GetAssetFromContext(sender)?.Asset;
            if (asset == null)
                return;

            if (string.IsNullOrWhiteSpace(asset.VesaitinKodu))
            {
                NotificationService.Info(
                    this,
                    "Barkod çap etmək üçün vəsait kodu olmalıdır.",
                    title: "Barkod");
                return;
            }

            var printWindow = new PrintQrCodesWindow(new List<Asset> { asset })
            {
                Owner = this
            };
            printWindow.ShowDialog();
        }

        private void AssetsDataGrid_SelectionChanged(object _, SelectionChangedEventArgs e)
        {
            if (_suppressSelectionChange)
                return;

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

        private void ClearFiltersButton_Click(object _, RoutedEventArgs e)
            => _viewModel.ClearFilters();

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
                AssetsDataGrid.SelectedItems.Clear();
                _selectedAssetVM = null;
                CloseDetailPanel();
                e.Handled = true;
            }
        }


        private void UpdateDetailView() => AssetDetailControl.DisplayAsset(_selectedAssetVM?.Asset, _viewModel.Workers.ToList());

        public void NavigateToAsset(int assetId)
        {
            if (assetId <= 0) return;

            _assetIdToSelectOnLoad = assetId;

            if (!IsLoaded)
                return;

            SelectAssetById(assetId);
        }

        public void SelectAssetById(int assetId)
        {
            var visibleAsset = _viewModel.VisibleAssets
                .FirstOrDefault(vm => vm.Asset.Id == assetId);

            if (visibleAsset != null)
            {
                AssetsDataGrid.SelectedItem = visibleAsset;
                AssetsDataGrid.ScrollIntoView(visibleAsset);

                _selectedAssetVM = visibleAsset;
                UpdateDetailView();

                if (!isDetailPanelOpen)
                    OpenDetailPanel();

                return;
            }

            var targetAsset = _viewModel.AllAssets
                .FirstOrDefault(vm => vm.Asset.Id == assetId);

            if (targetAsset == null)
            {
                var cachedAsset = AppData.GetAssets()
                    .FirstOrDefault(asset => asset.Id == assetId);

                if (cachedAsset != null)
                    targetAsset = new AssetCheckableViewModel(cachedAsset);
            }

            if (targetAsset == null)
                return;

            // Asset cari filter nəticəsində görünmürsə filter state-ni pozmuruq.
            // Sadəcə detail paneldə həmin asset-i göstəririk.
            _selectionTimer.Stop();
            _suppressSelectionChange = true;
            try
            {
                AssetsDataGrid.SelectedItem = null;
            }
            finally
            {
                _suppressSelectionChange = false;
            }

            _selectedAssetVM = targetAsset;
            UpdateDetailView();

            if (!isDetailPanelOpen)
                OpenDetailPanel();
        }

        #endregion

        #region Menu Handlers
        private void OpenDetailPanel()
        {
            double targetWidth = GetResponsiveDetailPanelWidth();

            var animation = new DoubleAnimation(
                DetailPanelContainer.ActualWidth,
                targetWidth,
                TimeSpan.FromMilliseconds(250))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            DetailPanelContainer.BeginAnimation(WidthProperty, animation);
            isDetailPanelOpen = true;
        }

        private double GetResponsiveDetailPanelWidth()
        {
            double available = Math.Max(ActualWidth, 900);
            return Math.Clamp(available * 0.36, 380, 540);
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
                                    NotificationService.Error(
                                        this,
                                        $"Kopyalana bilmədi: {ex.Message}");
                                }
                            }
                        }
                    }
                }
            }
        }
        private void MenuButton_Click(object _, RoutedEventArgs e)
            => SharedNavigationMenu.Open();

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