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
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class ColumnFilterViewModel : INotifyPropertyChanged
    {
        private string _searchText;
        private List<ColumnFilterItem> _allItems;
        private List<ColumnFilterItem> _filteredItems;

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText != value)
                {
                    _searchText = value;
                    OnPropertyChanged();
                    UpdateFilteredItems();
                }
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

        public bool HasActiveFilters => _allItems != null && _allItems.Any(item => !item.IsChecked);

        public ColumnFilterViewModel(IEnumerable<string> distinctValues)
        {
            _allItems = distinctValues
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct()
                .OrderBy(v => v)
                .Select(v => new ColumnFilterItem { Value = v, IsChecked = true })
                .ToList();

            FilteredItems = new List<ColumnFilterItem>(_allItems);
        }

        private void UpdateFilteredItems()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredItems = new List<ColumnFilterItem>(_allItems);
            }
            else
            {
                FilteredItems = _allItems
                    .Where(item => item.Value.ToLower().Contains(SearchText.ToLower()))
                    .ToList();
            }
        }

        public void SelectAll()
        {
            foreach (var item in _allItems)
            {
                item.IsChecked = true;
            }
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        public void ClearAll()
        {
            foreach (var item in _allItems)
            {
                item.IsChecked = false;
            }
            OnPropertyChanged(nameof(HasActiveFilters));
        }

        public List<string> GetSelectedValues()
        {
            return _allItems.Where(item => item.IsChecked).Select(item => item.Value).ToList();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
