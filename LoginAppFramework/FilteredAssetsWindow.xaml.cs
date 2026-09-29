using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class FilteredAssetsWindow : Window
    {
        private GridViewColumnHeader _lastHeaderClicked;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        private readonly string _title;
        private readonly List<Asset> _allAssetsInThisView;

        public FilteredAssetsWindow(string title, List<Asset> assetsToShow)
        {
            InitializeComponent();
            _title = title;
            _allAssetsInThisView = assetsToShow;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.Title = _title;
            TitleTextBlock.Text = _title;
            ApplyFilters();
            AssetsListView.AddHandler(GridViewColumnHeader.ClickEvent, new RoutedEventHandler(GridViewColumnHeader_Click));
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            IEnumerable<Asset> filteredView = _allAssetsInThisView;
            string searchText = SearchBox.Text;

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredView = filteredView.Where(asset =>
                    (asset.Name?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (asset.Category?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (asset.AssignedUser?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (asset.SerialNumber?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }

            var results = filteredView.ToList();
            AssetsListView.ItemsSource = results;

            // --- THE FIX IS HERE ---
            CountTextBlock.Text = $"{results.Count} element tapıldı";
        }

        private void AssetsListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AssetsListView.SelectedItem is not Asset selectedAsset)
                return;

            var ownerWindow = Owner;
            int assetId = selectedAsset.Id;

            Close();

            Dispatcher.BeginInvoke(
                new Action(async () =>
                    await NavigationManager.GoToAssetWindow(
                        ownerWindow,
                        assetId)));
        }

        private void GridViewColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader { Role: not GridViewColumnHeaderRole.Padding } headerClicked)
            {
                ListSortDirection direction;
                if (headerClicked != _lastHeaderClicked)
                {
                    direction = ListSortDirection.Ascending;
                }
                else
                {
                    direction = _lastDirection == ListSortDirection.Ascending ? ListSortDirection.Descending : ListSortDirection.Ascending;
                }

                if (headerClicked.Column.DisplayMemberBinding is Binding { Path.Path: var sortBy })
                {
                    Sort(sortBy, direction);
                }

                _lastHeaderClicked = headerClicked;
                _lastDirection = direction;
            }
        }

        private void Sort(string sortBy, ListSortDirection direction)
        {
            ICollectionView dataView = CollectionViewSource.GetDefaultView(AssetsListView.ItemsSource);
            if (dataView != null)
            {
                dataView.SortDescriptions.Clear();
                SortDescription sd = new(sortBy, direction);
                dataView.SortDescriptions.Add(sd);
                dataView.Refresh();
            }
        }
    }
}