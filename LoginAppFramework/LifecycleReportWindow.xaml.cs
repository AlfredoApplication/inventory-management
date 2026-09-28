using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Animation;

namespace LoginAppFramework
{
    public partial class LifecycleReportWindow : Window
    {
        private List<Asset> _allAssets;
        private bool isMenuOpen;
        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        public LifecycleReportWindow()
        {
            InitializeComponent();
            AssetReportListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeader_Click));
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(20);

            await Task.Run(() =>
            {
                _allAssets = AppData.GetAssets();
            });

            ApplyFilters();
            UpdateUserDisplay();
        }

        private void ApplyFilters()
        {
            if (_allAssets == null) return;

            IEnumerable<Asset> filteredAssets = _allAssets;

            // 1. Apply search text filter
            string searchText = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredAssets = filteredAssets.Where(a =>
                    (a.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.SerialNumber?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (a.AssignedUser?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }

            // 2. Apply the new RadioButton lifecycle filter
            if (ActiveAssetsFilterButton.IsChecked == true)
            {
                filteredAssets = filteredAssets.Where(a => !a.IsEndOfLife);
                TitleTextBlock.Text = "İstifadə Müddəti Davam Edən Vəsaitlər";
            }
            else if (ExpiredAssetsFilterButton.IsChecked == true)
            {
                filteredAssets = filteredAssets.Where(a => a.IsEndOfLife);
                TitleTextBlock.Text = "İstifadə Müddəti Bitmiş Vəsaitlər";
            }
            else // AllAssetsFilterButton is checked
            {
                TitleTextBlock.Text = "Bütün Vəsaitlər";
            }

            // 3. Apply the new default sorting logic
            var results = filteredAssets
                .OrderBy(a => a.IsEndOfLife)
                .ThenBy(a => a.EndOfLifeDate)
                .ToList();

            // 4. Update the UI
            AssetReportListView.ItemsSource = results;
            CountTextBlock.Text = $"{results.Count} element tapıldı";
        }

        private void Filter_Changed(object sender, RoutedEventArgs e)
        {
            ApplyFilters();
        }

        private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
        {
            var currentItems = AssetReportListView.ItemsSource as IEnumerable<Asset>;
            if (currentItems == null || !currentItems.Any())
            {
                MessageBox.Show("İxrac ediləcək məlumat tapılmadı.", "Xəbərdarlıq", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var saveDialog = new Microsoft.Win32.SaveFileDialog
            {
                Title        = "Excel Faylını Saxla",
                Filter       = "Excel Faylı (*.xlsx)|*.xlsx",
                FileName     = $"Vesait_Hesabat_{DateTime.Now:yyyy-MM-dd_HH-mm}"
            };

            if (saveDialog.ShowDialog() != true) return;

            try
            {
                ExportToExcel(currentItems.ToList(), saveDialog.FileName);
                var open = MessageBox.Show(
                    $"Fayl uğurla saxlanıldı:\n{saveDialog.FileName}\n\nFaylı indi açmaq istəyirsiniz?",
                    "İxrac Uğurlu", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (open == MessageBoxResult.Yes)
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(saveDialog.FileName) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Xəta baş verdi:\n{ex.Message}", "Xəta", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static void ExportToExcel(List<Asset> assets, string filePath)
        {
            var culture = CultureInfo.GetCultureInfo("az-Latn-AZ");

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Vəsait Hesabatı");

            // ── Header row ──────────────────────────────────────────────────
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

            // ── Data rows ───────────────────────────────────────────────────
            for (int r = 0; r < assets.Count; r++)
            {
                var a   = assets[r];
                int row = r + 2;

                // Alternate row background
                if (r % 2 == 1)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#EBF3FB");

                ws.Cell(row, 1).Value  = a.VesaitinKodu ?? "-";
                ws.Cell(row, 2).Value  = a.Name ?? "-";
                ws.Cell(row, 3).Value  = a.AssignedUser ?? "(Boşdur)";
                ws.Cell(row, 4).Value  = a.PurchaseDate;
                ws.Cell(row, 4).Style.DateFormat.Format = "dd.MM.yyyy";
                ws.Cell(row, 5).Value  = $"{a.UsefulLifeInYears} il";
                ws.Cell(row, 6).Value  = a.EndOfLifeDate;
                ws.Cell(row, 6).Style.DateFormat.Format = "dd.MM.yyyy";

                // Currency cells
                ws.Cell(row, 7).Value  = a.PurchaseCost;
                ws.Cell(row, 8).Value  = a.MonthlyDepreciation;
                ws.Cell(row, 9).Value  = a.AnnualDepreciation;
                ws.Cell(row, 10).Value = a.TotalDepreciation;
                ws.Cell(row, 11).Value = a.CurrentValue;

                // Right-align and format currency columns
                foreach (int col in new[] { 7, 8, 9, 10, 11 })
                {
                    ws.Cell(row, col).Style.NumberFormat.Format = "#,##0.00 \"₼\"";
                    ws.Cell(row, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                }

                // Highlight expired rows in light red
                if (a.IsEndOfLife)
                    ws.Row(row).Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
            }

            // ── Totals row ──────────────────────────────────────────────────
            int totalRow = assets.Count + 2;
            ws.Cell(totalRow, 1).Value = "CƏMI";
            ws.Cell(totalRow, 1).Style.Font.Bold = true;

            ws.Cell(totalRow, 7).Value  = assets.Sum(a => a.PurchaseCost);
            ws.Cell(totalRow, 8).Value  = assets.Sum(a => a.MonthlyDepreciation);
            ws.Cell(totalRow, 9).Value  = assets.Sum(a => a.AnnualDepreciation);
            ws.Cell(totalRow, 10).Value = assets.Sum(a => a.TotalDepreciation);
            ws.Cell(totalRow, 11).Value = assets.Sum(a => a.CurrentValue);

            foreach (int col in new[] { 7, 8, 9, 10, 11 })
            {
                ws.Cell(totalRow, col).Style.NumberFormat.Format  = "#,##0.00 \"₼\"";
                ws.Cell(totalRow, col).Style.Font.Bold             = true;
                ws.Cell(totalRow, col).Style.Alignment.Horizontal  = XLAlignmentHorizontalValues.Right;
                ws.Cell(totalRow, col).Style.Fill.BackgroundColor  = XLColor.FromHtml("#DBEAFE");
            }

            ws.Row(totalRow).Style.Fill.BackgroundColor = XLColor.FromHtml("#DBEAFE");

            // ── Styling ─────────────────────────────────────────────────────
            ws.Columns().AdjustToContents();
            // Cap very wide columns
            foreach (var col in ws.ColumnsUsed())
                if (col.Width > 45) col.Width = 45;

            ws.SheetView.FreezeRows(1);
            ws.RangeUsed().SetAutoFilter();

            wb.SaveAs(filePath);
        }

        private void AssetReportListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AssetReportListView.SelectedItem is Asset selectedAsset)
            {
                var assetWindow = new AssetWindow(selectedAsset.Id);
                assetWindow.Show();
                this.Close();
            }
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader { Role: not GridViewColumnHeaderRole.Padding } headerClicked)
            {
                var direction = (headerClicked == _lastHeaderClicked && _lastDirection == ListSortDirection.Ascending) ?
                                               ListSortDirection.Descending :
                                               ListSortDirection.Ascending;

                if (headerClicked.Column.DisplayMemberBinding is Binding { Path.Path: var sortBy })
                {
                    Sort(sortBy, direction);
                }

                _lastHeaderClicked = headerClicked;
                _lastDirection = direction;
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(AssetReportListView.ItemsSource);
            if (dataView == null) return;

            // Clear existing sorts and apply the new one
            dataView.SortDescriptions.Clear();
            SortDescription sd = new(sortBy, direction);
            dataView.SortDescriptions.Add(sd);
            dataView.Refresh();
        }

        private void CloseTheMenu() { isMenuOpen = false; MenuOverlay.Visibility = Visibility.Collapsed; (FindResource("CloseMenu") as Storyboard)?.Begin(); }
        private void MenuButton_Click(object sender, RoutedEventArgs e) { if (isMenuOpen) CloseTheMenu(); else { isMenuOpen = true; UserSwitchPopup.IsOpen = false; MenuOverlay.Visibility = Visibility.Visible; (FindResource("OpenMenu") as Storyboard)?.Begin(); } }
        private void CloseMenuButton_Click(object sender, RoutedEventArgs e) => CloseTheMenu();
        private void MenuOverlay_MouseDown(object sender, MouseButtonEventArgs e) => CloseTheMenu();
        private void DashboardButton_Click(object sender, RoutedEventArgs e) { new DashboardWindow().Show(); this.Close(); }
        private void UsersButton_Click(object sender, RoutedEventArgs e) { new WorkerListWindow().Show(); this.Close(); }
        private void AssetsButton_Click(object sender, RoutedEventArgs e) { new AssetWindow().Show(); this.Close(); }
        private void HistoryLogButton_Click(object sender, RoutedEventArgs e) { new HistoryLogWindow().Show(); this.Close(); }
        private void LifecycleReportButton_Click(object sender, RoutedEventArgs e) => CloseTheMenu();
        private void UpdateUserDisplay()
        {
            if (SessionManager.CurrentUser != null)
            {
                // This method no longer tries to change the icon, only the name.
                UserProfileName.Text = SessionManager.CurrentUser.FullName;
            }
        }
        private void UserProfileButton_Click(object sender, RoutedEventArgs e) => UserSwitchPopup.IsOpen = true;
        private void ReturnToLogin()
        {
            NavigationManager.RestartApplication();
        }
        private void SwitchUserButton_Click(object sender, RoutedEventArgs e) => ReturnToLogin();
        private void LogoutButton_Click(object sender, RoutedEventArgs e) => ReturnToLogin();
    }
}