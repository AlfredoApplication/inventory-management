using System.Linq;
using LoginAppFramework;
using Xunit;

namespace LoginAppFramework.Tests;

public class WorkerSelectionResolverTests
{
    [Fact]
    public void TypedExactWorkerName_ResolvesWorkerEvenWithoutSelectedItem()
    {
        var worker = new Worker
        {
            Id = 10,
            per_adiper_soyadi = "Aysel Məmmədova"
        };

        var options = new[]
        {
            WorkerSelectionOption.Clear(),
            WorkerSelectionOption.ForWorker(worker)
        };

        var result = WorkerSelectionResolver.Resolve(
            "Aysel Məmmədova",
            selectedOption: null,
            options,
            WorkerSelectionKind.ClearAssignment);

        Assert.True(result.IsValid);
        Assert.Equal(WorkerSelectionKind.Worker, result.Kind);
        Assert.Same(worker, result.Worker);
    }

    [Fact]
    public void UnknownTypedWorker_DoesNotSilentlyClearAssignment()
    {
        var worker = new Worker
        {
            Id = 10,
            per_adiper_soyadi = "Aysel Məmmədova"
        };

        var options = new[]
        {
            WorkerSelectionOption.Clear(),
            WorkerSelectionOption.ForWorker(worker)
        };

        var result = WorkerSelectionResolver.Resolve(
            "Mövcud Olmayan İşçi",
            selectedOption: null,
            options,
            WorkerSelectionKind.ClearAssignment);

        Assert.False(result.IsValid);
        Assert.Equal(WorkerSelectionKind.Invalid, result.Kind);
        Assert.NotEmpty(result.ErrorMessage);
    }

    [Fact]
    public void BlankBulkWorkerSelection_MeansNoChange()
    {
        var result = WorkerSelectionResolver.Resolve(
            string.Empty,
            selectedOption: null,
            new[] { WorkerSelectionOption.NoChange(), WorkerSelectionOption.Clear() },
            WorkerSelectionKind.NoChange);

        Assert.True(result.IsValid);
        Assert.Equal(WorkerSelectionKind.NoChange, result.Kind);
    }

    [Fact]
    public void DuplicateWorkerNames_RequireExplicitSelection()
    {
        var first = new Worker { Id = 1, per_adiper_soyadi = "Eyni Ad" };
        var second = new Worker { Id = 2, per_adiper_soyadi = "Eyni Ad" };

        var options = new[]
        {
            WorkerSelectionOption.ForWorker(first),
            WorkerSelectionOption.ForWorker(second)
        };

        var result = WorkerSelectionResolver.Resolve(
            "Eyni Ad",
            selectedOption: null,
            options,
            WorkerSelectionKind.ClearAssignment);

        Assert.False(result.IsValid);
        Assert.Equal(WorkerSelectionKind.Invalid, result.Kind);
    }
}

public class ColumnFilterSnapshotTests
{
    [Fact]
    public void RestoreSnapshot_RevertsSelectionsAndSearchText()
    {
        var filter = new ColumnFilterViewModel(
            new[] { "Bakı", "Gəncə", "Sumqayıt" });

        filter.SearchText = "Bakı";
        filter.FilteredItems.Single().IsChecked = false;

        var snapshot = filter.CreateSnapshot();

        filter.SearchText = "Gəncə";
        filter.SelectAll();
        filter.ClearAll();

        filter.RestoreSnapshot(snapshot);

        Assert.Equal("Bakı", filter.SearchText);
        Assert.True(filter.HasActiveFilters);
        Assert.DoesNotContain("Bakı", filter.GetSelectedValues());
        Assert.Contains("Gəncə", filter.GetSelectedValues());
        Assert.Contains("Sumqayıt", filter.GetSelectedValues());
    }

    [Fact]
    public void SnapshotApplyState_CanRemainChangedWhenNotRestored()
    {
        var filter = new ColumnFilterViewModel(
            new[] { "Aktiv", "Qeyri-aktiv" });

        var snapshot = filter.CreateSnapshot();

        filter.FilteredItems
            .Single(item => item.Value == "Qeyri-aktiv")
            .IsChecked = false;

        Assert.True(filter.HasActiveFilters);
        Assert.DoesNotContain(
            "Qeyri-aktiv",
            filter.GetSelectedValues());

        Assert.NotNull(snapshot);
    }
}
