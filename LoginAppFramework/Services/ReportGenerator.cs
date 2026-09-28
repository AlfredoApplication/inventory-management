using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LoginAppFramework
{
    public static class ReportGenerator
    {
        // Hesabat növünü müəyyən etmək üçün Enum
        public enum ReportType
        {
            FullInventory,
            AssetsByDepartment,
            WarrantyExpiration
        }

        // Bu metod bütün hesabatlarda təkrarlanan hissələri (başlıq, altbilgi) tətbiq edir.
        private static DocumentMetadata GetMetadata(string title)
        {
            return new DocumentMetadata
            {
                Title = title,
                Author = "Inventory Management System"
            };
        }

        private static void ComposeHeader(this IContainer container, string title)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text(title).Bold().FontSize(20).FontColor(Colors.BlueGrey.Darken4);
                    column.Item().Text($"Hesabat Tarixi: {System.DateTime.Now:yyyy-MM-dd HH:mm}").FontSize(9);
                });

                // Buraya logo da əlavə edə bilərsiniz
                // row.ConstantItem(100).Height(50).Placeholder(); 
            });
        }
        private static void ComposeSummaryBox(IContainer container, string title, string value, string color)
        {
            container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.Grey.Lighten4)
                .Padding(10)
                .Column(col =>
                {
                    col.Spacing(5);
                    col.Item().Text(title).FontSize(10).FontColor(Colors.Grey.Darken1);
                    col.Item().Text(value).Bold().FontSize(18).FontColor(color);
                });
        }
        private static void ComposeSummaryPage(IContainer container, List<Asset> assets)
        {
            // Bütün göstəriciləri əvvəlcədən hesablayırıq
            var totalCount = assets.Count;
            var totalPurchaseCost = assets.Sum(a => a.PurchaseCost);
            var totalCurrentValue = assets.Sum(a => a.CurrentValue);
            var totalDepreciation = totalPurchaseCost - totalCurrentValue;
            var assignedCount = assets.Count(a => a.WorkerId.HasValue);
            var inStockCount = totalCount - assignedCount;
            var endOfLifeCount = assets.Count(a => a.IsEndOfLife);

            var assetsByCategory = assets
                .Where(a => !string.IsNullOrEmpty(a.Category))
                .GroupBy(a => a.ParentCategory ?? a.Category ?? "Kateqoriyasız")
                .Select(g => Tuple.Create(g.Key, g.Count()))
                .OrderByDescending(x => x.Item2)
                .Take(5)
                .ToList();

            var valueByDepartment = assets
                .Where(a => a.WorkerId.HasValue)
                .GroupBy(a => a.Worker.pdp_adi)
                .Select(g => Tuple.Create(g.Key, (int)g.Sum(a => a.CurrentValue))) // Dəyəri int-ə çeviririk
                .OrderByDescending(x => x.Item2)
                .Take(5)
                .ToList();

            // Səhifəni iki sütuna bölürük
            container.Grid(grid =>
            {
                grid.Spacing(25);
                grid.Columns(6); // 12 sütunlu sistem kimi düşünək (6+6)

                // SOL SÜTUN
                grid.Item(6).Column(column =>
                {
                    column.Spacing(15);

                    column.Item().Text("Maliyyə Xülasəsi").Bold().FontSize(14);
                    ComposeKpi(column.Item(), "Cəmi Alış Dəyəri", $"{totalPurchaseCost:C0}", "📈", Colors.Blue.Darken2);
                    ComposeKpi(column.Item(), "Cəmi Cari Dəyər", $"{totalCurrentValue:C0}", "✅", Colors.Green.Darken2);
                    ComposeKpi(column.Item(), "Cəmi Amortizasiya", $"{totalDepreciation:C0}", "📉", Colors.Red.Darken2);

                    column.Item().PaddingTop(15).Text("Vəsait Statusu Xülasəsi").Bold().FontSize(14);

                    // Statuslar üçün 2 sütunlu grid
                    column.Item().Grid(statusGrid =>
                    {
                        statusGrid.Spacing(15);
                        statusGrid.Columns(2);

                        ComposeKpi(statusGrid.Item(), "Ümumi Say", $"{totalCount:N0}", "📦", Colors.Black);
                        ComposeKpi(statusGrid.Item(), "Təhkim Edilən", $"{assignedCount:N0}", "👨‍💼", Colors.Orange.Darken2);
                        ComposeKpi(statusGrid.Item(), "Anbarda", $"{inStockCount:N0}", "🏢", Colors.Grey.Darken2);
                        ComposeKpi(statusGrid.Item(), "İst. Müddəti Bitmiş", $"{endOfLifeCount:N0}", "⚠️", Colors.Red.Darken4);
                    });
                });

                // SAĞ SÜTUN
                grid.Item(6).Column(column =>
                {
                    column.Spacing(25);

                    // Kateqoriya qrafiki
                    ComposeBarChart(column.Item(), "Kateqoriya Üzrə Ən Çox Vəsait (Top 5)", assetsByCategory);

                    // Departament qrafiki
                    ComposeBarChart(column.Item(), "Departament Üzrə Ən Çox Dəyər (Top 5)", valueByDepartment);
                });
            });
        }
        private static void ComposeKpi(IContainer container, string title, string value, string icon, string valueColor)
        {
            container
                .Border(1)
                .BorderColor(Colors.Grey.Lighten2)
                .Background(Colors.Grey.Lighten5)
                .Padding(10)
                .Row(row =>
                {
                    row.Spacing(10);
                    row.ConstantItem(40).AlignCenter().Text(icon).FontSize(24).FontColor(valueColor);
                    row.RelativeItem().Column(column =>
                    {
                        column.Item().Text(title).FontSize(9).SemiBold().FontColor(Colors.Grey.Darken1);
                        column.Item().Text(value).FontSize(16).Bold().FontColor(valueColor);
                    });
                });
        }
        private static void ComposeBarChart(IContainer container, string title, List<Tuple<string, int>> data)
        {
            container.Column(column =>
            {
                column.Spacing(10);
                column.Item().Text(title).Bold().FontSize(12);

                var maxValue = data.Any() ? data.Max(x => x.Item2) : 1;

                foreach (var item in data)
                {
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Text(item.Item1).FontSize(9);
                        row.ConstantItem(40).Text(item.Item2.ToString("N0")).FontSize(9).Bold();
                    });

                    // Barların özü
                    column.Item().Height(10).Row(row =>
                    {
                        // Əgər elementin dəyəri sıfırdan böyükdürsə, onun üçün rəngli bar çək.
                        if (item.Item2 > 0)
                        {
                            row.RelativeItem((uint)item.Item2).Background(Colors.Blue.Lighten2).CornerRadius(5);
                        }

                        // Qalan boşluğun ölçüsünü hesablayırıq.
                        var emptySpace = maxValue - item.Item2;

                        // Əgər boşluq sıfırdan böyükdürsə, qalan hissəni boş olaraq çək.
                        if (emptySpace > 0)
                        {
                            row.RelativeItem((uint)emptySpace);
                        }
                    });
                }
            });
        }
        private static void ComposeFooter(this IContainer container)
        {
            container.AlignCenter().Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        }

        // ÜMUMİ İNVENTAR HESABATI
        public static void GenerateFullInventoryReport(List<Asset> assets, string filePath)
        {
            Document.Create(container =>
            {
                // Xülasə üçün ayrıca səhifə yaradırıq
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Element(header => ComposeHeader(header, "Ümumi İnventar Hesabatı - Xülasə"));
                    // Səhifənin məzmunu olaraq xülasəni əlavə edirik
                    page.Content().Element(content => ComposeSummaryPage(content, assets));
                    page.Footer().Element(ComposeFooter);
                });

                // Detallı cədvəl üçün ikinci (və davamı) səhifələri yaradırıq
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Element(header => ComposeHeader(header, "Ümumi İnventar Hesabatı - Detallı Siyahı"));
                    // Səhifənin məzmunu olaraq əvvəlki cədvəli əlavə edirik
                    page.Content().Element(content => ComposeContent_FullInventory(content, assets));
                    page.Footer().Element(ComposeFooter);
                });
            })
            .GeneratePdf(filePath);
        }

        private static void ComposeContent_FullInventory(IContainer container, List<Asset> assets)
        {
            container.PaddingVertical(20).Column(column =>
            {
                column.Spacing(10);

                // Cədvəl yaradırıq
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(100);
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn(2);
                        columns.ConstantColumn(80, Unit.Point);
                    });

                    // Cədvəl başlığı
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.BlueGrey.Lighten3).Padding(2).Text("Vəsait Kodu");
                        header.Cell().Background(Colors.BlueGrey.Lighten3).Padding(2).Text("Adı");
                        header.Cell().Background(Colors.BlueGrey.Lighten3).Padding(2).Text("Kateqoriya");
                        header.Cell().Background(Colors.BlueGrey.Lighten3).Padding(2).Text("Təhkim Edilən");
                        header.Cell().Background(Colors.BlueGrey.Lighten3).Padding(2).AlignRight().Text("Cari Dəyər");
                    });

                    // Sətirlər
                    foreach (var asset in assets)
                    {
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(asset.VesaitinKodu ?? "-");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(asset.Name ?? "-");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(asset.Category ?? "-");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(asset.AssignedUser ?? "Boşdur");
                        table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().Text($"{asset.CurrentValue:C}");
                    }
                });

                // Yekun
                var totalValue = assets.Sum(a => a.CurrentValue);
                column.Item().AlignRight().Text($"Cəmi Vəsait Sayı: {assets.Count}").Bold();
                column.Item().AlignRight().Text($"Cəmi Dəyər: {totalValue:C}").Bold();
            });
        }

        // DEPARTAMENTLƏR ÜZRƏ VƏSAİT HESABATI
        public static void GenerateAssetsByDepartmentReport(List<Asset> assets, string filePath)
        {
            Document.Create(container =>
            {
                // Xülasə səhifəsi
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Element(header => ComposeHeader(header, "Departamentlər Üzrə Hesabat - Xülasə"));
                    page.Content().Element(content => ComposeSummaryPage(content, assets));
                    page.Footer().Element(ComposeFooter);
                });

                // Detallı siyahı səhifəsi
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Element(header => ComposeHeader(header, "Departamentlər Üzrə Hesabat - Detallı Siyahı"));
                    page.Content().Element(content => ComposeContent_ByDepartment(content, assets));
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf(filePath);
        }

        private static void ComposeContent_ByDepartment(IContainer container, List<Asset> assets)
        {
            var assetsByDept = assets
                .GroupBy(a => a.Worker?.pdp_adi ?? "Təhkim Olunmayıb")
                .OrderBy(g => g.Key);

            container.PaddingVertical(20).Column(column =>
            {
                column.Spacing(20);

                foreach (var group in assetsByDept)
                {
                    // Hər departament üçün ayrıca başlıq və cədvəl
                    column.Item().Text(group.Key).Bold().FontSize(14).FontColor(Colors.BlueGrey.Darken2);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(100);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(2);
                            columns.ConstantColumn(80, Unit.Point);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.BlueGrey.Lighten3).Padding(2).Text("Vəsait Kodu");
                            header.Cell().Background(Colors.BlueGrey.Lighten3).Padding(2).Text("Adı");
                            header.Cell().Background(Colors.BlueGrey.Lighten3).Padding(2).Text("Təhkim Edilən İşçi");
                            header.Cell().Background(Colors.BlueGrey.Lighten3).Padding(2).AlignRight().Text("Cari Dəyər");
                        });

                        foreach (var asset in group)
                        {
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(asset.VesaitinKodu ?? "-");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(asset.Name);
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).Text(asset.AssignedUser ?? "-");
                            table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(2).AlignRight().Text($"{asset.CurrentValue:C}");
                        }

                        // Departament üçün yekun
                        var deptTotalValue = group.Sum(a => a.CurrentValue);
                        table.Cell().ColumnSpan(4).AlignRight().PaddingTop(5).Text($"Departament Cəmi: {deptTotalValue:C}").Bold();
                    });
                }
            });
        }

        // ZƏMANƏT MÜDDƏTİ BİTƏN VƏ YA BİTMƏK ÜZRƏ OLAN VƏSAİTLƏR CƏDVƏLİ
        public static void GenerateWarrantyExpirationReport(List<Asset> assets, string filePath)
        {
            // Sadece zəmanəti bitmiş və ya növbəti 30 gün ərzində bitəcək olan (və ya zəmanət tarixi bugünə qədər olan) vəsaitləri tapırıq.
            // Arxivdə olanları çıxarırıq.
            var expiringAssets = assets.Where(a => a.Status != "Arxivdə" && a.WarrantyExpirationDate != default(DateTime) && (a.WarrantyExpirationDate - DateTime.Today).TotalDays <= 30)
                                       .OrderBy(a => a.WarrantyExpirationDate)
                                       .ToList();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header().Element(header => ComposeHeader(header, "Zəmanət Müddəti Bitən / Bitmək Üzrə Olan Avadanlıqlar"));
                    page.Content().Element(content => ComposeContent_WarrantyExpiration(content, expiringAssets));
                    page.Footer().Element(ComposeFooter);
                });
            }).GeneratePdf(filePath);
        }

        private static void ComposeContent_WarrantyExpiration(IContainer container, List<Asset> assets)
        {
            if (!assets.Any())
            {
                container.PaddingVertical(20).AlignCenter().Text("Təyin edilmiş parametrə uyğun (zəmanəti bitən və ya 30 gün qalmış) vəsait tapılmadı.").FontSize(14).FontColor(Colors.Grey.Medium);
                return;
            }

            container.PaddingVertical(20).Column(column =>
            {
                column.Spacing(10);
                column.Item().Text($"Cəmi Say: {assets.Count}").Bold().FontSize(12).FontColor(Colors.Red.Darken2);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(100);  // Kodu
                        columns.RelativeColumn(3);    // Adı
                        columns.RelativeColumn(2);    // Seriya Nömrəsi
                        columns.ConstantColumn(100);  // Zəmanət Tarixi
                        columns.ConstantColumn(80);   // Qalan Gün
                    });

                    // Başlıq
                    table.Header(header =>
                    {
                        header.Cell().Background(Colors.Red.Lighten4).BorderBottom(1).Padding(4).Text("Vəsait Kodu").SemiBold();
                        header.Cell().Background(Colors.Red.Lighten4).BorderBottom(1).Padding(4).Text("Adı").SemiBold();
                        header.Cell().Background(Colors.Red.Lighten4).BorderBottom(1).Padding(4).Text("Seriya N. / Təchizatçı").SemiBold();
                        header.Cell().Background(Colors.Red.Lighten4).BorderBottom(1).Padding(4).Text("Zəmanət Bitmə T.").SemiBold();
                        header.Cell().Background(Colors.Red.Lighten4).BorderBottom(1).Padding(4).AlignCenter().Text("Qalan Gün").SemiBold();
                    });

                    // Sətirlər
                    foreach (var asset in assets)
                    {
                        var remainingDays = Math.Ceiling((asset.WarrantyExpirationDate - DateTime.Today).TotalDays);
                        string remainingText = remainingDays < 0 ? "Bitib" : $"{remainingDays} gün";
                        var bgColor = remainingDays < 0 ? Colors.Red.Lighten5 : Colors.Orange.Lighten5;

                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(asset.VesaitinKodu ?? "-").FontSize(10);
                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(asset.Name ?? "-").FontSize(10);
                        
                        string supplierInfo = !string.IsNullOrEmpty(asset.Supplier) ? $" ({asset.Supplier})" : "";
                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text($"{asset.SerialNumber ?? "-"}{supplierInfo}").FontSize(9);
                        
                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).Text(asset.WarrantyExpirationDate.ToString("dd.MM.yyyy")).FontSize(10).FontColor(Colors.Red.Darken3);
                        table.Cell().Background(bgColor).BorderBottom(1).BorderColor(Colors.Grey.Lighten3).Padding(4).AlignCenter().Text(remainingText).FontSize(10).Bold().FontColor(Colors.Red.Darken4);
                    }
                });
            });
        }
    }
}