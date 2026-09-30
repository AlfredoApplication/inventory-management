using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class GlobalSearchWindow : Window
    {
        private const string RecentSearchPreferenceKey =
            "global-search-recent";

        private readonly List<GlobalSearchResult> _allResults = new();
        private readonly ObservableCollection<string> _recentSearches = new();
        private GlobalSearchScope _scope = GlobalSearchScope.All;

        public GlobalSearchWindow()
        {
            InitializeComponent();
            LoadRecentSearches();
            BuildSearchIndex();
        }

        private void LoadRecentSearches()
        {
            foreach (string query in UiPreferenceStore
                .LoadStringList(RecentSearchPreferenceKey)
                .Take(8))
            {
                _recentSearches.Add(query);
            }

            RecentSearchesItemsControl.ItemsSource = _recentSearches;
            UpdateRecentSearchesVisibility();
        }

        private void RememberSearch(string query)
        {
            query = query?.Trim();
            if (string.IsNullOrWhiteSpace(query))
                return;

            var existing = _recentSearches.FirstOrDefault(value =>
                string.Equals(
                    value,
                    query,
                    StringComparison.CurrentCultureIgnoreCase));

            if (existing != null)
                _recentSearches.Remove(existing);

            _recentSearches.Insert(0, query);

            while (_recentSearches.Count > 8)
                _recentSearches.RemoveAt(_recentSearches.Count - 1);

            UiPreferenceStore.SaveStringList(
                RecentSearchPreferenceKey,
                _recentSearches);

            UpdateRecentSearchesVisibility();
        }

        private void BuildSearchIndex()
        {
            _allResults.Clear();

            foreach (var asset in AppData.GetAssets())
            {
                _allResults.Add(new GlobalSearchResult
                {
                    Kind = GlobalSearchResultKind.Asset,
                    Id = asset.Id,
                    Icon = AppIconKind.Assets,
                    TypeLabel = "Vəsait",
                    Title = string.Join(
                        " • ",
                        new[] { asset.VesaitinKodu, asset.VesaitinAdi }
                            .Where(value => !string.IsNullOrWhiteSpace(value))),
                    Subtitle = BuildAssetSubtitle(asset),
                    SearchText = string.Join(
                        " ",
                        new[]
                        {
                            asset.VesaitinKodu,
                            asset.VesaitinAdi,
                            asset.ITAvadanliqlarininSeriyaNomresi,
                            asset.Kateqoriya,
                            asset.Status,
                            asset.YerleshmeYeri,
                            asset.Erazi,
                            asset.Worker?.per_adiper_soyadi,
                            asset.Worker?.per_kod,
                            asset.Worker?.pdp_adi
                        }.Where(value => !string.IsNullOrWhiteSpace(value)))
                });
            }

            foreach (var worker in AppData.GetWorkers())
            {
                _allResults.Add(new GlobalSearchResult
                {
                    Kind = GlobalSearchResultKind.Worker,
                    Id = worker.Id,
                    Icon = AppIconKind.Workers,
                    TypeLabel = "İşçi",
                    Title = worker.per_adiper_soyadi ?? "Adsız işçi",
                    Subtitle = BuildWorkerSubtitle(worker),
                    SearchText = string.Join(
                        " ",
                        new[]
                        {
                            worker.per_kod,
                            worker.per_adiper_soyadi,
                            worker.pgk_gorev_adi,
                            worker.pdp_adi,
                            worker.IsActive ? "Aktiv" : "Qeyri-aktiv"
                        }.Where(value => !string.IsNullOrWhiteSpace(value)))
                });
            }
        }

        private static string BuildAssetSubtitle(Asset asset)
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(asset.ITAvadanliqlarininSeriyaNomresi)
                    ? null
                    : $"SN: {asset.ITAvadanliqlarininSeriyaNomresi}",
                asset.Kateqoriya,
                asset.Status,
                asset.Worker?.per_adiper_soyadi
            };

            return string.Join(
                "  •  ",
                parts.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private static string BuildWorkerSubtitle(Worker worker)
        {
            var parts = new[]
            {
                string.IsNullOrWhiteSpace(worker.per_kod)
                    ? null
                    : $"Kod: {worker.per_kod}",
                worker.pgk_gorev_adi,
                worker.pdp_adi,
                worker.IsActive ? "Aktiv" : "Qeyri-aktiv"
            };

            return string.Join(
                "  •  ",
                parts.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            SearchBox.Focus();
            SearchBox.CaretIndex = SearchBox.Text?.Length ?? 0;
            ApplySearch();
        }

        private void SearchBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
            => ApplySearch();

        private void SearchScope_Checked(
            object sender,
            RoutedEventArgs e)
        {
            if (sender is RadioButton radio &&
                radio.Tag is string tag &&
                Enum.TryParse(tag, true, out GlobalSearchScope scope))
            {
                _scope = scope;
            }

            ApplySearch();
        }

        private void ApplySearch()
        {
            if (SearchBox == null ||
                ResultsListView == null ||
                EmptyStatePanel == null)
            {
                return;
            }

            string query = SearchBox.Text?.Trim() ?? string.Empty;

            UpdateRecentSearchesVisibility();

            if (query.Length == 0)
            {
                ResultsListView.ItemsSource = null;
                OpenButton.IsEnabled = false;
                ResultCountTextBlock.Text = string.Empty;
                EmptyStatePanel.Visibility = Visibility.Visible;
                EmptyStatePanel.Title = "Axtarışa başlayın";
                EmptyStatePanel.Message =
                    _recentSearches.Count > 0
                        ? "Son axtarışlardan birini seçin və ya yeni sorğu yazın."
                        : "Yuxarıdakı sahəyə axtardığınız kodu və ya adı yazın.";
                return;
            }

            var results = _allResults
                .Where(IsInSelectedScope)
                .Select(result => new
                {
                    Result = result,
                    Score = FuzzySearchHelper.Score(
                        result.Title,
                        result.SearchText,
                        query)
                })
                .Where(item => item.Score != int.MaxValue)
                .OrderBy(item => item.Score)
                .ThenBy(item => item.Result.TypeLabel)
                .ThenBy(item => item.Result.Title)
                .Take(80)
                .Select(item =>
                {
                    item.Result.Highlight = query;
                    return item.Result;
                })
                .ToList();

            ResultsListView.ItemsSource = results;

            string scopeText = _scope switch
            {
                GlobalSearchScope.Asset => "vəsait",
                GlobalSearchScope.Worker => "işçi",
                _ => "nəticə"
            };

            ResultCountTextBlock.Text =
                $"{results.Count} {scopeText}";

            EmptyStatePanel.Visibility =
                results.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (results.Count == 0)
            {
                EmptyStatePanel.Title = "Nəticə tapılmadı";
                EmptyStatePanel.Message =
                    "Başqa kod, ad və ya açar sözlə yenidən yoxlayın.";
            }

            if (results.Count > 0)
                ResultsListView.SelectedIndex = 0;
        }

        private bool IsInSelectedScope(GlobalSearchResult result)
            => _scope switch
            {
                GlobalSearchScope.Asset =>
                    result.Kind == GlobalSearchResultKind.Asset,
                GlobalSearchScope.Worker =>
                    result.Kind == GlobalSearchResultKind.Worker,
                _ => true
            };

        private void UpdateRecentSearchesVisibility()
        {
            if (RecentSearchesPanel == null)
                return;

            bool show =
                _recentSearches.Count > 0 &&
                string.IsNullOrWhiteSpace(SearchBox?.Text);

            RecentSearchesPanel.Visibility =
                show ? Visibility.Visible : Visibility.Collapsed;
        }

        private void RecentSearchButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is not string query)
                return;

            SearchBox.Text = query;
            SearchBox.CaretIndex = SearchBox.Text.Length;
            SearchBox.Focus();
        }

        private void ResultsListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            OpenButton.IsEnabled =
                ResultsListView.SelectedItem is GlobalSearchResult;
        }

        private async void ResultsListView_MouseDoubleClick(
            object sender,
            MouseButtonEventArgs e)
        {
            await OpenSelectedAsync();
        }

        private async void OpenButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            await OpenSelectedAsync();
        }

        private async Task OpenSelectedAsync()
        {
            if (ResultsListView.SelectedItem is not GlobalSearchResult selected)
                return;

            RememberSearch(SearchBox.Text);

            Window owner = Owner;
            Close();

            if (selected.Kind == GlobalSearchResultKind.Asset)
            {
                await NavigationManager.GoToAssetWindow(
                    owner,
                    selected.Id);
            }
            else
            {
                await NavigationManager.GoToWorkerListWindow(
                    owner,
                    selected.Id);
            }
        }

        private async void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 &&
                (e.Key == Key.F || e.Key == Key.K))
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Down)
            {
                MoveSelection(1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Up)
            {
                MoveSelection(-1);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                Close();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Enter &&
                ResultsListView.SelectedItem is GlobalSearchResult)
            {
                await OpenSelectedAsync();
                e.Handled = true;
            }
        }

        private void MoveSelection(int delta)
        {
            int count = ResultsListView.Items.Count;
            if (count == 0)
                return;

            int current = ResultsListView.SelectedIndex;
            int next = current < 0
                ? 0
                : Math.Clamp(current + delta, 0, count - 1);

            ResultsListView.SelectedIndex = next;

            if (ResultsListView.SelectedItem != null)
                ResultsListView.ScrollIntoView(ResultsListView.SelectedItem);
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
            => Close();
    }

    public enum GlobalSearchResultKind
    {
        Asset,
        Worker
    }

    public enum GlobalSearchScope
    {
        All,
        Asset,
        Worker
    }

    public sealed class GlobalSearchResult
    {
        public GlobalSearchResultKind Kind { get; set; }
        public int Id { get; set; }
        public AppIconKind Icon { get; set; }
        public string TypeLabel { get; set; }
        public string Title { get; set; }
        public string Subtitle { get; set; }
        public string SearchText { get; set; }
        public string Highlight { get; set; }
    }
}
