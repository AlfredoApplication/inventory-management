using System;
using LoginAppFramework;
using Xunit;

namespace LoginAppFramework.Tests;

public class AssetTests
{
    [Fact]
    public void UsefulLifeZero_IsUnspecifiedAndNotExpired()
    {
        var asset = new Asset
        {
            PurchaseDate = DateTime.Today.AddYears(-10),
            PurchaseCost = 1200m,
            UsefulLifeInYears = 0
        };

        Assert.False(asset.HasUsefulLife);
        Assert.Null(asset.EndOfLifeDate);
        Assert.False(asset.IsEndOfLife);
        Assert.Equal("Təyin edilməyib", asset.UsefulLifeDisplay);
        Assert.Equal(1200m, asset.CurrentValue);
    }

    [Fact]
    public void PositiveUsefulLife_ComputesEndOfLife()
    {
        var purchaseDate = new DateTime(2020, 5, 10);
        var asset = new Asset
        {
            PurchaseDate = purchaseDate,
            PurchaseCost = 6000m,
            UsefulLifeInYears = 5
        };

        Assert.True(asset.HasUsefulLife);
        Assert.Equal(new DateTime(2025, 5, 10), asset.EndOfLifeDate);
        Assert.Equal(1200m, asset.AnnualDepreciation);
        Assert.Equal(100m, asset.MonthlyDepreciation);
    }

    [Fact]
    public void AssignWorker_UpdatesSourceOfTruthAndLegacySnapshots()
    {
        var worker = new Worker
        {
            Id = 42,
            per_adiper_soyadi = "Test Worker",
            pgk_gorev_adi = "Engineer",
            pdp_adi = "IT"
        };
        var asset = new Asset { Status = "Anbarda" };

        asset.AssignWorker(worker);

        Assert.Equal(42, asset.WorkerId);
        Assert.Same(worker, asset.Worker);
        Assert.Equal("Test Worker", asset.AssignedUser);
        Assert.Equal("Engineer", asset.AssignedPosition);
        Assert.Equal("IT", asset.Department);
        Assert.Equal("Test Worker", asset.LegacyAssignedWorkerName);
        Assert.Equal("Engineer", asset.LegacyAssignedWorkerPosition);
        Assert.Equal("IT", asset.LegacyAssignedWorkerDepartment);
        Assert.Equal("İstifadədədir", asset.Status);
    }

    [Fact]
    public void ClearWorkerAssignment_ClearsWorkerAndReturnsAssignedAssetToWarehouse()
    {
        var worker = new Worker { Id = 7, per_adiper_soyadi = "Worker" };
        var asset = new Asset();
        asset.AssignWorker(worker);

        asset.ClearWorkerAssignment();

        Assert.Null(asset.WorkerId);
        Assert.Null(asset.Worker);
        Assert.Null(asset.LegacyAssignedWorkerName);
        Assert.Null(asset.LegacyAssignedWorkerPosition);
        Assert.Null(asset.LegacyAssignedWorkerDepartment);
        Assert.Equal("Anbarda", asset.Status);
    }
}
