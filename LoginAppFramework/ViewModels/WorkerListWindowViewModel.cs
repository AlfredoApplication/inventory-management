using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LoginAppFramework
{
    public enum WorkerStatusFilter
    {
        Active,
        Inactive,
        All
    }

    public sealed class WorkerListWindowViewModel : INotifyPropertyChanged
    {
        private readonly List<WorkerViewModel> _allWorkers = new();
        private WorkerFilterViewModel _filters;
        private WorkerStatusFilter _statusFilter = WorkerStatusFilter.Active;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<WorkerViewModel> VisibleWorkers { get; } = new();
        public ObservableCollection<string> ActiveFilterTags { get; } = new();

        public WorkerFilterViewModel Filters
        {
            get => _filters;
            private set
            {
                if (_filters == value) return;
                if (_filters != null) _filters.FilterChanged -= ApplyFilters;
                _filters = value;
                if (_filters != null) _filters.FilterChanged += ApplyFilters;
                OnPropertyChanged();
            }
        }

        public WorkerStatusFilter StatusFilter
        {
            get => _statusFilter;
            set
            {
                if (_statusFilter == value) return;
                _statusFilter = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public bool HasResults => VisibleWorkers.Count > 0;

        public void Refresh()
        {
            var allWorkers = AppData.GetWorkers();
            var allAssets = AppData.GetAssets();
            var assetsByWorker = allAssets
                .Where(a => a.WorkerId.HasValue)
                .ToLookup(a => a.WorkerId.Value);

            _allWorkers.Clear();
            _allWorkers.AddRange(
                allWorkers.Select(w =>
                    new WorkerViewModel(w, assetsByWorker[w.Id].ToList())));

            string previousSearch = Filters?.SearchText;
            var selectedDepartments = Filters?.DepartmentOptions
                .Where(o => o.IsChecked)
                .Select(o => o.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            Filters = new WorkerFilterViewModel(allWorkers)
            {
                SearchText = previousSearch ?? string.Empty
            };

            foreach (var option in Filters.DepartmentOptions)
            {
                if (selectedDepartments.Contains(option.Value))
                    option.IsChecked = true;
            }

            ApplyFilters();
        }

        public void ClearFilters()
        {
            StatusFilter = WorkerStatusFilter.Active;
            Filters?.Clear();
            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (Filters == null) return;

            IEnumerable<WorkerViewModel> filtered = _allWorkers;

            filtered = StatusFilter switch
            {
                WorkerStatusFilter.Active => filtered.Where(w => w.IsActive),
                WorkerStatusFilter.Inactive => filtered.Where(w => !w.IsActive),
                _ => filtered
            };

            if (!string.IsNullOrWhiteSpace(Filters.SearchText))
            {
                filtered = filtered.Where(w =>
                    w.Name?.Contains(Filters.SearchText, StringComparison.CurrentCultureIgnoreCase) ?? false);
            }

            var departments = Filters.DepartmentOptions
                .Where(o => o.IsChecked)
                .Select(o => o.Value)
                .ToHashSet();

            if (departments.Count > 0)
            {
                filtered = filtered.Where(w =>
                    !string.IsNullOrWhiteSpace(w.Department) &&
                    departments.Contains(w.Department));
            }

            var results = filtered.ToList();

            VisibleWorkers.Clear();
            foreach (var worker in results)
                VisibleWorkers.Add(worker);

            Filters.UpdateFinancialSummary(results);
            RebuildActiveFilterTags();

            OnPropertyChanged(nameof(HasResults));
        }

        private void RebuildActiveFilterTags()
        {
            ActiveFilterTags.Clear();

            ActiveFilterTags.Add(StatusFilter switch
            {
                WorkerStatusFilter.Active => "Status: Aktiv",
                WorkerStatusFilter.Inactive => "Status: Qeyri-aktiv",
                _ => "Status: Hamısı"
            });

            if (!string.IsNullOrWhiteSpace(Filters?.SearchText))
                ActiveFilterTags.Add($"Axtarış: '{Filters.SearchText}'");

            var departments = Filters?.DepartmentOptions
                .Where(o => o.IsChecked)
                .Select(o => o.Value)
                .ToList() ?? new List<string>();

            if (departments.Count > 0)
                ActiveFilterTags.Add($"Departament: {string.Join(", ", departments)}");
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
