using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class AssetDetailControl : UserControl
    {
        public event EventHandler<Asset> OnAssetModified;
        public event EventHandler<Asset> OnAssetDeleted;
        public event EventHandler OnAssignmentChanged;
        public event EventHandler OnDetailPanelClosed;

        private Asset _currentAsset;
        private List<Worker> _allWorkers;
        private bool _isReadOnlyMode = false;

        public AssetDetailControl()
        {
            InitializeComponent();
        }

        private void OpenMaintenanceWindow(MaintenanceRecord recordToEdit = null)
        {
            if (_currentAsset == null) return;

            var editWindow = new AddMaintenanceRecordWindow(_currentAsset, recordToEdit) { Owner = Window.GetWindow(this) };

            if (editWindow.ShowDialog() == true)
            {
                if (editWindow.Result == MaintenanceEditResult.Saved)
                {
                    var savedRecord = editWindow.Record;
                    if (recordToEdit == null)
                    {
                        if (_currentAsset.MaintenanceHistory == null) _currentAsset.MaintenanceHistory = new List<MaintenanceRecord>();
                        _currentAsset.MaintenanceHistory.Add(savedRecord);
                    }
                }
                else if (editWindow.Result == MaintenanceEditResult.Deleted && recordToEdit != null)
                {
                    var recordInList = _currentAsset.MaintenanceHistory.FirstOrDefault(r => r.Id == recordToEdit.Id);
                    if (recordInList != null)
                    {
                        _currentAsset.MaintenanceHistory.Remove(recordInList);
                    }
                }

                AppData.SaveAndRefreshAsset(_currentAsset);
                PopulateFinancialsAndMaintenance();
            }
        }

        private void AddMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            OpenMaintenanceWindow();
        }

        private void MaintenanceListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                // Allow viewing but show message that editing is not allowed
                MessageBox.Show("Yalnız baxış rejimində redaktə edə bilməzsiniz.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MaintenanceListView.SelectedItem is MaintenanceRecord selectedRecord)
            {
                OpenMaintenanceWindow(selectedRecord);
            }
        }

        private void CloseDetailPanelButton_Click(object sender, RoutedEventArgs e)
        {
            OnDetailPanelClosed?.Invoke(this, EventArgs.Empty);
        }

        public void DisplayAsset(Asset asset, List<Worker> allWorkers)
        {
            _currentAsset = asset;
            _allWorkers = allWorkers;

            if (_currentAsset == null)
            {
                SelectAssetPrompt.Visibility = Visibility.Visible;
                DetailViewPanel.Visibility = Visibility.Collapsed;
                return;
            }

            SelectAssetPrompt.Visibility = Visibility.Collapsed;
            DetailViewPanel.Visibility = Visibility.Visible;

            VesaitinKoduValue.Text = _currentAsset.VesaitinKodu ?? "N/A";
            VesaitinAdiValue.Text = _currentAsset.VesaitinAdi ?? "N/A";
            SeriyaNomresiValue.Text = _currentAsset.ITAvadanliqlarininSeriyaNomresi ?? "N/A";
            KateqoriyaValue.Text = _currentAsset.Kateqoriya ?? "N/A";
            AlinmaTarixiValue.Text = _currentAsset.PurchaseDate > DateTime.MinValue
                ? _currentAsset.PurchaseDate.ToString("yyyy-MM-dd")
                : "N/A";
            StatusValue.Text = _currentAsset.Status ?? "N/A";
            StatusValue.Foreground = _currentAsset.StatusColor;
            DepartmentOrSectionValue.Text = _currentAsset.Worker?.pdp_adi ?? _currentAsset.BolmeShobeDepartment ?? "N/A";
            YerlesmeYeriValue.Text = _currentAsset.YerleshmeYeri ?? "N/A";
            EraziValue.Text = _currentAsset.Erazi ?? "N/A";

            GenerateBarcodeButton.IsEnabled = !string.IsNullOrEmpty(_currentAsset.VesaitinKodu);

            PopulateCustomFields();
            PopulateUserInfo();
            HistoryListView.ItemsSource = _currentAsset.History?.OrderByDescending(h => h.ChangeDate).ToList();
            PopulateFinancialsAndMaintenance();
            ApplyReadOnlyPermissions();
        }

        private void GenerateBarcodeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAsset != null && !string.IsNullOrEmpty(_currentAsset.VesaitinKodu))
            {
                var assetToPrint = new List<Asset> { _currentAsset };
                var printWindow = new PrintQrCodesWindow(assetToPrint) { Owner = Window.GetWindow(this) };
                printWindow.ShowDialog();
            }
        }

        private void PopulateUserInfo()
        {
            if (_currentAsset.Worker != null)
            {
                AssignedUserPanel.Visibility = Visibility.Visible;
                UnassignedUserPanel.Visibility = Visibility.Collapsed;
                UserNameTextBlock.Text = _currentAsset.Worker.per_adiper_soyadi;
                UserPositionTextBlock.Text = _currentAsset.Worker.pgk_gorev_adi;
            }
            else
            {
                AssignedUserPanel.Visibility = Visibility.Collapsed;
                UnassignedUserPanel.Visibility = Visibility.Visible;
            }
        }

        public void SetReadOnlyMode(bool isReadOnly)
        {
            _isReadOnlyMode = isReadOnly;
            ApplyReadOnlyPermissions();
        }

        private void ApplyReadOnlyPermissions()
        {
            bool canEdit = !_isReadOnlyMode;

            EditAssetButton.IsEnabled = canEdit;
            DeleteAssetButton.IsEnabled = canEdit;
            AssignButton.IsEnabled = canEdit;
            ReassignButton.IsEnabled = canEdit;
            UnassignButton.IsEnabled = canEdit;
            AddMaintenanceButton.IsEnabled = canEdit;

            // QR code generation remains enabled
            // GenerateBarcodeButton.IsEnabled stays true
        }

        private void AssignOrReassignButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentAsset == null) return;
            var availableWorkers = _allWorkers.Where(w => w.IsActive).ToList();
            var selectWindow = new SelectWorkerWindow(availableWorkers) { Owner = Window.GetWindow(this) };
            if (selectWindow.ShowDialog() == true && selectWindow.SelectedWorker != null)
            {
                Worker newWorker = selectWindow.SelectedWorker;
                string oldUser = _currentAsset.TehkimOlunanEmekdas;

                _currentAsset.Status = "İstifadədədir";
                _currentAsset.WorkerId = newWorker.Id;
                _currentAsset.TehkimOlunanEmekdas = newWorker.per_adiper_soyadi;
                _currentAsset.Vezifesi = newWorker.pgk_gorev_adi;
                _currentAsset.BolmeShobeDepartment = newWorker.pdp_adi;

                var historyAction = string.IsNullOrEmpty(oldUser) ? AssignmentAction.Assigned : AssignmentAction.Reassigned;
                if (_currentAsset.History == null) _currentAsset.History = new List<AssignmentHistoryEntry>();
                _currentAsset.History.Add(new AssignmentHistoryEntry { FromWorkerName = oldUser ?? "Sistem", ToWorkerName = newWorker.per_adiper_soyadi, ChangedBy = SessionManager.CurrentUser.Username, ChangeDate = DateTime.Now, Action = historyAction });
                AppData.SaveAndRefreshAsset(_currentAsset);
                OnAssignmentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void UnassignButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentAsset != null && MessageBox.Show($"'{_currentAsset.TehkimOlunanEmekdas}' adlı işçidən təhkimi ləğv etməyə əminsinizmi?", "Təsdiq", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                string oldUser = _currentAsset.TehkimOlunanEmekdas;
                _currentAsset.Status = "Anbarda";
                _currentAsset.WorkerId = null;
                _currentAsset.TehkimOlunanEmekdas = null;
                _currentAsset.Vezifesi = null;
                _currentAsset.BolmeShobeDepartment = null;

                if (_currentAsset.History == null) _currentAsset.History = new List<AssignmentHistoryEntry>();
                _currentAsset.History.Add(new AssignmentHistoryEntry { Action = AssignmentAction.Unassigned, FromWorkerName = oldUser, ToWorkerName = "Sistem", ChangedBy = SessionManager.CurrentUser.FullName, ChangeDate = DateTime.Now });
                AppData.SaveAndRefreshAsset(_currentAsset);
                OnAssignmentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void ArchiveAssetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAsset == null) return;
            var result = MessageBox.Show($"'{_currentAsset.VesaitinAdi}' adlı vəsaiti arxivləşdirməyə əminsinizmi? Vəsait qeyri-aktiv olacaq və əsas siyahıda görünməyəcək.", "Arxivləməni Təsdiq Et", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                string oldUser = _currentAsset.AssignedUser;

                _currentAsset.Status = "Arxivdə";
                _currentAsset.WorkerId = null;
                _currentAsset.TehkimOlunanEmekdas = null;
                _currentAsset.Vezifesi = null;
                _currentAsset.BolmeShobeDepartment = null;

                if (_currentAsset.History == null) _currentAsset.History = new List<AssignmentHistoryEntry>();
                if (!string.IsNullOrEmpty(oldUser))
                {
                    _currentAsset.History.Add(new AssignmentHistoryEntry { Action = AssignmentAction.Unassigned, FromWorkerName = oldUser, ToWorkerName = "Arxiv", ChangedBy = SessionManager.CurrentUser.FullName, ChangeDate = DateTime.Now });
                }
                AppData.SaveAndRefreshAsset(_currentAsset);
                OnAssignmentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void DeleteAssetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentAsset != null)
            {
                OnAssetDeleted?.Invoke(this, _currentAsset);
            }
        }

        private void PopulateFinancialsAndMaintenance()
        {
            if (_currentAsset == null) return;

            var cultureInfo = new CultureInfo("az-Latn-AZ");

            PurchaseCostValue.Text = _currentAsset.PurchaseCost.ToString("C", cultureInfo);
            SupplierValue.Text = string.IsNullOrEmpty(_currentAsset.Supplier) ? "N/A" : _currentAsset.Supplier;
            WarrantyValue.Text = _currentAsset.WarrantyExpirationDate > DateTime.MinValue ? _currentAsset.WarrantyExpirationDate.ToString("yyyy-MM-dd") : "N/A";
            WarrantyValue.Foreground = _currentAsset.WarrantyExpirationDate < DateTime.Today ? Brushes.IndianRed : Brushes.Black;
            UsefulLifeValue.Text = $"{_currentAsset.UsefulLifeInYears} İl";
            AnnualDepreciationValue.Text = _currentAsset.AnnualDepreciation.ToString("C", cultureInfo);
            CurrentValueValue.Text = _currentAsset.CurrentValue.ToString("C", cultureInfo);
            CurrentValueValue.Foreground = _currentAsset.IsEndOfLife ? Brushes.IndianRed : Brushes.Black;
            MaintenanceListView.ItemsSource = _currentAsset.MaintenanceHistory?.OrderByDescending(m => m.MaintenanceDate).ToList();
        }

        private void PopulateCustomFields()
        {
            CustomFieldsPanel.Children.Clear();
            if (_currentAsset == null || _currentAsset.CustomFields == null || !_currentAsset.CustomFields.Any()) { CustomFieldsSection.Visibility = Visibility.Collapsed; return; }
            CustomFieldsSection.Visibility = Visibility.Visible;
            foreach (var field in _currentAsset.CustomFields)
            {
                var grid = new Grid { Margin = new Thickness(0, 0, 0, 5) };
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto, MinWidth = 150 });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var label = new TextBlock { Text = $"{field.Key}:", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 10, 0) };
                var value = new TextBlock { Text = string.IsNullOrEmpty(field.Value) ? "N/A" : field.Value, TextWrapping = TextWrapping.Wrap };
                Grid.SetColumn(label, 0); Grid.SetColumn(value, 1);
                grid.Children.Add(label); grid.Children.Add(value);
                CustomFieldsPanel.Children.Add(grid);
            }
        }

        private void AssignedUserPanel_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_currentAsset?.Worker == null) return;
            var detailWindow = new WorkerDetailWindow(_currentAsset.Worker, AppData.GetAssets()) { Owner = Window.GetWindow(this) };
            detailWindow.ShowDialog();
        }

        private void EditAssetButton_Click(object sender, RoutedEventArgs e) => OnAssetModified?.Invoke(this, _currentAsset);
        private void ReassignButton_Click(object sender, RoutedEventArgs e) => AssignOrReassignButton_Click(sender, e);
    }
}