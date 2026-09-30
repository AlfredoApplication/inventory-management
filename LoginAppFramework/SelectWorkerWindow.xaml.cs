using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class SelectWorkerWindow : Window
    {
        public Worker SelectedWorker { get; private set; }

        // This now stores the original, unfiltered list of available workers
        private readonly List<Worker> _availableWorkers;

        public SelectWorkerWindow(List<Worker> availableWorkers)
        {
            InitializeComponent();

            // Store the full list
            _availableWorkers = availableWorkers.OrderBy(w => w.per_adiper_soyadi).ToList();

            // Initially, display the full, sorted list
            WorkersListView.ItemsSource = _availableWorkers;
            UpdateEmptyState(_availableWorkers.Count);
        }

        // This method is called whenever the text in the search box changes
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Start with the original, complete list of workers
            IEnumerable<Worker> filteredView = _availableWorkers;

            string searchText = SearchBox.Text;

            // If the search box isn't empty, apply the filter
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filteredView = filteredView.Where(worker =>
                    (worker.per_adiper_soyadi?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (worker.pgk_gorev_adi?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (worker.pdp_adi?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }

            // Update the ListView with the filtered results
            var results = filteredView.ToList();
            WorkersListView.ItemsSource = results;
            UpdateEmptyState(results.Count);
        }

        private void UpdateEmptyState(int count)
        {
            WorkersListView.Visibility =
                count > 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyState.Visibility =
                count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            if (WorkersListView.SelectedItem is Worker selected)
            {
                SelectedWorker = selected;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                NotificationService.Info(
                    this,
                    "Zəhmət olmasa siyahıdan bir işçi seçin.",
                    title: "Seçim Edilməyib");
            }
        }

        private void WorkersListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (WorkersListView.SelectedItem != null)
            {
                SelectButton_Click(sender, e);
            }
        }
    }
}