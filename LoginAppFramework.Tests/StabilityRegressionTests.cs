using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using LoginAppFramework;
using Xunit;

namespace LoginAppFramework.Tests;

public class AssetFormValidatorTests
{
    [Theory]
    [InlineData("1250,50", "1250.50")]
    [InlineData("1250.50", "1250.50")]
    [InlineData("0", "0")]
    [InlineData("", "0")]
    public void PurchaseCost_AcceptsValidNonNegativeValues(
        string text,
        string expectedText)
    {
        bool valid = AssetFormValidator.TryParsePurchaseCost(
            text,
            out decimal value,
            out string error);

        Assert.True(valid);
        Assert.Null(error);
        Assert.Equal(
            decimal.Parse(
                expectedText,
                System.Globalization.CultureInfo.InvariantCulture),
            value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("12,5,7")]
    public void PurchaseCost_RejectsInvalidValues(string text)
    {
        bool valid = AssetFormValidator.TryParsePurchaseCost(
            text,
            out _,
            out string error);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("3", 3)]
    [InlineData("", 0)]
    public void UsefulLife_AcceptsWholeNonNegativeYears(
        string text,
        int expected)
    {
        bool valid = AssetFormValidator.TryParseUsefulLife(
            text,
            out int value,
            out string error);

        Assert.True(valid);
        Assert.Null(error);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2.5")]
    [InlineData("abc")]
    public void UsefulLife_RejectsInvalidYears(string text)
    {
        bool valid = AssetFormValidator.TryParseUsefulLife(
            text,
            out _,
            out string error);

        Assert.False(valid);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void WarrantyDate_CannotPrecedePurchaseDate()
    {
        bool valid = AssetFormValidator.ValidateDates(
            new DateTime(2026, 9, 30),
            new DateTime(2026, 9, 29),
            out string error);

        Assert.False(valid);
        Assert.Contains("əvvəl", error);
    }

    [Fact]
    public void WarrantyDate_CanMatchOrFollowPurchaseDate()
    {
        bool sameDay = AssetFormValidator.ValidateDates(
            new DateTime(2026, 9, 30),
            new DateTime(2026, 9, 30),
            out _);

        bool later = AssetFormValidator.ValidateDates(
            new DateTime(2026, 9, 30),
            new DateTime(2027, 9, 30),
            out _);

        Assert.True(sameDay);
        Assert.True(later);
    }
}

public class Phase9RegressionTests
{
    [Fact]
    public void FuzzySearch_PrefersExactAndAcceptsTranspositionTypo()
    {
        int exact = FuzzySearchHelper.Score(
            "Laptop Dell",
            "Laptop Dell Latitude",
            "laptop");

        int typo = FuzzySearchHelper.Score(
            "Laptop Dell",
            "Laptop Dell Latitude",
            "laptpo");

        int unrelated = FuzzySearchHelper.Score(
            "Laptop Dell",
            "Laptop Dell Latitude",
            "printer");

        Assert.True(exact < typo);
        Assert.NotEqual(int.MaxValue, typo);
        Assert.Equal(int.MaxValue, unrelated);
    }

    [Fact]
    public void BulkPreview_StatusOnlySkipsAssignedAssets()
    {
        var assigned = new Asset
        {
            Id = 1,
            VesaitinKodu = "A-1",
            VesaitinAdi = "Assigned",
            WorkerId = 10,
            Status = "İstifadədədir"
        };

        var unassigned = new Asset
        {
            Id = 2,
            VesaitinKodu = "A-2",
            VesaitinAdi = "Free",
            WorkerId = null,
            Status = "Anbarda"
        };

        var preview = BulkActionPreviewBuilder.ForEdit(
            new[] { assigned, unassigned },
            new BulkAssetChanges
            {
                Status = "Təmirə göndərilib"
            });

        Assert.Equal(2, preview.SelectedCount);
        Assert.Equal(1, preview.EstimatedUpdatedCount);
        Assert.Single(preview.SkippedStatusAssets);
        Assert.Single(preview.Changes);
    }

    [Fact]
    public void ImportPreview_ExcludesExistingCodeDuplicate()
    {
        var duplicate = new Asset
        {
            VesaitinKodu = "IT-001",
            VesaitinAdi = "Duplicate",
            PurchaseDate = DateTime.Today
        };

        var unique = new Asset
        {
            VesaitinKodu = "IT-002",
            VesaitinAdi = "Unique",
            PurchaseDate = DateTime.Today
        };

        var batch = new AssetImportBatch
        {
            TotalRows = 2
        };
        batch.Assets.Add(duplicate);
        batch.Assets.Add(unique);
        batch.SourceRows[duplicate] = 2;
        batch.SourceRows[unique] = 3;

        var preview = AssetImportPreviewBuilder.Build(
            batch,
            new[]
            {
                new Asset
                {
                    VesaitinKodu = "IT-001",
                    VesaitinAdi = "Existing"
                }
            },
            new Dictionary<string, Worker>(
                StringComparer.OrdinalIgnoreCase));

        Assert.Equal(2, preview.Items.Count);
        Assert.Equal(1, preview.ErrorCount);
        Assert.Single(preview.AssetsToImport());
        Assert.Equal(
            "IT-002",
            preview.AssetsToImport()[0].VesaitinKodu);
    }

    [Fact]
    public void ImportPreview_UnmappedWorkerIsWarningButImportable()
    {
        var asset = new Asset
        {
            VesaitinKodu = "IT-003",
            VesaitinAdi = "Laptop",
            PurchaseDate = DateTime.Today
        };

        var batch = new AssetImportBatch
        {
            TotalRows = 1
        };
        batch.Assets.Add(asset);
        batch.SourceRows[asset] = 2;
        batch.RequestedWorkerNames[asset] = "Unknown Worker";

        var preview = AssetImportPreviewBuilder.Build(
            batch,
            Array.Empty<Asset>(),
            new Dictionary<string, Worker>(
                StringComparer.OrdinalIgnoreCase));

        Assert.Single(preview.Items);
        Assert.Equal(
            ImportPreviewSeverity.Warning,
            preview.Items[0].Severity);
        Assert.True(preview.Items[0].CanImport);
        Assert.Single(preview.AssetsToImport());
    }
}

public class Phase10AlertRegressionTests
{
    [Fact]
    public void WarrantyAlert_AppearsWithinDefaultThirtyDayWindow()
    {
        DateTime today = new DateTime(2026, 9, 30);

        var asset = new Asset
        {
            Id = 10,
            VesaitinKodu = "IT-010",
            VesaitinAdi = "Laptop",
            Status = "Anbarda",
            PurchaseDate = today.AddYears(-1),
            WarrantyExpirationDate = today.AddDays(20)
        };

        var alerts = AlertService.BuildAlerts(
            new[] { asset },
            Array.Empty<AlertRule>(),
            today);

        var warranty = Assert.Single(
            alerts.Where(alert =>
                alert.Category == AlertCategory.Warranty));

        Assert.Equal(AlertType.Warning, warranty.Type);
        Assert.Contains("20 gün", warranty.Message);
        Assert.Equal(asset.Id, warranty.TargetId);
    }

    [Fact]
    public void ExpiredWarranty_IsCritical()
    {
        DateTime today = new DateTime(2026, 9, 30);

        var asset = new Asset
        {
            Id = 11,
            VesaitinAdi = "Monitor",
            Status = "İstifadədədir",
            PurchaseDate = today.AddYears(-2),
            WarrantyExpirationDate = today.AddDays(-5)
        };

        var alerts = AlertService.BuildAlerts(
            new[] { asset },
            Array.Empty<AlertRule>(),
            today);

        var warranty = Assert.Single(
            alerts.Where(alert =>
                alert.Category == AlertCategory.Warranty));

        Assert.Equal(AlertType.Critical, warranty.Type);
        Assert.Contains("5 gün əvvəl", warranty.Message);
    }

    [Fact]
    public void MaintenanceAlert_UsesLastMaintenanceAsBaseline()
    {
        DateTime today = new DateTime(2026, 9, 30);

        var asset = new Asset
        {
            Id = 12,
            VesaitinAdi = "Printer",
            Status = "Anbarda",
            PurchaseDate = today.AddYears(-3),
            MaintenanceHistory = new List<MaintenanceRecord>
            {
                new()
                {
                    MaintenanceDate = today.AddDays(-30),
                    MaintenanceType = MaintenanceType.Inspection
                }
            }
        };

        var alerts = AlertService.BuildAlerts(
            new[] { asset },
            Array.Empty<AlertRule>(),
            today);

        Assert.DoesNotContain(
            alerts,
            alert => alert.Category == AlertCategory.Maintenance);
    }

    [Fact]
    public void MaintenanceRuleThresholdOverridesDefault()
    {
        DateTime today = new DateTime(2026, 9, 30);

        var asset = new Asset
        {
            Id = 13,
            VesaitinAdi = "Router",
            Status = "Anbarda",
            PurchaseDate = today.AddDays(-100)
        };

        var rules = new[]
        {
            new AlertRule
            {
                RuleName = "Texniki xidmət intervalı",
                IsEnabled = true,
                ThresholdValue = 90
            }
        };

        var alerts = AlertService.BuildAlerts(
            new[] { asset },
            rules,
            today);

        Assert.Contains(
            alerts,
            alert => alert.Category == AlertCategory.Maintenance);
    }
}

public class Phase10RecycleBinRegressionTests
{
    [Fact]
    public void SoftDeleteMetadata_RestoresWithoutChangingAssetState()
    {
        DateTime deletedAt =
            new DateTime(
                2026,
                9,
                30,
                10,
                0,
                0,
                DateTimeKind.Utc);

        var asset = new Asset
        {
            Id = 21,
            VesaitinKodu = "IT-021",
            VesaitinAdi = "Laptop",
            Status = "İstifadədədir",
            WorkerId = 99
        };

        AssetDeletionMetadata.MarkDeleted(
            asset,
            "Admin User",
            deletedAt);

        Assert.True(
            AssetDeletionMetadata.IsDeleted(asset));

        Assert.Equal(
            "Admin User",
            AssetDeletionMetadata.GetDeletedBy(asset));

        Assert.Equal(
            "İstifadədədir",
            asset.Status);

        Assert.Equal(99, asset.WorkerId);

        AssetDeletionMetadata.Restore(asset);

        Assert.False(
            AssetDeletionMetadata.IsDeleted(asset));

        Assert.Equal(
            "İstifadədədir",
            asset.Status);

        Assert.Equal(99, asset.WorkerId);
    }

    [Fact]
    public void SoftDeleteMetadata_UsesThirtyDayRetention()
    {
        DateTime deletedAt =
            new DateTime(
                2026,
                9,
                1,
                12,
                0,
                0,
                DateTimeKind.Utc);

        var asset = new Asset
        {
            Id = 22
        };

        AssetDeletionMetadata.MarkDeleted(
            asset,
            "Admin",
            deletedAt);

        Assert.Equal(
            1,
            AssetDeletionMetadata.GetDaysRemaining(
                asset,
                deletedAt.AddDays(29)));

        Assert.True(
            AssetDeletionMetadata.ShouldPurge(
                asset,
                deletedAt.AddDays(30)));
    }

    [Fact]
    public void DuplicateDetector_IgnoresCurrentAssetDuringEdit()
    {
        var asset = new Asset
        {
            Id = 23,
            VesaitinKodu = "IT-023",
            ITAvadanliqlarininSeriyaNomresi = "SN-023"
        };

        Assert.Null(
            AssetDuplicateDetector.FindCodeConflict(
                new[] { asset },
                " IT-023 ",
                asset.Id));

        Assert.Null(
            AssetDuplicateDetector.FindSerialConflict(
                new[] { asset },
                "sn-023",
                asset.Id));
    }

    [Fact]
    public void DuplicateDetector_ReservesDeletedAssetIdentity()
    {
        var deleted = new Asset
        {
            Id = 24,
            VesaitinKodu = "IT-024",
            VesaitinAdi = "Deleted laptop",
            ITAvadanliqlarininSeriyaNomresi = "SN-024"
        };

        AssetDeletionMetadata.MarkDeleted(
            deleted,
            "Admin",
            DateTime.UtcNow);

        var codeConflict =
            AssetDuplicateDetector.FindCodeConflict(
                new[] { deleted },
                "it-024");

        var serialConflict =
            AssetDuplicateDetector.FindSerialConflict(
                new[] { deleted },
                "SN-024");

        Assert.NotNull(codeConflict);
        Assert.True(codeConflict.IsDeleted);
        Assert.Contains("Silinənlər", codeConflict.Message);

        Assert.NotNull(serialConflict);
        Assert.True(serialConflict.IsDeleted);
    }
}

[CollectionDefinition("WPF smoke tests", DisableParallelization = true)]
public sealed class WpfSmokeTestCollection
{
}

[Collection("WPF smoke tests")]
public class WpfXamlSmokeTests
{
    [Fact]
    public void MainWindows_ConstructWithoutRuntimeXamlParseErrors()
    {
        Exception captured = null;

        var thread = new Thread(() =>
        {
            try
            {
                if (Application.Current == null)
                {
                    var app = new App();
                    app.InitializeComponent();
                    app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                }

                var windows = new List<Window>
                {
                    new MainWindow(),
                    new DashboardWindow(),
                    new AssetWindow(),
                    new WorkerListWindow(),
                    new HistoryLogWindow(),
                    new LifecycleReportWindow(),
                    new GlobalSearchWindow(),
                    new ReportsWindow(),
                    new AddEditAssetWindow(new Asset()),
                    new AddEditWorkerWindow(),
                    new BulkEditWindow(1),
                    new SelectAssetWindow(new List<Asset>()),
                    new SelectWorkerWindow(new List<Worker>()),
                    new FilteredAssetsWindow(
                        "XAML smoke test",
                        new List<Asset>()),
                    new AppDialogWindow(
                        null,
                        "Unsaved smoke test",
                        "Unsaved changes",
                        AppDialogType.Warning,
                        true,
                        "Save",
                        "Discard",
                        false,
                        showTertiary: true,
                        tertiaryText: "Cancel"),
                    new BulkActionPreviewWindow(
                        new BulkActionPreview
                        {
                            Title = "Preview",
                            SelectedCount = 1,
                            EstimatedUpdatedCount = 1
                        }),
                    new AssetImportPreviewWindow(
                        new AssetImportPreview
                        {
                            TotalRows = 1
                        }),
                    new NotificationCenterWindow(),
                    new RecycleBinWindow()
                };

                var controls = new object[]
                {
                    new HighlightTextBlock()
                };

                foreach (var window in windows)
                {
                    if (window.Content == null)
                    {
                        throw new InvalidOperationException(
                            $"{window.GetType().Name} XAML content was not created.");
                    }
                }

                GC.KeepAlive(windows);
                GC.KeepAlive(controls);
            }
            catch (Exception ex)
            {
                captured = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.Null(captured);
    }
}
