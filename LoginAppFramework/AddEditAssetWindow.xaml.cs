using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class AddEditAssetWindow : Window
    {
        public Asset Asset { get; }

        private List<Worker> _availableWorkers;
        private List<WorkerSelectionOption> _workerOptions;
        private string _initialFormFingerprint;
        private bool _isTrackingChanges;
        private bool _allowClose;

        public AddEditAssetWindow(Asset assetToEdit)
        {
            InitializeComponent();
            Asset = assetToEdit.Clone();
            DataContext = Asset;

            AddHandler(
                TextBox.TextChangedEvent,
                new TextChangedEventHandler(FormTextChanged),
                true);

            AddHandler(
                Selector.SelectionChangedEvent,
                new SelectionChangedEventHandler(FormSelectionChanged),
                true);

            PurchaseDateSelector.SelectedDateChanged += DateSelector_SelectedDateChanged;
            WarrantyDateSelector.SelectedDateChanged += DateSelector_SelectedDateChanged;
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
                return;
            }

            _availableWorkers = AppData.GetWorkers();
            CategoryComboBox.ItemsSource = AppData.GetDeviceCategories();

            PopulateUserComboBox();

            if (Asset.PurchaseDate <= DateTime.MinValue)
                Asset.PurchaseDate = DateTime.Today;

            PurchaseDateSelector.SelectedDate = Asset.PurchaseDate;
            WarrantyDateSelector.SelectedDate =
                Asset.WarrantyExpirationDate > DateTime.MinValue
                    ? Asset.WarrantyExpirationDate
                    : null;

            PurchaseCostTextBox.Text =
                Asset.PurchaseCost.ToString(
                    "0.##",
                    CultureInfo.CurrentCulture);

            UsefulLifeTextBox.Text =
                Asset.UsefulLifeInYears.ToString(
                    CultureInfo.InvariantCulture);

            SelectCurrentWorker();
            UpdateStatusBehavior();
            GenerateCustomFields(Asset.Kateqoriya);

            _initialFormFingerprint = BuildFormFingerprint();
            _isTrackingChanges = true;
            UpdateDirtyState();
        }

        private void ApplyResponsiveBounds()
        {
            var workArea = SystemParameters.WorkArea;

            MaxWidth = Math.Max(460, workArea.Width - 40);
            MaxHeight = Math.Max(420, workArea.Height - 40);

            MinWidth = Math.Min(560, MaxWidth);
            MinHeight = Math.Min(520, MaxHeight);

            Width = Math.Min(720, MaxWidth);
            Height = Math.Min(800, MaxHeight);
        }

        private void PopulateUserComboBox()
        {
            int? currentWorkerId = Asset.WorkerId;

            var visibleWorkers = _availableWorkers
                .Where(worker =>
                    worker.IsActive ||
                    (currentWorkerId.HasValue && worker.Id == currentWorkerId.Value))
                .GroupBy(worker => worker.Id)
                .Select(group => group.First())
                .OrderBy(worker => worker.per_adiper_soyadi)
                .ToList();

            _workerOptions = new List<WorkerSelectionOption>
            {
                WorkerSelectionOption.Clear()
            };

            _workerOptions.AddRange(
                visibleWorkers.Select(WorkerSelectionOption.ForWorker));

            UserComboBox.ItemsSource = _workerOptions;
        }

        private void SelectCurrentWorker()
        {
            if (Asset.WorkerId.HasValue)
            {
                var option = _workerOptions.FirstOrDefault(item =>
                    item.Kind == WorkerSelectionKind.Worker &&
                    item.Worker?.Id == Asset.WorkerId.Value);

                if (option != null)
                {
                    UserComboBox.SelectedItem = option;
                    return;
                }
            }

            UserComboBox.SelectedItem = _workerOptions
                .First(option => option.Kind == WorkerSelectionKind.ClearAssignment);
        }

        private void UserComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            UpdateStatusBehavior();
        }

        private void UserComboBox_LostKeyboardFocus(
            object sender,
            KeyboardFocusChangedEventArgs e)
        {
            var resolution = ResolveWorkerSelection();

            if (!resolution.IsValid)
                return;

            var matchingOption = FindOptionForResolution(resolution);
            if (matchingOption != null &&
                !ReferenceEquals(UserComboBox.SelectedItem, matchingOption))
            {
                UserComboBox.SelectedItem = matchingOption;
            }

            UpdateStatusBehavior();
        }

        private WorkerSelectionResolution ResolveWorkerSelection()
            => WorkerSelectionResolver.Resolve(
                UserComboBox.Text,
                UserComboBox.SelectedItem as WorkerSelectionOption,
                _workerOptions,
                WorkerSelectionKind.ClearAssignment);

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

        private void UpdateStatusBehavior()
        {
            if (!IsLoaded) return;

            var allStatuses = AppData.GetAssetStatuses();
            var selectedOption = UserComboBox.SelectedItem as WorkerSelectionOption;

            if (selectedOption?.Kind == WorkerSelectionKind.Worker &&
                selectedOption.Worker != null)
            {
                AssignedDepartmentTextBox.Text =
                    selectedOption.Worker.pdp_adi ?? "—";

                StatusComboBox.ItemsSource = allStatuses;
                StatusComboBox.SelectedItem = "İstifadədədir";
                StatusComboBox.IsEnabled = false;
                return;
            }

            AssignedDepartmentTextBox.Text = "—";

            var manualStatuses = allStatuses
                .Where(status =>
                    status != "İstifadədədir" &&
                    status != "Arxivdə")
                .ToList();

            StatusComboBox.ItemsSource = manualStatuses;

            if (Asset.Status == "İstifadədədir" ||
                string.IsNullOrEmpty(Asset.Status) ||
                !manualStatuses.Contains(Asset.Status))
            {
                StatusComboBox.SelectedItem = "Anbarda";
            }
            else
            {
                StatusComboBox.SelectedItem = Asset.Status;
            }

            StatusComboBox.IsEnabled = true;
        }

        private void StatusComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (StatusComboBox.SelectedItem != null)
                Asset.Status = StatusComboBox.SelectedItem.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
            => TrySaveAndClose();

        private bool TrySaveAndClose()
        {
            ClearValidation();

            if (string.IsNullOrWhiteSpace(AssetNameTextBox.Text))
            {
                ShowValidation(
                    "Vəsaitin adı tələb olunur.",
                    AssetNameTextBox);
                return false;
            }

            if (!AssetFormValidator.TryParsePurchaseCost(
                    PurchaseCostTextBox.Text,
                    out decimal purchaseCost,
                    out string purchaseCostError))
            {
                ShowValidation(
                    purchaseCostError,
                    PurchaseCostTextBox);
                return false;
            }

            if (!AssetFormValidator.TryParseUsefulLife(
                    UsefulLifeTextBox.Text,
                    out int usefulLifeInYears,
                    out string usefulLifeError))
            {
                ShowValidation(
                    usefulLifeError,
                    UsefulLifeTextBox);
                return false;
            }

            DateTime? purchaseDate = PurchaseDateSelector.SelectedDate;
            DateTime? warrantyDate = WarrantyDateSelector.SelectedDate;

            if (!AssetFormValidator.ValidateDates(
                    purchaseDate,
                    warrantyDate,
                    out string dateError))
            {
                ShowValidation(
                    dateError,
                    warrantyDate.HasValue &&
                    purchaseDate.HasValue &&
                    warrantyDate.Value.Date < purchaseDate.Value.Date
                        ? WarrantyDateSelector
                        : PurchaseDateSelector);
                return false;
            }

            var workerResolution = ResolveWorkerSelection();

            if (!workerResolution.IsValid)
            {
                ShowValidation(
                    workerResolution.ErrorMessage,
                    UserComboBox);
                return false;
            }

            Asset.VesaitinKodu = AssetCodeTextBox.Text?.Trim();
            Asset.VesaitinAdi = AssetNameTextBox.Text?.Trim();
            Asset.ITAvadanliqlarininSeriyaNomresi =
                SerialNumberTextBox.Text?.Trim();
            Asset.Kateqoriya = CategoryComboBox.SelectedItem?.ToString();
            Asset.YerleshmeYeri = LocationTextBox.Text?.Trim();
            Asset.Erazi = AreaTextBox.Text?.Trim();
            Asset.Supplier = SupplierTextBox.Text?.Trim();

            if (workerResolution.Kind == WorkerSelectionKind.Worker)
                Asset.AssignWorker(workerResolution.Worker);
            else
                Asset.ClearWorkerAssignment();

            Asset.PurchaseCost = purchaseCost;
            Asset.UsefulLifeInYears = usefulLifeInYears;
            Asset.PurchaseDate = purchaseDate.Value;
            Asset.WarrantyExpirationDate =
                warrantyDate ?? DateTime.MinValue;

            if (StatusComboBox.SelectedItem != null)
                Asset.Status = StatusComboBox.SelectedItem.ToString();

            Asset.Name = Asset.VesaitinAdi;
            Asset.SerialNumber = Asset.ITAvadanliqlarininSeriyaNomresi;

            Asset.CustomFields.Clear();

            foreach (var child in CustomFieldsPanel.Children)
            {
                if (child is Grid grid &&
                    grid.Children.OfType<TextBox>().FirstOrDefault() is { } textBox)
                {
                    Asset.CustomFields[textBox.Tag?.ToString() ?? string.Empty] =
                        textBox.Text ?? string.Empty;
                }
            }

            AppServices.Assets.Save(Asset);

            _isTrackingChanges = false;
            _initialFormFingerprint = BuildFormFingerprint();
            SaveButton.IsEnabled = false;
            DirtyStateTextBlock.Visibility = Visibility.Collapsed;
            _allowClose = true;

            DialogResult = true;
            return true;
        }

        private void ClearValidation()
        {
            ValidationTextBlock.Text = string.Empty;
            ValidationTextBlock.Visibility = Visibility.Collapsed;

            foreach (Control control in new Control[]
            {
                AssetNameTextBox,
                UserComboBox,
                PurchaseCostTextBox,
                UsefulLifeTextBox,
                PurchaseDateSelector,
                WarrantyDateSelector
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

        private void PurchaseCostTextBox_PreviewTextInput(
            object sender,
            TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            string proposed = BuildProposedText(textBox, e.Text);
            e.Handled =
                !AssetFormValidator.IsPotentialPurchaseCostText(proposed);
        }

        private void UsefulLifeTextBox_PreviewTextInput(
            object sender,
            TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
                return;

            string proposed = BuildProposedText(textBox, e.Text);
            e.Handled =
                !AssetFormValidator.IsPotentialUsefulLifeText(proposed);
        }

        private void PurchaseCostTextBox_Pasting(
            object sender,
            DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox ||
                !e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string pasted = e.DataObject.GetData(DataFormats.Text) as string;
            string proposed = BuildProposedText(textBox, pasted ?? string.Empty);

            if (!AssetFormValidator.IsPotentialPurchaseCostText(proposed))
                e.CancelCommand();
        }

        private void UsefulLifeTextBox_Pasting(
            object sender,
            DataObjectPastingEventArgs e)
        {
            if (sender is not TextBox textBox ||
                !e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string pasted = e.DataObject.GetData(DataFormats.Text) as string;
            string proposed = BuildProposedText(textBox, pasted ?? string.Empty);

            if (!AssetFormValidator.IsPotentialUsefulLifeText(proposed))
                e.CancelCommand();
        }

        private static string BuildProposedText(
            TextBox textBox,
            string insertedText)
        {
            string current = textBox.Text ?? string.Empty;
            int selectionStart = Math.Clamp(
                textBox.SelectionStart,
                0,
                current.Length);

            int selectionLength = Math.Clamp(
                textBox.SelectionLength,
                0,
                current.Length - selectionStart);

            return current
                .Remove(selectionStart, selectionLength)
                .Insert(selectionStart, insertedText ?? string.Empty);
        }

        private void FormTextChanged(
            object sender,
            TextChangedEventArgs e)
            => UpdateDirtyState();

        private void FormSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
            => UpdateDirtyState();

        private void DateSelector_SelectedDateChanged(
            object sender,
            EventArgs e)
            => UpdateDirtyState();

        private void UpdateDirtyState()
        {
            if (!_isTrackingChanges)
                return;

            bool isDirty =
                !string.Equals(
                    _initialFormFingerprint,
                    BuildFormFingerprint(),
                    StringComparison.Ordinal);

            SaveButton.IsEnabled = isDirty;
            DirtyStateTextBlock.Visibility =
                isDirty ? Visibility.Visible : Visibility.Collapsed;
        }

        private bool HasUnsavedChanges()
            => _isTrackingChanges &&
               !string.Equals(
                   _initialFormFingerprint,
                   BuildFormFingerprint(),
                   StringComparison.Ordinal);

        private string BuildFormFingerprint()
        {
            string workerKey =
                UserComboBox.SelectedItem is WorkerSelectionOption option
                    ? $"{option.Kind}:{option.Worker?.Id}"
                    : $"text:{UserComboBox.Text}";

            string customFields = string.Join(
                "\u001D",
                CustomFieldsPanel.Children
                    .OfType<Grid>()
                    .SelectMany(grid => grid.Children.OfType<TextBox>())
                    .Where(textBox => textBox.Tag != null)
                    .OrderBy(textBox => textBox.Tag.ToString())
                    .Select(textBox =>
                        $"{textBox.Tag}={textBox.Text ?? string.Empty}"));

            return string.Join(
                "\u001F",
                new[]
                {
                    AssetCodeTextBox.Text ?? string.Empty,
                    AssetNameTextBox.Text ?? string.Empty,
                    SerialNumberTextBox.Text ?? string.Empty,
                    CategoryComboBox.SelectedItem?.ToString() ?? string.Empty,
                    StatusComboBox.SelectedItem?.ToString() ?? string.Empty,
                    workerKey,
                    LocationTextBox.Text ?? string.Empty,
                    AreaTextBox.Text ?? string.Empty,
                    PurchaseCostTextBox.Text ?? string.Empty,
                    PurchaseDateSelector.SelectedDate?.ToString("O") ?? string.Empty,
                    SupplierTextBox.Text ?? string.Empty,
                    WarrantyDateSelector.SelectedDate?.ToString("O") ?? string.Empty,
                    UsefulLifeTextBox.Text ?? string.Empty,
                    customFields
                });
        }

        private void CancelButton_Click(
            object sender,
            RoutedEventArgs e)
            => Close();

        private void Window_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Escape)
                return;

            e.Handled = true;
            Close();
        }

        private void Window_Closing(
            object sender,
            CancelEventArgs e)
        {
            if (_allowClose ||
                !_isTrackingChanges ||
                !HasUnsavedChanges())
            {
                return;
            }

            e.Cancel = true;

            string itemName =
                !string.IsNullOrWhiteSpace(AssetNameTextBox.Text)
                    ? AssetNameTextBox.Text.Trim()
                    : "Yeni vəsait";

            var choice = DialogService.ConfirmUnsavedChanges(
                this,
                itemName);

            if (choice == UnsavedChangesChoice.Save)
            {
                TrySaveAndClose();
                return;
            }

            if (choice == UnsavedChangesChoice.Discard)
            {
                _allowClose = true;
                Close();
            }
        }

        private void CategoryComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 &&
                e.AddedItems[0] is string category)
            {
                GenerateCustomFields(category);

                if (Asset.Id <= 0 &&
                    Asset.UsefulLifeInYears == 0)
                {
                    var defaultLifecycles =
                        AppData.GetCategoryDefaultLifecycles();

                    if (defaultLifecycles.TryGetValue(
                        category,
                        out int defaultYears))
                    {
                        Asset.UsefulLifeInYears = defaultYears;
                        UsefulLifeTextBox.Text =
                            defaultYears.ToString(CultureInfo.InvariantCulture);
                    }
                }
            }
        }

        private void GenerateCustomFields(string category)
        {
            CustomFieldsPanel.Children.Clear();
            CustomFieldsSeparator.Visibility = Visibility.Collapsed;

            var categoryCustomFields = AppData.GetCategoryCustomFields();

            if (category == null ||
                !categoryCustomFields.TryGetValue(category, out var fields))
            {
                return;
            }

            CustomFieldsSeparator.Visibility = Visibility.Visible;

            foreach (var fieldName in fields)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(190) });
                grid.ColumnDefinitions.Add(
                    new ColumnDefinition
                    {
                        Width = new GridLength(1, GridUnitType.Star)
                    });

                var label = new Label
                {
                    Content = $"{fieldName}:",
                    VerticalAlignment = VerticalAlignment.Center
                };

                var textBox = new TextBox
                {
                    Margin = new Thickness(0, 0, 0, 10),
                    Padding = new Thickness(5),
                    Tag = fieldName
                };

                if (Asset.CustomFields.TryGetValue(
                    fieldName,
                    out var fieldValue))
                {
                    textBox.Text = fieldValue;
                }

                Grid.SetColumn(label, 0);
                Grid.SetColumn(textBox, 1);

                grid.Children.Add(label);
                grid.Children.Add(textBox);

                CustomFieldsPanel.Children.Add(grid);
            }
        }
    }
}
