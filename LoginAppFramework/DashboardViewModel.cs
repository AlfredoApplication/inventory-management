using LiveCharts;
using LiveCharts.Wpf;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Input;
using System.Windows.Media;

namespace LoginAppFramework
{
    public class LegendItemViewModel
    {
        public string CategoryName { get; set; }
        public int Count { get; set; }
        public string PercentageFormatted { get; set; }
        public Brush Brush { get; set; }
        private string _toolTipText;
        public string ToolTipText { get { if (_toolTipText == null) { GenerateToolTip(); } return _toolTipText; } }
        public List<KeyValuePair<string, int>> SubCategoryCounts { get; set; }
        public LegendItemViewModel() { SubCategoryCounts = new List<KeyValuePair<string, int>>(); }
        private void GenerateToolTip()
        {
            if (SubCategoryCounts == null || !SubCategoryCounts.Any()) { _toolTipText = $"{CategoryName}\nÜmumi: {Count}"; return; }
            var sb = new StringBuilder();
            sb.AppendLine($"{CategoryName} (Ümumi: {Count})");
            sb.AppendLine("--------------------");
            foreach (var sub in SubCategoryCounts.OrderByDescending(s => s.Value)) { sb.AppendLine($"• {sub.Key}: {sub.Value}"); }
            _toolTipText = sb.ToString();
        }
    }

    public class DashboardViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public class TopAssetDataPoint
        {
            public string Name { get; set; }
            public int Count { get; set; }
            public decimal AvgCost { get; set; }
        }

        public int TotalAssets { get; private set; }
        public string TotalPurchaseCost { get; private set; }
        public string TangibleAssetsCost { get; private set; }
        public string IntangibleAssetsCost { get; private set; }
        public string AnnualDepreciation { get; private set; }
        public string CurrentValue { get; private set; }

        public ObservableCollection<CategoryCostViewModel> TangibleCategoryCosts { get; set; }
        public ObservableCollection<CategoryCostViewModel> IntangibleCategoryCosts { get; set; }

        private bool _isTangiblePopupOpen;
        public bool IsTangiblePopupOpen
        {
            get => _isTangiblePopupOpen;
            set { _isTangiblePopupOpen = value; OnPropertyChanged(nameof(IsTangiblePopupOpen)); }
        }

        private bool _isIntangiblePopupOpen;
        public bool IsIntangiblePopupOpen
        {
            get => _isIntangiblePopupOpen;
            set { _isIntangiblePopupOpen = value; OnPropertyChanged(nameof(IsIntangiblePopupOpen)); }
        }

        public ICommand OpenTangiblePopupCommand { get; }
        public ICommand OpenIntangiblePopupCommand { get; }

        public SeriesCollection CategorySeries { get; set; }
        public ObservableCollection<LegendItemViewModel> LegendItems { get; set; }
        public SeriesCollection TopAssetsSeries { get; set; }
        public string[] TopAssetsLabels { get; set; }
        public string[] TopAssetsFullNames { get; private set; }
        public Func<double, string> TopAssetsFormatter { get; set; }
        public IChartTooltip TopAssetsTooltip { get; private set; }
        public SeriesCollection DepartmentsSeries { get; set; }
        public string[] DepartmentsLabels { get; set; }

        public DashboardViewModel()
        {
            TangibleCategoryCosts = new ObservableCollection<CategoryCostViewModel>();
            IntangibleCategoryCosts = new ObservableCollection<CategoryCostViewModel>();

            OpenTangiblePopupCommand = new RelayCommand(() =>
            {
                IsIntangiblePopupOpen = false;
                IsTangiblePopupOpen = true;
            });
            OpenIntangiblePopupCommand = new RelayCommand(() =>
            {
                IsTangiblePopupOpen = false;
                IsIntangiblePopupOpen = true;
            });

            CategorySeries = new SeriesCollection();
            LegendItems = new ObservableCollection<LegendItemViewModel>();
            TopAssetsSeries = new SeriesCollection();
            DepartmentsSeries = new SeriesCollection();
        }

        public void LoadAllData(List<Asset> allAssets, List<Worker> allWorkers)
        {
            if (allAssets == null || !allAssets.Any() || allWorkers == null) return;
            LoadKpiData(allAssets, allWorkers);
            LoadCategoryPieChartData(allAssets);
            LoadTopAssetsChart(allAssets);
            LoadDepartmentsChart(allAssets);
            OnPropertyChanged(null);
        }

