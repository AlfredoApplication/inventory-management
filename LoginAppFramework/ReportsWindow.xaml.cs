using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

namespace LoginAppFramework
{
    public partial class ReportsWindow : Window
    {
        // Hesabatın UI adı ilə Enum dəyərini əlaqələndiririk
        private readonly Dictionary<string, ReportGenerator.ReportType> _reportTypes;

        public ReportsWindow()
        {
            InitializeComponent();
            _reportTypes = new Dictionary<string, ReportGenerator.ReportType>
            {
                { "Ümumi İnventar Hesabatı", ReportGenerator.ReportType.FullInventory },
                { "Departamentlər Üzrə Vəsait Hesabatı", ReportGenerator.ReportType.AssetsByDepartment },
                // Buraya gələcəkdə yeni hesabatlar əlavə edə bilərsiniz
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ReportTypeComboBox.ItemsSource = _reportTypes.Keys;
            ReportTypeComboBox.SelectedIndex = 0;
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            var selectedReportName = ReportTypeComboBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedReportName) || !_reportTypes.ContainsKey(selectedReportName))
            {
                MessageBox.Show("Zəhmət olmasa, etibarlı bir hesabat növü seçin.", "Xəta");
                return;
            }

            var reportType = _reportTypes[selectedReportName];

            var saveFileDialog = new SaveFileDialog
            {
                Filter = "PDF Sənədi|*.pdf",
                Title = "PDF Hesabatını Yadda Saxla",
                FileName = $"{selectedReportName}_{DateTime.Now:yyyyMMdd}.pdf"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                string filePath = saveFileDialog.FileName;
                GenerateButton.IsEnabled = false;
                StatusTextBlock.Text = "Hesabat yaradılır, zəhmət olmasa gözləyin...";

                try
                {
                    // Keşlənmiş məlumatları götürürük
                    var allAssets = AppData.GetAssets();

                    // Seçilmiş hesabat növünə uyğun funksiyanı çağırırıq
                    switch (reportType)
                    {
                        case ReportGenerator.ReportType.FullInventory:
                            ReportGenerator.GenerateFullInventoryReport(allAssets, filePath);
                            break;
                        case ReportGenerator.ReportType.AssetsByDepartment:
                            ReportGenerator.GenerateAssetsByDepartmentReport(allAssets, filePath);
                            break;
                    }

                    StatusTextBlock.Text = string.Empty;
                    var result = MessageBox.Show("Hesabat uğurla yaradıldı!\nİndi faylı açmaq istəyirsinizmi?", "Uğurlu Əməliyyat", MessageBoxButton.YesNo, MessageBoxImage.Information);

                    if (result == MessageBoxResult.Yes)
                    {
                        Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
                    }
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = string.Empty;
                    MessageBox.Show($"Hesabat yaradılarkən xəta baş verdi: {ex.Message}", "Xəta", MessageBoxButton.OK, MessageBoxImage.Error);
                }
                finally
                {
                    GenerateButton.IsEnabled = true;
                }
            }
        }
    }
}