using System;
using System.IO;
using ClosedXML.Excel;
using LoginAppFramework;
using Xunit;

namespace LoginAppFramework.Tests;

public class AssetExcelServiceTests
{
    [Fact]
    public void ParseImport_ReadsCoreFieldsAndWorkerName()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Assets");
                sheet.Cell(1, 1).Value = "Vəsaitin Adı";
                sheet.Cell(1, 2).Value = "Vəsaitin Kodu";
                sheet.Cell(1, 3).Value = "Təhkim Olunan Əməkdaş";
                sheet.Cell(1, 4).Value = "Alış qiyməti";
                sheet.Cell(1, 5).Value = "İstifadə müddəti (İl)";

                sheet.Cell(2, 1).Value = "Notebook";
                sheet.Cell(2, 2).Value = "NB-1";
                sheet.Cell(2, 3).Value = "Worker One";
                sheet.Cell(2, 4).Value = 1999.99m;
                sheet.Cell(2, 5).Value = 3;

                workbook.SaveAs(path);
            }

            var service = new AssetExcelService();
            var batch = service.ParseImport(path);

            var asset = Assert.Single(batch.Assets);
            Assert.Empty(batch.Errors);
            Assert.Equal("Notebook", asset.VesaitinAdi);
            Assert.Equal("NB-1", asset.VesaitinKodu);
            Assert.Equal(1999.99m, asset.PurchaseCost);
            Assert.Equal(3, asset.UsefulLifeInYears);
            Assert.Equal("Worker One", batch.RequestedWorkerNames[asset]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ParseImport_MissingNameColumnThrows()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            using (var workbook = new XLWorkbook())
            {
                var sheet = workbook.Worksheets.Add("Assets");
                sheet.Cell(1, 1).Value = "Vəsaitin Kodu";
                sheet.Cell(2, 1).Value = "A-1";
                workbook.SaveAs(path);
            }

            var service = new AssetExcelService();

            Assert.Throws<InvalidOperationException>(() => service.ParseImport(path));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}


public class PurchaseDateExcelReportTests
{
    [Fact]
    public void ExportPurchaseDateReport_FiltersInclusivelyAndExportsFullDetails()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");

        try
        {
            var included = new Asset
            {
                Id = 10,
                VesaitinKodu = "A-2025",
                VesaitinAdi = "Included Asset",
                ITAvadanliqlarininSeriyaNomresi = "SERIAL-25",
                Kateqoriya = "Notebook",
                YerleshmeYeri = "HQ",
                Erazi = "Bakı",
                Status = "Anbarda",
                PurchaseCost = 2500m,
                PurchaseDate = new DateTime(2025, 6, 15),
                UsefulLifeInYears = 4,
                Supplier = "Supplier A",
                WarrantyExpirationDate = new DateTime(2027, 6, 15),
                CustomFields = new Dictionary<string, string>
                {
                    ["RAM"] = "32 GB",
                    ["CPU"] = "Core i7"
                },
                MaintenanceHistory = new List<MaintenanceRecord>
                {
                    new()
                    {
                        MaintenanceDate = new DateTime(2025, 7, 1),
                        MaintenanceType = MaintenanceType.Inspection,
                        Description = "Yoxlama",
                        Cost = 25m,
                        PerformedBy = "IT Team"
                    }
                },
                History = new List<AssignmentHistoryEntry>
                {
                    new()
                    {
                        Action = AssignmentAction.Assigned,
                        FromWorkerName = "Anbar",
                        ToWorkerName = "Worker A",
                        ChangedBy = "Admin",
                        ChangeDate = new DateTime(2025, 6, 20)
                    }
                }
            };

            var excluded = new Asset
            {
                Id = 11,
                VesaitinKodu = "A-2024",
                VesaitinAdi = "Excluded Asset",
                PurchaseDate = new DateTime(2024, 12, 31)
            };

            var service = new AssetExcelService();

            int count = service.ExportPurchaseDateReport(
                path,
                new[] { included, excluded },
                new DateTime(2025, 1, 1),
                new DateTime(2025, 12, 31));

            Assert.Equal(1, count);

            using var workbook = new XLWorkbook(path);

            var assetsSheet = workbook.Worksheet("Vəsaitlər");
            Assert.Equal("A-2025", assetsSheet.Cell(6, 2).GetString());
            Assert.Equal("Included Asset", assetsSheet.Cell(6, 3).GetString());

            var headers = assetsSheet.Row(5)
                .CellsUsed()
                .Select(cell => cell.GetString())
                .ToList();

            Assert.Contains("Təchizatçı", headers);
            Assert.Contains("Cari Dəyər", headers);
            Assert.Contains("Texniki Xidmət Sayı", headers);
            Assert.Contains("Custom: RAM", headers);
            Assert.Contains("Custom: CPU", headers);

            var historySheet = workbook.Worksheet("Təhkim Tarixçəsi");
            Assert.Equal("A-2025", historySheet.Cell(2, 2).GetString());

            var maintenanceSheet = workbook.Worksheet("Texniki Xidmət");
            Assert.Equal("A-2025", maintenanceSheet.Cell(2, 2).GetString());
            Assert.Equal("Yoxlama", maintenanceSheet.Cell(2, 7).GetString());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void ExportPurchaseDateReport_InvalidDateRangeThrows()
    {
        var service = new AssetExcelService();

        Assert.Throws<InvalidOperationException>(() =>
            service.ExportPurchaseDateReport(
                "report.xlsx",
                new[] { new Asset { PurchaseDate = DateTime.Today } },
                new DateTime(2025, 12, 31),
                new DateTime(2025, 1, 1)));
    }
}
