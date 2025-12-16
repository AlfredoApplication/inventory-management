using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace LoginAppFramework
{
    public class AssetFilterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Action FilterChanged { get; set; }
        private string _searchText;

        // --- NEW PROPERTY FOR THE FILTER ---
        private bool _showOnlyUncategorized;

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(nameof(SearchText)); FilterChanged?.Invoke(); }
        }

        // --- NEW PUBLIC PROPERTY WITH NOTIFICATION ---
        public bool ShowOnlyUncategorized
        {
            get => _showOnlyUncategorized;
            set
            {
                if (_showOnlyUncategorized != value)
                {
                    _showOnlyUncategorized = value;
                    OnPropertyChanged(nameof(ShowOnlyUncategorized));
                    FilterChanged?.Invoke();
                }
            }
        }

        public DateTime? PurchaseDateStart { get; set; }
        public DateTime? PurchaseDateEnd { get; set; }

        public ObservableCollection<CategoryFilterNodeViewModel> CategoryTree { get; set; }
        public ObservableCollection<FilterOption<string>> StatusOptions { get; set; }
        public ObservableCollection<FilterOption<string>> DepartmentOptions { get; set; }

        // Column-specific filters (Excel-like)
        public Dictionary<string, ColumnFilterViewModel> ColumnFilters { get; set; }

        public AssetFilterViewModel()
        {
            var rootCategories = AppData.GetHierarchicalCategories();
            CategoryTree = new ObservableCollection<CategoryFilterNodeViewModel>();
            foreach (var cat in rootCategories)
            {
                CategoryTree.Add(BuildCategoryNode(cat, null));
            }

            var statuses = AppData.GetAssetStatuses()
                .Where(s => s != "Arxivdə")
                .OrderBy(s => s);
            StatusOptions = new ObservableCollection<FilterOption<string>>(statuses.Select(s => new FilterOption<string> { Value = s, DisplayName = s }));
            foreach (var option in StatusOptions) { option.PropertyChanged += (s, e) => FilterChanged?.Invoke(); }

            var departments = AppData.GetWorkerDepartments().OrderBy(d => d);
            DepartmentOptions = new ObservableCollection<FilterOption<string>>(departments.Select(d => new FilterOption<string> { Value = d, DisplayName = d }));
            foreach (var option in DepartmentOptions) { option.PropertyChanged += (s, e) => FilterChanged?.Invoke(); }

            // Initialize column filters dictionary
            ColumnFilters = new Dictionary<string, ColumnFilterViewModel>();
        }

        private CategoryFilterNodeViewModel BuildCategoryNode(DeviceCategory category, CategoryFilterNodeViewModel parent)
        {
            var node = new CategoryFilterNodeViewModel(category.Name, parent);
            node.FilterChanged += () => FilterChanged?.Invoke();

            foreach (var subCategory in category.Subcategories)
            {
                node.Subcategories.Add(BuildCategoryNode(subCategory, node));
            }
            return node;
        }

        public List<string> GetSelectedCategories()
        {
            var selected = new List<string>();
            void FindChecked(IEnumerable<CategoryFilterNodeViewModel> nodes)
            {
                foreach (var node in nodes)
                {
                    if (node.IsChecked == true)
                    {
                        selected.Add(node.Name);
                        AddAllChildren(node, selected);
                    }
                    else
                    {
                        FindChecked(node.Subcategories);
                    }
                }
            }

            void AddAllChildren(CategoryFilterNodeViewModel parent, List<string> list)
            {
                foreach (var child in parent.Subcategories)
                {
                    if (!list.Contains(child.Name))
                    {
                        list.Add(child.Name);
                    }
                    AddAllChildren(child, list);
                }
            }

            FindChecked(CategoryTree);
            return selected.Distinct().ToList();
        }

        public void Clear()
        {
            SearchText = string.Empty;
            PurchaseDateStart = null;
            PurchaseDateEnd = null;
            ShowOnlyUncategorized = false; // Reset the new filter
            foreach (var node in CategoryTree) { node.IsChecked = false; }
            foreach (var option in StatusOptions) { option.IsChecked = false; }
            foreach (var option in DepartmentOptions) { option.IsChecked = false; }

            // Clear column-specific filters
            foreach (var columnFilter in ColumnFilters.Values)
            {
                columnFilter.SelectAll();
            }

            FilterChanged?.Invoke();
        }

        protected void OnPropertyChanged(string propertyName) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}