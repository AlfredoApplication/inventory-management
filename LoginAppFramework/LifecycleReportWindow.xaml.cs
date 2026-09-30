using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class LifecycleReportWindow : Window, INavigationRefreshable
    {
        private List<Asset> _allAssets;
        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        public LifecycleReportWindow()
        {
            InitializeComponent();
            AssetReportListView.AddHandler(
                GridViewColumnHeader.ClickEvent,
                new RoutedEventHandler(GridViewColumnHeader_Click));
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(20);

            try
            {
                _allAssets = await Task.Run(() => AppData.GetAssets());
                ApplyFilters();
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Yükləmə Xətası",
                    $"Həyat dövrü hesabatı yüklənərkən xəta baş verdi:\n\n{ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        public async Task RefreshForNavigationAsync()
        {
            _allAssets = await Task.Run(() => AppData.GetAssets());
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (_allAssets == null)
                return;

            IEnumerable<Asset> filteredAssets = _allAssets;
            string searchText = SearchBox.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredAssets = filteredAssets.Where(a =>
                    (a.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.SerialNumber?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.VesaitinKodu?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.AssignedUser?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (ActiveAssetsFilterButton.IsChecked == true)
            {
                filteredAssets = filteredAssets.Where(a => a.HasUsefulLife && !a.IsEndOfLife);
                TitleTextBlock.Text = "İstifadə Müddəti Davam Edən Vəsaitlər";
            }
            else if (ExpiredAssetsFilterButton.IsChecked == true)
            {
                filteredAssets = filteredAssets.Where(a => a.IsEndOfLife);
                TitleTextBlock.Text = "İstifadə Müddəti Bitmiş Vəsaitlər";
            }
            else
            {
                TitleTextBlock.Text = "Bütün Vəsaitlər";
            }

            var results = filteredAssets
                .OrderBy(a => a.HasUsefulLife ? 0 : 1)
                .ThenBy(a => a.IsEndOfLife)
                .ThenBy(a => a.EndOfLifeDate ?? DateTime.MaxValue)
                .ToList();

            AssetReportListView.ItemsSource = results;
            CountTextBlock.Text = $"{results.Count} element tapıldı";
            NoResultsPanel.Visibility =
                results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
            => ApplyFilters();

        private async void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            var currentItems = AssetReportListView.ItemsSource as IEnumerable<Asset>;
            if (currentItems == null || !currentItems.Any())
            {
                NotificationService.Warning(
                    this,
                    "İxrac ediləcək məlumat tapılmadı.");
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Excel Faylını Saxla",
                Filter = "Excel Faylı (*.xlsx)|*.xlsx",
                FileName = $"Vesait_Hesabat_{DateTime.Now:yyyy-MM-dd_HH-mm}"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            var exportItems = currentItems.ToList();
            ExportExcelButton.IsEnabled = false;
            OperationProgressOverlay.Show(
                "Həyat dövrü hesabatı yaradılır...",
                $"{exportItems.Count} vəsait hazırlanır.");

            var progress = new Progress<OperationProgressInfo>(
                value => OperationProgressOverlay.Report(value));

            try
            {
                await Task.Run(
                    () => ExportToExcel(
                        exportItems,
                        saveDialog.FileName,
                        progress));

                NotificationService.Success(
                    this,
                    "Excel hesabatı uğurla yadda saxlanıldı.");

                bool openFile = DialogService.Confirm(
                    this,
                    "İxrac Tamamlandı",
                    $"Fayl uğurla saxlanıldı:\n{saveDialog.FileName}\n\nFaylı indi açmaq istəyirsiniz?",
                    "Faylı aç",
                    "Bağla");

                if (openFile)
                {
                    System.Diagnostics.Process.Start(
                        new System.Diagnostics.ProcessStartInfo(saveDialog.FileName)
                        {
                            UseShellExecute = true
                        });
                }
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "İxrac Xətası",
                    $"Excel hesabatı yaradılarkən xəta baş verdi:\n\n{ex.Message}");
            }
            finally
            {
                OperationProgressOverlay.Hide();
                ExportExcelButton.IsEnabled = true;
            }
        }

        private static void ExportToExcel(
            List<Asset> assets,
            string filePath,
            IProgress<OperationProgressInfo> progress)
        {
            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Vəsait Hesabatı");

            string[] headers =
            {
                "Vəsaitin Kodu",
                "Vəsaitin Adı",
                "Təhkim Olunmuş Əməkdaş",
                "Alış tarixi",
                "Amortizasiya müddəti (il)",
                "İstifadə Müddətinin Sonu",
                "Alış qiyməti",
                "Aylıq amortizasiya",
                "İllik amortizasiya",
                "Yığılmış amortizasiya",
                "Cari dəyər"
            };

            for (int c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(1, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2E75B6");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            }

            progress?.Report(new OperationProgressInfo(
                "Həyat dövrü hesabatı yaradılır...",
                0,
                assets.Count,
                "Vəsaitlər Excel səhifəsinə yazılır."));

            for (int r = 0; r < assets.Count; r++)
            {
                var a = assets[r];
                int row = r + 2;

                if (r % 2 == 1)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#EBF3FB");

                ws.Cell(row, 1).Value = a.VesaitinKodu ?? "-";
                ws.Cell(row, 2).Value = a.Name ?? "-";
                ws.Cell(row, 3).Value = a.AssignedUser ?? "(Boşdur)";
                ws.Cell(row, 4).Value = a.PurchaseDate;
                ws.Cell(row, 4).Style.DateFormat.Format = "dd.MM.yyyy";
                ws.Cell(row, 5).Value = a.UsefulLifeDisplay;

                if (a.EndOfLifeDate.HasValue)
                {
                    ws.Cell(row, 6).Value = a.EndOfLifeDate.Value;
                    ws.Cell(row, 6).Style.DateFormat.Format = "dd.MM.yyyy";
                }
                else
                {
                    ws.Cell(row, 6).Value = "Təyin edilməyib";
                }

                ws.Cell(row, 7).Value = a.PurchaseCost;
                ws.Cell(row, 8).Value = a.MonthlyDepreciation;
                ws.Cell(row, 9).Value = a.AnnualDepreciation;
                ws.Cell(row, 10).Value = a.TotalDepreciation;
                ws.Cell(row, 11).Value = a.CurrentValue;

                foreach (int col in new[] { 7, 8, 9, 10, 11 })
                {
                    ws.Cell(row, col).Style.NumberFormat.Format = "#,##0.00 \"₼\"";
                    ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }

                if (a.IsEndOfLife)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");

                progress?.Report(new OperationProgressInfo(
                    "Həyat dövrü hesabatı yaradılır...",
                    r + 1,
                    assets.Count,
                    $"{r + 1} / {assets.Count} vəsait yazıldı"));
            }

            int totalRow = assets.Count + 2;
            ws.Cell(totalRow, 1).Value = "CƏMI";
            ws.Cell(totalRow, 1).Style.Font.Bold = true;

            ws.Cell(totalRow, 7).Value = assets.Sum(a => a.PurchaseCost);
            ws.Cell(totalRow, 8).Value = assets.Sum(a => a.MonthlyDepreciation);
            ws.Cell(totalRow, 9).Value = assets.Sum(a => a.AnnualDepreciation);
            ws.Cell(totalRow, 10).Value = assets.Sum(a => a.TotalDepreciation);
            ws.Cell(totalRow, 11).Value = assets.Sum(a => a.CurrentValue);

            foreach (int col in new[] { 7, 8, 9, 10, 11 })
            {
                ws.Cell(totalRow, col).Style.NumberFormat.Format = "#,##0.00 \"₼\"";
                ws.Cell(totalRow, col).Style.Font.Bold = true;
                ws.Cell(totalRow, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Cell(totalRow, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
            }

            ws.Row(totalRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");
            ws.Columns().AdjustToContents();

            foreach (var col in ws.ColumnsUsed())
            {
                if (col.Width > 45)
                    col.Width = 45;
            }

            ws.SheetView.FreezeRows(1);
            ws.RangeUsed().SetAutoFilter();

            progress?.Report(new OperationProgressInfo(
                "Hesabat saxlanılır...",
                assets.Count,
                assets.Count,
                "Excel faylı diskə yazılır."));

            wb.SaveAs(filePath);
        }

        private async void AssetReportListView_MouseDoubleClick(
            object sender,
            MouseButtonEventArgs e)
        {
            if (AssetReportListView.SelectedItem is not Asset selectedAsset)
                return;

            await NavigationManager.GoToAssetWindow(this, selectedAsset.Id);
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is not GridViewColumnHeader
                {
                    Role: not GridViewColumnHeaderRole.Padding
                } headerClicked)
            {
                return;
            }

            var direction =
                headerClicked == _lastHeaderClicked &&
                _lastDirection == ListSortDirection.Ascending
                    ? ListSortDirection.Descending
                    : ListSortDirection.Ascending;

            if (headerClicked.Column.DisplayMemberBinding is Binding
                {
                    Path.Path: var sortBy
                })
            {
                Sort(sortBy, direction);
            }

            _lastHeaderClicked = headerClicked;
            _lastDirection = direction;
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView =
                CollectionViewSource.GetDefaultView(AssetReportListView.ItemsSource);

            if (dataView == null)
                return;

            dataView.SortDescriptions.Clear();
            dataView.SortDescriptions.Add(new SortDescription(sortBy, direction));
            dataView.Refresh();
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.F)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
            => SharedNavigationMenu.Open();
    }
}
