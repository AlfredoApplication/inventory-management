using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ZXing;
using ZXing.Common;
using ZXing.Windows.Compatibility;
using GDI = System.Drawing;

namespace LoginAppFramework
{
    public partial class PrintQrCodesWindow : Window
    {
        private List<Asset> _assetsToPrint;

        // Barcode bitmap is generated at this DPI.
        // 300 DPI → at print time, WPF maps 1 bitmap-px = 1 printer-px on a 300 DPI printer.
        private const double PrintDpi = 300.0;

        public PrintQrCodesWindow(List<Asset> assetsToPrint)
        {
            InitializeComponent();
            _assetsToPrint = assetsToPrint;
            this.Title = $"Barkodları Çap Et ({assetsToPrint.Count} ədəd)";
            CountTextBlock.Text = $"{assetsToPrint.Count} seçilmiş vəsait üçün barkodlar";
            RenderPreview();
        }

        // WPF layout units (96 DPI) — used for page geometry
        private double MmToWpf(double mm) => mm * 96.0 / 25.4;

        // Actual raster pixels at PrintDpi — used for the barcode bitmap
        private int MmToBmpPx(double mm) => (int)System.Math.Round(mm * PrintDpi / 25.4);

        private void RefreshButton_Click(object sender, RoutedEventArgs e) => RenderPreview();

        private void RenderPreview()
        {
            QrCodePanel.Children.Clear();

            if (!double.TryParse(WidthTextBox.Text,  out double wMm)) wMm = 50;
            if (!double.TryParse(HeightTextBox.Text, out double hMm)) hMm = 25;

            foreach (var asset in _assetsToPrint)
            {
                // ── PREVIEW at 3× zoom ─────────────────────────────────────────────────
                // 50mm × 25mm → 150mm × 75mm on screen.
                // The barcode bitmap IS generated at 300 DPI (591 px wide for 50mm).
                // At 3×: display width ≈ 567 WPF px from 591 source px → nearly 1:1 → bars visible.
                var label = BuildLabel(asset, wMm, hMm, scale: 3.0);

                QrCodePanel.Children.Add(new Border
                {
                    BorderBrush     = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Margin          = new Thickness(8),
                    Child           = label
                });
            }
        }

        /// <summary>
        /// Builds one label visual.
        /// scale=1.0 for print (exact paper size), scale=3.0 for screen preview (so bars are visible).
        /// </summary>
        private FrameworkElement BuildLabel(Asset asset, double widthMm, double heightMm, double scale)
        {
            double wpfW = MmToWpf(widthMm) * scale;
            double wpfH = MmToWpf(heightMm) * scale;

            // ── Barcode bitmap at 300 DPI ──────────────────────────────────────────────
            // 50mm → 591 px wide | barcode area = 65% of label height
            int bmpW = MmToBmpPx(widthMm);               // e.g. 591
            int bmpH = MmToBmpPx(heightMm * 0.65);       // 65% of label height for bars

            // Margin=10: adds a quiet zone (white border) inside the bitmap.
            // Code 128 standard requires ≥10× the narrowest-bar width on each side.
            // PureBarcode=false: ZXing applies its own internal margin on top of ours.
            var writer = new BarcodeWriter<GDI.Bitmap>
            {
                Format   = BarcodeFormat.CODE_128,
                Renderer = new BitmapRenderer(),
                Options  = new EncodingOptions
                {
                    Width       = bmpW,
                    Height      = bmpH,
                    Margin      = 10,
                    // PureBarcode=true → bars only, no text drawn inside the bitmap.
                    // The quiet zone comes from Margin=10 above, NOT from PureBarcode.
                    PureBarcode = true
                }
            };

            GDI.Bitmap bmp = writer.Write(asset.VesaitinKodu ?? string.Empty);

            // ── Layout: barcode row + text row ─────────────────────────────────────────
            double textH    = wpfH * 0.16;
            double barcodeH = wpfH - textH;

            var grid = new Grid { Width = wpfW, Height = wpfH, Background = Brushes.White };
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(barcodeH) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(textH) });

            // Row 0: barcode image
            // NearestNeighbor = zero anti-aliasing, pixel-perfect sharp bars.
            // Stretch.Fill: X and Y scale independently but RELATIVE bar widths are
            // preserved since all X columns scale by the same factor.
            var img = new System.Windows.Controls.Image
            {
                Source              = ToBitmapImage(bmp),
                Stretch             = Stretch.Fill,
                SnapsToDevicePixels = true
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.NearestNeighbor);
            Grid.SetRow(img, 0);
            grid.Children.Add(img);

            // Row 1: human-readable code (Courier New = monospace, clean for dot-separated codes)
            var text = new TextBlock
            {
                Text                = asset.VesaitinKodu,
                TextWrapping        = TextWrapping.NoWrap,
                TextTrimming        = TextTrimming.CharacterEllipsis,
                TextAlignment       = TextAlignment.Center,
                FontFamily          = new FontFamily("Courier New"),
                FontSize            = 7.0 * scale,   // scales with preview zoom
                VerticalAlignment   = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(text, 1);
            grid.Children.Add(text);

            return grid;
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new PrintDialog();
            if (dlg.ShowDialog() != true) return;

            if (!double.TryParse(WidthTextBox.Text,  out double wMm)) wMm = 50;
            if (!double.TryParse(HeightTextBox.Text, out double hMm)) hMm = 25;

            // WPF print layout uses 96 DPI coordinate space.
            // The printer driver automatically scales to real printer DPI (300/600 DPI).
            // Our 300-DPI bitmap → at 300 DPI printer: 1 source px = 1 printer px (perfect).
            // At 600 DPI printer: 1 source px = 2 printer px (crisp 2× upscale, still sharp).
            double wpfW = MmToWpf(wMm);
            double wpfH = MmToWpf(hMm);

            var doc = new FixedDocument();
            doc.DocumentPaginator.PageSize = new Size(wpfW, wpfH);

            foreach (var asset in _assetsToPrint)
            {
                var label = BuildLabel(asset, wMm, hMm, scale: 1.0);

                label.Measure(new Size(wpfW, wpfH));
                label.Arrange(new Rect(0, 0, wpfW, wpfH));
                label.UpdateLayout();

                var page = new FixedPage { Width = wpfW, Height = wpfH };
                page.Children.Add(label);

                var pageContent = new PageContent();
                ((System.Windows.Markup.IAddChild)pageContent).AddChild(page);
                doc.Pages.Add(pageContent);
            }

            dlg.PrintDocument(doc.DocumentPaginator, "Barkodları Çap Et");
        }

        private static BitmapImage ToBitmapImage(GDI.Bitmap bitmap)
        {
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource   = ms;
            bi.CacheOption    = BitmapCacheOption.OnLoad;
            bi.EndInit();
            bi.Freeze();
            return bi;
        }
    }
}