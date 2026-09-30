using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class ReportsWindow : Window
    {
        private List<Asset> _allAssets = new();
        private List<Asset> _currentResults = new();
        private ReportQuery _currentQuery = new();

        public ReportsWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            _allAssets = AppData.GetAssets();

            AlertService.Refresh(_allAssets);

            PopulateFilterOptions();

            PurchaseDateFromSelector.SelectedDateChanged +=
                DateFilterChanged;

            PurchaseDateToSelector.SelectedDateChanged +=
                DateFilterChanged;

            ReloadRecentReports();
            ApplyFilters();
        }

        private void PopulateFilterOptions()
        {
            var departments = new List<string>
            {
                "Hamısı"
            };

            departments.AddRange(
                AppData.GetWorkerDepartments()
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(value)));

            DepartmentComboBox.ItemsSource =
                departments
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

            DepartmentComboBox.SelectedIndex = 0;

            var categories = new List<string>
            {
                "Hamısı"
            };

            categories.AddRange(
                AppData.GetDeviceCategories()
                    .Where(value =>
                        !string.IsNullOrWhiteSpace(value)));

            CategoryComboBox.ItemsSource =
                categories
                    .Distinct(StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

            CategoryComboBox.SelectedIndex = 0;
        }

        private void ReportKind_Checked(
            object sender,
            RoutedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ApplyFilters();
        }

        private void FilterSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
                return;

            ApplyFilters();
        }

        private void DateFilterChanged(
            object sender,
            EventArgs e)
        {
            if (!IsLoaded)
                return;

            ApplyFilters();
        }

        private void ApplyFilters()
        {
            if (!IsLoaded ||
                PreviewDataGrid == null)
            {
                return;
            }

            StatusTextBlock.Text = string.Empty;

            DateTime? from =
                PurchaseDateFromSelector.SelectedDate;

            DateTime? to =
                PurchaseDateToSelector.SelectedDate;

            if (from.HasValue &&
                to.HasValue &&
                from.Value.Date > to.Value.Date)
            {
                StatusTextBlock.Text =
                    "Başlanğıc tarixi son tarixdən böyük ola bilməz.";

                _currentResults = new List<Asset>();
                PreviewDataGrid.ItemsSource = _currentResults;
                UpdateResultState();
                return;
            }

            _currentQuery = new ReportQuery
            {
                Kind = GetSelectedReportKind(),
                PurchaseDateFrom = from?.Date,
                PurchaseDateTo = to?.Date,
                Department =
                    DepartmentComboBox.SelectedItem?.ToString(),
                Category =
                    CategoryComboBox.SelectedItem?.ToString()
            };

            _currentResults = ReportCenterService
                .FilterAssets(
                    _allAssets,
                    _currentQuery,
                    AlertService.CurrentAlerts)
                .ToList();

            PreviewDataGrid.ItemsSource =
                _currentResults;

            UpdateResultState();
        }

        private ReportKind GetSelectedReportKind()
        {
            var selected = FindVisualChildren<RadioButton>(this)
                .FirstOrDefault(radio =>
                    radio.GroupName == "ReportKind" &&
                    radio.IsChecked == true);

            if (selected?.Tag is string tag &&
                Enum.TryParse(
                    tag,
                    ignoreCase: true,
                    out ReportKind kind))
            {
                return kind;
            }

            return ReportKind.Inventory;
        }

        private void UpdateResultState()
        {
            CurrentReportTitleTextBlock.Text =
                ReportCenterService.GetTitle(
                    _currentQuery.Kind);

            decimal purchase =
                _currentResults.Sum(asset =>
                    asset.PurchaseCost);

            decimal current =
                _currentResults.Sum(asset =>
                    asset.CurrentValue);

            ResultSummaryTextBlock.Text =
                $"{_currentResults.Count} vəsait • Alış: {purchase:N2} ₼ • Cari: {current:N2} ₼";

            bool hasResults =
                _currentResults.Count > 0;

            PreviewDataGrid.Visibility =
                hasResults
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            EmptyState.Visibility =
                hasResults
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            ExportExcelButton.IsEnabled =
                hasResults;

            ExportPdfButton.IsEnabled =
                hasResults;
        }

        private void ClearFiltersButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            PurchaseDateFromSelector.SelectedDate = null;
            PurchaseDateToSelector.SelectedDate = null;
            DepartmentComboBox.SelectedIndex = 0;
            CategoryComboBox.SelectedIndex = 0;
            ApplyFilters();
        }

        private async void ExportExcelButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_currentResults.Count == 0)
                return;

            var dialog = new SaveFileDialog
            {
                Filter = "Excel Workbook|*.xlsx",
                Title = "Excel Hesabatını Yadda Saxla",
                FileName =
                    BuildFileName(
                        _currentQuery.Kind,
                        "xlsx")
            };

            if (dialog.ShowDialog() != true)
                return;

            ExportExcelButton.IsEnabled = false;

            OperationProgressOverlay.Show(
                "Excel hesabatı yaradılır...",
                $"{_currentResults.Count} vəsait hazırlanır.");

            var progress =
                new Progress<OperationProgressInfo>(
                    value =>
                        OperationProgressOverlay.Report(value));

            try
            {
                var exportItems =
                    _currentResults.ToList();

                await Task.Run(() =>
                    AppServices.AssetExcel.Export(
                        dialog.FileName,
                        exportItems,
                        progress));

                ReportCenterService.Remember(
                    _currentQuery,
                    "Excel",
                    exportItems.Count);

                ReloadRecentReports();

                NotificationService.Success(
                    this,
                    $"{exportItems.Count} vəsait Excel hesabatına çıxarıldı.",
                    title: "Hesabat");

                OfferOpenFile(dialog.FileName);
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Excel Export Xətası",
                    $"Hesabat yaradıla bilmədi:\n\n{ex.Message}");
            }
            finally
            {
                OperationProgressOverlay.Hide();
                ExportExcelButton.IsEnabled =
                    _currentResults.Count > 0;
            }
        }

        private async void ExportPdfButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_currentResults.Count == 0)
                return;

            var dialog = new SaveFileDialog
            {
                Filter = "PDF Document|*.pdf",
                Title = "PDF Hesabatını Yadda Saxla",
                FileName =
                    BuildFileName(
                        _currentQuery.Kind,
                        "pdf")
            };

            if (dialog.ShowDialog() != true)
                return;

            ExportPdfButton.IsEnabled = false;

            OperationProgressOverlay.Show(
                "PDF hesabatı yaradılır...",
                $"{_currentResults.Count} vəsait hazırlanır.");

            var progress =
                new Progress<OperationProgressInfo>(
                    value =>
                        OperationProgressOverlay.Report(value));

            try
            {
                var exportItems =
                    _currentResults.ToList();

                var queryCopy = CloneQuery(
                    _currentQuery);

                string title =
                    ReportCenterService.GetTitle(
                        queryCopy.Kind);

                await Task.Run(() =>
                    ReportCenterService.ExportPdf(
                        dialog.FileName,
                        title,
                        exportItems,
                        queryCopy,
                        progress));

                ReportCenterService.Remember(
                    queryCopy,
                    "PDF",
                    exportItems.Count);

                ReloadRecentReports();

                NotificationService.Success(
                    this,
                    $"{exportItems.Count} vəsait PDF hesabatına çıxarıldı.",
                    title: "Hesabat");

                OfferOpenFile(dialog.FileName);
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "PDF Export Xətası",
                    $"PDF hesabatı yaradıla bilmədi:\n\n{ex.Message}");
            }
            finally
            {
                OperationProgressOverlay.Hide();
                ExportPdfButton.IsEnabled =
                    _currentResults.Count > 0;
            }
        }

        private static ReportQuery CloneQuery(
            ReportQuery query)
            => new()
            {
                Kind = query.Kind,
                PurchaseDateFrom =
                    query.PurchaseDateFrom,
                PurchaseDateTo =
                    query.PurchaseDateTo,
                Department =
                    query.Department,
                Category =
                    query.Category
            };

        private void OfferOpenFile(string filePath)
        {
            bool open = DialogService.Confirm(
                this,
                "Hesabat Hazırdır",
                "Fayl uğurla yaradıldı. İndi açmaq istəyirsiniz?",
                "Faylı aç",
                "Bağla");

            if (!open)
                return;

            Process.Start(
                new ProcessStartInfo(filePath)
                {
                    UseShellExecute = true
                });
        }

        private void ReloadRecentReports()
        {
            var recent =
                ReportCenterService
                    .LoadRecent()
                    .ToList();

            RecentReportsItemsControl.ItemsSource =
                recent;

            RecentEmptyTextBlock.Visibility =
                recent.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void RecentReportButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.Tag
                is not RecentReportEntry recent ||
                recent.Query == null)
            {
                return;
            }

            ApplyQueryToControls(
                recent.Query);
        }

        private void ApplyQueryToControls(
            ReportQuery query)
        {
            foreach (var radio in
                FindVisualChildren<RadioButton>(this)
                    .Where(radio =>
                        radio.GroupName == "ReportKind"))
            {
                radio.IsChecked =
                    string.Equals(
                        radio.Tag?.ToString(),
                        query.Kind.ToString(),
                        StringComparison.OrdinalIgnoreCase);
            }

            PurchaseDateFromSelector.SelectedDate =
                query.PurchaseDateFrom;

            PurchaseDateToSelector.SelectedDate =
                query.PurchaseDateTo;

            DepartmentComboBox.SelectedItem =
                string.IsNullOrWhiteSpace(query.Department)
                    ? "Hamısı"
                    : query.Department;

            if (DepartmentComboBox.SelectedIndex < 0)
                DepartmentComboBox.SelectedIndex = 0;

            CategoryComboBox.SelectedItem =
                string.IsNullOrWhiteSpace(query.Category)
                    ? "Hamısı"
                    : query.Category;

            if (CategoryComboBox.SelectedIndex < 0)
                CategoryComboBox.SelectedIndex = 0;

            ApplyFilters();
        }

        private async void PreviewDataGrid_MouseDoubleClick(
            object sender,
            MouseButtonEventArgs e)
        {
            if (PreviewDataGrid.SelectedItem
                is not Asset asset)
            {
                return;
            }

            await NavigationManager.GoToAssetWindow(
                this,
                asset.Id);
        }

        private static string BuildFileName(
            ReportKind kind,
            string extension)
        {
            string prefix = kind switch
            {
                ReportKind.Purchases =>
                    "Alis_Tarixi",
                ReportKind.Warranty =>
                    "Zemanet",
                ReportKind.Maintenance =>
                    "Texniki_Xidmet",
                ReportKind.Lifecycle =>
                    "Heyat_Dovru",
                ReportKind.Unassigned =>
                    "Tehkim_Olunmayan",
                _ =>
                    "Inventar"
            };

            return
                $"{prefix}_Hesabati_{DateTime.Now:yyyyMMdd_HHmmss}.{extension}";
        }

        private static IEnumerable<T>
            FindVisualChildren<T>(
                DependencyObject root)
            where T : DependencyObject
        {
            if (root == null)
                yield break;

            int count =
                System.Windows.Media.VisualTreeHelper
                    .GetChildrenCount(root);

            for (int index = 0;
                 index < count;
                 index++)
            {
                var child =
                    System.Windows.Media.VisualTreeHelper
                        .GetChild(root, index);

                if (child is T match)
                    yield return match;

                foreach (var descendant
                    in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
            }
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
            => Close();
    }
}
