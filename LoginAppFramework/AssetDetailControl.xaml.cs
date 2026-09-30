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

                AppServices.Assets.Save(_currentAsset);
                PopulateFinancialsAndMaintenance();
                PopulateActivityTimeline();
            }
        }

        private void AddMaintenanceButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                DialogService.Warning(
                    Window.GetWindow(this),
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            OpenMaintenanceWindow();
        }

        private void MaintenanceListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                // Allow viewing but show message that editing is not allowed
                DialogService.Info(
                    Window.GetWindow(this),
                    "Giriş Qadağandır",
                    "Yalnız baxış rejimində redaktə edə bilməzsiniz.");
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
            StatusBadge.Status = _currentAsset.Status;
            DepartmentOrSectionValue.Text = _currentAsset.Department ?? "N/A";
            YerlesmeYeriValue.Text = _currentAsset.YerleshmeYeri ?? "N/A";
            EraziValue.Text = _currentAsset.Erazi ?? "N/A";

            GenerateBarcodeButton.IsEnabled = !string.IsNullOrEmpty(_currentAsset.VesaitinKodu);

            PopulateCustomFields();
            PopulateUserInfo();
            PopulateFinancialsAndMaintenance();
            PopulateActivityTimeline();
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
                DialogService.Warning(
                    Window.GetWindow(this),
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            if (_currentAsset == null)
                return;

            var owner = Window.GetWindow(this);
            var availableWorkers = _allWorkers
                .Where(worker => worker.IsActive)
                .ToList();

            var selectWindow = new SelectWorkerWindow(availableWorkers)
            {
                Owner = owner
            };

            if (selectWindow.ShowDialog() == true &&
                selectWindow.SelectedWorker != null)
            {
                var snapshot = _currentAsset.Clone();
                string workerName =
                    selectWindow.SelectedWorker.per_adiper_soyadi;

                AppServices.Assets.Assign(
                    _currentAsset,
                    selectWindow.SelectedWorker,
                    "Vəsait detalları");

                OnAssignmentChanged?.Invoke(this, EventArgs.Empty);

                NotificationService.Undo(
                    owner,
                    $"Vəsait {workerName} adlı işçiyə təhkim edildi.",
                    () =>
                    {
                        AppServices.Assets.Save(snapshot);
                        OnAssignmentChanged?.Invoke(this, EventArgs.Empty);
                        NotificationService.Success(
                            owner,
                            "Təhkimat geri qaytarıldı.");
                    });
            }
        }

        private void UnassignButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                DialogService.Warning(
                    Window.GetWindow(this),
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            if (_currentAsset != null)
            {
                bool confirm = DialogService.Confirm(
                    Window.GetWindow(this),
                    "Təhkimatı Ləğv Et",
                    $"'{_currentAsset.AssignedUser}' adlı işçidən təhkimi ləğv etməyə əminsinizmi?",
                    "Təhkimatı ləğv et",
                    "Geri qayıt");

                if (!confirm)
                    return;

                var owner = Window.GetWindow(this);
                var snapshot = _currentAsset.Clone();

                AppServices.Assets.Unassign(
                    _currentAsset,
                    "Vəsait detalları");

                OnAssignmentChanged?.Invoke(this, EventArgs.Empty);

                NotificationService.Undo(
                    owner,
                    "Təhkimat ləğv edildi.",
                    () =>
                    {
                        AppServices.Assets.Save(snapshot);
                        OnAssignmentChanged?.Invoke(this, EventArgs.Empty);
                        NotificationService.Success(
                            owner,
                            "Təhkimat bərpa edildi.");
                    });
            }
        }

        private void ArchiveAssetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentAsset == null) return;

            bool result = DialogService.Confirm(
                Window.GetWindow(this),
                "Arxivləməni Təsdiq Et",
                $"'{_currentAsset.VesaitinAdi}' adlı vəsaiti arxivləşdirməyə əminsinizmi? Vəsait qeyri-aktiv olacaq və əsas siyahıda görünməyəcək.",
                "Arxivlə",
                "Ləğv et");

            if (!result)
                return;

            var owner = Window.GetWindow(this);
            var snapshot = _currentAsset.Clone();

            AppServices.Assets.Archive(_currentAsset, "Arxiv");
            OnAssignmentChanged?.Invoke(this, EventArgs.Empty);

            NotificationService.Undo(
                owner,
                "Vəsait arxivləşdirildi.",
                () =>
                {
                    AppServices.Assets.Save(snapshot);
                    OnAssignmentChanged?.Invoke(this, EventArgs.Empty);
                    NotificationService.Success(
                        owner,
                        "Arxivləmə geri qaytarıldı.");
                });
        }

        private void DeleteAssetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                DialogService.Warning(
                    Window.GetWindow(this),
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            if (_currentAsset != null)
            {
                OnAssetDeleted?.Invoke(this, _currentAsset);
            }
        }

        private void PopulateActivityTimeline()
        {
            if (_currentAsset == null)
                return;

            var activities = new List<AssetActivityItem>();

            if (_currentAsset.PurchaseDate > DateTime.MinValue)
            {
                var purchaseDescriptionParts = new List<string>();

                if (_currentAsset.PurchaseCost > 0)
                {
                    purchaseDescriptionParts.Add(
                        $"Alış qiyməti: {_currentAsset.PurchaseCost.ToString("C", new CultureInfo("az-Latn-AZ"))}");
                }

                if (!string.IsNullOrWhiteSpace(_currentAsset.Supplier))
                    purchaseDescriptionParts.Add($"Təchizatçı: {_currentAsset.Supplier}");

                activities.Add(new AssetActivityItem
                {
                    Date = _currentAsset.PurchaseDate,
                    Kind = AssetActivityKind.Purchase,
                    Icon = AppIconKind.Money,
                    Title = "Vəsait alındı",
                    Description = string.Join(" • ", purchaseDescriptionParts),
                    Actor = null
                });
            }

            foreach (var entry in _currentAsset.History ?? Enumerable.Empty<AssignmentHistoryEntry>())
            {
                string title;
                string description;
                AppIconKind icon;
                AssetActivityKind kind;

                switch (entry.Action)
                {
                    case AssignmentAction.Assigned:
                        title = "Vəsait təhkim edildi";
                        description = string.IsNullOrWhiteSpace(entry.ToWorkerName)
                            ? "İşçiyə təhkim edildi."
                            : $"{entry.ToWorkerName} adlı işçiyə təhkim edildi.";
                        icon = AppIconKind.Assign;
                        kind = AssetActivityKind.Assigned;
                        break;

                    case AssignmentAction.Unassigned:
                        title = "Təhkimat ləğv edildi";
                        description = string.IsNullOrWhiteSpace(entry.FromWorkerName)
                            ? "İşçidən təhkim ləğv edildi."
                            : $"{entry.FromWorkerName} adlı işçidən təhkim ləğv edildi.";
                        icon = AppIconKind.User;
                        kind = AssetActivityKind.Unassigned;
                        break;

                    default:
                        title = "Təhkimat dəyişdirildi";
                        description =
                            $"{entry.FromWorkerName ?? "—"} → {entry.ToWorkerName ?? "—"}";
                        icon = AppIconKind.SwitchUser;
                        kind = AssetActivityKind.Reassigned;
                        break;
                }

                activities.Add(new AssetActivityItem
                {
                    Date = entry.ChangeDate,
                    Kind = kind,
                    Icon = icon,
                    Title = title,
                    Description = description,
                    Actor = $"Dəyişdirən: {entry.ChangedBy ?? "Sistem"}"
                });
            }

            foreach (var record in _currentAsset.MaintenanceHistory ?? new List<MaintenanceRecord>())
            {
                var details = new List<string>
                {
                    GetMaintenanceTypeText(record.MaintenanceType)
                };

                if (!string.IsNullOrWhiteSpace(record.Description))
                    details.Add(record.Description);

                if (record.Cost > 0)
                {
                    details.Add(
                        record.Cost.ToString("C", new CultureInfo("az-Latn-AZ")));
                }

                activities.Add(new AssetActivityItem
                {
                    Date = record.MaintenanceDate,
                    Kind = AssetActivityKind.Maintenance,
                    Icon = AppIconKind.Edit,
                    Title = "Texniki xidmət qeydi",
                    Description = string.Join(" • ", details),
                    Actor = string.IsNullOrWhiteSpace(record.PerformedBy)
                        ? null
                        : $"İcra edən: {record.PerformedBy}"
                });
            }

            var ordered = activities
                .OrderByDescending(item => item.Date)
                .ToList();

            ActivityTimelineList.ItemsSource = ordered;
            ActivityTimelineList.Visibility =
                ordered.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            ActivityEmptyState.Visibility =
                ordered.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private static string GetMaintenanceTypeText(MaintenanceType type)
            => type switch
            {
                MaintenanceType.Repair => "Təmir",
                MaintenanceType.Upgrade => "Təkmilləşdirmə",
                MaintenanceType.Inspection => "Yoxlama",
                MaintenanceType.Cleaning => "Təmizləmə",
                MaintenanceType.SoftwareUpdate => "Proqram yeniləməsi",
                _ => type.ToString()
            };

        private void PopulateFinancialsAndMaintenance()
        {
            if (_currentAsset == null) return;

            var cultureInfo = new CultureInfo("az-Latn-AZ");

            PurchaseCostValue.Text = _currentAsset.PurchaseCost.ToString("C", cultureInfo);
            SupplierValue.Text = string.IsNullOrEmpty(_currentAsset.Supplier) ? "N/A" : _currentAsset.Supplier;
            WarrantyValue.Text = _currentAsset.WarrantyExpirationDate > DateTime.MinValue ? _currentAsset.WarrantyExpirationDate.ToString("yyyy-MM-dd") : "N/A";
            WarrantyValue.Foreground = _currentAsset.WarrantyExpirationDate < DateTime.Today ? Brushes.IndianRed : Brushes.Black;
            UsefulLifeValue.Text = _currentAsset.UsefulLifeDisplay;
            AnnualDepreciationValue.Text = _currentAsset.AnnualDepreciation.ToString("C", cultureInfo);
            CurrentValueValue.Text = _currentAsset.CurrentValue.ToString("C", cultureInfo);
            CurrentValueValue.Foreground = _currentAsset.IsEndOfLife ? Brushes.IndianRed : Brushes.Black;
            var maintenanceItems = _currentAsset.MaintenanceHistory?
                .OrderByDescending(m => m.MaintenanceDate)
                .ToList() ?? new List<MaintenanceRecord>();

            MaintenanceListView.ItemsSource = maintenanceItems;
            MaintenanceListView.Visibility =
                maintenanceItems.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            MaintenanceEmptyState.Visibility =
                maintenanceItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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