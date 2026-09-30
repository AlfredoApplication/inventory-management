using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class GlobalSearchWindow : Window
    {
        private readonly List<GlobalSearchResult> _allResults = new();

        public GlobalSearchWindow()
        {
            InitializeComponent();
            BuildSearchIndex();
        }

        private void BuildSearchIndex()
        {
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
                            worker.pdp_adi
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
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string query = SearchBox.Text?.Trim() ?? string.Empty;

            if (query.Length == 0)
            {
                ResultsListView.ItemsSource = null;
                ResultCountTextBlock.Text = string.Empty;
                EmptyStatePanel.Visibility = Visibility.Visible;
                EmptyStatePanel.Title = "Axtarışa başlayın";
                EmptyStatePanel.Message =
                    "Yuxarıdakı sahəyə axtardığınız kodu və ya adı yazın.";
                return;
            }

            string[] terms = query.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

            var results = _allResults
                .Where(result =>
                    terms.All(term =>
                        result.SearchText?.Contains(
                            term,
                            StringComparison.CurrentCultureIgnoreCase) == true))
                .Select(result => new
                {
                    Result = result,
                    Score = CalculateScore(result, query)
                })
                .OrderBy(item => item.Score)
                .ThenBy(item => item.Result.TypeLabel)
                .ThenBy(item => item.Result.Title)
                .Take(80)
                .Select(item => item.Result)
                .ToList();

            ResultsListView.ItemsSource = results;
            ResultCountTextBlock.Text = $"{results.Count} nəticə";

            EmptyStatePanel.Visibility =
                results.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (results.Count == 0)
            {
                EmptyStatePanel.Title = "Nəticə tapılmadı";
                EmptyStatePanel.Message =
                    "Başqa kod, ad və ya açar sözlə yenidən yoxlayın.";
            }

            if (results.Count > 0)
                ResultsListView.SelectedIndex = 0;
        }

        private static int CalculateScore(
            GlobalSearchResult result,
            string query)
        {
            if (result.Title?.StartsWith(
                    query,
                    StringComparison.CurrentCultureIgnoreCase) == true)
            {
                return 0;
            }

            if (result.Title?.Contains(
                    query,
                    StringComparison.CurrentCultureIgnoreCase) == true)
            {
                return 1;
            }

            return 2;
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

        private async void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            await OpenSelectedAsync();
        }

        private async Task OpenSelectedAsync()
        {
            if (ResultsListView.SelectedItem is not GlobalSearchResult selected)
                return;

            Window owner = Owner;
            Close();

            if (selected.Kind == GlobalSearchResultKind.Asset)
            {
                await NavigationManager.GoToAssetWindow(owner, selected.Id);
            }
            else
            {
                await NavigationManager.GoToWorkerListWindow(owner, selected.Id);
            }
        }

        private async void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => Close();
    }

    public enum GlobalSearchResultKind
    {
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
    }
}
