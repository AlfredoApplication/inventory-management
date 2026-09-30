using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
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
        private const double PrintDpi = 300.0;
        private const int MaximumPreviewPages = 3;
        private readonly List<Asset> _assetsToPrint;

        public PrintQrCodesWindow(List<Asset> assetsToPrint)
        {
            InitializeComponent();

            _assetsToPrint = assetsToPrint?
                .Where(asset =>
                    asset != null &&
                    !string.IsNullOrWhiteSpace(asset.VesaitinKodu))
                .ToList()
                ?? new List<Asset>();

            Title = $"QR / Barkod Çapı ({_assetsToPrint.Count} ədəd)";
        }

        private void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            PresetComboBox.ItemsSource =
                BarcodePrintLayout.Presets;

            PresetComboBox.SelectedIndex = 0;

            CountTextBlock.Text =
                $"{_assetsToPrint.Count} seçilmiş vəsait";

            RenderPreview();
        }

        private static double MmToWpf(double mm)
            => mm * 96.0 / 25.4;

        private static int MmToBmpPx(double mm)
            => Math.Max(
                32,
                (int)Math.Round(mm * PrintDpi / 25.4));

        private void PresetComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded ||
                PresetComboBox.SelectedItem is not BarcodeLabelPreset preset)
            {
                return;
            }

            WidthTextBox.Text =
                preset.WidthMm.ToString(
                    "0.##",
                    CultureInfo.CurrentCulture);

            HeightTextBox.Text =
                preset.HeightMm.ToString(
                    "0.##",
                    CultureInfo.CurrentCulture);

            ColumnsTextBox.Text =
                Math.Max(1, preset.Columns).ToString();

            RowsTextBox.Text =
                Math.Max(1, preset.Rows).ToString();

            MarginTextBox.Text =
                preset.MarginMm.ToString(
                    "0.##",
                    CultureInfo.CurrentCulture);

            GapTextBox.Text =
                preset.GapMm.ToString(
                    "0.##",
                    CultureInfo.CurrentCulture);

            SelectComboItemByTag(
                PaperModeComboBox,
                preset.PaperMode.ToString());

            UpdatePaperModeUi();
            RenderPreview();
        }

        private void PaperModeComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            UpdatePaperModeUi();
        }

        private void UpdatePaperModeUi()
        {
            bool isA4 =
                GetSelectedTag(PaperModeComboBox) == "A4";

            A4SettingsPanel.Visibility =
                isA4
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void RefreshButton_Click(
            object sender,
            RoutedEventArgs e)
            => RenderPreview();

        private bool TryReadSettings(
            out BarcodePrintSettings settings)
        {
            settings = null;
            HideValidation();

            if (!TryParseDouble(WidthTextBox.Text, out double width) ||
                !TryParseDouble(HeightTextBox.Text, out double height) ||
                !TryParseDouble(FontSizeTextBox.Text, out double fontSize) ||
                !TryParseDouble(CodeAreaTextBox.Text, out double codeArea) ||
                !TryParseDouble(MarginTextBox.Text, out double margin) ||
                !TryParseDouble(GapTextBox.Text, out double gap) ||
                !int.TryParse(ColumnsTextBox.Text, out int columns) ||
                !int.TryParse(RowsTextBox.Text, out int rows))
            {
                ShowValidation(
                    "Ölçü və grid sahələrinə düzgün rəqəm daxil edin.");
                return false;
            }

            var paperMode =
                GetSelectedTag(PaperModeComboBox) == "A4"
                    ? BarcodePrintPaperMode.A4
                    : BarcodePrintPaperMode.Label;

            var format =
                GetSelectedTag(FormatComboBox) == "QrCode"
                    ? BarcodeCodeFormat.QrCode
                    : BarcodeCodeFormat.Code128;

            settings = new BarcodePrintSettings
            {
                PaperMode = paperMode,
                Format = format,
                LabelWidthMm = width,
                LabelHeightMm = height,
                Columns = Math.Max(1, columns),
                Rows = Math.Max(1, rows),
                PageMarginMm = margin,
                GapMm = gap,
                FontSize = fontSize,
                CodeAreaPercent = codeArea,
                ShowAssetName =
                    ShowAssetNameCheckBox.IsChecked == true
            };

            if (!BarcodePrintLayout.Validate(
                    settings,
                    out string error))
            {
                ShowValidation(error);
                settings = null;
                return false;
            }

            return true;
        }

        private void RenderPreview()
        {
            if (!IsLoaded ||
                PreviewPagesPanel == null)
            {
                return;
            }

            PreviewPagesPanel.Children.Clear();

            if (_assetsToPrint.Count == 0)
            {
                ShowValidation(
                    "Barkod yaratmaq üçün kodu olan vəsait tapılmadı.");
                PrintButton.IsEnabled = false;
                return;
            }

            if (!TryReadSettings(out var settings))
            {
                PrintButton.IsEnabled = false;
                return;
            }

            PrintButton.IsEnabled = true;

            int pageCount =
                BarcodePrintLayout.GetPageCount(
                    _assetsToPrint.Count,
                    settings);

            PageSummaryTextBlock.Text =
                settings.PaperMode == BarcodePrintPaperMode.A4
                    ? $"{pageCount} A4 səhifə • hər səhifədə maksimum {settings.PageCapacity} etiket"
                    : $"{pageCount} etiket səhifəsi • {settings.LabelWidthMm:0.#} × {settings.LabelHeightMm:0.#} mm";

            if (settings.PaperMode == BarcodePrintPaperMode.A4)
            {
                int capacity = settings.PageCapacity;
                int previewPages =
                    Math.Min(pageCount, MaximumPreviewPages);

                for (int pageIndex = 0;
                     pageIndex < previewPages;
                     pageIndex++)
                {
                    var pageAssets = _assetsToPrint
                        .Skip(pageIndex * capacity)
                        .Take(capacity)
                        .ToList();

                    PreviewPagesPanel.Children.Add(
                        CreatePreviewFrame(
                            BuildSheetPage(
                                pageAssets,
                                settings,
                                scale: 0.72),
                            $"A4 • səhifə {pageIndex + 1}"));
                }

                if (pageCount > MaximumPreviewPages)
                {
                    PreviewPagesPanel.Children.Add(
                        new TextBlock
                        {
                            Text =
                                $"+ {pageCount - MaximumPreviewPages} əlavə səhifə çap zamanı yaradılacaq",
                            Foreground =
                                (Brush)FindResource("SubtleTextBrush"),
                            HorizontalAlignment =
                                HorizontalAlignment.Center,
                            Margin = new Thickness(0, 6, 0, 12)
                        });
                }

                return;
            }

            var wrap = new WrapPanel
            {
                HorizontalAlignment =
                    HorizontalAlignment.Center
            };

            foreach (var asset in _assetsToPrint.Take(12))
            {
                var label = BuildLabel(
                    asset,
                    settings,
                    scale: 2.6);

                wrap.Children.Add(
                    new Border
                    {
                        Background = Brushes.White,
                        BorderBrush = Brushes.LightGray,
                        BorderThickness = new Thickness(1),
                        Margin = new Thickness(7),
                        Child = label
                    });
            }

            PreviewPagesPanel.Children.Add(wrap);

            if (_assetsToPrint.Count > 12)
            {
                PreviewPagesPanel.Children.Add(
                    new TextBlock
                    {
                        Text =
                            $"+ {_assetsToPrint.Count - 12} əlavə etiket çap olunacaq",
                        Foreground =
                            (Brush)FindResource("SubtleTextBrush"),
                        HorizontalAlignment =
                            HorizontalAlignment.Center,
                        Margin = new Thickness(0, 8, 0, 12)
                    });
            }
        }

        private FrameworkElement CreatePreviewFrame(
            FrameworkElement content,
            string caption)
        {
            var panel = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 18)
            };

            panel.Children.Add(
                new TextBlock
                {
                    Text = caption,
                    FontWeight = FontWeights.SemiBold,
                    Foreground =
                        (Brush)FindResource("BodyTextBrush"),
                    Margin = new Thickness(0, 0, 0, 7)
                });

            panel.Children.Add(
                new Border
                {
                    Background = Brushes.White,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness = new Thickness(1),
                    Effect =
                        new System.Windows.Media.Effects.DropShadowEffect
                        {
                            BlurRadius = 8,
                            Opacity = 0.12,
                            ShadowDepth = 2
                        },
                    Child = content
                });

            return panel;
        }

        private Canvas BuildSheetPage(
            IReadOnlyList<Asset> pageAssets,
            BarcodePrintSettings settings,
            double scale)
        {
            double pageWidth =
                MmToWpf(BarcodePrintLayout.A4WidthMm) * scale;

            double pageHeight =
                MmToWpf(BarcodePrintLayout.A4HeightMm) * scale;

            var canvas = new Canvas
            {
                Width = pageWidth,
                Height = pageHeight,
                Background = Brushes.White
            };

            for (int index = 0;
                 index < pageAssets.Count;
                 index++)
            {
                int row = index / settings.Columns;
                int column = index % settings.Columns;

                double xMm =
                    settings.PageMarginMm +
                    column *
                    (settings.LabelWidthMm + settings.GapMm);

                double yMm =
                    settings.PageMarginMm +
                    row *
                    (settings.LabelHeightMm + settings.GapMm);

                var label = BuildLabel(
                    pageAssets[index],
                    settings,
                    scale);

                var border = new Border
                {
                    Width =
                        MmToWpf(settings.LabelWidthMm) * scale,
                    Height =
                        MmToWpf(settings.LabelHeightMm) * scale,
                    BorderBrush = Brushes.LightGray,
                    BorderThickness =
                        new Thickness(scale < 1 ? 0.7 : 0.3),
                    Background = Brushes.White,
                    Child = label
                };

                Canvas.SetLeft(
                    border,
                    MmToWpf(xMm) * scale);

                Canvas.SetTop(
                    border,
                    MmToWpf(yMm) * scale);

                canvas.Children.Add(border);
            }

            return canvas;
        }

        private FrameworkElement BuildLabel(
            Asset asset,
            BarcodePrintSettings settings,
            double scale)
        {
            double width =
                MmToWpf(settings.LabelWidthMm) * scale;

            double height =
                MmToWpf(settings.LabelHeightMm) * scale;

            var root = new Grid
            {
                Width = width,
                Height = height,
                Background = Brushes.White,
                Margin = new Thickness(1.5 * scale)
            };

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(
                        settings.CodeAreaPercent,
                        GridUnitType.Star)
                });

            root.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(
                        100 - settings.CodeAreaPercent,
                        GridUnitType.Star)
                });

            using GDI.Bitmap bitmap =
                CreateCodeBitmap(
                    asset.VesaitinKodu,
                    settings);

            var image = new Image
            {
                Source = ToBitmapImage(bitmap),
                Stretch =
                    settings.Format == BarcodeCodeFormat.QrCode
                        ? Stretch.Uniform
                        : Stretch.Fill,
                SnapsToDevicePixels = true,
                Margin = new Thickness(1.2 * scale)
            };

            RenderOptions.SetBitmapScalingMode(
                image,
                BitmapScalingMode.NearestNeighbor);

            Grid.SetRow(image, 0);
            root.Children.Add(image);

            var textPanel = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            textPanel.Children.Add(
                new TextBlock
                {
                    Text = asset.VesaitinKodu,
                    TextAlignment = TextAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontFamily = new FontFamily("Courier New"),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = settings.FontSize * scale,
                    Foreground = Brushes.Black
                });

            if (settings.ShowAssetName &&
                !string.IsNullOrWhiteSpace(asset.VesaitinAdi))
            {
                textPanel.Children.Add(
                    new TextBlock
                    {
                        Text = asset.VesaitinAdi,
                        TextAlignment = TextAlignment.Center,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        FontSize =
                            Math.Max(
                                5,
                                settings.FontSize * 0.82) * scale,
                        Foreground = Brushes.Black,
                        Margin = new Thickness(2 * scale, 0, 2 * scale, 0)
                    });
            }

            Grid.SetRow(textPanel, 1);
            root.Children.Add(textPanel);

            return root;
        }

        private static GDI.Bitmap CreateCodeBitmap(
            string value,
            BarcodePrintSettings settings)
        {
            string code = value ?? string.Empty;

            int width =
                MmToBmpPx(settings.LabelWidthMm * 0.92);

            int height =
                MmToBmpPx(
                    settings.LabelHeightMm *
                    settings.CodeAreaPercent /
                    100.0);

            if (settings.Format == BarcodeCodeFormat.QrCode)
            {
                int side =
                    Math.Max(
                        100,
                        Math.Min(width, height));

                width = side;
                height = side;
            }

            var writer =
                new ZXing.Windows.Compatibility.BarcodeWriter
                {
                    Format =
                        settings.Format == BarcodeCodeFormat.QrCode
                            ? BarcodeFormat.QR_CODE
                            : BarcodeFormat.CODE_128,
                    Options =
                        new EncodingOptions
                        {
                            Width = width,
                            Height = height,
                            Margin =
                                settings.Format == BarcodeCodeFormat.QrCode
                                    ? 1
                                    : 10,
                            PureBarcode = true
                        }
                };

            return writer.Write(code);
        }

        private void PrintButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (!TryReadSettings(out var settings))
                return;

            if (_assetsToPrint.Count == 0)
                return;

            var dialog = new PrintDialog();

            if (dialog.ShowDialog() != true)
                return;

            var document = new FixedDocument();

            if (settings.PaperMode == BarcodePrintPaperMode.A4)
            {
                double pageWidth =
                    MmToWpf(BarcodePrintLayout.A4WidthMm);

                double pageHeight =
                    MmToWpf(BarcodePrintLayout.A4HeightMm);

                document.DocumentPaginator.PageSize =
                    new Size(pageWidth, pageHeight);

                int capacity = settings.PageCapacity;

                for (int offset = 0;
                     offset < _assetsToPrint.Count;
                     offset += capacity)
                {
                    var pageAssets = _assetsToPrint
                        .Skip(offset)
                        .Take(capacity)
                        .ToList();

                    var content =
                        BuildSheetPage(
                            pageAssets,
                            settings,
                            scale: 1.0);

                    AddFixedPage(
                        document,
                        content,
                        pageWidth,
                        pageHeight);
                }
            }
            else
            {
                double pageWidth =
                    MmToWpf(settings.LabelWidthMm);

                double pageHeight =
                    MmToWpf(settings.LabelHeightMm);

                document.DocumentPaginator.PageSize =
                    new Size(pageWidth, pageHeight);

                foreach (var asset in _assetsToPrint)
                {
                    var label =
                        BuildLabel(
                            asset,
                            settings,
                            scale: 1.0);

                    AddFixedPage(
                        document,
                        label,
                        pageWidth,
                        pageHeight);
                }
            }

            dialog.PrintDocument(
                document.DocumentPaginator,
                "QR / Barkod Etiketləri");

            NotificationService.Success(
                this,
                $"{_assetsToPrint.Count} etiket printerə göndərildi.",
                title: "Çap");
        }

        private static void AddFixedPage(
            FixedDocument document,
            FrameworkElement content,
            double width,
            double height)
        {
            content.Measure(
                new Size(width, height));

            content.Arrange(
                new Rect(0, 0, width, height));

            content.UpdateLayout();

            var page = new FixedPage
            {
                Width = width,
                Height = height,
                Background = Brushes.White
            };

            page.Children.Add(content);

            var pageContent = new PageContent();

            ((System.Windows.Markup.IAddChild)pageContent)
                .AddChild(page);

            document.Pages.Add(pageContent);
        }

        private static BitmapImage ToBitmapImage(
            GDI.Bitmap bitmap)
        {
            using var stream = new MemoryStream();

            bitmap.Save(
                stream,
                System.Drawing.Imaging.ImageFormat.Png);

            stream.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.StreamSource = stream;
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            return image;
        }

        private static string GetSelectedTag(ComboBox comboBox)
            => (comboBox?.SelectedItem as ComboBoxItem)?
                .Tag?
                .ToString();

        private static void SelectComboItemByTag(
            ComboBox comboBox,
            string tag)
        {
            foreach (var item in comboBox.Items
                .OfType<ComboBoxItem>())
            {
                if (!string.Equals(
                        item.Tag?.ToString(),
                        tag,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                comboBox.SelectedItem = item;
                return;
            }
        }

        private static bool TryParseDouble(
            string text,
            out double value)
        {
            if (double.TryParse(
                    text,
                    NumberStyles.Float,
                    CultureInfo.CurrentCulture,
                    out value))
            {
                return true;
            }

            return double.TryParse(
                text?.Replace(',', '.'),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out value);
        }

        private void ShowValidation(string message)
        {
            ValidationTextBlock.Text = message;
            ValidationTextBlock.Visibility =
                Visibility.Visible;
        }

        private void HideValidation()
        {
            ValidationTextBlock.Text = string.Empty;
            ValidationTextBlock.Visibility =
                Visibility.Collapsed;
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
            else if (Keyboard.Modifiers == ModifierKeys.Control &&
                     e.Key == Key.P)
            {
                PrintButton_Click(
                    PrintButton,
                    new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
            => Close();
    }
}
