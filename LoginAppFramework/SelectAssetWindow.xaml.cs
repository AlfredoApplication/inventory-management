using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class SelectAssetWindow : Window
    {
        public Asset SelectedAsset { get; private set; }

        private readonly List<Asset> _availableAssets;

        public SelectAssetWindow(List<Asset> availableAssets)
        {
            InitializeComponent();

            _availableAssets = availableAssets;

            AvailableAssetsListView.ItemsSource = _availableAssets;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            IEnumerable<Asset> filteredView = _availableAssets;

            string searchText = SearchBox.Text;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredView = filteredView.Where(asset =>
                    (asset.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (asset.Category?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (asset.SerialNumber?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }

            AvailableAssetsListView.ItemsSource = filteredView.ToList();
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (AvailableAssetsListView.SelectedItem is Asset selected)
            {
                SelectedAsset = selected;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                NotificationService.Info(
                    this,
                    "Zəhmət olmasa siyahıdan bir vəsait seçin.",
                    title: "Seçim Edilməyib");
            }
        }

        private void AvailableAssetsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AvailableAssetsListView.SelectedItem != null)
            {
                SelectButton_Click(sender, e);
            }
        }
    }
}