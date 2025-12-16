using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QRCoder;

namespace LoginAppFramework
{
    public partial class PrintQrCodesWindow : Window
    {
        public PrintQrCodesWindow(List<Asset> assetsToPrint)
        {
            InitializeComponent();

            int count = assetsToPrint.Count;
            this.Title = $"QR Kodları Çap Et ({count} ədəd)";
            CountTextBlock.Text = $"{count} seçilmiş vəsait üçün yaradılmış QR kodlar";

            foreach (var asset in assetsToPrint)
            {
                var stackPanel = new StackPanel
                {
                    Width = 150,
                    Height = 180,
                    Margin = new Thickness(10)
                };

                QRCodeGenerator qrGenerator = new QRCodeGenerator();
                QRCodeData qrCodeData = qrGenerator.CreateQrCode(asset.VesaitinKodu, QRCodeGenerator.ECCLevel.Q);
                QRCode qrCode = new QRCode(qrCodeData);
                Bitmap qrCodeImage = qrCode.GetGraphic(20);

                var imageControl = new System.Windows.Controls.Image
                {
                    Width = 140,
                    Height = 140,
                    Source = ConvertBitmapToBitmapImage(qrCodeImage)
                };

                var textBlock = new TextBlock
                {
                    Text = asset.VesaitinAdi,
                    TextWrapping = TextWrapping.Wrap,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 5, 0, 0)
                };

                stackPanel.Children.Add(imageControl);
                stackPanel.Children.Add(textBlock);
                QrCodePanel.Children.Add(stackPanel);
            }
        }

        private void PrintButton_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                printDialog.PrintVisual(printArea, "QR Kodları");
            }
        }

        private BitmapImage ConvertBitmapToBitmapImage(Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                memory.Position = 0;
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.StreamSource = memory;
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapImage.EndInit();
                bitmapImage.Freeze();
                return bitmapImage;
            }
        }
    }
}