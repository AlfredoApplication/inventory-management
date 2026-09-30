using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Windows;

namespace LoginAppFramework
{
    public partial class ReportsWindow : Window
    {
        private DateTime? _purchaseDateFrom;
        private DateTime? _purchaseDateTo;

        public ReportsWindow()
        {
            InitializeComponent();
        }

        private void SelectFromDateButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DateSelectionWindow(
                "Başlanğıc alış tarixini seçin",
                _purchaseDateFrom)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true || !dialog.SelectedDate.HasValue)
                return;

            _purchaseDateFrom = dialog.SelectedDate.Value.Date;
            PurchaseDateFromTextBox.Text =
                _purchaseDateFrom.Value.ToString("dd.MM.yyyy");
        }

        private void SelectToDateButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DateSelectionWindow(
                "Son alış tarixini seçin",
                _purchaseDateTo)
            {
                Owner = this
            };

            if (dialog.ShowDialog() != true || !dialog.SelectedDate.HasValue)
                return;

            _purchaseDateTo = dialog.SelectedDate.Value.Date;
            PurchaseDateToTextBox.Text =
                _purchaseDateTo.Value.ToString("dd.MM.yyyy");
        }

        private void ClearDateFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _purchaseDateFrom = null;
            _purchaseDateTo = null;
            PurchaseDateFromTextBox.Clear();
            PurchaseDateToTextBox.Clear();
            StatusTextBlock.Text = string.Empty;
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
            => GeneratePurchaseDateExcelReport();

        private void GeneratePurchaseDateExcelReport()
        {
            if (_purchaseDateFrom.HasValue &&
                _purchaseDateTo.HasValue &&
                _purchaseDateFrom.Value > _purchaseDateTo.Value)
            {
                DialogService.Warning(
                    this,
                    "Tarix Filtri",
                    "Başlanğıc tarixi son tarixdən böyük ola bilməz.");
                return;
            }

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook|*.xlsx",
                Title = "Excel Hesabatını Yadda Saxla",
                FileName = BuildExcelReportFileName(
                    _purchaseDateFrom,
                    _purchaseDateTo)
            };

            if (saveFileDialog.ShowDialog() != true)
                return;

            GenerateButton.IsEnabled = false;
            StatusTextBlock.Text = "Excel hesabatı yaradılır...";

            try
            {
                int exportedCount =
                    AppServices.AssetExcel.ExportPurchaseDateReport(
                        saveFileDialog.FileName,
                        AppData.GetAssets(),
                        _purchaseDateFrom,
                        _purchaseDateTo);

                StatusTextBlock.Text =
                    $"{exportedCount} vəsait hesabatına daxil edildi.";

                NotificationService.Success(
                    this,
                    $"{exportedCount} vəsait Excel hesabatına çıxarıldı.");

                bool result = DialogService.Confirm(
                    this,
                    "Excel Hesabatı Hazırdır",
                    "Faylı indi açmaq istəyirsinizmi?",
                    "Faylı aç",
                    "Bağla");

                if (result)
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

                DialogService.Error(
                    this,
                    "Xəta",
                    $"Excel hesabatı yaradılarkən xəta baş verdi: {ex.Message}");
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