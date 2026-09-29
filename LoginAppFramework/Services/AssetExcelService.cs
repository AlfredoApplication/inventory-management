using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public sealed class AssetImportBatch
    {
        public List<Asset> Assets { get; } = new();
        public Dictionary<Asset, string> RequestedWorkerNames { get; } = new();
        public List<string> Errors { get; } = new();
    }

    public interface IAssetExcelService
    {
        AssetImportBatch ParseImport(string filePath);
        void Export(string filePath, IEnumerable<Asset> assets);
        int ExportPurchaseDateReport(
            string filePath,
            IEnumerable<Asset> assets,
            DateTime? purchaseDateFrom,
            DateTime? purchaseDateTo);
    }

    public sealed class AssetExcelService : IAssetExcelService
    {
        public AssetImportBatch ParseImport(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Excel faylı göstərilməyib.", nameof(filePath));

            var result = new AssetImportBatch();

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet == null || worksheet.FirstRowUsed() == null)
                throw new InvalidOperationException("Excel faylı boşdur və ya başlıq sətri yoxdur.");

            var headerRow = worksheet.FirstRowUsed();
            var columnMap = headerRow.Cells()
                .Where(cell => !string.IsNullOrWhiteSpace(cell.GetString()))
                .ToDictionary(
                    cell => cell.GetString().Trim(),
                    cell => cell.Address.ColumnNumber,
                    StringComparer.OrdinalIgnoreCase);

            int nameColumn = ResolveRequiredColumn(
                columnMap,
                "Vəsaitin Adı",
                "Vəsait adı");

            var categoryDefaultLifecycles = AppData.GetCategoryDefaultLifecycles();

            foreach (var row in worksheet.RowsUsed().Skip(1))
            {
                try
                {
                    string assetName = row.Cell(nameColumn).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(assetName)) continue;

                    var asset = new Asset
                    {
                        VesaitinAdi = assetName,
                        Status = "Anbarda",
                        PurchaseDate = DateTime.Today,
                        UsefulLifeInYears = 0
                    };

                    if (TryResolveColumn(columnMap, out int codeColumn, "Vəsaitin Kodu"))
                        asset.VesaitinKodu = row.Cell(codeColumn).GetString().Trim();

                    if (TryResolveColumn(
                        columnMap,
                        out int serialColumn,
                        "IT Seriya No",
                        "ITAvadanliqlarininSeriyaNomresi",
                        "İT avadanlıqlarının seriya №-i"))
                    {
                        asset.ITAvadanliqlarininSeriyaNomresi =
                            row.Cell(serialColumn).GetString().Trim();
                    }

                    if (TryResolveColumn(columnMap, out int categoryColumn, "Kateqoriya"))
                        asset.Kateqoriya = row.Cell(categoryColumn).GetString().Trim();

                    if (TryResolveColumn(columnMap, out int locationColumn, "Yerləşmə Yeri"))
                        asset.YerleshmeYeri = row.Cell(locationColumn).GetString().Trim();

                    if (TryResolveColumn(columnMap, out int areaColumn, "Ərazi"))
                        asset.Erazi = row.Cell(areaColumn).GetString().Trim();

                    if (TryResolveColumn(columnMap, out int costColumn, "Alış qiyməti") &&
                        row.Cell(costColumn).TryGetValue(out decimal cost))
                    {
                        asset.PurchaseCost = cost;
                    }

                    if (TryResolveColumn(
                        columnMap,
                        out int purchaseDateColumn,
                        "Alış tarixi",
                        "Alınma tarixi",
                        "Alış vaxtı") &&
                        row.Cell(purchaseDateColumn).TryGetValue(out DateTime purchaseDate) &&
                        purchaseDate > DateTime.MinValue)
                    {
                        asset.PurchaseDate = purchaseDate;
                    }

                    if (TryResolveColumn(
                        columnMap,
                        out int lifeColumn,
                        "Faydalı ömür",
                        "Faydalı ömrü",
                        "İstifadə müddəti (İl)") &&
                        row.Cell(lifeColumn).TryGetValue(out int usefulLife))
                    {
                        asset.UsefulLifeInYears = usefulLife;
                    }

                    if (TryResolveColumn(
                        columnMap,
                        out int supplierColumn,
                        "Təchizatçı",
                        "Supplier"))
                    {
                        asset.Supplier = row.Cell(supplierColumn).GetString().Trim();
                    }

                    if (TryResolveColumn(
                        columnMap,
                        out int warrantyColumn,
                        "Zəmanət Müddəti",
                        "Zəmanət bitmə tarixi",
                        "WarrantyExpirationDate") &&
                        row.Cell(warrantyColumn).TryGetValue(out DateTime warrantyDate))
                    {
                        asset.WarrantyExpirationDate = warrantyDate;
                    }

                    if (TryResolveColumn(columnMap, out int workerColumn, "Təhkim Olunan Əməkdaş"))
                    {
                        string workerName = row.Cell(workerColumn).GetString().Trim();
                        if (!string.IsNullOrWhiteSpace(workerName))
                            result.RequestedWorkerNames[asset] = workerName;
                    }

                    if (asset.UsefulLifeInYears <= 0 &&
                        !string.IsNullOrWhiteSpace(asset.Kateqoriya) &&
                        categoryDefaultLifecycles.TryGetValue(asset.Kateqoriya, out int defaultYears) &&
                        defaultYears > 0)
                    {
                        asset.UsefulLifeInYears = defaultYears;
                    }

                    result.Assets.Add(asset);
                }
                catch (Exception ex)
                {
                    result.Errors.Add(
                        $"Sətir {row.RowNumber()}: {ex.Message}");
                }
            }

            return result;
        }

        public void Export(string filePath, IEnumerable<Asset> assets)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Export faylı göstərilməyib.", nameof(filePath));

            var assetList = assets?.ToList() ?? new List<Asset>();
            if (assetList.Count == 0)
                throw new InvalidOperationException("Export üçün vəsait tapılmadı.");

            using var workbook = new XLWorkbook();

            var worksheet = workbook.Worksheets.Add("Vəsaitlər");
            string[] headers =
            {
                "Vəsaitin Kodu",
                "Vəsaitin Adı",
                "IT Seriya No",
                "Kateqoriya",
                "Təhkim Olunan Əməkdaş",
                "Vəzifəsi",
                "Bölmə/Şöbə/Departament",
                "Yerləşmə Yeri",
                "Ərazi",
                "Status",
                "Alış Qiyməti",
                "Alınma Tarixi",
                "İstifadə müddəti (İl)",
                "Aylıq Amortizasiya"
            };

            for (int i = 0; i < headers.Length; i++)
                worksheet.Cell(1, i + 1).Value = headers[i];

            worksheet.Row(1).Style.Font.Bold = true;

            int row = 2;
            foreach (var asset in assetList)
            {
                worksheet.Cell(row, 1).Value = asset.VesaitinKodu;
                worksheet.Cell(row, 2).Value = asset.VesaitinAdi;
                worksheet.Cell(row, 3).Value = asset.ITAvadanliqlarininSeriyaNomresi;
                worksheet.Cell(row, 4).Value = asset.Kateqoriya;
                worksheet.Cell(row, 5).Value = asset.AssignedUser;
                worksheet.Cell(row, 6).Value = asset.AssignedPosition;
                worksheet.Cell(row, 7).Value = asset.Department;
                worksheet.Cell(row, 8).Value = asset.YerleshmeYeri;
                worksheet.Cell(row, 9).Value = asset.Erazi;
                worksheet.Cell(row, 10).Value = asset.Status;
                worksheet.Cell(row, 11).Value = asset.PurchaseCost;
                worksheet.Cell(row, 12).Value =
                    asset.PurchaseDate > DateTime.MinValue
                        ? asset.PurchaseDate.ToString("yyyy-MM-dd")
                        : string.Empty;
                worksheet.Cell(row, 13).Value = asset.UsefulLifeInYears;
                worksheet.Cell(row, 14).Value = asset.MonthlyDepreciation;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            var historySheet = workbook.Worksheets.Add("Vəsait Tarixçəsi");
            string[] historyHeaders =
            {
                "Vəsaitin Kodu",
                "Vəsaitin Adı",
                "IT Seriya No",
                "Əməliyyat",
                "Kimdən Alındı",
                "Kimə Verildi",
                "Dəyişikliyi Edən",
                "Dəyişiklik Tarixi"
            };

            for (int i = 0; i < historyHeaders.Length; i++)
                historySheet.Cell(1, i + 1).Value = historyHeaders[i];

            historySheet.Row(1).Style.Font.Bold = true;

            int historyRow = 2;
            foreach (var asset in assetList)
            {
                foreach (var entry in asset.History?
                    .OrderBy(history => history.ChangeDate)
                    ?? Enumerable.Empty<AssignmentHistoryEntry>())
                {
                    historySheet.Cell(historyRow, 1).Value = asset.VesaitinKodu;
                    historySheet.Cell(historyRow, 2).Value = asset.VesaitinAdi;
                    historySheet.Cell(historyRow, 3).Value = asset.ITAvadanliqlarininSeriyaNomresi;
                    historySheet.Cell(historyRow, 4).Value = entry.Action switch
                    {
                        AssignmentAction.Assigned => "Təhkim edilib",
                        AssignmentAction.Reassigned => "Yenidən Təhkim edilib",
                        AssignmentAction.Unassigned => "Geri Alınıb",
                        _ => entry.Action.ToString()
                    };
                    historySheet.Cell(historyRow, 5).Value = entry.FromWorkerName ?? "-";
                    historySheet.Cell(historyRow, 6).Value = entry.ToWorkerName ?? "-";
                    historySheet.Cell(historyRow, 7).Value = entry.ChangedBy ?? "-";
                    historySheet.Cell(historyRow, 8).Value =
                        entry.ChangeDate.ToString("yyyy-MM-dd HH:mm");
                    historyRow++;
                }
            }

            historySheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
        }

        public int ExportPurchaseDateReport(
            string filePath,
            IEnumerable<Asset> assets,
            DateTime? purchaseDateFrom,
            DateTime? purchaseDateTo)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("Export faylı göstərilməyib.", nameof(filePath));

            DateTime? from = purchaseDateFrom?.Date;
            DateTime? to = purchaseDateTo?.Date;

            if (from.HasValue && to.HasValue && from.Value > to.Value)
                throw new InvalidOperationException("Başlanğıc tarixi son tarixdən böyük ola bilməz.");

            var source = assets?.ToList() ?? new List<Asset>();
            var filteredAssets = source
                .Where(asset =>
                {
                    if (!from.HasValue && !to.HasValue)
                        return true;

                    if (asset.PurchaseDate <= DateTime.MinValue)
                        return false;

                    DateTime purchaseDate = asset.PurchaseDate.Date;
                    if (from.HasValue && purchaseDate < from.Value) return false;
                    if (to.HasValue && purchaseDate > to.Value) return false;
                    return true;
                })
                .OrderBy(asset => asset.PurchaseDate <= DateTime.MinValue ? DateTime.MaxValue : asset.PurchaseDate)
                .ThenBy(asset => asset.Id)
                .ToList();

            if (filteredAssets.Count == 0)
                throw new InvalidOperationException("Seçilmiş alış tarixi intervalında vəsait tapılmadı.");

            var customFieldNames = filteredAssets
                .SelectMany(asset => asset.CustomFields?.Keys ?? Enumerable.Empty<string>())
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name)
                .ToList();

            using var workbook = new XLWorkbook();
            WriteComprehensiveAssetsSheet(
                workbook,
                filteredAssets,
                customFieldNames,
                from,
                to);
            WriteAssignmentHistorySheet(workbook, filteredAssets);
            WriteMaintenanceSheet(workbook, filteredAssets);

            workbook.SaveAs(filePath);
            return filteredAssets.Count;
        }

        private static void WriteComprehensiveAssetsSheet(
            XLWorkbook workbook,
            IReadOnlyList<Asset> assets,
            IReadOnlyList<string> customFieldNames,
            DateTime? from,
            DateTime? to)
        {
            var worksheet = workbook.Worksheets.Add("Vəsaitlər");

            var standardHeaders = new[]
            {
                "ID",
                "Vəsaitin Kodu",
                "Vəsaitin Adı",
                "IT Seriya No",
                "Kateqoriya",
                "Ana Kateqoriya",
                "Worker ID",
                "Təhkim Olunan Əməkdaş",
                "Vəzifə",
                "Bölmə/Şöbə/Departament",
                "Yerləşmə Yeri",
                "Ərazi",
                "Status",
                "Lifecycle Status",
                "Alış Qiyməti",
                "Alış Tarixi",
                "İstifadə Müddəti (İl)",
                "İstifadə Müddətinin Sonu",
                "İstifadə Müddəti Bitib",
                "İllik Amortizasiya",
                "Aylıq Amortizasiya",
                "Cəmi Amortizasiya",
                "Cari Dəyər",
                "Təchizatçı",
                "Zəmanət Bitmə Tarixi",
                "Texniki Xidmət Sayı",
                "Texniki Xidmət Cəmi Xərc"
            };

            var headers = standardHeaders
                .Concat(customFieldNames.Select(name => $"Custom: {name}"))
                .ToList();

            int totalColumns = headers.Count;
            int headerRow = 5;

            worksheet.Range(1, 1, 1, totalColumns).Merge();
            worksheet.Cell(1, 1).Value = "Alış Tarixinə Görə İnventar Hesabatı";
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 16;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Range(2, 1, 2, totalColumns).Merge();
            string fromText = from?.ToString("dd.MM.yyyy") ?? "Başlanğıcsız";
            string toText = to?.ToString("dd.MM.yyyy") ?? "Sonsuz";
            worksheet.Cell(2, 1).Value = $"Alış tarixi filtri: {fromText} - {toText}";

            worksheet.Range(3, 1, 3, totalColumns).Merge();
            worksheet.Cell(3, 1).Value =
                $"Vəsait sayı: {assets.Count} | Hesabat tarixi: {DateTime.Now:dd.MM.yyyy HH:mm}";

            for (int i = 0; i < headers.Count; i++)
                worksheet.Cell(headerRow, i + 1).Value = headers[i];

            var headerRange = worksheet.Range(headerRow, 1, headerRow, totalColumns);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
            headerRange.Style.Alignment.WrapText = true;

            int row = headerRow + 1;
            foreach (var asset in assets)
            {
                int column = 1;
                worksheet.Cell(row, column++).Value = asset.Id;
                worksheet.Cell(row, column++).Value = asset.VesaitinKodu ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.VesaitinAdi ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.ITAvadanliqlarininSeriyaNomresi ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.Kateqoriya ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.ParentCategory ?? string.Empty;

                if (asset.WorkerId.HasValue)
                    worksheet.Cell(row, column).Value = asset.WorkerId.Value;
                column++;

                worksheet.Cell(row, column++).Value = asset.AssignedUser ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.AssignedPosition ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.Department ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.YerleshmeYeri ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.Erazi ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.Status ?? string.Empty;
                worksheet.Cell(row, column++).Value = asset.LifecycleStatus.ToString();
                worksheet.Cell(row, column++).Value = asset.PurchaseCost;

                if (asset.PurchaseDate > DateTime.MinValue)
                {
                    worksheet.Cell(row, column).Value = asset.PurchaseDate;
                    worksheet.Cell(row, column).Style.DateFormat.Format = "dd.MM.yyyy";
                }
                column++;

                if (asset.UsefulLifeInYears > 0)
                    worksheet.Cell(row, column).Value = asset.UsefulLifeInYears;
                column++;

                if (asset.EndOfLifeDate.HasValue)
                {
                    worksheet.Cell(row, column).Value = asset.EndOfLifeDate.Value;
                    worksheet.Cell(row, column).Style.DateFormat.Format = "dd.MM.yyyy";
                }
                column++;

                worksheet.Cell(row, column++).Value = asset.IsEndOfLife ? "Bəli" : "Xeyr";
                worksheet.Cell(row, column++).Value = asset.AnnualDepreciation;
                worksheet.Cell(row, column++).Value = asset.MonthlyDepreciation;
                worksheet.Cell(row, column++).Value = asset.TotalDepreciation;
                worksheet.Cell(row, column++).Value = asset.CurrentValue;
                worksheet.Cell(row, column++).Value = asset.Supplier ?? string.Empty;

                if (asset.WarrantyExpirationDate > DateTime.MinValue)
                {
                    worksheet.Cell(row, column).Value = asset.WarrantyExpirationDate;
                    worksheet.Cell(row, column).Style.DateFormat.Format = "dd.MM.yyyy";
                }
                column++;

                var maintenance = asset.MaintenanceHistory ?? new List<MaintenanceRecord>();
                worksheet.Cell(row, column++).Value = maintenance.Count;
                worksheet.Cell(row, column++).Value = maintenance.Sum(record => record.Cost);

                foreach (string customFieldName in customFieldNames)
                {
                    string value = string.Empty;
                    if (asset.CustomFields != null)
                    {
                        var match = asset.CustomFields.FirstOrDefault(pair =>
                            string.Equals(
                                pair.Key,
                                customFieldName,
                                StringComparison.OrdinalIgnoreCase));
                        value = match.Value ?? string.Empty;
                    }

                    worksheet.Cell(row, column++).Value = value;
                }

                row++;
            }

            int lastRow = row - 1;

            foreach (int moneyColumn in new[] { 15, 20, 21, 22, 23, 27 })
            {
                worksheet.Range(headerRow + 1, moneyColumn, lastRow, moneyColumn)
                    .Style.NumberFormat.Format = "#,##0.00";
            }

            worksheet.Range(headerRow, 1, lastRow, totalColumns).SetAutoFilter();
            worksheet.SheetView.FreezeRows(headerRow);
            worksheet.SheetView.FreezeColumns(3);
            worksheet.RangeUsed().Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            worksheet.Columns().AdjustToContents();
            foreach (var usedColumn in worksheet.ColumnsUsed())
            {
                if (usedColumn.Width > 45)
                    usedColumn.Width = 45;
            }
        }

        private static void WriteAssignmentHistorySheet(
            XLWorkbook workbook,
            IReadOnlyList<Asset> assets)
        {
            var worksheet = workbook.Worksheets.Add("Təhkim Tarixçəsi");
            string[] headers =
            {
                "Asset ID",
                "Vəsaitin Kodu",
                "Vəsaitin Adı",
                "History ID",
                "Əməliyyat",
                "Kimdən Alındı",
                "Kimə Verildi",
                "Dəyişikliyi Edən",
                "Dəyişiklik Tarixi"
            };

            for (int i = 0; i < headers.Length; i++)
                worksheet.Cell(1, i + 1).Value = headers[i];

            StyleDetailSheetHeader(worksheet, headers.Length);

            int row = 2;
            foreach (var asset in assets)
            {
                foreach (var entry in asset.History?
                    .OrderBy(item => item.ChangeDate)
                    ?? Enumerable.Empty<AssignmentHistoryEntry>())
                {
                    worksheet.Cell(row, 1).Value = asset.Id;
                    worksheet.Cell(row, 2).Value = asset.VesaitinKodu ?? string.Empty;
                    worksheet.Cell(row, 3).Value = asset.VesaitinAdi ?? string.Empty;
                    worksheet.Cell(row, 4).Value = entry.Id;
                    worksheet.Cell(row, 5).Value = entry.Action switch
                    {
                        AssignmentAction.Assigned => "Təhkim edilib",
                        AssignmentAction.Reassigned => "Yenidən təhkim edilib",
                        AssignmentAction.Unassigned => "Təhkim ləğv edilib",
                        _ => entry.Action.ToString()
                    };
                    worksheet.Cell(row, 6).Value = entry.FromWorkerName ?? string.Empty;
                    worksheet.Cell(row, 7).Value = entry.ToWorkerName ?? string.Empty;
                    worksheet.Cell(row, 8).Value = entry.ChangedBy ?? string.Empty;
                    worksheet.Cell(row, 9).Value = entry.ChangeDate;
                    worksheet.Cell(row, 9).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
                    row++;
                }
            }

            FinalizeDetailSheet(worksheet);
        }

        private static void WriteMaintenanceSheet(
            XLWorkbook workbook,
            IReadOnlyList<Asset> assets)
        {
            var worksheet = workbook.Worksheets.Add("Texniki Xidmət");
            string[] headers =
            {
                "Asset ID",
                "Vəsaitin Kodu",
                "Vəsaitin Adı",
                "Qeyd ID",
                "Tarix",
                "Növ",
                "Açıqlama",
                "Xərc",
                "İcra edən"
            };

            for (int i = 0; i < headers.Length; i++)
                worksheet.Cell(1, i + 1).Value = headers[i];

            StyleDetailSheetHeader(worksheet, headers.Length);

            int row = 2;
            foreach (var asset in assets)
            {
                foreach (var record in asset.MaintenanceHistory?
                    .OrderBy(item => item.MaintenanceDate)
                    ?? Enumerable.Empty<MaintenanceRecord>())
                {
                    worksheet.Cell(row, 1).Value = asset.Id;
                    worksheet.Cell(row, 2).Value = asset.VesaitinKodu ?? string.Empty;
                    worksheet.Cell(row, 3).Value = asset.VesaitinAdi ?? string.Empty;
                    worksheet.Cell(row, 4).Value = record.Id.ToString();
                    worksheet.Cell(row, 5).Value = record.MaintenanceDate;
                    worksheet.Cell(row, 5).Style.DateFormat.Format = "dd.MM.yyyy";
                    worksheet.Cell(row, 6).Value = record.MaintenanceType.ToString();
                    worksheet.Cell(row, 7).Value = record.Description ?? string.Empty;
                    worksheet.Cell(row, 8).Value = record.Cost;
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(row, 9).Value = record.PerformedBy ?? string.Empty;
                    row++;
                }
            }

            FinalizeDetailSheet(worksheet);
        }

        private static void StyleDetailSheetHeader(IXLWorksheet worksheet, int columnCount)
        {
            var headerRange = worksheet.Range(1, 1, 1, columnCount);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Font.FontColor = XLColor.White;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1F4E78");
        }

        private static void FinalizeDetailSheet(IXLWorksheet worksheet)
        {
            var range = worksheet.RangeUsed();
            if (range == null) return;

            if (range.RowCount() > 1)
                worksheet.Range(1, 1, range.LastRow().RowNumber(), range.LastColumn().ColumnNumber())
                    .SetAutoFilter();

            worksheet.SheetView.FreezeRows(1);
            worksheet.Columns().AdjustToContents();

            foreach (var usedColumn in worksheet.ColumnsUsed())
            {
                if (usedColumn.Width > 45)
                    usedColumn.Width = 45;
            }
        }

        private static int ResolveRequiredColumn(
            IReadOnlyDictionary<string, int> columns,
            params string[] aliases)
        {
            if (TryResolveColumn(columns, out int column, aliases))
                return column;

            throw new InvalidOperationException(
                $"Excel faylında tələb olunan sütun tapılmadı: {string.Join(" / ", aliases)}");
        }

        private static bool TryResolveColumn(
            IReadOnlyDictionary<string, int> columns,
            out int column,
            params string[] aliases)
        {
            foreach (string alias in aliases)
            {
                if (columns.TryGetValue(alias, out column))
                    return true;
            }

            column = 0;
            return false;
        }
    }
}
