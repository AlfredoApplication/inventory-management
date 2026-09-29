using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoginAppFramework
{
    // NEW ENUM to communicate the result back to the calling window
    public enum MaintenanceEditResult
    {
        Cancelled,
        Saved,
        Deleted
    }

    public partial class AddMaintenanceRecordWindow : Window
    {
        private readonly Asset _asset;
        private readonly MaintenanceRecord _recordToEdit;
        private readonly bool _isEditMode;

        // Public properties to pass data back
        public MaintenanceRecord Record { get; private set; }
        public MaintenanceEditResult Result { get; private set; } = MaintenanceEditResult.Cancelled;

        private Dictionary<MaintenanceType, string> _maintenanceTypeTranslations;

        public AddMaintenanceRecordWindow(Asset asset, MaintenanceRecord recordToEdit = null)
        {
            InitializeComponent();
            _asset = asset;
            _recordToEdit = recordToEdit;
            _isEditMode = _recordToEdit != null; // Set the mode based on if a record was passed in

            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            // Populate the dropdown
            _maintenanceTypeTranslations = new Dictionary<MaintenanceType, string>
            {
                { MaintenanceType.Repair, "Təmir" },
                { MaintenanceType.Upgrade, "Təkmilləşdirmə" },
                { MaintenanceType.Inspection, "Yoxlama" },
                { MaintenanceType.Cleaning, "Təmizlik" },
                { MaintenanceType.SoftwareUpdate, "Proqram Təminatı Yeniləməsi" }
            };
            MaintenanceTypeComboBox.ItemsSource = _maintenanceTypeTranslations;
            MaintenanceTypeComboBox.DisplayMemberPath = "Value";
            MaintenanceTypeComboBox.SelectedValuePath = "Key";

            if (_isEditMode)
            {
                // EDIT MODE
                this.Title = "Texniki Xidmət Qeydini Dəyiş";
                AssetTitle.Text = $"Qeyd dəyişdirilir: {_asset.Name}";
                DeleteButton.Visibility = Visibility.Visible;

                // Populate fields with existing data
                MaintenanceDatePicker.SelectedDate = _recordToEdit.MaintenanceDate;
                MaintenanceTypeComboBox.SelectedValue = _recordToEdit.MaintenanceType;
                CostTextBox.Text = _recordToEdit.Cost.ToString(CultureInfo.CurrentCulture);
                DescriptionTextBox.Text = _recordToEdit.Description;
            }
            else
            {
                // ADD MODE
                this.Title = "Yeni Texniki Xidmət Qeydi";
                AssetTitle.Text = $"Yeni qeyd əlavə olunur: {_asset.Name}";
                MaintenanceDatePicker.SelectedDate = DateTime.Today;
                MaintenanceTypeComboBox.SelectedIndex = 0;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            ClearValidation();

            if (MaintenanceDatePicker.SelectedDate == null)
            {
                ShowValidation(
                    "Texniki xidmət tarixini seçin.",
                    MaintenanceDatePicker);
                return;
            }

            if (MaintenanceTypeComboBox.SelectedValue == null)
            {
                ShowValidation(
                    "Texniki xidmət növünü seçin.",
                    MaintenanceTypeComboBox);
                return;
            }

            if (!decimal.TryParse(
                CostTextBox.Text,
                NumberStyles.Any,
                CultureInfo.CurrentCulture,
                out var cost))
            {
                ShowValidation(
                    "Xərc düzgün rəqəm formatında olmalıdır.",
                    CostTextBox);
                return;
            }

            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text))
            {
                ShowValidation(
                    "Təsvir daxil edin.",
                    DescriptionTextBox);
                return;
            }

            // Create a new record or use the one being edited
            Record = _isEditMode ? _recordToEdit : new MaintenanceRecord();

            Record.MaintenanceDate = MaintenanceDatePicker.SelectedDate.Value;
            Record.MaintenanceType = (MaintenanceType)MaintenanceTypeComboBox.SelectedValue;
            Record.Cost = cost;
            Record.Description = DescriptionTextBox.Text;
            Record.PerformedBy = SessionManager.CurrentUser.FullName;

            Result = MaintenanceEditResult.Saved;
            DialogResult = true;
            Close();
        }

        private void ClearValidation()
        {
            ValidationTextBlock.Text = string.Empty;
            ValidationTextBlock.Visibility = Visibility.Collapsed;

            foreach (Control control in new Control[]
            {
                MaintenanceDatePicker,
                MaintenanceTypeComboBox,
                CostTextBox,
                DescriptionTextBox
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

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmResult = MessageBox.Show(
                "Bu texniki xidmət qeydini həmişəlik silmək istədiyinizə əminsinizmi?",
                "Silməni Təsdiq Et",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirmResult == MessageBoxResult.Yes)
            {
                Result = MaintenanceEditResult.Deleted;
                DialogResult = true;
                Close();
            }
        }
    }
}