using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LoginAppFramework
{
    public partial class ReportsWindow : Window
    {
        private const string PurchaseDateExcelReportName =
            "Alış Tarixinə Görə Excel Hesabatı";

        private readonly Dictionary<string, ReportGenerator.ReportType> _pdfReportTypes;

        public ReportsWindow()
        {
            InitializeComponent();

            _pdfReportTypes = new Dictionary<string, ReportGenerator.ReportType>
            {
                { "Ümumi İnventar Hesabatı", ReportGenerator.ReportType.FullInventory },
                { "Departamentlər Üzrə Vəsait Hesabatı", ReportGenerator.ReportType.AssetsByDepartment },
                { "Zəmanəti Bitən / Bitmək Üzrə Olan Avadanlıqlar", ReportGenerator.ReportType.WarrantyExpiration }
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ReportTypeComboBox.ItemsSource = _pdfReportTypes.Keys
                .Concat(new[] { PurchaseDateExcelReportName })
                .ToList();

            ReportTypeComboBox.SelectedIndex = 0;
            UpdateReportUi();
        }

        private void ReportTypeComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            UpdateReportUi();
        }

        private void UpdateReportUi()
        {
            bool isExcelReport =
                string.Equals(
                    ReportTypeComboBox.SelectedItem as string,
                    PurchaseDateExcelReportName,
                    StringComparison.Ordinal);

            PurchaseDateFilterPanel.Visibility =
                isExcelReport ? Visibility.Visible : Visibility.Collapsed;

            GenerateButton.Content =
                isExcelReport
                    ? "Excel Hesabatı Yarat və Yadda Saxla"
                    : "PDF Yarat və Yadda Saxla";

            StatusTextBlock.Text = isExcelReport
                ? "Tarixlər boş saxlanılarsa bütün vəsaitlər export ediləcək."
                : string.Empty;
        }

        private void ClearDateFilterButton_Click(object sender, RoutedEventArgs e)
        {
            PurchaseDateFromPicker.SelectedDate = null;
            PurchaseDateToPicker.SelectedDate = null;
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedReportName = ReportTypeComboBox.SelectedItem as string;
            if (string.IsNullOrWhiteSpace(selectedReportName))
            {
                MessageBox.Show(
                    "Zəhmət olmasa, etibarlı bir hesabat növü seçin.",
                    "Xəta");
                return;
            }

            if (selectedReportName == PurchaseDateExcelReportName)
            {
                GeneratePurchaseDateExcelReport();
                return;
            }

            GeneratePdfReport(selectedReportName);
        }

        private void GeneratePurchaseDateExcelReport()
        {
            DateTime? fromDate = PurchaseDateFromPicker.SelectedDate?.Date;
            DateTime? toDate = PurchaseDateToPicker.SelectedDate?.Date;

            if (fromDate.HasValue &&
                toDate.HasValue &&
                fromDate.Value > toDate.Value)
            {
                MessageBox.Show(
                    "Başlanğıc tarixi son tarixdən böyük ola bilməz.",
                    "Tarix Filtri",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook|*.xlsx",
                Title = "Excel Hesabatını Yadda Saxla",
                FileName = BuildExcelReportFileName(fromDate, toDate)
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            GenerateButton.IsEnabled = false;
            StatusTextBlock.Text = "Excel hesabatı yaradılır...";

            try
            {
                var allAssets = AppData.GetAssets();

                int exportedCount = AppServices.AssetExcel.ExportPurchaseDateReport(
                    saveFileDialog.FileName,
                    allAssets,
                    fromDate,
                    toDate);

                StatusTextBlock.Text = string.Empty;

                var result = MessageBox.Show(
                    $"{exportedCount} vəsait Excel hesabatına çıxarıldı.\n\nFaylı indi açmaq istəyirsinizmi?",
                    "Excel Hesabatı Hazırdır",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(
                        new ProcessStartInfo(saveFileDialog.FileName)
                        {
                            UseShellExecute = true
                        });
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = string.Empty;
                MessageBox.Show(
                    $"Excel hesabatı yaradılarkən xəta baş verdi: {ex.Message}",
                    "Xəta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                GenerateButton.IsEnabled = true;
            }
        }

        private void GeneratePdfReport(string selectedReportName)
        {
            if (!_pdfReportTypes.TryGetValue(selectedReportName, out var reportType))
            {
                MessageBox.Show(
                    "Zəhmət olmasa, etibarlı bir hesabat növü seçin.",
                    "Xəta");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF Sənədi|*.pdf",
                Title = "PDF Hesabatını Yadda Saxla",
                FileName =
                    $"{selectedReportName.Replace("/", "").Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            string filePath = saveFileDialog.FileName;
            GenerateButton.IsEnabled = false;
            StatusTextBlock.Text = "Hesabat yaradılır...";

            try
            {
                var allAssets = AppData.GetAssets();

                switch (reportType)
                {
                    case ReportGenerator.ReportType.FullInventory:
                        ReportGenerator.GenerateFullInventoryReport(allAssets, filePath);
                        break;

                    case ReportGenerator.ReportType.AssetsByDepartment:
                        ReportGenerator.GenerateAssetsByDepartmentReport(allAssets, filePath);
                        break;

                    case ReportGenerator.ReportType.WarrantyExpiration:
                        ReportGenerator.GenerateWarrantyExpirationReport(allAssets, filePath);
                        break;
                }

                StatusTextBlock.Text = string.Empty;

                var result = MessageBox.Show(
                    "Hesabat uğurla yaradıldı!\nİndi faylı açmaq istəyirsinizmi?",
                    "Uğurlu Əməliyyat",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);

                if (result == MessageBoxResult.Yes)
                {
                    Process.Start(
                        new ProcessStartInfo(filePath)
                        {
                            UseShellExecute = true
                        });
                }
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = string.Empty;
                MessageBox.Show(
                    $"Hesabat yaradılarkən xəta baş verdi: {ex.Message}",
                    "Xəta",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            finally
            {
                GenerateButton.IsEnabled = true;
            }
        }

        private static string BuildExcelReportFileName(
            DateTime? fromDate,
            DateTime? toDate)
        {
            string from = fromDate?.ToString("yyyyMMdd") ?? "ALL";
            string to = toDate?.ToString("yyyyMMdd") ?? "ALL";
            return $"Alis_Tarixi_Hesabati_{from}_{to}_{DateTime.Now:HHmmss}.xlsx";
        }
    }
}