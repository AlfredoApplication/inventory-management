using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace LoginAppFramework
{
    public sealed class AuditRestoreBatchResult
    {
        public int SuccessCount { get; init; }
        public int FailureCount { get; init; }
        public IReadOnlyList<string> Failures { get; init; } = Array.Empty<string>();
    }

    public sealed class HistoryLogWindowViewModel : INotifyPropertyChanged
    {
        private const int PageSize = 100;
        private readonly IAuditService _auditService;
        private readonly List<HistoryLogViewModel> _allEntries = new();
        private List<HistoryLogViewModel> _filteredEntries = new();

        private string _searchText = string.Empty;
        private string _selectedAction;
        private DateTime? _selectedDate;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private bool _hasRestorableItems;
        private int _selectedRestoreCount;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<HistoryLogViewModel> PagedEntries { get; } = new();
        public ObservableCollection<string> ActionTypes { get; } = new()
        {
            "Bütün Əməliyyatlar",
            "Təhkimat",
            "Yaradılan",
            "Dəyişdirilən",
            "Silinən"
        };

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value ?? string.Empty;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string SelectedAction
        {
            get => _selectedAction;
            set
            {
                if (_selectedAction == value) return;
                _selectedAction = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public DateTime? SelectedDate
        {
            get => _selectedDate;
            set
            {
                if (_selectedDate == value) return;
                _selectedDate = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (_currentPage == value) return;
                _currentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfo));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }

        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                if (_totalPages == value) return;
                _totalPages = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(PageInfo));
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
            }
        }

        public int FilteredCount => _filteredEntries.Count;
        public bool HasResults => FilteredCount > 0;
        public string CountText => $"{FilteredCount} qeyd";
        public string PageInfo => $"Səhifə {CurrentPage} / {TotalPages} ({FilteredCount} qeyd)";
        public bool CanGoPrevious => CurrentPage > 1;
        public bool CanGoNext => CurrentPage < TotalPages;

        public bool HasRestorableItems
        {
            get => _hasRestorableItems;
            private set
            {
                if (_hasRestorableItems == value) return;
                _hasRestorableItems = value;
                OnPropertyChanged();
            }
        }

        public int SelectedRestoreCount
        {
            get => _selectedRestoreCount;
            private set
            {
                if (_selectedRestoreCount == value) return;
                _selectedRestoreCount = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedRestoreCountText));
                OnPropertyChanged(nameof(CanRestoreSelected));
            }
        }

        public string SelectedRestoreCountText => $"{SelectedRestoreCount} seçilib";
        public bool CanRestoreSelected => SelectedRestoreCount > 0;

        public HistoryLogWindowViewModel(IAuditService auditService = null)
        {
            _auditService = auditService ?? AppServices.Audit;
            _selectedAction = ActionTypes[0];
        }

        public async Task LoadAsync()
        {
            var data = await Task.Run(() =>
            {
                var assetLogs = _auditService.GetAssetLogs();
                var assignmentHistory = _auditService.GetAssignmentHistory();
                var existingAssetCodes = _auditService.GetCurrentAssetCodes();

                var assetLogViewModels = assetLogs.Select(log =>
                    new HistoryLogViewModel(
                        log,
                        string.IsNullOrWhiteSpace(log.VesaitinKodu) ||
                        !existingAssetCodes.Contains(log.VesaitinKodu)));

                var assignmentViewModels = assignmentHistory
                    .Where(h => h.Asset != null)
                    .Select(h => new HistoryLogViewModel(h, h.Asset.Name));

                return assetLogViewModels
                    .Concat(assignmentViewModels)
                    .OrderByDescending(vm => vm.Timestamp)
                    .ToList();
            });

            ReplaceEntries(data);
        }

        public void ClearFilters()
        {
            _searchText = string.Empty;
            _selectedAction = ActionTypes[0];
            _selectedDate = null;

            OnPropertyChanged(nameof(SearchText));
            OnPropertyChanged(nameof(SelectedAction));
            OnPropertyChanged(nameof(SelectedDate));

            ApplyFilters();
        }

        public void GoToPreviousPage()
        {
            if (!CanGoPrevious) return;
            CurrentPage--;
            UpdatePage();
        }

        public void GoToNextPage()
        {
            if (!CanGoNext) return;
            CurrentPage++;
            UpdatePage();
        }

        public void GoToPage(int page)
        {
            if (page < 1 || page > TotalPages || page == CurrentPage) return;
            CurrentPage = page;
            UpdatePage();
        }

        public void SelectAllRestorable(bool selected)
        {
            foreach (var item in _filteredEntries.Where(v => v.CanRestore))
            {
                item.IsSelected = selected;
            }

            UpdateRestoreState();
        }

        public IReadOnlyList<HistoryLogViewModel> GetSelectedRestoreEntries()
            => _allEntries
                .Where(v => v.CanRestore && v.IsSelected && v.Log != null)
                .ToList();

        public async Task<AuditRestoreBatchResult> RestoreSelectedAsync()
        {
            var selected = GetSelectedRestoreEntries();
            if (selected.Count == 0) return new AuditRestoreBatchResult();

            var result = await Task.Run(() =>
            {
                int success = 0;
                var failures = new List<string>();

                foreach (var item in selected)
                {
                    try
                    {
                        var restore = _auditService.RestoreDeletedAsset(item.Log);
                        if (restore.Success) success++;
                        else failures.Add(restore.Message);
                    }
                    catch (Exception ex)
                    {
                        failures.Add($"{item.Log.VesaitinKodu}: {ex.Message}");
                    }
                }

                return new AuditRestoreBatchResult
                {
                    SuccessCount = success,
                    FailureCount = failures.Count,
                    Failures = failures
                };
            });

            await LoadAsync();
            return result;
        }

        private void ReplaceEntries(IEnumerable<HistoryLogViewModel> entries)
        {
            foreach (var item in _allEntries)
            {
                item.PropertyChanged -= HistoryItem_PropertyChanged;
            }

            _allEntries.Clear();
            _allEntries.AddRange(entries);

            foreach (var item in _allEntries)
            {
                item.PropertyChanged += HistoryItem_PropertyChanged;
            }

            ApplyFilters();
        }

        private void HistoryItem_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HistoryLogViewModel.IsSelected))
            {
                UpdateRestoreState();
            }
        }

        private void ApplyFilters()
        {
            IEnumerable<HistoryLogViewModel> filtered = _allEntries;

            if (SelectedDate.HasValue)
            {
                var date = SelectedDate.Value.Date;
                filtered = filtered.Where(e => e.Timestamp.Date == date);
            }

            if (!string.IsNullOrWhiteSpace(SelectedAction) &&
                SelectedAction != ActionTypes[0])
            {
                filtered = filtered.Where(entry =>
                    SelectedAction == "Təhkimat"
                        ? entry.Status == "Təhkimat"
                        : entry.Status == SelectedAction);
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                filtered = filtered.Where(entry =>
                    entry.Description?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false);
            }

            _filteredEntries = filtered.ToList();
            CurrentPage = 1;
            TotalPages = Math.Max(1, (int)Math.Ceiling((double)_filteredEntries.Count / PageSize));

            OnPropertyChanged(nameof(FilteredCount));
            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(CountText));
            OnPropertyChanged(nameof(PageInfo));

            UpdatePage();
            UpdateRestoreState();
        }

        private void UpdatePage()
        {
            if (CurrentPage > TotalPages) CurrentPage = TotalPages;

            PagedEntries.Clear();
            foreach (var entry in _filteredEntries
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize))
            {
                PagedEntries.Add(entry);
            }

            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
        }

        private void UpdateRestoreState()
        {
            HasRestorableItems = _allEntries.Any(v => v.CanRestore);
            SelectedRestoreCount = _allEntries.Count(v => v.CanRestore && v.IsSelected);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
