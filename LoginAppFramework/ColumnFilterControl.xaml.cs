using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class ColumnFilterControl : UserControl
    {
        public event EventHandler<ColumnFilterEventArgs> FilterApplied;

        private ColumnFilterViewModel _viewModel;

        public ColumnFilterControl()
        {
            InitializeComponent();
        }

        public void InitializeFilter(ColumnFilterViewModel viewModel)
        {
            _viewModel = viewModel;
            DataContext = _viewModel;
            UpdateFilterIcon();
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.SelectAll();
            UpdateFilterIcon();
        }

        private void ClearAllButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel?.ClearAll();
            UpdateFilterIcon();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            FilterToggleButton.IsChecked = false;
            UpdateFilterIcon();
            FilterApplied?.Invoke(this, new ColumnFilterEventArgs(_viewModel));
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            FilterToggleButton.IsChecked = false;
        }

        private void FilterItem_CheckChanged(object sender, RoutedEventArgs e)
        {
            UpdateFilterIcon();
        }

        private void UpdateFilterIcon()
        {
            if (_viewModel != null && _viewModel.HasActiveFilters)
            {
                // Show filtered icon (different color)
                FilterIcon.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 212)); // Blue color
                FilterIcon.FontWeight = FontWeights.Bold;
            }
            else
            {
                // Show default icon
                FilterIcon.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102)); // Gray
                FilterIcon.FontWeight = FontWeights.Normal;
            }
        }
    }

    public class ColumnFilterEventArgs : EventArgs
    {
        public ColumnFilterViewModel FilterViewModel { get; }

        public ColumnFilterEventArgs(ColumnFilterViewModel viewModel)
        {
            FilterViewModel = viewModel;
        }
    }
}
