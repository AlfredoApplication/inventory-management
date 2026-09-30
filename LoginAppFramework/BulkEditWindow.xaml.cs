using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;

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
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
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

        private void ClearValidation()
        {
            ValidationTextBlock.Text = string.Empty;
            ValidationTextBlock.Visibility = Visibility.Collapsed;

            foreach (Control control in new Control[]
            {
                UserComboBox,
                PurchaseCostTextBox,
                UsefulLifeTextBox
            })
            {
                control.ClearValue(Control.BorderBrushProperty);
                control.ClearValue(Control.BorderThicknessProperty);
            }
        }

        private void ShowValidation(
            string message,
            Control control)
        {
            ValidationTextBlock.Text = message;
            ValidationTextBlock.Visibility = Visibility.Visible;

            control.BorderBrush =
                (Brush)FindResource("DangerBrush");
            control.BorderThickness = new Thickness(2);
            control.Focus();
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ClearValidation();
            Changes = new BulkAssetChanges();

            Changes.VesaitinKodu = BulkAssetChanges.ResolveTextChange(
                VesaitinKoduTextBox.Text,
                ClearVesaitinKoduCheckBox.IsChecked == true);

            if (!string.IsNullOrWhiteSpace(VesaitinAdiTextBox.Text))
                Changes.VesaitinAdi = VesaitinAdiTextBox.Text.Trim();

            Changes.ITAvadanliqlarininSeriyaNomresi =
                BulkAssetChanges.ResolveTextChange(
                    SeriyaNomresiTextBox.Text,
                    ClearSerialCheckBox.IsChecked == true);

            if (CategoryComboBox.SelectedIndex > 0)
                Changes.Kateqoriya = CategoryComboBox.SelectedItem.ToString();

            Changes.YerleshmeYeri = BulkAssetChanges.ResolveTextChange(
                LocationTextBox.Text,
                ClearLocationCheckBox.IsChecked == true);

            Changes.Erazi = BulkAssetChanges.ResolveTextChange(
                AreaTextBox.Text,
                ClearAreaCheckBox.IsChecked == true);

            if (StatusComboBox.SelectedIndex > 0)
                Changes.Status = StatusComboBox.SelectedItem.ToString();

            var workerResolution = ResolveWorkerSelection();

            if (!workerResolution.IsValid)
            {
                ShowValidation(
                    workerResolution.ErrorMessage,
                    UserComboBox);
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
                if (!AssetFormValidator.TryParsePurchaseCost(
                        PurchaseCostTextBox.Text,
                        out decimal cost,
                        out string costError))
                {
                    ShowValidation(
                        costError,
                        PurchaseCostTextBox);
                    return;
                }

                Changes.PurchaseCost = cost;
            }

            if (PurchaseDateSelector.SelectedDate.HasValue)
                Changes.PurchaseDate =
                    PurchaseDateSelector.SelectedDate.Value;

            if (!string.IsNullOrWhiteSpace(UsefulLifeTextBox.Text))
            {
                if (!AssetFormValidator.TryParseUsefulLife(
                        UsefulLifeTextBox.Text,
                        out int life,
                        out string lifeError))
                {
                    ShowValidation(
                        lifeError,
                        UsefulLifeTextBox);
                    return;
                }

                Changes.UsefulLifeInYears = life;
            }

            Changes.Supplier = BulkAssetChanges.ResolveTextChange(
                SupplierTextBox.Text,
                ClearSupplierCheckBox.IsChecked == true);

            if (WarrantyDateSelector.SelectedDate.HasValue)
                Changes.WarrantyExpirationDate =
                    WarrantyDateSelector.SelectedDate.Value;

            if (PurchaseDateSelector.SelectedDate.HasValue &&
                WarrantyDateSelector.SelectedDate.HasValue &&
                !AssetFormValidator.ValidateDates(
                    PurchaseDateSelector.SelectedDate,
                    WarrantyDateSelector.SelectedDate,
                    out string dateError))
            {
                ShowValidation(
                    dateError,
                    WarrantyDateSelector);
                return;
            }

            DialogResult = true;
            Close();
        }
    }
}
