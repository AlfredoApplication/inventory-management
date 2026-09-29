using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LoginAppFramework
{
    public class ColumnFilterItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public string Value { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(
            [CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(name));
    }

    public sealed class ColumnFilterSnapshot
    {
        public string SearchText { get; init; }
        public IReadOnlyDictionary<string, bool> Selections { get; init; }
    }

    public class ColumnFilterViewModel : INotifyPropertyChanged
    {
        private string _searchText;
        private readonly List<ColumnFilterItem> _allItems;
        private List<ColumnFilterItem> _filteredItems;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                UpdateFilteredItems();
            }
        }

        public List<ColumnFilterItem> FilteredItems
        {
            get => _filteredItems;
            private set
            {
                _filteredItems = value;
                OnPropertyChanged();
            }
        }

        public bool HasActiveFilters
            => _allItems.Any(item => !item.IsChecked);

        public ColumnFilterViewModel(IEnumerable<string> distinctValues)
        {
            _allItems = distinctValues
                .Where(value => !string.IsNullOrEmpty(value))
                .Distinct()
                .OrderBy(value => value)
                .Select(value => new ColumnFilterItem
                {
                    Value = value,
                    IsChecked = true
                })
                .ToList();

            foreach (var item in _allItems)
            {
                item.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(ColumnFilterItem.IsChecked))
                        OnPropertyChanged(nameof(HasActiveFilters));
                };
            }

            FilteredItems = new List<ColumnFilterItem>(_allItems);
        }

        public ColumnFilterSnapshot CreateSnapshot()
            => new()
            {
                SearchText = SearchText,
                Selections = _allItems.ToDictionary(
                    item => item.Value,
                    item => item.IsChecked,
                    StringComparer.Ordinal)
            };

        public void RestoreSnapshot(ColumnFilterSnapshot snapshot)
        {
            if (snapshot == null) return;

            foreach (var item in _allItems)
            {
                if (snapshot.Selections.TryGetValue(
                    item.Value,
                    out bool isChecked))
                {
                    item.IsChecked = isChecked;
                }
            }

            _searchText = snapshot.SearchText;
            OnPropertyChanged(nameof(SearchText));
            UpdateFilteredItems();
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        private void UpdateFilteredItems()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredItems = new List<ColumnFilterItem>(_allItems);
                return;
            }

            FilteredItems = _allItems
                .Where(item =>
                    item.Value.Contains(
                        SearchText,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public void SelectAll()
        {
            foreach (var item in _allItems)
                item.IsChecked = true;

            OnPropertyChanged(nameof(HasActiveFilters));
        }

        public void ClearAll()
        {
            foreach (var item in _allItems)
                item.IsChecked = false;

            OnPropertyChanged(nameof(HasActiveFilters));
        }

        public List<string> GetSelectedValues()
            => _allItems
                .Where(item => item.IsChecked)
                .Select(item => item.Value)
                .ToList();

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged(
            [CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(name));
    }
}
