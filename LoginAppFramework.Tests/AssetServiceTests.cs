using System.Linq;
using LoginAppFramework;
using Xunit;

namespace LoginAppFramework.Tests;

public class AssetServiceTests
{
    [Fact]
    public void ApplyAssignment_RecordsAssignmentHistory()
    {
        var service = new AssetService();
        var asset = new Asset { Status = "Anbarda" };
        var worker = new Worker
        {
            Id = 5,
            per_adiper_soyadi = "A Worker",
            pgk_gorev_adi = "Specialist",
            pdp_adi = "Finance"
        };

        service.ApplyAssignment(asset, worker, "Unit test");

        var history = Assert.Single(asset.History);
        Assert.Equal(AssignmentAction.Assigned, history.Action);
        Assert.Equal("Unit test", history.FromWorkerName);
        Assert.Equal("A Worker", history.ToWorkerName);
        Assert.Equal(5, asset.WorkerId);
    }

    [Fact]
    public void ApplyReassignment_RecordsPreviousWorker()
    {
        var service = new AssetService();
        var first = new Worker { Id = 1, per_adiper_soyadi = "First" };
        var second = new Worker { Id = 2, per_adiper_soyadi = "Second" };
        var asset = new Asset();
        asset.AssignWorker(first);

        service.ApplyAssignment(asset, second, "Bulk edit");

        var history = Assert.Single(asset.History);
        Assert.Equal(AssignmentAction.Reassigned, history.Action);
        Assert.Equal("First", history.FromWorkerName);
        Assert.Equal("Second", history.ToWorkerName);
        Assert.Equal(2, asset.WorkerId);
    }

    [Fact]
    public void ApplyUnassignment_RecordsPreviousWorkerAndClearsRelation()
    {
        var service = new AssetService();
        var worker = new Worker { Id = 1, per_adiper_soyadi = "Assigned Worker" };
        var asset = new Asset();
        asset.AssignWorker(worker);

        service.ApplyUnassignment(asset, "Unit test");

        Assert.Null(asset.WorkerId);
        Assert.Equal("Anbarda", asset.Status);
        var history = Assert.Single(asset.History);
        Assert.Equal(AssignmentAction.Unassigned, history.Action);
        Assert.Equal("Assigned Worker", history.FromWorkerName);
    }
}
