using System;
using System.Collections.Generic;

namespace LoginAppFramework
{
    public enum BarcodePrintPaperMode
    {
        Label,
        A4
    }

    public enum BarcodeCodeFormat
    {
        Code128,
        QrCode
    }

    public sealed class BarcodePrintSettings
    {
        public BarcodePrintPaperMode PaperMode { get; init; }
        public BarcodeCodeFormat Format { get; init; }
        public double LabelWidthMm { get; init; }
        public double LabelHeightMm { get; init; }
        public int Columns { get; init; } = 1;
        public int Rows { get; init; } = 1;
        public double PageMarginMm { get; init; }
        public double GapMm { get; init; }
        public double FontSize { get; init; } = 7;
        public double CodeAreaPercent { get; init; } = 68;
        public bool ShowAssetName { get; init; } = true;

        public int PageCapacity =>
            PaperMode == BarcodePrintPaperMode.A4
                ? Math.Max(1, Columns * Rows)
                : 1;
    }

    public sealed class BarcodeLabelPreset
    {
        public string Name { get; init; }
        public BarcodePrintPaperMode PaperMode { get; init; }
        public double WidthMm { get; init; }
        public double HeightMm { get; init; }
        public int Columns { get; init; }
        public int Rows { get; init; }
        public double MarginMm { get; init; }
        public double GapMm { get; init; }

        public override string ToString() => Name;
    }

    public static class BarcodePrintLayout
    {
        public const double A4WidthMm = 210;
        public const double A4HeightMm = 297;

        public static IReadOnlyList<BarcodeLabelPreset> Presets { get; } =
            new[]
            {
                new BarcodeLabelPreset
                {
                    Name = "50 × 25 mm • Standart etiket",
                    PaperMode = BarcodePrintPaperMode.Label,
                    WidthMm = 50,
                    HeightMm = 25,
                    Columns = 1,
                    Rows = 1
                },
                new BarcodeLabelPreset
                {
                    Name = "40 × 20 mm • Kiçik etiket",
                    PaperMode = BarcodePrintPaperMode.Label,
                    WidthMm = 40,
                    HeightMm = 20,
                    Columns = 1,
                    Rows = 1
                },
                new BarcodeLabelPreset
                {
                    Name = "60 × 30 mm • Böyük etiket",
                    PaperMode = BarcodePrintPaperMode.Label,
                    WidthMm = 60,
                    HeightMm = 30,
                    Columns = 1,
                    Rows = 1
                },
                new BarcodeLabelPreset
                {
                    Name = "A4 • 3 × 8 etiket",
                    PaperMode = BarcodePrintPaperMode.A4,
                    WidthMm = 62,
                    HeightMm = 33,
                    Columns = 3,
                    Rows = 8,
                    MarginMm = 8,
                    GapMm = 2
                },
                new BarcodeLabelPreset
                {
                    Name = "A4 • 4 × 10 etiket",
                    PaperMode = BarcodePrintPaperMode.A4,
                    WidthMm = 46,
                    HeightMm = 26,
                    Columns = 4,
                    Rows = 10,
                    MarginMm = 8,
                    GapMm = 2
                },
                new BarcodeLabelPreset
                {
                    Name = "Fərdi ölçü",
                    PaperMode = BarcodePrintPaperMode.Label,
                    WidthMm = 50,
                    HeightMm = 25,
                    Columns = 1,
                    Rows = 1
                }
            };

        public static bool Validate(
            BarcodePrintSettings settings,
            out string error)
        {
            error = null;

            if (settings == null)
            {
                error = "Çap parametrləri tapılmadı.";
                return false;
            }

            if (settings.LabelWidthMm < 15 ||
                settings.LabelHeightMm < 12)
            {
                error = "Etiket ölçüsü ən azı 15 × 12 mm olmalıdır.";
                return false;
            }

            if (settings.LabelWidthMm > 210 ||
                settings.LabelHeightMm > 297)
            {
                error = "Etiket ölçüsü A4 sərhədlərini keçə bilməz.";
                return false;
            }

            if (settings.FontSize < 5 ||
                settings.FontSize > 24)
            {
                error = "Şrift ölçüsü 5–24 aralığında olmalıdır.";
                return false;
            }

            if (settings.CodeAreaPercent < 45 ||
                settings.CodeAreaPercent > 85)
            {
                error = "Kod sahəsi 45–85% aralığında olmalıdır.";
                return false;
            }

            if (settings.PaperMode != BarcodePrintPaperMode.A4)
                return true;

            if (settings.Columns < 1 ||
                settings.Columns > 8 ||
                settings.Rows < 1 ||
                settings.Rows > 20)
            {
                error = "A4 üçün sütun 1–8, sətir 1–20 aralığında olmalıdır.";
                return false;
            }

            if (settings.PageMarginMm < 0 ||
                settings.GapMm < 0)
            {
                error = "Margin və boşluq mənfi ola bilməz.";
                return false;
            }

            double usedWidth =
                settings.PageMarginMm * 2 +
                settings.Columns * settings.LabelWidthMm +
                Math.Max(0, settings.Columns - 1) * settings.GapMm;

            double usedHeight =
                settings.PageMarginMm * 2 +
                settings.Rows * settings.LabelHeightMm +
                Math.Max(0, settings.Rows - 1) * settings.GapMm;

            if (usedWidth > A4WidthMm + 0.01 ||
                usedHeight > A4HeightMm + 0.01)
            {
                error =
                    $"Etiketlər A4 səhifəyə sığmır. İstifadə olunan sahə: {usedWidth:0.#} × {usedHeight:0.#} mm.";
                return false;
            }

            return true;
        }

        public static int GetPageCount(
            int itemCount,
            BarcodePrintSettings settings)
        {
            if (itemCount <= 0 || settings == null)
                return 0;

            int capacity = Math.Max(1, settings.PageCapacity);

            return (int)Math.Ceiling(
                itemCount / (double)capacity);
        }
    }
}
