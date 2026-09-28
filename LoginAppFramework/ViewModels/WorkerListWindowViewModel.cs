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
        private readonly IWorkerService _workerService;
        private WorkerFilterViewModel _filters;
        private WorkerStatusFilter _statusFilter = WorkerStatusFilter.Active;
        private int _selectedCount;

        public event PropertyChangedEventHandler PropertyChanged;

        public WorkerListWindowViewModel(IWorkerService workerService = null)
        {
            _workerService = workerService ?? AppServices.Workers;
        }

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
                OnPropertyChanged(nameof(IsActiveFilterSelected));
                OnPropertyChanged(nameof(IsInactiveFilterSelected));
                OnPropertyChanged(nameof(IsAllFilterSelected));
                ApplyFilters();
            }
        }

        public bool HasResults => VisibleWorkers.Count > 0;
        public int SelectedCount
        {
            get => _selectedCount;
            private set
            {
                if (_selectedCount == value) return;
                _selectedCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasBulkSelection));
                OnPropertyChanged(nameof(SelectionCountText));
            }
        }

        public bool HasBulkSelection => SelectedCount > 1;
        public string SelectionCountText => $"{SelectedCount} element seçildi";

        public bool IsActiveFilterSelected
        {
            get => StatusFilter == WorkerStatusFilter.Active;
            set { if (value) StatusFilter = WorkerStatusFilter.Active; }
        }

        public bool IsInactiveFilterSelected
        {
            get => StatusFilter == WorkerStatusFilter.Inactive;
            set { if (value) StatusFilter = WorkerStatusFilter.Inactive; }
        }

        public bool IsAllFilterSelected
        {
            get => StatusFilter == WorkerStatusFilter.All;
            set { if (value) StatusFilter = WorkerStatusFilter.All; }
        }

        public void SetSelectedCount(int count)
            => SelectedCount = Math.Max(0, count);

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

        public System.Threading.Tasks.Task<int> SynchronizeAsync()
            => _workerService.SynchronizeFromHrAsync();

        public int SetActiveState(IEnumerable<int> workerIds, bool isActive)
            => _workerService.SetActiveState(workerIds, isActive);

        public void SaveWorker(Worker worker)
            => _workerService.Save(worker);

        public void DeleteWorker(Worker worker)
            => _workerService.Delete(worker);

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
