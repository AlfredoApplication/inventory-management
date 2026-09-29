using System;
using System.Text.Json;
using LoginAppFramework;
using Xunit;

namespace LoginAppFramework.Tests;

public class AssetAuditSnapshotTests
{
    [Fact]
    public void DeletedSnapshot_RestoresExtendedAssetFields()
    {
        var details = JsonSerializer.Serialize(new
        {
            VesaitinKodu = "INV-001",
            VesaitinAdi = "Laptop",
            ITAvadanliqlarininSeriyaNomresi = "SN-1",
            Kateqoriya = "Notebook",
            YerleshmeYeri = "HQ",
            Erazi = "Bakı",
            Status = "İstifadədədir",
            PurchaseCost = 2500.50m,
            PurchaseDate = "2024-01-15T00:00:00",
            UsefulLifeInYears = 4,
            Supplier = "Supplier A",
            WarrantyExpirationDate = "2027-01-15T00:00:00",
            WorkerId = 77,
            TehkimOlunanEmekdas = "Worker Name",
            CustomFields = JsonSerializer.Serialize(new { CPU = "i7" })
        });

        var log = new AssetLog
        {
            status = "Silinən",
            VesaitinKodu = "fallback-code",
            ChangeDetails = details
        };

        var snapshot = AssetAuditSnapshot.FromDeletedLog(log);

        Assert.Equal("INV-001", snapshot.Asset.VesaitinKodu);
        Assert.Equal("Laptop", snapshot.Asset.VesaitinAdi);
        Assert.Equal(2500.50m, snapshot.Asset.PurchaseCost);
        Assert.Equal(new DateTime(2024, 1, 15), snapshot.Asset.PurchaseDate);
        Assert.Equal(4, snapshot.Asset.UsefulLifeInYears);
        Assert.Equal("Supplier A", snapshot.Asset.Supplier);
        Assert.Equal(new DateTime(2027, 1, 15), snapshot.Asset.WarrantyExpirationDate);
        Assert.Equal(77, snapshot.WorkerId);
        Assert.Equal("Worker Name", snapshot.WorkerName);
        Assert.Equal("i7", snapshot.Asset.CustomFields["CPU"]);
    }

    [Fact]
    public void MissingSnapshotFields_FallsBackToLegacyLogColumns()
    {
        var log = new AssetLog
        {
            status = "Silinən",
            VesaitinKodu = "LEG-1",
            VesaitinAdi = "Legacy Asset",
            Kateqoriya = "Legacy Category",
            ChangeDetails = "{}"
        };

        var snapshot = AssetAuditSnapshot.FromDeletedLog(log);

        Assert.Equal("LEG-1", snapshot.Asset.VesaitinKodu);
        Assert.Equal("Legacy Asset", snapshot.Asset.VesaitinAdi);
        Assert.Equal("Legacy Category", snapshot.Asset.Kateqoriya);
        Assert.Equal("Anbarda", snapshot.Asset.Status);
    }
}
