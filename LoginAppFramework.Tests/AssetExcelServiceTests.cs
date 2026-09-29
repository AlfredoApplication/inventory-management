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
