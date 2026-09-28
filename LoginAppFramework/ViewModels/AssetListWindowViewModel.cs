using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LoginAppFramework
{
    public sealed class AssetListWindowViewModel : INotifyPropertyChanged
    {
        private readonly List<AssetCheckableViewModel> _allAssets = new();
        private readonly List<Worker> _workers = new();
        private AssetFilterViewModel _filters;

        public event PropertyChangedEventHandler PropertyChanged;

        public ObservableCollection<AssetCheckableViewModel> VisibleAssets { get; } = new();
        public ObservableCollection<string> ActiveFilterTags { get; } = new();

        public AssetFilterViewModel Filters
        {
            get => _filters;
            private set
            {
                if (_filters == value) return;

                if (_filters != null)
                    _filters.FilterChanged -= ApplyFilters;

                _filters = value;

                if (_filters != null)
                    _filters.FilterChanged += ApplyFilters;

                OnPropertyChanged();
            }
        }

        public IReadOnlyList<AssetCheckableViewModel> AllAssets => _allAssets;
        public IReadOnlyList<Worker> Workers => _workers;
        public bool HasResults => VisibleAssets.Count > 0;
        public string AssetCountText => $"{VisibleAssets.Count} vəsait tapıldı";
        public int CheckedCount => _allAssets.Count(vm => vm.IsChecked);
        public bool HasCheckedAssets => CheckedCount > 0;
        public string CheckedCountText => $"{CheckedCount} element işarələnib";
        public bool AreAllVisibleChecked =>
            VisibleAssets.Count > 0 && VisibleAssets.All(vm => vm.IsChecked);

        public void Refresh()
        {
            foreach (var item in _allAssets)
                item.PropertyChanged -= AssetCheckable_PropertyChanged;

            _allAssets.Clear();
            _allAssets.AddRange(
                AppData.GetAssets()
                    .Select(asset => new AssetCheckableViewModel(asset)));

            foreach (var item in _allAssets)
                item.PropertyChanged += AssetCheckable_PropertyChanged;

            _workers.Clear();
            _workers.AddRange(AppData.GetWorkers());

            Filters = new AssetFilterViewModel();
            BuildColumnFilters();
            ApplyFilters();

            OnPropertyChanged(nameof(AllAssets));
            OnPropertyChanged(nameof(Workers));
            NotifyCheckedStateChanged();
        }

        public void ClearFilters()
            => Filters?.Clear();

        public List<Asset> GetCheckedAssets()
            => _allAssets
                .Where(vm => vm.IsChecked)
                .Select(vm => vm.Asset)
                .ToList();

        public ColumnFilterViewModel GetColumnFilter(string key)
        {
            if (Filters?.ColumnFilters == null) return null;
            return Filters.ColumnFilters.TryGetValue(key, out var filter)
                ? filter
                : null;
        }

        public void ReapplyFilters()
            => ApplyFilters();

        public void SetAllVisibleChecked(bool isChecked)
        {
            foreach (var item in VisibleAssets)
                item.IsChecked = isChecked;

            NotifyCheckedStateChanged();
        }

        private void AssetCheckable_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(AssetCheckableViewModel.IsChecked))
                NotifyCheckedStateChanged();
        }

        private void NotifyCheckedStateChanged()
        {
            OnPropertyChanged(nameof(CheckedCount));
            OnPropertyChanged(nameof(HasCheckedAssets));
            OnPropertyChanged(nameof(CheckedCountText));
            OnPropertyChanged(nameof(AreAllVisibleChecked));
        }

        private void BuildColumnFilters()
        {
            if (Filters == null) return;

            Filters.ColumnFilters["VesaitinKodu"] = new ColumnFilterViewModel(
                _allAssets.Select(vm => vm.VesaitinKodu)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct());

            Filters.ColumnFilters["VesaitinAdi"] = new ColumnFilterViewModel(
                _allAssets.Select(vm => vm.VesaitinAdi)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct());

            Filters.ColumnFilters["Kateqoriya"] = new ColumnFilterViewModel(
                _allAssets.Select(vm => vm.Kateqoriya)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct());

            Filters.ColumnFilters["Worker"] = new ColumnFilterViewModel(
                _allAssets.Select(vm => vm.Worker?.per_adiper_soyadi)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct());

            Filters.ColumnFilters["Department"] = new ColumnFilterViewModel(
                _allAssets.Select(vm => vm.Worker?.pdp_adi)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct());

            Filters.ColumnFilters["YerleshmeYeri"] = new ColumnFilterViewModel(
                _allAssets.Select(vm => vm.YerleshmeYeri)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct());

            Filters.ColumnFilters["Erazi"] = new ColumnFilterViewModel(
                _allAssets.Select(vm => vm.Erazi)
                    .Where(v => !string.IsNullOrEmpty(v))
                    .Distinct());
        }

        private void ApplyFilters()
        {
            if (Filters == null) return;

            IEnumerable<AssetCheckableViewModel> filtered = _allAssets
                .Where(vm => vm.Asset.Status != "Arxivdə");

            var validCategories = AppData.GetDeviceCategories().ToHashSet();

            if (Filters.ShowOnlyUncategorized)
            {
                filtered = filtered.Where(vm =>
                    string.IsNullOrEmpty(vm.Asset.Kateqoriya) ||
                    !validCategories.Contains(vm.Asset.Kateqoriya));
            }
            else
            {
                var selectedCategories = Filters.GetSelectedCategories();
                if (selectedCategories.Count > 0)
                {
                    var categorySet = selectedCategories.ToHashSet();
                    filtered = filtered.Where(vm =>
                        !string.IsNullOrEmpty(vm.Asset.Kateqoriya) &&
                        categorySet.Contains(vm.Asset.Kateqoriya));
                }
            }

            if (!string.IsNullOrWhiteSpace(Filters.SearchText))
            {
                string searchText = Filters.SearchText;
                var compareInfo = CultureInfo.CurrentCulture.CompareInfo;
                const CompareOptions options = CompareOptions.IgnoreCase;

                filtered = filtered.Where(vm =>
                    Contains(vm.Asset.VesaitinKodu, searchText, compareInfo, options) ||
                    Contains(vm.Asset.VesaitinAdi, searchText, compareInfo, options) ||
                    Contains(vm.Asset.ITAvadanliqlarininSeriyaNomresi, searchText, compareInfo, options) ||
                    Contains(vm.Asset.Worker?.per_adiper_soyadi, searchText, compareInfo, options));
            }

            var statuses = Filters.StatusOptions
                .Where(o => o.IsChecked)
                .Select(o => o.Value)
                .ToHashSet();

            if (statuses.Count > 0)
            {
                filtered = filtered.Where(vm =>
                    !string.IsNullOrEmpty(vm.Asset.Status) &&
                    statuses.Contains(vm.Asset.Status));
            }

            var departments = Filters.DepartmentOptions
                .Where(o => o.IsChecked)
                .Select(o => o.Value)
                .ToHashSet();

            if (departments.Count > 0)
            {
                filtered = filtered.Where(vm =>
                    !string.IsNullOrEmpty(vm.Asset.Worker?.pdp_adi) &&
                    departments.Contains(vm.Asset.Worker.pdp_adi));
            }

            ApplyColumnFilter(ref filtered, "VesaitinKodu", vm => vm.VesaitinKodu);
            ApplyColumnFilter(ref filtered, "VesaitinAdi", vm => vm.VesaitinAdi);
            ApplyColumnFilter(ref filtered, "Kateqoriya", vm => vm.Kateqoriya);
            ApplyColumnFilter(ref filtered, "Worker", vm => vm.Worker?.per_adiper_soyadi);
            ApplyColumnFilter(ref filtered, "Department", vm => vm.Worker?.pdp_adi);
            ApplyColumnFilter(ref filtered, "YerleshmeYeri", vm => vm.YerleshmeYeri);
            ApplyColumnFilter(ref filtered, "Erazi", vm => vm.Erazi);

            var results = filtered.ToList();

            VisibleAssets.Clear();
            foreach (var item in results)
                VisibleAssets.Add(item);

            RebuildActiveFilterTags();

            OnPropertyChanged(nameof(HasResults));
            OnPropertyChanged(nameof(AssetCountText));
            NotifyCheckedStateChanged();
        }

        private void ApplyColumnFilter(
            ref IEnumerable<AssetCheckableViewModel> filtered,
            string key,
            Func<AssetCheckableViewModel, string> valueSelector)
        {
            if (Filters?.ColumnFilters == null ||
                !Filters.ColumnFilters.TryGetValue(key, out var filter) ||
                !filter.HasActiveFilters)
            {
                return;
            }

            var selectedValues = filter.GetSelectedValues().ToHashSet();
            filtered = filtered.Where(vm =>
            {
                string value = valueSelector(vm);
                return !string.IsNullOrEmpty(value) && selectedValues.Contains(value);
            });
        }

        private void RebuildActiveFilterTags()
        {
            ActiveFilterTags.Clear();

            if (!string.IsNullOrWhiteSpace(Filters.SearchText))
                ActiveFilterTags.Add($"Axtarış: '{Filters.SearchText}'");

            if (Filters.ShowOnlyUncategorized)
            {
                ActiveFilterTags.Add("Kateqoriya: Yalnız Təyin Edilməmişlər");
            }
            else
            {
                var categories = Filters.GetSelectedCategories();
                if (categories.Count > 0)
                    ActiveFilterTags.Add($"Kateqoriya: {string.Join(", ", categories)}");
            }

            var statuses = Filters.StatusOptions
                .Where(o => o.IsChecked)
                .Select(o => o.Value)
                .ToList();

            if (statuses.Count > 0)
                ActiveFilterTags.Add($"Status: {string.Join(", ", statuses)}");

            var departments = Filters.DepartmentOptions
                .Where(o => o.IsChecked)
                .Select(o => o.Value)
                .ToList();

            if (departments.Count > 0)
                ActiveFilterTags.Add($"Departament: {string.Join(", ", departments)}");
        }

        private static bool Contains(
            string value,
            string search,
            CompareInfo compareInfo,
            CompareOptions options)
            => !string.IsNullOrEmpty(value) &&
               compareInfo.IndexOf(value, search, options) >= 0;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
