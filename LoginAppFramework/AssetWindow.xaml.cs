using ClosedXML.Excel;
using LoginAppFramework; // Artıq bu using əlavə olunub
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
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
        // 1. _allAssets siyahısının tipini dəyişdirin
        private List<AssetCheckableViewModel> _allCheckableAssets; // Keçmiş _allAssets
        private List<Worker> _allWorkers;
        // 2. _selectedAsset-in tipini dəyişdirin
        private AssetCheckableViewModel _selectedAssetVM; // Keçmiş _selectedAsset
        private bool isMenuOpen = false;
        private readonly int? _assetIdToSelectOnLoad;
        private AssetFilterViewModel _filterViewModel;
        private readonly DispatcherTimer _selectionTimer;

        // Constructor dəyişməz qalır...
        public AssetWindow()
        {
            InitializeComponent();
            _assetIdToSelectOnLoad = null;
            _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _selectionTimer.Tick += SelectionTimer_Tick;
        }

        public AssetWindow(int assetIdToSelect = -1)
        {
            InitializeComponent();
            _assetIdToSelectOnLoad = assetIdToSelect > 0 ? assetIdToSelect : null;
            _selectionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _selectionTimer.Tick += SelectionTimer_Tick;
        }

        // Window_Loaded metodunu yeniləyin
        private async void Window_Loaded(object _, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(20);

            // 3. Məlumatları yeni ViewModel-ə çevirərək yükləyin
            await Task.Run(() =>
            {
                var allAssets = AppData.GetAssets();
                _allCheckableAssets = allAssets.Select(a => new AssetCheckableViewModel(a)).ToList();
                _allWorkers = AppData.GetWorkers();
            });

            _filterViewModel = new AssetFilterViewModel();
            _filterViewModel.FilterChanged += ApplyFilters;
            FilterPanel.DataContext = _filterViewModel;

            // Initialize column filters
            InitializeColumnFilters();

            ApplyFilters();
            if (_assetIdToSelectOnLoad.HasValue)
            {
                SelectAssetById(_assetIdToSelectOnLoad.Value);
            }
            else
            {
                UpdateDetailView();
            }
            UpdateUserDisplay();
            // 4. DetailControl hadisələrinin parametrlərini düzəldin
            AssetDetailControl.OnAssetModified += (s, asset) => DetailControl_EditAsset(s, new AssetCheckableViewModel(asset));
            AssetDetailControl.OnAssetDeleted += (s, asset) => DetailControl_DeleteAsset(s, new AssetCheckableViewModel(asset));
            AssetDetailControl.OnAssignmentChanged += DetailControl_AssignmentChanged;
            LoadingOverlay.Visibility = Visibility.Collapsed;
            AssetDetailControl.OnDetailPanelClosed += DetailControl_PanelClosed;

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
            var checkedAssets = _allCheckableAssets
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

        // ExportButton_Click metodunu yeniləyin
        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            var assetsToExport = (AssetsDataGrid.ItemsSource as IEnumerable<AssetCheckableViewModel>)?.Select(vm => vm.Asset);
            // ... metodun qalanı eyni qalır
            if (assetsToExport == null || !assetsToExport.Any())
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

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    using (var workbook = new XLWorkbook())
                    {
                        // 1. Əsas "Vəsaitlər" Səhifəsi (Siyahı)
                        var worksheet = workbook.Worksheets.Add("Vəsaitlər");

                        var headers = new string[]
                        {
                        "Vəsaitin Kodu", "Vəsaitin Adı", "IT Seriya No", "Kateqoriya",
                        "Təhkim Olunan Əməkdaş", "Vəzifəsi", "Bölmə/Şöbə/Departament",
                        "Yerləşmə Yeri", "Ərazi", "Status", "Alış Qiyməti", "Alınma Tarixi",
                        "İstifadə müddəti (İl)", "Aylıq Amortizasiya"
                        };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            worksheet.Cell(1, i + 1).Value = headers[i];
                        }
                        worksheet.Row(1).Style.Font.Bold = true;

                        int currentRow = 2;
                        foreach (var asset in assetsToExport)
                        {
                            worksheet.Cell(currentRow, 1).Value = asset.VesaitinKodu;
                            worksheet.Cell(currentRow, 2).Value = asset.VesaitinAdi;
                            worksheet.Cell(currentRow, 3).Value = asset.ITAvadanliqlarininSeriyaNomresi;
                            worksheet.Cell(currentRow, 4).Value = asset.Kateqoriya;
                            worksheet.Cell(currentRow, 5).Value = asset.Worker?.per_adiper_soyadi;
                            worksheet.Cell(currentRow, 6).Value = asset.Worker?.pgk_gorev_adi;
                            worksheet.Cell(currentRow, 7).Value = asset.Worker?.pdp_adi;
                            worksheet.Cell(currentRow, 8).Value = asset.YerleshmeYeri;
                            worksheet.Cell(currentRow, 9).Value = asset.Erazi;
                            worksheet.Cell(currentRow, 10).Value = asset.Status;
                            worksheet.Cell(currentRow, 11).Value = asset.PurchaseCost;
                            worksheet.Cell(currentRow, 12).Value = asset.PurchaseDate > DateTime.MinValue ? asset.PurchaseDate.ToString("yyyy-MM-dd") : "";
                            worksheet.Cell(currentRow, 13).Value = asset.UsefulLifeInYears;
                            worksheet.Cell(currentRow, 14).Value = asset.MonthlyDepreciation;
                            currentRow++;
                        }

                        worksheet.Columns().AdjustToContents();

                        // 2. İkinci "Vəsait Tarixçəsi" Səhifəsi (History)
                        var historySheet = workbook.Worksheets.Add("Vəsait Tarixçəsi");
                        var historyHeaders = new string[]
                        {
                            "Vəsaitin Kodu", "Vəsaitin Adı", "IT Seriya No", "Əməliyyat",
                            "Kimdən Alındı", "Kimə Verildi", "Dəyişikliyi Edən", "Dəyişiklik Tarixi"
                        };
                        for (int i = 0; i < historyHeaders.Length; i++)
                        {
                            historySheet.Cell(1, i + 1).Value = historyHeaders[i];
                        }
                        historySheet.Row(1).Style.Font.Bold = true;

                        int historyRow = 2;
                        foreach (var asset in assetsToExport)
                        {
                            if (asset.History != null && asset.History.Any())
                            {
                                foreach (var historyEntry in asset.History.OrderBy(h => h.ChangeDate))
                                {
                                    historySheet.Cell(historyRow, 1).Value = asset.VesaitinKodu;
                                    historySheet.Cell(historyRow, 2).Value = asset.VesaitinAdi;
                                    historySheet.Cell(historyRow, 3).Value = asset.ITAvadanliqlarininSeriyaNomresi;
                                    
                                    string actionText = historyEntry.Action switch
                                    {
                                        AssignmentAction.Assigned => "Təhkim edilib",
                                        AssignmentAction.Reassigned => "Yenidən Təhkim edilib",
                                        AssignmentAction.Unassigned => "Geri Alınıb",
                                        _ => historyEntry.Action.ToString()
                                    };
                                    historySheet.Cell(historyRow, 4).Value = actionText;
                                    
                                    historySheet.Cell(historyRow, 5).Value = historyEntry.FromWorkerName ?? "-";
                                    historySheet.Cell(historyRow, 6).Value = historyEntry.ToWorkerName ?? "-";
                                    historySheet.Cell(historyRow, 7).Value = historyEntry.ChangedBy ?? "-";
                                    historySheet.Cell(historyRow, 8).Value = historyEntry.ChangeDate.ToString("yyyy-MM-dd HH:mm");
                                    
                                    historyRow++;
                                }
                            }
                        }

                        historySheet.Columns().AdjustToContents();

                        // Yekun saxlanma
                        workbook.SaveAs(saveFileDialog.FileName);
                    }

                    MessageBox.Show("Məlumatlar uğurla Excel faylına export edildi.", "Export Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export zamanı xəta baş verdi: {ex.Message}", "Xəta", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ImportButton_Click və ProcessExcelFile dəyişməz qalır
        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var openFileDialog = new OpenFileDialog { Title = "Import üçün Excel faylı seçin", Filter = "Excel Files (*.xlsx)|*.xlsx" };
            if (openFileDialog.ShowDialog() != true) return;

            var assetsToImport = new List<Asset>();
            var errorLog = new List<string>();
            var excelUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                ProcessExcelFile(openFileDialog.FileName, assetsToImport, errorLog, excelUserNames);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel faylı oxunarkən xəta baş verdi:\n\n{ex.Message}", "Import Xətası", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            var dbWorkers = AppData.GetWorkers();
            var confirmedMappings = new Dictionary<string, Worker>(StringComparer.OrdinalIgnoreCase);
            var mappingViewModels = excelUserNames
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Select(name => new ImportMappingViewModel(name, dbWorkers))
                .ToList();

            var namesThatNeedManualMapping = mappingViewModels
                .Where(vm => vm.IsUnmapped)
                .Select(vm => vm.ExcelUserName)
                .ToList();

            foreach (var vm in mappingViewModels.Where(vm => !vm.IsUnmapped))
            {
                confirmedMappings[vm.ExcelUserName] = vm.SelectedDbWorker;
            }

            if (namesThatNeedManualMapping.Any())
            {
                var mappingWindow = new ImportMappingWindow(namesThatNeedManualMapping, dbWorkers) { Owner = this };
                if (mappingWindow.ShowDialog() == true)
                {
                    foreach (var mapping in mappingWindow.ConfirmedMappings)
                    {
                        if (mapping.Value != null && mapping.Value.Id > 0)
                        {
                            confirmedMappings[mapping.Key] = mapping.Value;
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Import ləğv edildi.");
                    return;
                }
            }

            foreach (var asset in assetsToImport)
            {
                if (!string.IsNullOrWhiteSpace(asset.TehkimOlunanEmekdas) && confirmedMappings.TryGetValue(asset.TehkimOlunanEmekdas, out Worker mappedWorker))
                {
                    asset.WorkerId = mappedWorker.Id;
                    asset.TehkimOlunanEmekdas = mappedWorker.per_adiper_soyadi;
                    asset.Vezifesi = mappedWorker.pgk_gorev_adi;
                    asset.BolmeShobeDepartment = mappedWorker.pdp_adi;
                    asset.Status = "İstifadədədir";
                }
                else
                {
                    asset.WorkerId = null;
                    asset.TehkimOlunanEmekdas = null;
                    asset.Vezifesi = null;
                }
            }

            if (!assetsToImport.Any())
            {
                MessageBox.Show("İmport üçün etibarlı vəsait tapılmadı.", "Import Başa Çatdı", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var summary = new StringBuilder();
            summary.AppendLine($"{assetsToImport.Count} yeni vəsait importa hazırdır.");
            if (errorLog.Any())
            {
                summary.AppendLine($"\nXətalara görə {errorLog.Count} sətir ötürüldü.");
            }
            summary.AppendLine("\nBu vəsaitləri verilənlər bazasında yadda saxlamaq istəyirsiniz?");
            var confirmResult = MessageBox.Show(summary.ToString(), "İmportu Təsdiq Et", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            try
            {
                using (var context = new InventoryDbContext(SessionManager.CurrentUserConnectionString))
                using (var transaction = context.Database.BeginTransaction())
                {
                    foreach (var asset in assetsToImport)
                    {
                        DataAccess.SaveAssetInTransaction(context, asset);
                    }
                    context.SaveChanges();
                    transaction.Commit();
                }
                MessageBox.Show($"{assetsToImport.Count} vəsait uğurla import edildi.", "Import Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
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
        private void ProcessExcelFile(string filePath, List<Asset> assetsToImport, List<string> errorLog, HashSet<string> excelUserNames)
        {
            using (var workbook = new XLWorkbook(filePath))
            {
                var worksheet = workbook.Worksheets.FirstOrDefault();
                if (worksheet == null || worksheet.FirstRowUsed() == null)
                {
                    throw new Exception("Excel faylı boşdur və ya başlıq sətri yoxdur.");
                }

                var headerRow = worksheet.FirstRowUsed();
                var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var cell in headerRow.Cells())
                {
                    columnMap[cell.GetString().Trim()] = cell.Address.ColumnNumber;
                }

                int adiCol;
                if (columnMap.ContainsKey("Vəsaitin Adı")) { adiCol = columnMap["Vəsaitin Adı"]; }
                else if (columnMap.ContainsKey("Vəsait adı")) { adiCol = columnMap["Vəsait adı"]; }
                else { throw new Exception("Excel faylında tələb olunan 'Vəsaitin Adı' və ya 'Vəsait adı' sütunu tapılmadı."); }

                var dataRows = worksheet.RowsUsed().Skip(1);
                foreach (var row in dataRows)
                {
                    string assetName = row.Cell(adiCol).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(assetName)) continue;

                    var asset = new Asset
                    {
                        VesaitinAdi = assetName,
                        Status = "Anbarda",
                        PurchaseDate = DateTime.Today,
                        UsefulLifeInYears = 0
                    };

                    if (columnMap.TryGetValue("Vəsaitin Kodu", out int koduCol)) asset.VesaitinKodu = row.Cell(koduCol).GetString().Trim();
                    if (columnMap.TryGetValue("IT Seriya No", out int seriyaCol) ||
                        columnMap.TryGetValue("ITAvadanliqlarininSeriyaNomresi", out seriyaCol) ||
                        columnMap.TryGetValue("İT avadanlıqlarının seriya №-i", out seriyaCol))
                    {
                        asset.ITAvadanliqlarininSeriyaNomresi = row.Cell(seriyaCol).GetString().Trim();
                    }
                    if (columnMap.TryGetValue("Kateqoriya", out int katCol)) asset.Kateqoriya = row.Cell(katCol).GetString().Trim();
                    if (columnMap.TryGetValue("Bölmə/Şöbə/Departament", out int deptCol)) asset.BolmeShobeDepartment = row.Cell(deptCol).GetString().Trim();
                    if (columnMap.TryGetValue("Yerləşmə Yeri", out int yerlesmeCol)) asset.YerleshmeYeri = row.Cell(yerlesmeCol).GetString().Trim();
                    if (columnMap.TryGetValue("Ərazi", out int eraziCol)) asset.Erazi = row.Cell(eraziCol).GetString().Trim();

                    if (columnMap.TryGetValue("Alış qiyməti", out int costCol) && row.Cell(costCol).TryGetValue(out decimal cost))
                    {
                        asset.PurchaseCost = cost;
                    }

                    if ((columnMap.TryGetValue("Alış tarixi", out int dateCol) ||
                         columnMap.TryGetValue("Alınma tarixi", out dateCol) ||
                         columnMap.TryGetValue("Alış vaxtı", out dateCol))
                        && row.Cell(dateCol).TryGetValue(out DateTime date) && date > DateTime.MinValue)
                    {
                        asset.PurchaseDate = date;
                    }

                    if ((columnMap.TryGetValue("Faydalı ömür", out int lifeCol) ||
                         columnMap.TryGetValue("Faydalı ömrü", out lifeCol) ||
                         columnMap.TryGetValue("İstifadə müddəti (İl)", out lifeCol))
                        && row.Cell(lifeCol).TryGetValue(out int usefulLife))
                    {
                        asset.UsefulLifeInYears = usefulLife;
                    }

                    if (columnMap.TryGetValue("Təhkim Olunan Əməkdaş", out int userCol))
                    {
                        string assignedUserName = row.Cell(userCol).GetString().Trim();
                        if (!string.IsNullOrEmpty(assignedUserName))
                        {
                            asset.TehkimOlunanEmekdas = assignedUserName;
                            excelUserNames.Add(assignedUserName);
                        }
                    }

                    if (asset.UsefulLifeInYears <= 0)
                    {
                        asset.UsefulLifeInYears = 3;
                    }

                    assetsToImport.Add(asset);
                }
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

            var checkedAssets = _allCheckableAssets.Where(vm => vm.IsChecked).Select(vm => vm.Asset).ToList();
            if (!checkedAssets.Any()) return;

            var result = MessageBox.Show($"İşarələnmiş {checkedAssets.Count} vəsaiti həmişəlik silməyə əminsinizmi?", "Toplu Silməni Təsdiq Et", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                DataAccess.BulkDeleteAssets(checkedAssets);
                RefreshDataAndSelection();
            }
        }

        private void BulkEditButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var checkedAssets = _allCheckableAssets.Where(vm => vm.IsChecked).Select(vm => vm.Asset).ToList();
            if (!checkedAssets.Any()) return;

            var editWindow = new BulkEditWindow(checkedAssets.Count) { Owner = this };
            if (editWindow.ShowDialog() == true)
            {
                var changesToApply = editWindow.Changes;
                var assetsToUpdateInDb = new List<Asset>();
                var skippedStatusAssets = new List<Asset>();
                var skippedWorkerChangeAssets = new List<Asset>();

                foreach (var asset in checkedAssets)
                {
                    bool assetWasChanged = false;

                    if (changesToApply.VesaitinKodu != null) { asset.VesaitinKodu = changesToApply.VesaitinKodu; assetWasChanged = true; }
                    if (changesToApply.VesaitinAdi != null) { asset.VesaitinAdi = changesToApply.VesaitinAdi; assetWasChanged = true; }
                    if (changesToApply.ITAvadanliqlarininSeriyaNomresi != null) { asset.ITAvadanliqlarininSeriyaNomresi = changesToApply.ITAvadanliqlarininSeriyaNomresi; assetWasChanged = true; }
                    if (changesToApply.Kateqoriya != null) { asset.Kateqoriya = changesToApply.Kateqoriya; assetWasChanged = true; }
                    if (changesToApply.BolmeShobeDepartment != null) { asset.BolmeShobeDepartment = changesToApply.BolmeShobeDepartment; assetWasChanged = true; }
                    if (changesToApply.YerleshmeYeri != null) { asset.YerleshmeYeri = changesToApply.YerleshmeYeri; assetWasChanged = true; }
                    if (changesToApply.Erazi != null) { asset.Erazi = changesToApply.Erazi; assetWasChanged = true; }
                    if (changesToApply.PurchaseCost.HasValue) { asset.PurchaseCost = changesToApply.PurchaseCost.Value; assetWasChanged = true; }
                    if (changesToApply.PurchaseDate.HasValue) { asset.PurchaseDate = changesToApply.PurchaseDate.Value; assetWasChanged = true; }
                    if (changesToApply.UsefulLifeInYears.HasValue) { asset.UsefulLifeInYears = changesToApply.UsefulLifeInYears.Value; assetWasChanged = true; }
                    if (changesToApply.Supplier != null) { asset.Supplier = changesToApply.Supplier; assetWasChanged = true; }
                    if (changesToApply.WarrantyExpirationDate.HasValue) { asset.WarrantyExpirationDate = changesToApply.WarrantyExpirationDate.Value; assetWasChanged = true; }

                    // Handle worker assignment changes
                    if (changesToApply.AssignedWorker != null)
                    {
                        if (changesToApply.AssignedWorker.Id == 0)
                        {
                            // Unassign worker (set to empty)
                            string oldWorkerName = asset.TehkimOlunanEmekdas;
                            asset.WorkerId = null;
                            asset.TehkimOlunanEmekdas = null;
                            asset.Vezifesi = null;
                            asset.BolmeShobeDepartment = null;
                            asset.Status = "Anbarda";

                            // Add unassignment history entry if there was a worker
                            if (!string.IsNullOrEmpty(oldWorkerName))
                            {
                                if (asset.History == null) asset.History = new List<AssignmentHistoryEntry>();
                                asset.History.Add(new AssignmentHistoryEntry
                                {
                                    Action = AssignmentAction.Unassigned,
                                    FromWorkerName = oldWorkerName,
                                    ToWorkerName = "Sistem (Toplu Redaktə)",
                                    ChangedBy = SessionManager.CurrentUser.FullName,
                                    ChangeDate = DateTime.Now
                                });
                            }
                            assetWasChanged = true;
                        }
                        else
                        {
                            // Assign or reassign to new worker
                            var newWorker = changesToApply.AssignedWorker;
                            string oldWorkerName = asset.TehkimOlunanEmekdas;
                            bool isReassignment = asset.WorkerId.HasValue;

                            asset.WorkerId = newWorker.Id;
                            asset.TehkimOlunanEmekdas = newWorker.per_adiper_soyadi;
                            asset.Vezifesi = newWorker.pgk_gorev_adi;
                            asset.BolmeShobeDepartment = newWorker.pdp_adi;
                            asset.Status = "İstifadədədir";

                            // Add assignment/reassignment history entry
                            if (asset.History == null) asset.History = new List<AssignmentHistoryEntry>();
                            asset.History.Add(new AssignmentHistoryEntry
                            {
                                Action = isReassignment ? AssignmentAction.Reassigned : AssignmentAction.Assigned,
                                FromWorkerName = isReassignment ? oldWorkerName : "Sistem (Toplu Redaktə)",
                                ToWorkerName = newWorker.per_adiper_soyadi,
                                ChangedBy = SessionManager.CurrentUser.FullName,
                                ChangeDate = DateTime.Now
                            });
                            assetWasChanged = true;
                        }
                    }

                    if (changesToApply.Status != null)
                    {
                        if (asset.WorkerId.HasValue) { skippedStatusAssets.Add(asset); }
                        else { asset.Status = changesToApply.Status; assetWasChanged = true; }
                    }

                    if (assetWasChanged) { assetsToUpdateInDb.Add(asset); }
                }

                if (assetsToUpdateInDb.Any())
                {
                    try
                    {
                        AppData.BulkSaveAndRefreshAssets(assetsToUpdateInDb);
                        RefreshDataAndSelection();
                        MessageBox.Show($"{assetsToUpdateInDb.Count} vəsait uğurla dəyişdirildi.", "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Məlumat bazasına yadda saxlayarkən xəta baş verdi: {ex.Message}", "Xəta", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }

                // Show warnings for skipped status changes
                if (skippedStatusAssets.Any())
                {
                    var skippedNames = string.Join("\n", skippedStatusAssets.Select(a => $"- {a.VesaitinAdi}"));
                    MessageBox.Show($"XƏBƏRDARLIQ: {skippedStatusAssets.Count} vəsaitin statusu dəyişdirilmədi, çünki onlar artıq işçilərə təhkim olunub və 'İstifadədədir' statusunda qalmalıdırlar:\n\n{skippedNames}",
                                    "Status Dəyişikliyi Ötürüldü", MessageBoxButton.OK, MessageBoxImage.Warning);
                }

                if (!assetsToUpdateInDb.Any() && !skippedStatusAssets.Any())
                {
                    MessageBox.Show("Heç bir dəyişiklik edilmədi.", "Məlumat", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private void BulkAssignButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var checkedAssets = _allCheckableAssets.Where(vm => vm.IsChecked).Select(vm => vm.Asset).ToList();
            if (!checkedAssets.Any())
            {
                MessageBox.Show("Təhkim etmək üçün ən azı bir vəsait işarələyin.", "Vəsait Seçilməyib", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectWindow = new SelectWorkerWindow(_allWorkers) { Owner = this };
            if (selectWindow.ShowDialog() == true)
            {
                Worker newWorker = selectWindow.SelectedWorker;

                try
                {
                    // Prepare all assets for bulk save
                    foreach (var asset in checkedAssets)
                    {
                        string oldWorkerName = asset.TehkimOlunanEmekdas;
                        bool isReassignment = asset.WorkerId.HasValue;

                        asset.WorkerId = newWorker.Id;
                        asset.TehkimOlunanEmekdas = newWorker.per_adiper_soyadi;
                        asset.Vezifesi = newWorker.pgk_gorev_adi;
                        asset.BolmeShobeDepartment = newWorker.pdp_adi;
                        asset.Status = "İstifadədədir";

                        if (asset.History == null) asset.History = new List<AssignmentHistoryEntry>();
                        asset.History.Add(new AssignmentHistoryEntry
                        {
                            Action = isReassignment ? AssignmentAction.Reassigned : AssignmentAction.Assigned,
                            FromWorkerName = isReassignment ? oldWorkerName : "Sistem (Toplu Təhkim)",
                            ToWorkerName = newWorker.per_adiper_soyadi,
                            ChangedBy = SessionManager.CurrentUser.FullName,
                            ChangeDate = DateTime.Now
                        });
                    }

                    // Save all assets at once using bulk save
                    AppData.BulkSaveAndRefreshAssets(checkedAssets);
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

        // ApplyFilters metodunu yeniləyin
        private void ApplyFilters()
        {
            if (_filterViewModel == null || _allCheckableAssets == null) return;
            IEnumerable<AssetCheckableViewModel> filteredAssets = _allCheckableAssets.Where(vm => vm.Asset.Status != "Arxivdə");
            var validCategories = new HashSet<string>(AppData.GetDeviceCategories());
            if (_filterViewModel.ShowOnlyUncategorized)
            {
                filteredAssets = filteredAssets.Where(vm => string.IsNullOrEmpty(vm.Asset.Kateqoriya) || !validCategories.Contains(vm.Asset.Kateqoriya));
            }
            else
            {
                var selectedCategories = _filterViewModel.GetSelectedCategories();
                if (selectedCategories.Any()) { filteredAssets = filteredAssets.Where(vm => vm.Asset.Kateqoriya != null && selectedCategories.Contains(vm.Asset.Kateqoriya)); }

            }
            string searchText = _filterViewModel.SearchText;
            var culture = CultureInfo.CurrentCulture;
            var compareOptions = CompareOptions.IgnoreCase;
            if (!string.IsNullOrWhiteSpace(searchText)) { filteredAssets = filteredAssets.Where(vm => (vm.Asset.VesaitinKodu != null && culture.CompareInfo.IndexOf(vm.Asset.VesaitinKodu, searchText, compareOptions) >= 0) || (vm.Asset.VesaitinAdi != null && culture.CompareInfo.IndexOf(vm.Asset.VesaitinAdi, searchText, compareOptions) >= 0) || (vm.Asset.ITAvadanliqlarininSeriyaNomresi != null && culture.CompareInfo.IndexOf(vm.Asset.ITAvadanliqlarininSeriyaNomresi, searchText, compareOptions) >= 0) || (vm.Asset.Worker?.per_adiper_soyadi != null && culture.CompareInfo.IndexOf(vm.Asset.Worker.per_adiper_soyadi, searchText, compareOptions) >= 0)); }

            var selectedStatuses = _filterViewModel.StatusOptions.Where(o => o.IsChecked).Select(o => o.Value).ToList();
            if (selectedStatuses.Any()) { filteredAssets = filteredAssets.Where(vm => vm.Asset.Status != null && selectedStatuses.Contains(vm.Asset.Status)); }

            var selectedDepartments = _filterViewModel.DepartmentOptions.Where(o => o.IsChecked).Select(o => o.Value).ToList();
            if (selectedDepartments.Any()) { filteredAssets = filteredAssets.Where(vm => vm.Asset.Worker?.pdp_adi != null && selectedDepartments.Contains(vm.Asset.Worker.pdp_adi)); }

            // Apply column-specific filters
            if (_filterViewModel.ColumnFilters != null)
            {
                // Filter by Vəsaitin Kodu
                if (_filterViewModel.ColumnFilters.TryGetValue("VesaitinKodu", out var koduFilter) && koduFilter.HasActiveFilters)
                {
                    var selectedValues = koduFilter.GetSelectedValues();
                    filteredAssets = filteredAssets.Where(vm => !string.IsNullOrEmpty(vm.VesaitinKodu) && selectedValues.Contains(vm.VesaitinKodu));
                }

                // Filter by Vəsaitin Adı
                if (_filterViewModel.ColumnFilters.TryGetValue("VesaitinAdi", out var adiFilter) && adiFilter.HasActiveFilters)
                {
                    var selectedValues = adiFilter.GetSelectedValues();
                    filteredAssets = filteredAssets.Where(vm => !string.IsNullOrEmpty(vm.VesaitinAdi) && selectedValues.Contains(vm.VesaitinAdi));
                }

                // Filter by Kateqoriya
                if (_filterViewModel.ColumnFilters.TryGetValue("Kateqoriya", out var kateqoriyaFilter) && kateqoriyaFilter.HasActiveFilters)
                {
                    var selectedValues = kateqoriyaFilter.GetSelectedValues();
                    filteredAssets = filteredAssets.Where(vm => !string.IsNullOrEmpty(vm.Kateqoriya) && selectedValues.Contains(vm.Kateqoriya));
                }

                // Filter by Worker
                if (_filterViewModel.ColumnFilters.TryGetValue("Worker", out var workerFilter) && workerFilter.HasActiveFilters)
                {
                    var selectedValues = workerFilter.GetSelectedValues();
                    filteredAssets = filteredAssets.Where(vm => vm.Worker != null && !string.IsNullOrEmpty(vm.Worker.per_adiper_soyadi) && selectedValues.Contains(vm.Worker.per_adiper_soyadi));
                }

                // Filter by Department
                if (_filterViewModel.ColumnFilters.TryGetValue("Department", out var deptFilter) && deptFilter.HasActiveFilters)
                {
                    var selectedValues = deptFilter.GetSelectedValues();
                    filteredAssets = filteredAssets.Where(vm => vm.Worker != null && !string.IsNullOrEmpty(vm.Worker.pdp_adi) && selectedValues.Contains(vm.Worker.pdp_adi));
                }

                // Filter by Yerləşmə Yeri
                if (_filterViewModel.ColumnFilters.TryGetValue("YerleshmeYeri", out var yerlesmeFilter) && yerlesmeFilter.HasActiveFilters)
                {
                    var selectedValues = yerlesmeFilter.GetSelectedValues();
                    filteredAssets = filteredAssets.Where(vm => !string.IsNullOrEmpty(vm.YerleshmeYeri) && selectedValues.Contains(vm.YerleshmeYeri));
                }

                // Filter by Ərazi
                if (_filterViewModel.ColumnFilters.TryGetValue("Erazi", out var eraziFilter) && eraziFilter.HasActiveFilters)
                {
                    var selectedValues = eraziFilter.GetSelectedValues();
                    filteredAssets = filteredAssets.Where(vm => !string.IsNullOrEmpty(vm.Erazi) && selectedValues.Contains(vm.Erazi));
                }
            }

            var results = filteredAssets.ToList();
            AssetsDataGrid.ItemsSource = results;
            // ... (qalan hissə eyni) ...
            NoResultsTextBlock.Visibility = results.Any() ? Visibility.Collapsed : Visibility.Visible;
            AssetCountTextBlock.Text = $"{results.Count} vəsait tapıldı";
            AssetCountTextBlock.Visibility = Visibility.Visible;
            UpdateActiveFilterTags();
        }

        private void UpdateActiveFilterTags()
        {
            if (_filterViewModel == null) return;
            ActiveFiltersPanel.Children.Clear();
            var activeFilters = new List<string>();
            if (!string.IsNullOrWhiteSpace(_filterViewModel.SearchText)) { activeFilters.Add($"Axtarış: '{_filterViewModel.SearchText}'"); }

            if (_filterViewModel.ShowOnlyUncategorized)
            {
                activeFilters.Add("Kateqoriya: Yalnız Təyin Edilməmişlər");
            }
            else
            {
                var selectedCategories = _filterViewModel.GetSelectedCategories();
                if (selectedCategories.Any()) { activeFilters.Add($"Kateqoriya: {string.Join(", ", selectedCategories)}"); }
            }
            var selectedStatuses = _filterViewModel.StatusOptions.Where(o => o.IsChecked).Select(o => o.Value).ToList();
            if (selectedStatuses.Any()) { activeFilters.Add($"Status: {string.Join(", ", selectedStatuses)}"); }
            var selectedDepartments = _filterViewModel.DepartmentOptions.Where(o => o.IsChecked).Select(o => o.Value).ToList();
            if (selectedDepartments.Any()) { activeFilters.Add($"Departament: {string.Join(", ", selectedDepartments)}"); }
            foreach (var filterText in activeFilters) { ActiveFiltersPanel.Children.Add(new Border { Background = Brushes.LightGray, CornerRadius = new CornerRadius(10), Margin = new Thickness(2), Padding = new Thickness(8, 3, 8, 3), Child = new TextBlock { Text = filterText, Foreground = Brushes.Black } }); }
        }

        private bool isDetailPanelOpen = false;
        private void RefreshDataAndSelection(int? assetIdToSelect = null)
        {
            AppData.LoadAllData();
            var allAssets = AppData.GetAssets(); // Temporary get raw assets
            _allCheckableAssets = allAssets.Select(a => new AssetCheckableViewModel(a)).ToList();
            _allWorkers = AppData.GetWorkers();
            _filterViewModel = new AssetFilterViewModel();
            _filterViewModel.FilterChanged += ApplyFilters;
            FilterPanel.DataContext = _filterViewModel;

            // Reinitialize column filters with fresh data
            InitializeColumnFilters();

            ApplyFilters();
            UpdateBulkActionPanelVisibility(); // Uncheck all after refresh
            if (assetIdToSelect.HasValue) { SelectAssetById(assetIdToSelect.Value); }
            else { _selectedAssetVM = null; UpdateDetailView(); }
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
                AppData.DeleteAndRefreshAsset(assetVMToDelete.Asset);
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

        private void ClearFiltersButton_Click(object _, RoutedEventArgs e) => _filterViewModel.Clear();


        private void UpdateDetailView() => AssetDetailControl.DisplayAsset(_selectedAssetVM?.Asset, _allWorkers);

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

        // ---- YENİ METODLAR ----
        private void CheckBox_Toggled(object sender, RoutedEventArgs e)
        {
            UpdateBulkActionPanelVisibility();
        }
        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox headerCheckBox && headerCheckBox.IsChecked.HasValue)
            {
                bool shouldBeChecked = headerCheckBox.IsChecked.Value;
                // Yalnız filtrlənmiş, görünən elementlərə tətbiq edin
                if (AssetsDataGrid.ItemsSource is IEnumerable<AssetCheckableViewModel> visibleItems)
                {
                    foreach (var vm in visibleItems)
                    {
                        vm.IsChecked = shouldBeChecked;
                    }
                }
                UpdateBulkActionPanelVisibility();
            }
        }
        private void UpdateBulkActionPanelVisibility()
        {
            // İşarələnmiş elementlərin sayını hesablayın
            int checkedCount = _allCheckableAssets?.Count(vm => vm.IsChecked) ?? 0;
            if (checkedCount > 0)
            {
                // Əgər ən azı bir element işarələnibsə, toplu əməliyyat panelini göstərin
                BulkActionPanel.Visibility = Visibility.Visible;
                SelectionCountText.Text = $"{checkedCount} element işarələnib";
            }
            else
            {
                BulkActionPanel.Visibility = Visibility.Collapsed;
            }
            if (AssetsDataGrid.ItemsSource is IEnumerable<AssetCheckableViewModel> visibleItems)
            {
                bool allVisibleAreChecked = visibleItems.Any() && visibleItems.All(vm => vm.IsChecked);
                if (SelectAllCheckBox != null)
                {
                    SelectAllCheckBox.IsChecked = allVisibleAreChecked;
                }
            }
        }
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
            if (_allCheckableAssets == null) return;

            // Initialize Vəsaitin Kodu filter
            var koduValues = _allCheckableAssets.Select(vm => vm.VesaitinKodu).Where(v => !string.IsNullOrEmpty(v)).Distinct();
            var koduFilterVM = new ColumnFilterViewModel(koduValues);
            VesaitinKoduFilter.InitializeFilter(koduFilterVM);
            VesaitinKoduFilter.FilterApplied += OnColumnFilterApplied;
            _filterViewModel.ColumnFilters["VesaitinKodu"] = koduFilterVM;

            // Initialize Vəsaitin Adı filter
            var adiValues = _allCheckableAssets.Select(vm => vm.VesaitinAdi).Where(v => !string.IsNullOrEmpty(v)).Distinct();
            var adiFilterVM = new ColumnFilterViewModel(adiValues);
            VesaitinAdiFilter.InitializeFilter(adiFilterVM);
            VesaitinAdiFilter.FilterApplied += OnColumnFilterApplied;
            _filterViewModel.ColumnFilters["VesaitinAdi"] = adiFilterVM;

            // Initialize Kateqoriya filter
            var kateqoriyaValues = _allCheckableAssets.Select(vm => vm.Kateqoriya).Where(v => !string.IsNullOrEmpty(v)).Distinct();
            var kateqoriyaFilterVM = new ColumnFilterViewModel(kateqoriyaValues);
            KateqoriyaFilter.InitializeFilter(kateqoriyaFilterVM);
            KateqoriyaFilter.FilterApplied += OnColumnFilterApplied;
            _filterViewModel.ColumnFilters["Kateqoriya"] = kateqoriyaFilterVM;

            // Initialize Worker filter
            var workerValues = _allCheckableAssets.Select(vm => vm.Worker?.per_adiper_soyadi).Where(v => !string.IsNullOrEmpty(v)).Distinct();
            var workerFilterVM = new ColumnFilterViewModel(workerValues);
            WorkerFilter.InitializeFilter(workerFilterVM);
            WorkerFilter.FilterApplied += OnColumnFilterApplied;
            _filterViewModel.ColumnFilters["Worker"] = workerFilterVM;

            // Initialize Department filter
            var departmentValues = _allCheckableAssets.Select(vm => vm.Worker?.pdp_adi).Where(v => !string.IsNullOrEmpty(v)).Distinct();
            var departmentFilterVM = new ColumnFilterViewModel(departmentValues);
            DepartmentFilter.InitializeFilter(departmentFilterVM);
            DepartmentFilter.FilterApplied += OnColumnFilterApplied;
            _filterViewModel.ColumnFilters["Department"] = departmentFilterVM;

            // Initialize Yerləşmə Yeri filter
            var yerlesmeValues = _allCheckableAssets.Select(vm => vm.YerleshmeYeri).Where(v => !string.IsNullOrEmpty(v)).Distinct();
            var yerlesmeFilterVM = new ColumnFilterViewModel(yerlesmeValues);
            YerleshmeYeriFilter.InitializeFilter(yerlesmeFilterVM);
            YerleshmeYeriFilter.FilterApplied += OnColumnFilterApplied;
            _filterViewModel.ColumnFilters["YerleshmeYeri"] = yerlesmeFilterVM;

            // Initialize Ərazi filter
            var eraziValues = _allCheckableAssets.Select(vm => vm.Erazi).Where(v => !string.IsNullOrEmpty(v)).Distinct();
            var eraziFilterVM = new ColumnFilterViewModel(eraziValues);
            EraziFilter.InitializeFilter(eraziFilterVM);
            EraziFilter.FilterApplied += OnColumnFilterApplied;
            _filterViewModel.ColumnFilters["Erazi"] = eraziFilterVM;
        }

        private void OnColumnFilterApplied(object sender, ColumnFilterEventArgs e)
        {
            // Trigger the main filter apply logic
            ApplyFilters();
        }

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