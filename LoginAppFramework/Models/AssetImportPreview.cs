using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public enum ImportPreviewSeverity
    {
        Ready,
        Warning,
        Error
    }

    public sealed class AssetImportPreviewItem
    {
        public Asset Asset { get; init; }
        public int RowNumber { get; init; }
        public ImportPreviewSeverity Severity { get; init; }
        public string StateText { get; init; }
        public string IssueText { get; init; }
        public string RequestedWorker { get; init; }
        public string ResolvedWorker { get; init; }
        public bool CanImport => Severity != ImportPreviewSeverity.Error;

        public string Code => Asset?.VesaitinKodu;
        public string Name => Asset?.VesaitinAdi;
        public string Serial => Asset?.ITAvadanliqlarininSeriyaNomresi;
        public string Category => Asset?.Kateqoriya;
    }

    public sealed class AssetImportPreviewIssue
    {
        public ImportPreviewSeverity Severity { get; init; }
        public string SeverityText { get; init; }
        public string Message { get; init; }
    }

    public sealed class AssetImportPreview
    {
        public int TotalRows { get; init; }
        public IReadOnlyList<AssetImportPreviewItem> Items { get; init; }
            = Array.Empty<AssetImportPreviewItem>();
        public IReadOnlyList<AssetImportPreviewIssue> Issues { get; init; }
            = Array.Empty<AssetImportPreviewIssue>();

        public int ReadyCount =>
            Items.Count(item => item.Severity == ImportPreviewSeverity.Ready);

        public int WarningCount =>
            Items.Count(item => item.Severity == ImportPreviewSeverity.Warning) +
            Issues.Count(issue => issue.Severity == ImportPreviewSeverity.Warning);

        public int ErrorCount =>
            Items.Count(item => item.Severity == ImportPreviewSeverity.Error) +
            Issues.Count(issue => issue.Severity == ImportPreviewSeverity.Error);

        public int ImportableCount =>
            Items.Count(item => item.CanImport);

        public bool CanImport => ImportableCount > 0;
        public bool HasIssues => WarningCount > 0 || ErrorCount > 0;

        public string TotalRowsText => $"{TotalRows} sətir";
        public string ReadyCountText => $"{ReadyCount} hazır";
        public string WarningCountText => $"{WarningCount} xəbərdarlıq";
        public string ErrorCountText => $"{ErrorCount} problem";

        public List<Asset> AssetsToImport()
            => Items
                .Where(item => item.CanImport)
                .Select(item => item.Asset)
                .Where(asset => asset != null)
                .ToList();
    }

    public static class AssetImportPreviewBuilder
    {
        public static AssetImportPreview Build(
            AssetImportBatch batch,
            IReadOnlyCollection<Asset> existingAssets,
            IReadOnlyDictionary<string, Worker> confirmedMappings)
        {
            if (batch == null)
                throw new ArgumentNullException(nameof(batch));

            existingAssets ??= Array.Empty<Asset>();
            confirmedMappings ??=
                new Dictionary<string, Worker>(StringComparer.OrdinalIgnoreCase);

            var existingCodes = existingAssets
                .Select(asset => asset?.VesaitinKodu?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingSerials = existingAssets
                .Select(asset => asset?.ITAvadanliqlarininSeriyaNomresi?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var seenCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSerials = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var items = new List<AssetImportPreviewItem>();

            foreach (var asset in batch.Assets)
            {
                if (asset == null)
                    continue;

                var errors = new List<string>();
                var warnings = new List<string>();

                batch.SourceRows.TryGetValue(asset, out int rowNumber);

                string code = asset.VesaitinKodu?.Trim();
                if (!string.IsNullOrWhiteSpace(code))
                {
                    if (existingCodes.Contains(code))
                    {
                        errors.Add($"'{code}' kodu artıq bazada mövcuddur.");
                    }
                    else if (!seenCodes.Add(code))
                    {
                        errors.Add($"'{code}' kodu Excel faylında təkrarlanır.");
                    }
                }

                string serial =
                    asset.ITAvadanliqlarininSeriyaNomresi?.Trim();

                if (!string.IsNullOrWhiteSpace(serial))
                {
                    if (existingSerials.Contains(serial))
                    {
                        errors.Add(
                            $"'{serial}' seriya nömrəsi artıq bazada mövcuddur.");
                    }
                    else if (!seenSerials.Add(serial))
                    {
                        errors.Add(
                            $"'{serial}' seriya nömrəsi Excel faylında təkrarlanır.");
                    }
                }

                if (asset.PurchaseCost < 0)
                    errors.Add("Alış qiyməti mənfi ola bilməz.");

                if (asset.UsefulLifeInYears < 0)
                    errors.Add("İstifadə müddəti mənfi ola bilməz.");

                if (asset.WarrantyExpirationDate > DateTime.MinValue &&
                    asset.PurchaseDate > DateTime.MinValue &&
                    asset.WarrantyExpirationDate.Date <
                    asset.PurchaseDate.Date)
                {
                    errors.Add(
                        "Zəmanət bitmə tarixi alınma tarixindən əvvəldir.");
                }

                string requestedWorker = null;
                string resolvedWorker = null;

                if (batch.RequestedWorkerNames.TryGetValue(
                        asset,
                        out string workerName) &&
                    !string.IsNullOrWhiteSpace(workerName))
                {
                    requestedWorker = workerName.Trim();

                    if (confirmedMappings.TryGetValue(
                            requestedWorker,
                            out Worker mappedWorker) &&
                        mappedWorker != null)
                    {
                        resolvedWorker = mappedWorker.per_adiper_soyadi;
                    }
                    else
                    {
                        warnings.Add(
                            $"'{requestedWorker}' işçisi uyğunlaşdırılmayıb; vəsait təhkimsiz import olunacaq.");
                    }
                }

                ImportPreviewSeverity severity =
                    errors.Count > 0
                        ? ImportPreviewSeverity.Error
                        : warnings.Count > 0
                            ? ImportPreviewSeverity.Warning
                            : ImportPreviewSeverity.Ready;

                items.Add(new AssetImportPreviewItem
                {
                    Asset = asset,
                    RowNumber = rowNumber,
                    Severity = severity,
                    StateText = severity switch
                    {
                        ImportPreviewSeverity.Ready => "Hazır",
                        ImportPreviewSeverity.Warning => "Xəbərdarlıq",
                        _ => "Skip"
                    },
                    IssueText = string.Join(
                        " ",
                        errors.Concat(warnings)),
                    RequestedWorker = requestedWorker,
                    ResolvedWorker = resolvedWorker
                });
            }

            var issues = new List<AssetImportPreviewIssue>();

            issues.AddRange(
                batch.Errors.Select(message =>
                    new AssetImportPreviewIssue
                    {
                        Severity = ImportPreviewSeverity.Error,
                        SeverityText = "Xəta",
                        Message = message
                    }));

            issues.AddRange(
                batch.Warnings.Select(message =>
                    new AssetImportPreviewIssue
                    {
                        Severity = ImportPreviewSeverity.Warning,
                        SeverityText = "Xəbərdarlıq",
                        Message = message
                    }));

            return new AssetImportPreview
            {
                TotalRows = batch.TotalRows,
                Items = items,
                Issues = issues
            };
        }
    }
}
