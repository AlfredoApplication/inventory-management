using ClosedXML.Excel;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LoginAppFramework
{
    public partial class AssetImportPreviewWindow : Window
    {
        private readonly AssetImportPreview _preview;

        public AssetImportPreviewWindow(AssetImportPreview preview)
        {
            InitializeComponent();
            _preview = preview ?? throw new ArgumentNullException(nameof(preview));
            DataContext = _preview;
        }

        public List<Asset> AssetsToImport =>
            _preview.AssetsToImport();

        private void ImportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!_preview.CanImport)
                return;

            DialogResult = true;
            Close();
        }

        private void ExportIssuesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "Excel Workbook|*.xlsx",
                Title = "Import problemləri hesabatını yadda saxla",
                FileName =
                    $"Import_Problemleri_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx"
            };

            if (saveDialog.ShowDialog() != true)
                return;

            try
            {
                using var workbook = new XLWorkbook();
                var sheet = workbook.Worksheets.Add("Import Preview");

                string[] headers =
                {
                    "Sətir",
                    "Status",
                    "Vəsait Kodu",
                    "Vəsait Adı",
                    "Seriya",
                    "Kateqoriya",
                    "Excel-də İşçi",
                    "Uyğunlaşdırılmış İşçi",
                    "Problem / Qeyd"
                };

                for (int i = 0; i < headers.Length; i++)
                    sheet.Cell(1, i + 1).Value = headers[i];

                sheet.Row(1).Style.Font.Bold = true;

                int row = 2;
                foreach (var item in _preview.Items.Where(item =>
                    item.Severity != ImportPreviewSeverity.Ready))
                {
                    sheet.Cell(row, 1).Value = item.RowNumber;
                    sheet.Cell(row, 2).Value = item.StateText;
                    sheet.Cell(row, 3).Value = item.Code ?? string.Empty;
                    sheet.Cell(row, 4).Value = item.Name ?? string.Empty;
                    sheet.Cell(row, 5).Value = item.Serial ?? string.Empty;
                    sheet.Cell(row, 6).Value = item.Category ?? string.Empty;
                    sheet.Cell(row, 7).Value =
                        item.RequestedWorker ?? string.Empty;
                    sheet.Cell(row, 8).Value =
                        item.ResolvedWorker ?? string.Empty;
                    sheet.Cell(row, 9).Value =
                        item.IssueText ?? string.Empty;
                    row++;
                }

                foreach (var issue in _preview.Issues)
                {
                    sheet.Cell(row, 2).Value = issue.SeverityText;
                    sheet.Cell(row, 9).Value = issue.Message ?? string.Empty;
                    row++;
                }

                sheet.Columns().AdjustToContents();

                foreach (var column in sheet.ColumnsUsed())
                {
                    if (column.Width > 60)
                        column.Width = 60;
                }

                workbook.SaveAs(saveDialog.FileName);

                NotificationService.Success(
                    this,
                    "Import problemləri hesabatı Excel faylına yazıldı.");
            }
            catch (Exception ex)
            {
                NotificationService.Error(
                    this,
                    $"Problem hesabatı yaradıla bilmədi: {ex.Message}");
            }
        }
    }
}
