using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace LoginAppFramework
{
    public enum ReportKind
    {
        Inventory,
        Purchases,
        Warranty,
        Maintenance,
        Lifecycle,
        Unassigned
    }

    public sealed class ReportQuery
    {
        public ReportKind Kind { get; set; } = ReportKind.Inventory;
        public DateTime? PurchaseDateFrom { get; set; }
        public DateTime? PurchaseDateTo { get; set; }
        public string Department { get; set; }
        public string Category { get; set; }
    }

    public sealed class RecentReportEntry
    {
        public ReportQuery Query { get; set; }
        public string Format { get; set; }
        public int ResultCount { get; set; }
        public DateTime RunAt { get; set; }

        public string Title =>
            ReportCenterService.GetTitle(
                Query?.Kind ?? ReportKind.Inventory);

        public string Summary =>
            $"{Format} • {ResultCount} vəsait • {RunAt:dd.MM.yyyy HH:mm}";

        public string FilterSummary =>
            ReportCenterService.BuildFilterSummary(Query);
    }

    public static class ReportCenterService
    {
        private const string RecentPreferenceKey =
            "report-center-recent";

        static ReportCenterService()
        {
            QuestPDF.Settings.License =
                LicenseType.Community;
        }

        public static IReadOnlyList<Asset> FilterAssets(
            IEnumerable<Asset> assets,
            ReportQuery query,
            IEnumerable<Alert> alerts = null,
            DateTime? today = null)
        {
            query ??= new ReportQuery();

            var source = assets?
                .Where(asset => asset != null)
                .ToList()
                ?? new List<Asset>();

            IEnumerable<Asset> filtered = source;

            if (query.PurchaseDateFrom.HasValue)
            {
                DateTime from =
                    query.PurchaseDateFrom.Value.Date;

                filtered = filtered.Where(asset =>
                    asset.PurchaseDate > DateTime.MinValue &&
                    asset.PurchaseDate.Date >= from);
            }

            if (query.PurchaseDateTo.HasValue)
            {
                DateTime to =
                    query.PurchaseDateTo.Value.Date;

                filtered = filtered.Where(asset =>
                    asset.PurchaseDate > DateTime.MinValue &&
                    asset.PurchaseDate.Date <= to);
            }

            if (!string.IsNullOrWhiteSpace(query.Department) &&
                query.Department != "Hamısı")
            {
                filtered = filtered.Where(asset =>
                    string.Equals(
                        asset.Department,
                        query.Department,
                        StringComparison.CurrentCultureIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(query.Category) &&
                query.Category != "Hamısı")
            {
                filtered = filtered.Where(asset =>
                    string.Equals(
                        asset.Kateqoriya,
                        query.Category,
                        StringComparison.CurrentCultureIgnoreCase));
            }

            var alertList = alerts?.ToList()
                ?? new List<Alert>();

            if (query.Kind == ReportKind.Warranty)
            {
                var ids = alertList
                    .Where(alert =>
                        alert.Category == AlertCategory.Warranty)
                    .Select(alert => alert.TargetId)
                    .ToHashSet();

                if (ids.Count > 0)
                {
                    filtered = filtered.Where(asset =>
                        ids.Contains(asset.Id));
                }
                else
                {
                    DateTime reference =
                        (today ?? DateTime.Today).Date;

                    filtered = filtered.Where(asset =>
                        asset.WarrantyExpirationDate >
                            DateTime.MinValue &&
                        asset.WarrantyExpirationDate.Date <=
                            reference.AddDays(90));
                }
            }
            else if (query.Kind == ReportKind.Maintenance)
            {
                var ids = alertList
                    .Where(alert =>
                        alert.Category == AlertCategory.Maintenance)
                    .Select(alert => alert.TargetId)
                    .ToHashSet();

                if (ids.Count > 0)
                {
                    filtered = filtered.Where(asset =>
                        ids.Contains(asset.Id));
                }
                else
                {
                    DateTime reference =
                        (today ?? DateTime.Today).Date;

                    filtered = filtered.Where(asset =>
                    {
                        DateTime? lastMaintenance =
                            asset.MaintenanceHistory?
                                .Where(record =>
                                    record != null &&
                                    record.MaintenanceDate >
                                        DateTime.MinValue)
                                .OrderByDescending(record =>
                                    record.MaintenanceDate)
                                .Select(record =>
                                    (DateTime?)record.MaintenanceDate.Date)
                                .FirstOrDefault();

                        DateTime? baseline =
                            lastMaintenance ??
                            (asset.PurchaseDate >
                                DateTime.MinValue
                                ? asset.PurchaseDate.Date
                                : null);

                        return baseline.HasValue &&
                               (reference -
                                baseline.Value.Date).Days >= 180;
                    });
                }
            }
            else if (query.Kind == ReportKind.Lifecycle)
            {
                filtered = filtered.Where(asset =>
                    asset.IsEndOfLife);
            }
            else if (query.Kind == ReportKind.Unassigned)
            {
                filtered = filtered.Where(asset =>
                    !asset.WorkerId.HasValue);
            }
            else if (query.Kind == ReportKind.Purchases)
            {
                filtered = filtered.Where(asset =>
                    asset.PurchaseDate > DateTime.MinValue);
            }

            return filtered
                .OrderBy(asset => asset.VesaitinAdi)
                .ThenBy(asset => asset.VesaitinKodu)
                .ToList();
        }

        public static string GetTitle(ReportKind kind)
            => kind switch
            {
                ReportKind.Purchases =>
                    "Alış Tarixi Hesabatı",
                ReportKind.Warranty =>
                    "Zəmanət Xəbərdarlıqları",
                ReportKind.Maintenance =>
                    "Texniki Xidmət Hesabatı",
                ReportKind.Lifecycle =>
                    "İstifadə Müddəti Bitənlər",
                ReportKind.Unassigned =>
                    "Təhkim Olunmayan Vəsaitlər",
                _ =>
                    "Ümumi İnventar Hesabatı"
            };

        public static string BuildFilterSummary(
            ReportQuery query)
        {
            if (query == null)
                return "Filter yoxdur";

            var parts = new List<string>();

            if (query.PurchaseDateFrom.HasValue ||
                query.PurchaseDateTo.HasValue)
            {
                parts.Add(
                    $"Alış tarixi: {query.PurchaseDateFrom?.ToString("dd.MM.yyyy") ?? "—"} – {query.PurchaseDateTo?.ToString("dd.MM.yyyy") ?? "—"}");
            }

            if (!string.IsNullOrWhiteSpace(query.Department) &&
                query.Department != "Hamısı")
            {
                parts.Add(
                    $"Departament: {query.Department}");
            }

            if (!string.IsNullOrWhiteSpace(query.Category) &&
                query.Category != "Hamısı")
            {
                parts.Add(
                    $"Kateqoriya: {query.Category}");
            }

            return parts.Count == 0
                ? "Əlavə filter yoxdur"
                : string.Join(" • ", parts);
        }

        public static void ExportPdf(
            string filePath,
            string title,
            IReadOnlyList<Asset> assets,
            ReportQuery query,
            IProgress<OperationProgressInfo> progress = null)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException(
                    "PDF faylı göstərilməyib.",
                    nameof(filePath));

            assets ??= Array.Empty<Asset>();

            progress?.Report(
                new OperationProgressInfo(
                    "PDF hesabatı yaradılır...",
                    0,
                    assets.Count,
                    "Səhifə layout-u hazırlanır."));

            string filterSummary =
                BuildFilterSummary(query);

            decimal totalPurchase =
                assets.Sum(asset => asset.PurchaseCost);

            decimal totalCurrent =
                assets.Sum(asset => asset.CurrentValue);

            Document.Create(document =>
            {
                document.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(22);
                    page.DefaultTextStyle(style =>
                        style
                            .FontFamily("Arial")
                            .FontSize(8));

                    page.Header().Column(column =>
                    {
                        column.Item()
                            .Text(title)
                            .FontSize(18)
                            .SemiBold()
                            .FontColor(Colors.Blue.Darken2);

                        column.Item()
                            .PaddingTop(3)
                            .Text(filterSummary)
                            .FontSize(8)
                            .FontColor(Colors.Grey.Darken1);

                        column.Item()
                            .PaddingTop(4)
                            .Text(
                                $"{assets.Count} vəsait • Alış dəyəri: {totalPurchase:N2} ₼ • Cari dəyər: {totalCurrent:N2} ₼")
                            .FontSize(8)
                            .SemiBold();
                    });

                    page.Content()
                        .PaddingTop(12)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1.0f);
                                columns.RelativeColumn(1.6f);
                                columns.RelativeColumn(1.1f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.4f);
                                columns.RelativeColumn(1.0f);
                                columns.RelativeColumn(0.9f);
                                columns.RelativeColumn(0.9f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Kod").FontColor(Colors.White).SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Vəsait").FontColor(Colors.White).SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Kateqoriya").FontColor(Colors.White).SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Əməkdaş").FontColor(Colors.White).SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Departament").FontColor(Colors.White).SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Status").FontColor(Colors.White).SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Alış tarixi").FontColor(Colors.White).SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Cari dəyər").FontColor(Colors.White).SemiBold();
                            });

                            foreach (var asset in assets)
                            {
                                table.Cell().Element(BodyCellStyle)
                                    .Text(asset.VesaitinKodu ?? "—");

                                table.Cell().Element(BodyCellStyle)
                                    .Text(asset.VesaitinAdi ?? "—");

                                table.Cell().Element(BodyCellStyle)
                                    .Text(asset.Kateqoriya ?? "—");

                                table.Cell().Element(BodyCellStyle)
                                    .Text(asset.AssignedUser ?? "—");

                                table.Cell().Element(BodyCellStyle)
                                    .Text(asset.Department ?? "—");

                                table.Cell().Element(BodyCellStyle)
                                    .Text(asset.Status ?? "—");

                                table.Cell().Element(BodyCellStyle)
                                    .Text(
                                        asset.PurchaseDate > DateTime.MinValue
                                            ? asset.PurchaseDate.ToString("dd.MM.yyyy")
                                            : "—");

                                table.Cell().Element(BodyCellStyle)
                                    .Text($"{asset.CurrentValue:N2} ₼");
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(text =>
                        {
                            text.Span("Səhifə ");
                            text.CurrentPageNumber();
                            text.Span(" / ");
                            text.TotalPages();
                        });
                });
            }).GeneratePdf(filePath);

            progress?.Report(
                new OperationProgressInfo(
                    "PDF hesabatı hazırdır",
                    assets.Count,
                    assets.Count,
                    "PDF faylı diskə yazıldı."));
        }

        private static IContainer HeaderCellStyle(
            IContainer container)
            => container
                .Background(Colors.Blue.Darken2)
                .Padding(5);

        private static IContainer BodyCellStyle(
            IContainer container)
            => container
                .BorderBottom(0.5f)
                .BorderColor(Colors.Grey.Lighten2)
                .PaddingVertical(4)
                .PaddingHorizontal(3);

        public static IReadOnlyList<RecentReportEntry> LoadRecent()
        {
            var entries = new List<RecentReportEntry>();

            foreach (string value in UiPreferenceStore
                .LoadStringList(RecentPreferenceKey))
            {
                try
                {
                    var item =
                        JsonSerializer.Deserialize<RecentReportEntry>(
                            value);

                    if (item?.Query != null)
                        entries.Add(item);
                }
                catch
                {
                    // Ignore stale preference entries.
                }
            }

            return entries
                .OrderByDescending(item => item.RunAt)
                .Take(8)
                .ToList();
        }

        public static void Remember(
            ReportQuery query,
            string format,
            int resultCount)
        {
            if (query == null)
                return;

            var current = LoadRecent()
                .ToList();

            current.Insert(
                0,
                new RecentReportEntry
                {
                    Query = new ReportQuery
                    {
                        Kind = query.Kind,
                        PurchaseDateFrom =
                            query.PurchaseDateFrom,
                        PurchaseDateTo =
                            query.PurchaseDateTo,
                        Department =
                            query.Department,
                        Category =
                            query.Category
                    },
                    Format =
                        string.IsNullOrWhiteSpace(format)
                            ? "Hesabat"
                            : format,
                    ResultCount = resultCount,
                    RunAt = DateTime.Now
                });

            UiPreferenceStore.SaveStringList(
                RecentPreferenceKey,
                current
                    .Take(8)
                    .Select(item =>
                        JsonSerializer.Serialize(item)));
        }
    }
}
