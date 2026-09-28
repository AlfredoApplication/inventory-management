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
