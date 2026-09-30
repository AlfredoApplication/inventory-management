using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace LoginAppFramework
{
    public sealed class BulkActionPreview
    {
        public string Title { get; init; }
        public string Description { get; init; }
        public string ConfirmText { get; init; } = "Tətbiq et";
        public int SelectedCount { get; init; }
        public int EstimatedUpdatedCount { get; init; }
        public IReadOnlyList<BulkChangePreviewItem> Changes { get; init; }
            = Array.Empty<BulkChangePreviewItem>();
        public IReadOnlyList<Asset> SkippedStatusAssets { get; init; }
            = Array.Empty<Asset>();
        public IReadOnlyList<string> SampleAssets { get; init; }
            = Array.Empty<string>();

        public string SelectedCountText => $"{SelectedCount} seçilib";
        public string UpdatedCountText => $"{EstimatedUpdatedCount} dəyişəcək";
        public string SkippedCountText => $"{SkippedStatusAssets.Count} skip";
        public bool HasSkippedItems => SkippedStatusAssets.Count > 0;
    }

    public sealed class BulkChangePreviewItem
    {
        public string Field { get; init; }
        public string Value { get; init; }
    }

    public static class BulkActionPreviewBuilder
    {
        public static BulkActionPreview ForEdit(
            IReadOnlyList<Asset> assets,
            BulkAssetChanges changes)
        {
            assets ??= Array.Empty<Asset>();
            changes ??= new BulkAssetChanges();

            var changeItems = DescribeChanges(changes);
            bool hasNonStatusChanges = HasNonStatusChanges(changes);

            var skippedStatusAssets =
                changes.Status == null
                    ? new List<Asset>()
                    : assets
                        .Where(asset => asset?.WorkerId.HasValue == true)
                        .ToList();

            int estimatedUpdated = assets.Count(asset =>
                asset != null &&
                (hasNonStatusChanges ||
                 (changes.Status != null && !asset.WorkerId.HasValue)));

            return new BulkActionPreview
            {
                Title = "Toplu dəyişikliyi yoxlayın",
                Description =
                    "Aşağıdakı dəyişikliklər yalnız təsdiqdən sonra seçilmiş vəsaitlərə tətbiq ediləcək.",
                ConfirmText = "Dəyişiklikləri tətbiq et",
                SelectedCount = assets.Count,
                EstimatedUpdatedCount = estimatedUpdated,
                Changes = changeItems,
                SkippedStatusAssets = skippedStatusAssets,
                SampleAssets = BuildSampleNames(assets)
            };
        }

        public static BulkActionPreview ForAssignment(
            IReadOnlyList<Asset> assets,
            Worker worker)
        {
            assets ??= Array.Empty<Asset>();

            return new BulkActionPreview
            {
                Title = "Toplu təhkimatı yoxlayın",
                Description =
                    "Seçilmiş vəsaitlərin təhkimatı aşağıdakı əməkdaşla əvəz olunacaq.",
                ConfirmText = "Təhkim et",
                SelectedCount = assets.Count,
                EstimatedUpdatedCount = assets.Count,
                Changes = new[]
                {
                    new BulkChangePreviewItem
                    {
                        Field = "Təhkim olunan əməkdaş",
                        Value = worker?.per_adiper_soyadi ?? "—"
                    },
                    new BulkChangePreviewItem
                    {
                        Field = "Status",
                        Value = "İstifadədədir"
                    }
                },
                SampleAssets = BuildSampleNames(assets)
            };
        }

        public static IReadOnlyList<BulkChangePreviewItem> DescribeChanges(
            BulkAssetChanges changes)
        {
            var items = new List<BulkChangePreviewItem>();

            Add(items, "Vəsaitin kodu", changes.VesaitinKodu);
            Add(items, "Vəsaitin adı", changes.VesaitinAdi);
            Add(items, "Seriya nömrəsi", changes.ITAvadanliqlarininSeriyaNomresi);
            Add(items, "Kateqoriya", changes.Kateqoriya);
            Add(items, "Yerləşmə yeri", changes.YerleshmeYeri);
            Add(items, "Ərazi", changes.Erazi);
            Add(items, "Status", changes.Status);

            if (changes.AssignedWorker != null)
            {
                Add(
                    items,
                    "Təhkim olunan əməkdaş",
                    changes.AssignedWorker.Id == 0
                        ? "Təhkimatı ləğv et"
                        : changes.AssignedWorker.per_adiper_soyadi);
            }

            if (changes.PurchaseCost.HasValue)
            {
                Add(
                    items,
                    "Alış qiyməti",
                    changes.PurchaseCost.Value.ToString(
                        "0.##",
                        CultureInfo.CurrentCulture));
            }

            if (changes.PurchaseDate.HasValue)
                Add(items, "Alınma tarixi", changes.PurchaseDate.Value.ToString("dd.MM.yyyy"));

            if (changes.UsefulLifeInYears.HasValue)
                Add(items, "İstifadə müddəti", $"{changes.UsefulLifeInYears.Value} il");

            Add(items, "Təchizatçı", changes.Supplier);

            if (changes.WarrantyExpirationDate.HasValue)
            {
                Add(
                    items,
                    "Zəmanət bitmə tarixi",
                    changes.WarrantyExpirationDate.Value.ToString("dd.MM.yyyy"));
            }

            return items;
        }

        private static bool HasNonStatusChanges(BulkAssetChanges changes)
            => changes.VesaitinKodu != null ||
               changes.VesaitinAdi != null ||
               changes.ITAvadanliqlarininSeriyaNomresi != null ||
               changes.Kateqoriya != null ||
               changes.AssignedWorker != null ||
               changes.YerleshmeYeri != null ||
               changes.Erazi != null ||
               changes.PurchaseCost.HasValue ||
               changes.PurchaseDate.HasValue ||
               changes.UsefulLifeInYears.HasValue ||
               changes.Supplier != null ||
               changes.WarrantyExpirationDate.HasValue;

        private static void Add(
            ICollection<BulkChangePreviewItem> items,
            string field,
            string value)
        {
            if (value == null)
                return;

            items.Add(new BulkChangePreviewItem
            {
                Field = field,
                Value = string.IsNullOrEmpty(value) ? "(təmizlənəcək)" : value
            });
        }

        private static IReadOnlyList<string> BuildSampleNames(
            IEnumerable<Asset> assets)
            => assets
                .Where(asset => asset != null)
                .Take(10)
                .Select(asset =>
                    string.Join(
                        " • ",
                        new[]
                        {
                            asset.VesaitinKodu,
                            asset.VesaitinAdi
                        }.Where(value => !string.IsNullOrWhiteSpace(value))))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToList();
    }
}
