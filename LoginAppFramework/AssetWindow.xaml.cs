using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
namespace LoginAppFramework
{
    public partial class AssetWindow : Window, INavigationRefreshable
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

        public Task RefreshForNavigationAsync()
        {
            int? selectedAssetId = _selectedAssetVM?.Asset.Id;

            _viewModel.RefreshPreservingFilters();
            InitializeColumnFilters();

            if (selectedAssetId.HasValue)
                SelectAssetById(selectedAssetId.Value);
            else
                UpdateDetailView();

            ApplyRoleBasedPermissions();
            return Task.CompletedTask;
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

            var dryRun = AssetImportPreviewBuilder.Build(
                batch,
                AppData.GetAssetsIncludingDeleted(),
                confirmedMappings);

            var previewWindow = new AssetImportPreviewWindow(dryRun)
            {
                Owner = this
            };

            if (previewWindow.ShowDialog() != true)
                return;

            var assetsToImport = previewWindow.AssetsToImport;

            foreach (var asset in assetsToImport)
            {
                if (batch.RequestedWorkerNames.TryGetValue(
                        asset,
                        out string workerName) &&
                    confirmedMappings.TryGetValue(
                        workerName,
                        out Worker worker))
                {
                    asset.AssignWorker(worker);
                }
                else
                {
                    asset.ClearWorkerAssignment();
                }
            }

            ImportButton.IsEnabled = false;
            OperationProgressOverlay.Show(
                "Vəsaitlər import edilir...",
                $"{assetsToImport.Count} vəsait verilənlər bazasına yazılır.");

            OperationProgressOverlay.Report(
                new OperationProgressInfo(
                    "Vəsaitlər import edilir...",
                    0,
                    assetsToImport.Count,
                    "Verilənlər bazasına yazılır."));

            try
            {
                await Task.Run(
                    () => _viewModel.ImportAssets(assetsToImport));

                OperationProgressOverlay.Report(
                    new OperationProgressInfo(
                        "Import tamamlandı",
                        assetsToImport.Count,
                        assetsToImport.Count,
                        $"{assetsToImport.Count} vəsait yadda saxlanıldı."));

                NotificationService.Success(
                    this,
                    $"{assetsToImport.Count} vəsait import edildi.");
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
                "Silinənlərə Göndər",
                $"İşarələnmiş {checkedAssets.Count} vəsait Silinənlər bölməsinə göndəriləcək və 30 gün ərzində bərpa edilə biləcək.",
                "Silinənlərə göndər",
                "Ləğv et");

            if (!result)
                return;

            _viewModel.DeleteCheckedAssets();
            RefreshDataAndSelection();

            NotificationService.SuccessWithAction(
                this,
                $"{checkedAssets.Count} vəsait Silinənlər bölməsinə göndərildi.",
                "Geri qaytar",
                () =>
                {
                    AppServices.Assets.RestoreMany(checkedAssets);
                    RefreshDataAndSelection();

                    NotificationService.Success(
                        this,
                        $"{checkedAssets.Count} vəsait bərpa edildi.");
                },
                title: "Silinənlər");
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

            if (editWindow.ShowDialog() != true)
                return;

            var preview =
                BulkActionPreviewBuilder.ForEdit(
                    checkedAssets,
                    editWindow.Changes);

            if (preview.Changes.Count == 0)
            {
                NotificationService.Info(
                    this,
                    "Dəyişiklik üçün yeni dəyər seçilməyib.",
                    title: "Toplu Redaktə");
                return;
            }

            var previewWindow = new BulkActionPreviewWindow(preview)
            {
                Owner = this
            };

            if (previewWindow.ShowDialog() != true)
                return;

            var snapshots = checkedAssets
                .Select(asset => asset.Clone())
                .ToList();

            try
            {
                var result = _viewModel.ApplyBulkChanges(editWindow.Changes);

                RefreshDataAndSelection();

                if (result.UpdatedCount > 0)
                {
                    NotificationService.Undo(
                        this,
                        $"{result.UpdatedCount} vəsait dəyişdirildi.",
                        () => RestoreAssetSnapshots(
                            snapshots,
                            "Toplu dəyişiklik geri qaytarıldı."));
                }

                if (result.SkippedStatusAssets.Count > 0)
                {
                    string skippedNames = string.Join(
                        "\n",
                        result.SkippedStatusAssets
                            .Take(15)
                            .Select(a => $"- {a.VesaitinAdi}"));

                    DialogService.Warning(
                        this,
                        "Status Dəyişikliyi Ötürüldü",
                        $"{result.SkippedStatusAssets.Count} vəsaitin statusu dəyişdirilmədi, çünki onlar işçiyə təhkim olunub və 'İstifadədədir' statusunda qalmalıdırlar:\n\n{skippedNames}");
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

            var selectWindow =
                new SelectWorkerWindow(_viewModel.Workers.ToList())
                {
                    Owner = this
                };

            if (selectWindow.ShowDialog() != true ||
                selectWindow.SelectedWorker == null)
            {
                return;
            }

            Worker newWorker = selectWindow.SelectedWorker;

            var preview = BulkActionPreviewBuilder.ForAssignment(
                checkedAssets,
                newWorker);

            var previewWindow = new BulkActionPreviewWindow(preview)
            {
                Owner = this
            };

            if (previewWindow.ShowDialog() != true)
                return;

            var snapshots = checkedAssets
                .Select(asset => asset.Clone())
                .ToList();

            try
            {
                _viewModel.AssignCheckedAssets(newWorker);
                RefreshDataAndSelection();

                NotificationService.Undo(
                    this,
                    $"{checkedAssets.Count} vəsait {newWorker.per_adiper_soyadi} adlı işçiyə təhkim edildi.",
                    () => RestoreAssetSnapshots(
                        snapshots,
                        "Toplu təhkimat geri qaytarıldı."));
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Xəta",
                    $"Toplu təhkim zamanı xəta baş verdi:\n\n{ex.Message}");
            }
        }
        private void RestoreAssetSnapshots(
            IReadOnlyList<Asset> snapshots,
            string successMessage)
        {
            if (snapshots == null || snapshots.Count == 0)
                return;

            foreach (var snapshot in snapshots)
                AppServices.Assets.Save(snapshot);

            RefreshDataAndSelection();

            NotificationService.Success(
                this,
                successMessage);
        }

        #endregion

        #region Filtering and Data Logic

        private const string AssetDetailWidthPreferenceKey = "assets-detail-panel-width";
        private const double AssetDetailMinimumWidth = 340;
        private const double AssetListMinimumWidth = 360;

        private bool isDetailPanelOpen = false;
        private double? _preferredDetailPanelWidth;

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
            var asset = assetVMToDelete.Asset;

            bool confirm = DialogService.Confirm(
                this,
                "Silinənlərə Göndər",
                $"'{asset.VesaitinAdi}' adlı vəsait Silinənlər bölməsinə göndəriləcək. 30 gün ərzində onu bərpa edə bilərsiniz.",
                "Silinənlərə göndər",
                "Ləğv et");

            if (!confirm)
                return;

            _viewModel.DeleteAsset(asset);
            _selectedAssetVM = null;
            RefreshDataAndSelection();

            NotificationService.SuccessWithAction(
                this,
                $"'{asset.VesaitinAdi}' Silinənlər bölməsinə göndərildi.",
                "Geri qaytar",
                () =>
                {
                    AppServices.Assets.Restore(asset);
                    RefreshDataAndSelection(asset.Id);

                    NotificationService.Success(
                        this,
                        "Vəsait bərpa edildi.");
                },
                title: "Silinənlər");
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

        private void AssetsDataGrid_LoadingRow(
            object sender,
            DataGridRowEventArgs e)
        {
            var menu = new ContextMenu
            {
                DataContext = e.Row.Item
            };

            menu.Items.Add(CreateAssetContextMenuItem(
                "Detalları aç",
                OpenAssetDetailsContext_Click,
                e.Row.Item));

            menu.Items.Add(CreateAssetContextMenuItem(
                "Redaktə et",
                EditAssetContext_Click,
                e.Row.Item));

            menu.Items.Add(new Separator());

            menu.Items.Add(CreateAssetContextMenuItem(
                "Vəsait kodunu kopyala",
                CopyAssetCodeContext_Click,
                e.Row.Item));

            menu.Items.Add(CreateAssetContextMenuItem(
                "Seriya nömrəsini kopyala",
                CopyAssetSerialContext_Click,
                e.Row.Item));

            menu.Items.Add(new Separator());

            menu.Items.Add(CreateAssetContextMenuItem(
                "İşçiyə təhkim et...",
                AssignAssetContext_Click,
                e.Row.Item));

            menu.Items.Add(CreateAssetContextMenuItem(
                "Barkod çap et",
                PrintAssetBarcodeContext_Click,
                e.Row.Item));

            e.Row.ContextMenu = menu;
        }

        private static MenuItem CreateAssetContextMenuItem(
            string header,
            RoutedEventHandler handler,
            object dataContext)
        {
            var item = new MenuItem
            {
                Header = header,
                DataContext = dataContext
            };

            item.Click += handler;
            return item;
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

            var snapshot = assetVm.Asset.Clone();
            int assetId = assetVm.Asset.Id;

            AppServices.Assets.Assign(
                assetVm.Asset,
                selectWindow.SelectedWorker,
                "Vəsait siyahısı");

            RefreshDataAndSelection(assetId);

            NotificationService.Undo(
                this,
                $"Vəsait {selectWindow.SelectedWorker.per_adiper_soyadi} adlı işçiyə təhkim edildi.",
                () =>
                {
                    AppServices.Assets.Save(snapshot);
                    RefreshDataAndSelection(assetId);
                    NotificationService.Success(
                        this,
                        "Təhkimat geri qaytarıldı.");
                });
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
            bool control =
                (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (control && e.Key == Key.F)
            {
                SetFilterPanelVisibility(true);
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (control && e.Key == Key.K)
            {
                NavigationManager.GoToGlobalSearch(this);
                e.Handled = true;
                return;
            }

            if (control && e.Key == Key.N)
            {
                AddAssetButton_Click(this, new RoutedEventArgs());
                e.Handled = true;
                return;
            }

            if (Keyboard.FocusedElement is TextBoxBase)
                return;

            var selected =
                AssetsDataGrid.SelectedItem as AssetCheckableViewModel;

            if (control && e.Key == Key.E && selected != null)
            {
                EditSelectedAsset(selected);
                e.Handled = true;
                return;
            }

            if (control && e.Key == Key.P && selected != null)
            {
                PrintSelectedAsset(selected);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Space && selected != null)
            {
                selected.IsChecked = !selected.IsChecked;
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter && selected != null)
            {
                ShowAssetDetails(selected);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Delete && selected != null)
            {
                DeleteSelectedAsset(selected);
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

        private void EditSelectedAsset(
            AssetCheckableViewModel selected)
        {
            if (selected == null)
                return;

            if (!SessionManager.CanEdit())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            DetailControl_EditAsset(this, selected);
        }

        private void PrintSelectedAsset(
            AssetCheckableViewModel selected)
        {
            if (selected?.Asset == null)
                return;

            if (string.IsNullOrWhiteSpace(
                    selected.Asset.VesaitinKodu))
            {
                NotificationService.Warning(
                    this,
                    "Barkod çapı üçün vəsait kodu tələb olunur.",
                    title: "Çap");
                return;
            }

            var window = new PrintQrCodesWindow(
                new List<Asset>
                {
                    selected.Asset
                })
            {
                Owner = this
            };

            window.ShowDialog();
        }

        private void DeleteSelectedAsset(
            AssetCheckableViewModel selected)
        {
            if (selected == null)
                return;

            if (!SessionManager.CanDelete())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            DetailControl_DeleteAsset(
                this,
                selected);
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
            double targetWidth = GetPreferredDetailPanelWidth();

            DetailPanelSplitter.Visibility = Visibility.Visible;

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

        private double GetPreferredDetailPanelWidth()
        {
            _preferredDetailPanelWidth ??=
                UiPreferenceStore.LoadScalar(AssetDetailWidthPreferenceKey);

            double fallback = GetResponsiveDetailPanelWidth();
            double preferred = _preferredDetailPanelWidth ?? fallback;

            return ClampDetailPanelWidth(preferred);
        }

        private double GetResponsiveDetailPanelWidth()
        {
            double available =
                (DetailPanelContainer.Parent as FrameworkElement)?.ActualWidth
                ?? Math.Max(ActualWidth, 900);

            return Math.Clamp(
                available * 0.38,
                AssetDetailMinimumWidth,
                Math.Max(AssetDetailMinimumWidth, Math.Min(560, available - AssetListMinimumWidth)));
        }

        private double ClampDetailPanelWidth(double width)
        {
            double available =
                (DetailPanelContainer.Parent as FrameworkElement)?.ActualWidth
                ?? ActualWidth;

            double maximum = Math.Max(
                AssetDetailMinimumWidth,
                Math.Min(720, available - AssetListMinimumWidth - 7));

            return Math.Clamp(
                width,
                AssetDetailMinimumWidth,
                maximum);
        }

        private void DetailPanelSplitter_DragDelta(
            object sender,
            DragDeltaEventArgs e)
        {
            if (!isDetailPanelOpen)
                return;

            DetailPanelContainer.BeginAnimation(WidthProperty, null);

            double current =
                double.IsNaN(DetailPanelContainer.Width)
                    ? DetailPanelContainer.ActualWidth
                    : DetailPanelContainer.Width;

            double next = ClampDetailPanelWidth(
                current - e.HorizontalChange);

            DetailPanelContainer.Width = next;
            _preferredDetailPanelWidth = next;
            e.Handled = true;
        }

        private void DetailPanelSplitter_DragCompleted(
            object sender,
            DragCompletedEventArgs e)
        {
            if (!_preferredDetailPanelWidth.HasValue)
                return;

            UiPreferenceStore.SaveScalar(
                AssetDetailWidthPreferenceKey,
                _preferredDetailPanelWidth.Value);
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!isDetailPanelOpen)
                return;

            DetailPanelContainer.BeginAnimation(WidthProperty, null);
            DetailPanelContainer.Width = GetPreferredDetailPanelWidth();
        }

        private void CloseDetailPanel()
        {
            DetailPanelContainer.BeginAnimation(WidthProperty, null);

            var animation = new DoubleAnimation(
                DetailPanelContainer.ActualWidth,
                0,
                TimeSpan.FromMilliseconds(200))
            {
                EasingFunction = new CubicEase
                {
                    EasingMode = EasingMode.EaseIn
                }
            };

            animation.Completed += (_, _) =>
            {
                DetailPanelContainer.BeginAnimation(WidthProperty, null);
                DetailPanelContainer.Width = 0;
                DetailPanelSplitter.Visibility = Visibility.Collapsed;
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