        private void LoadKpiData(List<Asset> allAssets, List<Worker> allWorkers)
        {
            var activeAssets = allAssets.Where(a => a.Status != "Arxivdə").ToList();
            TotalAssets = activeAssets.Count;

            var culture = new CultureInfo("az-AZ");
            AnnualDepreciation = activeAssets.Sum(a => a.AnnualDepreciation).ToString("C0", culture);
            CurrentValue = activeAssets.Sum(a => a.CurrentValue).ToString("C0", culture);

            var allCategories = AppData.GetHierarchicalCategories();
            TangibleCategoryCosts.Clear();
            IntangibleCategoryCosts.Clear();
            decimal totalTangibleCost = 0;
            decimal totalIntangibleCost = 0;

            var assetsByCategory = activeAssets
                .Where(a => !string.IsNullOrEmpty(a.Kateqoriya))
                .GroupBy(a => a.Kateqoriya)
                .ToDictionary(g => g.Key, g => g.ToList());

            List<string> GetAllDescendantNames(DeviceCategory parent)
            {
                var names = new List<string>();
                void Recurse(DeviceCategory current) { names.Add(current.Name); foreach (var child in current.Subcategories) Recurse(child); }
                Recurse(parent);
                return names.Distinct().ToList();
            }

            var intangibleRoot = allCategories.FirstOrDefault(c => c.Name == "Qeyri-maddi əsas vəsaitlər");

            if (intangibleRoot != null)
            {
                foreach (var subCategory in intangibleRoot.Subcategories.OrderBy(sc => sc.Name))
                {
                    var allChildNames = GetAllDescendantNames(subCategory);
                    decimal subCategoryPurchaseCost = 0;
                    decimal subCategoryCurrentValue = 0;

                    foreach (var childName in allChildNames)
                    {
                        if (assetsByCategory.TryGetValue(childName, out var assets))
                        {
                            subCategoryPurchaseCost += assets.Sum(a => a.PurchaseCost);
                            subCategoryCurrentValue += assets.Sum(a => a.CurrentValue);
                        }
                    }

                    if (subCategoryPurchaseCost > 0)
                    {
                        IntangibleCategoryCosts.Add(new CategoryCostViewModel
                        {
                            CategoryName = subCategory.Name,
                            FormattedPurchaseCost = subCategoryPurchaseCost.ToString("C0", culture),
                            FormattedCurrentValue = subCategoryCurrentValue.ToString("C0", culture)
                        });
                        totalIntangibleCost += subCategoryPurchaseCost;
                    }
                }
            }

            var tangibleRoots = allCategories.Where(c => c.Name != "Qeyri-maddi əsas vəsaitlər").OrderBy(c => c.Name);
            foreach (var rootCategory in tangibleRoots)
            {
                var allChildNames = GetAllDescendantNames(rootCategory);
                decimal rootCategoryPurchaseCost = 0;
                decimal rootCategoryCurrentValue = 0;

                foreach (var childName in allChildNames)
                {
                    if (assetsByCategory.TryGetValue(childName, out var assets))
                    {
                        rootCategoryPurchaseCost += assets.Sum(a => a.PurchaseCost);
                        rootCategoryCurrentValue += assets.Sum(a => a.CurrentValue);
                    }
                }

                if (rootCategoryPurchaseCost > 0)
                {
                    TangibleCategoryCosts.Add(new CategoryCostViewModel
                    {
                        CategoryName = rootCategory.Name,
                        FormattedPurchaseCost = rootCategoryPurchaseCost.ToString("C0", culture),
                        FormattedCurrentValue = rootCategoryCurrentValue.ToString("C0", culture)
                    });
                    totalTangibleCost += rootCategoryPurchaseCost;
                }
            }

            decimal uncategorizedCost = activeAssets.Where(a => string.IsNullOrEmpty(a.Kateqoriya)).Sum(a => a.PurchaseCost);
            if (uncategorizedCost > 0)
            {
                decimal uncategorizedCurrentValue = activeAssets.Where(a => string.IsNullOrEmpty(a.Kateqoriya)).Sum(a => a.CurrentValue);
                TangibleCategoryCosts.Add(new CategoryCostViewModel
                {
                    CategoryName = "Kateqoriyasız",
                    FormattedPurchaseCost = uncategorizedCost.ToString("C0", culture),
                    FormattedCurrentValue = uncategorizedCurrentValue.ToString("C0", culture)
                });
                totalTangibleCost += uncategorizedCost;
            }

            TangibleAssetsCost = totalTangibleCost.ToString("C0", culture);
            IntangibleAssetsCost = totalIntangibleCost.ToString("C0", culture);
            TotalPurchaseCost = (totalTangibleCost + totalIntangibleCost).ToString("C0", culture);
        }

