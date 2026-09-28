using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;

namespace LoginAppFramework
{
    public class WorkerFilterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Action FilterChanged { get; set; }
        private string _searchText;

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(nameof(SearchText)); FilterChanged?.Invoke(); }
        }

        public ObservableCollection<FilterOption<string>> DepartmentOptions { get; set; }

        // --- NEW PROPERTIES FOR THE SUMMARY PANEL ---
        private string _totalPurchaseCost;
        public string TotalPurchaseCost
        {
            get => _totalPurchaseCost;
            set { _totalPurchaseCost = value; OnPropertyChanged(nameof(TotalPurchaseCost)); }
        }

        private string _totalDepreciation;
        public string TotalDepreciation
        {
            get => _totalDepreciation;
            set { _totalDepreciation = value; OnPropertyChanged(nameof(TotalDepreciation)); }
        }

        private string _totalCurrentValue;
        public string TotalCurrentValue
        {
            get => _totalCurrentValue;
            set { _totalCurrentValue = value; OnPropertyChanged(nameof(TotalCurrentValue)); }
        }

        private string _totalMonthlyDepreciation;
        public string TotalMonthlyDepreciation
        {
            get => _totalMonthlyDepreciation;
            set { _totalMonthlyDepreciation = value; OnPropertyChanged(nameof(TotalMonthlyDepreciation)); }
        }

        public WorkerFilterViewModel(List<Worker> allWorkers)
        {
            var departments = AppData.GetWorkerDepartments().OrderBy(d => d);
            DepartmentOptions = new ObservableCollection<FilterOption<string>>(departments.Select(d => new FilterOption<string> { Value = d, DisplayName = d }));
            foreach (var option in DepartmentOptions) { option.PropertyChanged += (s, e) => FilterChanged?.Invoke(); }

            // Initialize with zero values
            UpdateFinancialSummary(new List<WorkerViewModel>());
        }

        // --- NEW METHOD TO CALCULATE AND UPDATE THE TOTALS ---
        public void UpdateFinancialSummary(List<WorkerViewModel> visibleWorkers)
        {
            var culture = CultureInfo.GetCultureInfo("az-Latn-AZ");

            decimal purchaseCost = visibleWorkers.Sum(w => w.RawTotalPurchaseCost);
            decimal depreciation = visibleWorkers.Sum(w => w.RawTotalDepreciation);
            decimal currentValue = visibleWorkers.Sum(w => w.RawTotalCurrentValue);
            decimal monthlyDepreciation = visibleWorkers.Sum(w => w.RawMonthlyDepreciation);

            TotalPurchaseCost = purchaseCost.ToString("C", culture);
            TotalDepreciation = depreciation.ToString("C", culture);
            TotalCurrentValue = currentValue.ToString("C", culture);
            TotalMonthlyDepreciation = monthlyDepreciation.ToString("C", culture);
        }

        public void Clear()
        {
            SearchText = string.Empty;
            foreach (var option in DepartmentOptions) { option.IsChecked = false; }
            OnPropertyChanged(nameof(SearchText));
        }

        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}