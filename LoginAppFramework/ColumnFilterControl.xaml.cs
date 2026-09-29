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
        private ColumnFilterSnapshot _editSnapshot;
        private bool _acceptChanges;

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

        private void FilterToggleButton_Checked(
            object sender,
            RoutedEventArgs e)
        {
            if (_viewModel == null) return;

            _editSnapshot = _viewModel.CreateSnapshot();
            _acceptChanges = false;

            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    SearchTextBox.Focus();
                    SearchTextBox.SelectAll();
                }));
        }

        private void SelectAllButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _viewModel?.SelectAll();
            UpdateFilterIcon();
        }

        private void ClearAllButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _viewModel?.ClearAll();
            UpdateFilterIcon();
        }

        private void ApplyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _acceptChanges = true;
            FilterToggleButton.IsChecked = false;
            UpdateFilterIcon();

            FilterApplied?.Invoke(
                this,
                new ColumnFilterEventArgs(_viewModel));
        }

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _acceptChanges = false;
            FilterToggleButton.IsChecked = false;
        }

        private void FilterPopup_Closed(
            object sender,
            EventArgs e)
        {
            if (!_acceptChanges &&
                _editSnapshot != null &&
                _viewModel != null)
            {
                _viewModel.RestoreSnapshot(_editSnapshot);
            }

            _editSnapshot = null;
            _acceptChanges = false;
            UpdateFilterIcon();
        }

        private void FilterItem_CheckChanged(
            object sender,
            RoutedEventArgs e)
        {
            UpdateFilterIcon();
        }

        private void UpdateFilterIcon()
        {
            if (_viewModel != null &&
                _viewModel.HasActiveFilters)
            {
                FilterIcon.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(0, 120, 212));
                FilterIcon.FontWeight = FontWeights.Bold;
            }
            else
            {
                FilterIcon.Foreground =
                    new SolidColorBrush(
                        Color.FromRgb(102, 102, 102));
                FilterIcon.FontWeight = FontWeights.Normal;
            }
        }
    }

    public class ColumnFilterEventArgs : EventArgs
    {
        public ColumnFilterViewModel FilterViewModel { get; }

        public ColumnFilterEventArgs(
            ColumnFilterViewModel viewModel)
        {
            FilterViewModel = viewModel;
        }
    }
}
