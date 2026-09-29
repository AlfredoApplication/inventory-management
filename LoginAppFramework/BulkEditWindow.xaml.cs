using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class BulkEditWindow : Window
    {
        public BulkAssetChanges Changes { get; private set; }

        private List<WorkerSelectionOption> _workerOptions;

        public BulkEditWindow(int assetCount)
        {
            InitializeComponent();

            InstructionText.Text =
                $"Seçilmiş {assetCount} element üçün dəyişdirmək istədiyiniz sahələrə YENİ dəyər daxil edin. " +
                "Sahələri boş buraxmaq onların orijinal dəyərlərini saxlayacaq. Optional mətn sahəsini silmək üçün 'Təmizlə' seçin.";

            PopulateComboBoxes();
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyResponsiveBounds();

            if (!SessionManager.CanEdit())
            {
                MessageBox.Show(
                    "Bu əməliyyat üçün icazəniz yoxdur.",
                    "Giriş Qadağandır",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                Close();
            }
        }

        private void ApplyResponsiveBounds()
        {
            var workArea = SystemParameters.WorkArea;

            MaxWidth = Math.Max(460, workArea.Width - 40);
            MaxHeight = Math.Max(420, workArea.Height - 40);

            MinWidth = Math.Min(520, MaxWidth);
            MinHeight = Math.Min(480, MaxHeight);

            Width = Math.Min(620, MaxWidth);
            Height = Math.Min(720, MaxHeight);
        }

        private void PopulateComboBoxes()
        {
            const string noChangeOption = "(Dəyişiklik yoxdur)";

            var categories = new List<string> { noChangeOption };
            categories.AddRange(AppData.GetDeviceCategories());
            CategoryComboBox.ItemsSource = categories;
            CategoryComboBox.SelectedIndex = 0;

            var statuses = new List<string> { noChangeOption };
            statuses.AddRange(
                AppData.GetAssetStatuses()
                    .Where(status => status != "Arxivdə"));
            StatusComboBox.ItemsSource = statuses;
            StatusComboBox.SelectedIndex = 0;

            _workerOptions = new List<WorkerSelectionOption>
            {
                WorkerSelectionOption.NoChange(),
                WorkerSelectionOption.Clear()
            };

            _workerOptions.AddRange(
                AppData.GetWorkers()
                    .Where(worker => worker.IsActive)
                    .OrderBy(worker => worker.per_adiper_soyadi)
                    .Select(WorkerSelectionOption.ForWorker));

            UserComboBox.ItemsSource = _workerOptions;
            UserComboBox.SelectedItem = _workerOptions[0];
        }

        private void UserComboBox_LostKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs e)
        {
            var resolution = ResolveWorkerSelection();

            if (!resolution.IsValid)
                return;

            var option = FindOptionForResolution(resolution);
            if (option != null &&
                !ReferenceEquals(UserComboBox.SelectedItem, option))
            {
                UserComboBox.SelectedItem = option;
            }
        }

        private WorkerSelectionResolution ResolveWorkerSelection()
            => WorkerSelectionResolver.Resolve(
                UserComboBox.Text,
                UserComboBox.SelectedItem as WorkerSelectionOption,
                _workerOptions,
                WorkerSelectionKind.NoChange);

        private WorkerSelectionOption FindOptionForResolution(
            WorkerSelectionResolution resolution)
        {
            if (resolution.Kind == WorkerSelectionKind.Worker)
            {
                return _workerOptions.FirstOrDefault(option =>
                    option.Kind == WorkerSelectionKind.Worker &&
                    option.Worker?.Id == resolution.Worker?.Id);
            }

            return _workerOptions.FirstOrDefault(option =>
                option.Kind == resolution.Kind);
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            Changes = new BulkAssetChanges();

            if (!string.IsNullOrWhiteSpace(VesaitinKoduTextBox.Text))
                Changes.VesaitinKodu = VesaitinKoduTextBox.Text.Trim();

            if (!string.IsNullOrWhiteSpace(VesaitinAdiTextBox.Text))
                Changes.VesaitinAdi = VesaitinAdiTextBox.Text.Trim();

            if (!string.IsNullOrWhiteSpace(SeriyaNomresiTextBox.Text))
                Changes.ITAvadanliqlarininSeriyaNomresi =
                    SeriyaNomresiTextBox.Text.Trim();

            if (CategoryComboBox.SelectedIndex > 0)
                Changes.Kateqoriya = CategoryComboBox.SelectedItem.ToString();

            if (!string.IsNullOrWhiteSpace(LocationTextBox.Text))
                Changes.YerleshmeYeri = LocationTextBox.Text.Trim();

            if (!string.IsNullOrWhiteSpace(AreaTextBox.Text))
                Changes.Erazi = AreaTextBox.Text.Trim();

            if (StatusComboBox.SelectedIndex > 0)
                Changes.Status = StatusComboBox.SelectedItem.ToString();

            var workerResolution = ResolveWorkerSelection();

            if (!workerResolution.IsValid)
            {
                MessageBox.Show(
                    workerResolution.ErrorMessage,
                    "Əməkdaş Seçimi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                UserComboBox.Focus();
                return;
            }

            if (workerResolution.Kind == WorkerSelectionKind.Worker)
            {
                Changes.AssignedWorker = workerResolution.Worker;
            }
            else if (workerResolution.Kind == WorkerSelectionKind.ClearAssignment)
            {
                Changes.AssignedWorker = new Worker { Id = 0 };
            }

            if (!string.IsNullOrWhiteSpace(PurchaseCostTextBox.Text))
            {
                if (decimal.TryParse(
                    PurchaseCostTextBox.Text,
                    out decimal cost))
                {
                    Changes.PurchaseCost = cost;
                }
                else
                {
                    MessageBox.Show(
                        "Alış qiyməti düzgün rəqəm formatında deyil.",
                        "Xəta",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            if (PurchaseDateSelector.SelectedDate.HasValue)
                Changes.PurchaseDate =
                    PurchaseDateSelector.SelectedDate.Value;

            if (!string.IsNullOrWhiteSpace(UsefulLifeTextBox.Text))
            {
                if (int.TryParse(
                    UsefulLifeTextBox.Text,
                    out int life))
                {
                    if (life < 0)
                    {
                        MessageBox.Show(
                            "İstifadə müddəti mənfi ola bilməz. 0 dəyəri 'təyin edilməyib' kimi qəbul olunur.",
                            "Xəta",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        return;
                    }

                    Changes.UsefulLifeInYears = life;
                }
                else
                {
                    MessageBox.Show(
                        "İstifadə müddəti düzgün rəqəm formatında deyil.",
                        "Xəta",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }
            }

            if (!string.IsNullOrWhiteSpace(SupplierTextBox.Text))
                Changes.Supplier = SupplierTextBox.Text.Trim();

            if (WarrantyDateSelector.SelectedDate.HasValue)
                Changes.WarrantyExpirationDate =
                    WarrantyDateSelector.SelectedDate.Value;

            DialogResult = true;
            Close();
        }
    }
}