        private void LoadCategoryPieChartData(List<Asset> allAssets)
        {
            var activeAssets = allAssets.Where(a => a.Status != "Arxivdə").ToList();
            CategorySeries.Clear();
            LegendItems.Clear();
            var categoryTree = AppData.GetHierarchicalCategories();
            var childToParentMap = new Dictionary<string, string>();
            void BuildParentMap(IEnumerable<DeviceCategory> categories, string rootParentName)
            {
                foreach (var category in categories)
                {
                    childToParentMap[category.Name] = rootParentName;
                    if (category.Subcategories.Any()) BuildParentMap(category.Subcategories, rootParentName);
                }
            }
            foreach (var rootCategory in categoryTree)
            {
                childToParentMap[rootCategory.Name] = rootCategory.Name;
                BuildParentMap(rootCategory.Subcategories, rootCategory.Name);
            }
            var assetsGroupedByParent = activeAssets
                .GroupBy(asset => childToParentMap.GetValueOrDefault(asset.Kateqoriya ?? "Naməlum", "Naməlum"))
                .Select(g => new {
                    ParentName = g.Key,
                    TotalCount = g.Count(),
                    SubCategoryCounts = g.GroupBy(subAsset => subAsset.Kateqoriya ?? "Naməlum")
                                         .ToDictionary(subG => subG.Key, subG => subG.Count())
                }).OrderBy(x => x.ParentName).ToList();
            double totalAssetCount = activeAssets.Count;
            if (totalAssetCount == 0) return;
            int colorIndex = 0;
            var colorPalette = new[] { "#0082D5", "#E74C3C", "#F1C40F", "#2ECC71", "#9B59B6", "#3498DB", "#E67E22", "#7F8C8D" };
            foreach (var group in assetsGroupedByParent)
            {
                var color = (Brush)new BrushConverter().ConvertFrom(colorPalette[colorIndex % colorPalette.Length]);
                double percentage = group.TotalCount / totalAssetCount;
                CategorySeries.Add(new PieSeries
                {
                    Title = group.ParentName,
                    Values = new ChartValues<int> { group.TotalCount },
                    DataLabels = true,
                    Fill = color
                });
                var legendItem = new LegendItemViewModel
                {
                    CategoryName = group.ParentName,
                    Count = group.TotalCount,
                    PercentageFormatted = percentage.ToString("P1"),
                    Brush = color,
                    SubCategoryCounts = group.SubCategoryCounts.ToList()
                };
                LegendItems.Add(legendItem);
                colorIndex++;
            }
        }

        private void LoadTopAssetsChart(List<Asset> allAssets)
        {
            var activeAssets = allAssets.Where(a => a.Status != "Arxivdə").ToList();
            TopAssetsSeries.Clear();
            var topAssets = activeAssets.GroupBy(a => a.VesaitinAdi)
                .Select(g => new TopAssetDataPoint { Name = g.Key, Count = g.Count(), AvgCost = g.Average(a => a.PurchaseCost) })
                .OrderByDescending(x => x.Count).Take(10).ToList();
            if (!topAssets.Any()) return;
            var culture = new CultureInfo("az-AZ");
            TopAssetsSeries.Add(new ColumnSeries
            {
                Title = "Sayı",
                Values = new ChartValues<TopAssetDataPoint>(topAssets),
                DataLabels = true,
                LabelPoint = point => ((TopAssetDataPoint)point.Instance).Count.ToString("N0"),
                Configuration = new LiveCharts.Configurations.CartesianMapper<TopAssetDataPoint>().Y(point => point.Count),
                ScalesYAt = 0
            });
            TopAssetsSeries.Add(new ColumnSeries
            {
                Title = "Ortalama Qiymət",
                Values = new ChartValues<TopAssetDataPoint>(topAssets),
                DataLabels = true,
                LabelPoint = point => ((TopAssetDataPoint)point.Instance).AvgCost.ToString("C0", culture),
                Configuration = new LiveCharts.Configurations.CartesianMapper<TopAssetDataPoint>().Y(point => (double)point.AvgCost),
                ScalesYAt = 1
            });
            TopAssetsFullNames = topAssets.Select(a => a.Name).ToArray();
            TopAssetsLabels = topAssets.Select(a => {
                const int maxLength = 15;
                if (a.Name.Length > maxLength) { return string.Concat(a.Name.AsSpan(0, maxLength - 3), "..."); }
                return a.Name;
            }).ToArray();
            TopAssetsTooltip = new DefaultTooltip
            {
                SelectionMode = TooltipSelectionMode.SharedXValues,
                Content = new Func<ChartPoint, string>(point =>
                {
                    var assetData = topAssets[(int)point.X];
                    return $"{assetData.Name}\nSayı: {assetData.Count}\nOrtalama Qiymət: {assetData.AvgCost:C0}";
                })
            };
        }

        private void LoadDepartmentsChart(List<Asset> allAssets)
        {
            var activeAssets = allAssets.Where(a => a.Status != "Arxivdə").ToList();
            DepartmentsSeries.Clear();
            var departmentGroups = activeAssets.Where(a => !string.IsNullOrEmpty(a.BolmeShobeDepartment))
                .GroupBy(a => a.BolmeShobeDepartment)
                .Select(g => new { Name = g.Key, Count = g.Count() }).OrderBy(x => x.Count).ToList();
            DepartmentsSeries.Add(new RowSeries
            {
                Title = "Departament",
                Values = new ChartValues<int>(departmentGroups.Select(s => s.Count)),
                DataLabels = true,
                LabelPoint = point => point.X.ToString("N0")
            });
            DepartmentsLabels = departmentGroups.Select(s => s.Name).ToArray();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}