using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

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

        private void OpenFromCalendarButton_Click(object sender, RoutedEventArgs e)
        {
            PurchaseDateToPopup.IsOpen = false;
            PurchaseDateFromCalendar.SelectedDate = _purchaseDateFrom;
            PurchaseDateFromCalendar.DisplayDate =
                _purchaseDateFrom ?? DateTime.Today;
            PurchaseDateFromPopup.IsOpen = !PurchaseDateFromPopup.IsOpen;
        }

        private void OpenToCalendarButton_Click(object sender, RoutedEventArgs e)
        {
            PurchaseDateFromPopup.IsOpen = false;
            PurchaseDateToCalendar.SelectedDate = _purchaseDateTo;
            PurchaseDateToCalendar.DisplayDate =
                _purchaseDateTo ?? DateTime.Today;
            PurchaseDateToPopup.IsOpen = !PurchaseDateToPopup.IsOpen;
        }

        private void PurchaseDateFromCalendar_SelectedDatesChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!PurchaseDateFromCalendar.SelectedDate.HasValue)
                return;

            _purchaseDateFrom = PurchaseDateFromCalendar.SelectedDate.Value.Date;
            PurchaseDateFromTextBox.Text =
                _purchaseDateFrom.Value.ToString("dd.MM.yyyy");
            PurchaseDateFromPopup.IsOpen = false;
        }

        private void PurchaseDateToCalendar_SelectedDatesChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!PurchaseDateToCalendar.SelectedDate.HasValue)
                return;

            _purchaseDateTo = PurchaseDateToCalendar.SelectedDate.Value.Date;
            PurchaseDateToTextBox.Text =
                _purchaseDateTo.Value.ToString("dd.MM.yyyy");
            PurchaseDateToPopup.IsOpen = false;
        }

        private void ClearDateFilterButton_Click(object sender, RoutedEventArgs e)
        {
            _purchaseDateFrom = null;
            _purchaseDateTo = null;

            PurchaseDateFromTextBox.Clear();
            PurchaseDateToTextBox.Clear();

            PurchaseDateFromCalendar.SelectedDate = null;
            PurchaseDateToCalendar.SelectedDate = null;

            PurchaseDateFromPopup.IsOpen = false;
            PurchaseDateToPopup.IsOpen = false;

            StatusTextBlock.Text = string.Empty;
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            GeneratePurchaseDateExcelReport();
        }

        private void GeneratePurchaseDateExcelReport()
        {
            DateTime? fromDate = _purchaseDateFrom;
            DateTime? toDate = _purchaseDateTo;

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

                int exportedCount =
                    AppServices.AssetExcel.ExportPurchaseDateReport(
                        saveFileDialog.FileName,
                        allAssets,
                        fromDate,
                        toDate);

                StatusTextBlock.Text =
                    $"{exportedCount} vəsait hesabatına daxil edildi.";

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

        private static string BuildExcelReportFileName(
            DateTime? fromDate,
            DateTime? toDate)
        {
            string from = fromDate?.ToString("yyyyMMdd") ?? "ALL";
            string to = toDate?.ToString("yyyyMMdd") ?? "ALL";

            return
                $"Alis_Tarixi_Hesabati_{from}_{to}_{DateTime.Now:HHmmss}.xlsx";
        }
    }
}