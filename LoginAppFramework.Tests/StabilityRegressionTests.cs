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
                        tertiaryText: "Cancel")
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
