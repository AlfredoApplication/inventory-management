using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class RecycleBinWindow : Window
    {
        private List<RecycleBinAssetItem> _allItems = new();

        public RecycleBinWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (!SessionManager.CanDelete())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Silinənlər bölməsi üçün Admin icazəsi tələb olunur.");

                Close();
                return;
            }

            ReloadItems();
            SearchTextBox.Focus();
        }

        private void ReloadItems()
        {
            DateTime nowUtc = DateTime.UtcNow;

            var expired = AppData.GetDeletedAssets()
                .Where(asset =>
                    AssetDeletionMetadata.ShouldPurge(
                        asset,
                        nowUtc))
                .ToList();

            if (expired.Count > 0)
                AppServices.Assets.PermanentlyDeleteMany(expired);

            _allItems = AppData.GetDeletedAssets()
                .Select(asset =>
                    new RecycleBinAssetItem(
                        asset,
                        nowUtc))
                .OrderBy(item => item.DaysRemaining)
                .ThenByDescending(item => item.DeletedAtUtc)
                .ToList();

            ApplyFilter();

            RetentionSummaryTextBlock.Text =
                _allItems.Count == 0
                    ? "Səbət boşdur"
                    : $"{_allItems.Count} vəsait • 30 gün saxlanma";

            EmptyBinButton.IsEnabled =
                _allItems.Count > 0;
        }

        private void SearchTextBox_TextChanged(
            object sender,
            TextChangedEventArgs e)
            => ApplyFilter();

        private void ApplyFilter()
        {
            if (RecycleDataGrid == null ||
                EmptyState == null ||
                CountTextBlock == null)
            {
                return;
            }

            string query =
                SearchTextBox?.Text?.Trim() ?? string.Empty;

            var visible = string.IsNullOrWhiteSpace(query)
                ? _allItems
                : _allItems
                    .Where(item =>
                        Contains(item.Code, query) ||
                        Contains(item.Name, query) ||
                        Contains(item.Serial, query) ||
                        Contains(item.DeletedBy, query) ||
                        Contains(item.Status, query))
                    .ToList();

            RecycleDataGrid.ItemsSource = visible;

            CountTextBlock.Text =
                $"{visible.Count} vəsait";

            RecycleDataGrid.Visibility =
                visible.Count > 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            EmptyState.Visibility =
                visible.Count == 0
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            UpdateActionState();
        }

        private static bool Contains(
            string value,
            string query)
            => !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf(
                   query,
                   StringComparison.CurrentCultureIgnoreCase) >= 0;

        private void RecycleDataGrid_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
            => UpdateActionState();

        private void UpdateActionState()
        {
            if (RestoreButton == null ||
                PermanentDeleteButton == null)
            {
                return;
            }

            int selected =
                RecycleDataGrid?.SelectedItems?.Count ?? 0;

            RestoreButton.IsEnabled = selected > 0;
            PermanentDeleteButton.IsEnabled = selected > 0;
        }

        private List<Asset> GetSelectedAssets()
            => RecycleDataGrid.SelectedItems
                .OfType<RecycleBinAssetItem>()
                .Select(item => item.Asset)
                .Where(asset => asset != null)
                .ToList();

        private void RestoreButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var selected = GetSelectedAssets();
            if (selected.Count == 0)
                return;

            var activeAssets = AppData.GetAssets();
            var conflicts = new List<string>();

            foreach (var asset in selected)
            {
                var codeConflict =
                    AssetDuplicateDetector.FindCodeConflict(
                        activeAssets,
                        asset.VesaitinKodu,
                        asset.Id);

                var serialConflict =
                    AssetDuplicateDetector.FindSerialConflict(
                        activeAssets,
                        asset.ITAvadanliqlarininSeriyaNomresi,
                        asset.Id);

                if (codeConflict != null)
                    conflicts.Add(codeConflict.Message);

                if (serialConflict != null)
                    conflicts.Add(serialConflict.Message);
            }

            if (conflicts.Count > 0)
            {
                DialogService.Warning(
                    this,
                    "Bərpa Mümkün Deyil",
                    "Seçilmiş vəsaitlərdən bəzilərinin kodu və ya seriya nömrəsi aktiv siyahıda artıq istifadə olunur. Əvvəl konflikti həll edin.\n\n" +
                    string.Join(
                        "\n",
                        conflicts.Distinct().Take(10)));

                return;
            }

            AppServices.Assets.RestoreMany(selected);

            NotificationService.Success(
                this,
                $"{selected.Count} vəsait bərpa edildi.",
                title: "Silinənlər");

            ReloadItems();
        }

        private void PermanentDeleteButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var selected = GetSelectedAssets();
            if (selected.Count == 0)
                return;

            bool confirmed = DialogService.Confirm(
                this,
                "Həmişəlik Silməni Təsdiq Et",
                $"Seçilmiş {selected.Count} vəsait həmişəlik silinəcək. Bu əməliyyatı geri qaytarmaq mümkün deyil.",
                "Həmişəlik sil",
                "Ləğv et",
                destructive: true);

            if (!confirmed)
                return;

            AppServices.Assets.PermanentlyDeleteMany(selected);

            NotificationService.Success(
                this,
                $"{selected.Count} vəsait həmişəlik silindi.",
                title: "Silinənlər");

            ReloadItems();
        }

        private void EmptyBinButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var assets = AppData.GetDeletedAssets();
            if (assets.Count == 0)
                return;

            bool confirmed = DialogService.Confirm(
                this,
                "Silinənləri Boşalt",
                $"Silinənlər bölməsindəki {assets.Count} vəsait həmişəlik silinəcək. Bu əməliyyatı geri qaytarmaq mümkün deyil.",
                "Hamısını həmişəlik sil",
                "Ləğv et",
                destructive: true);

            if (!confirmed)
                return;

            AppServices.Assets.PermanentlyDeleteMany(assets);

            NotificationService.Success(
                this,
                "Silinənlər bölməsi boşaldıldı.",
                title: "Silinənlər");

            ReloadItems();
        }

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            Close();
            e.Handled = true;
        }

        private void CloseButton_Click(
            object sender,
            RoutedEventArgs e)
            => Close();
    }

    public sealed class RecycleBinAssetItem
    {
        public RecycleBinAssetItem(
            Asset asset,
            DateTime nowUtc)
        {
            Asset = asset;

            DeletedAtUtc =
                AssetDeletionMetadata.GetDeletedAtUtc(asset)
                ?? nowUtc;

            DaysRemaining =
                AssetDeletionMetadata.GetDaysRemaining(
                    asset,
                    nowUtc);
        }

        public Asset Asset { get; }
        public DateTime DeletedAtUtc { get; }
        public int DaysRemaining { get; }

        public string Code => Asset?.VesaitinKodu;
        public string Name => Asset?.VesaitinAdi;
        public string Serial =>
            Asset?.ITAvadanliqlarininSeriyaNomresi;
        public string Status => Asset?.Status;
        public string DeletedBy =>
            AssetDeletionMetadata.GetDeletedBy(Asset)
            ?? "Sistem";

        public string DeletedAtDisplay =>
            DeletedAtUtc.ToLocalTime()
                .ToString("dd.MM.yyyy HH:mm");

        public string RetentionDisplay =>
            DaysRemaining <= 0
                ? "Müddət bitib"
                : $"{DaysRemaining} gün";
    }
}
