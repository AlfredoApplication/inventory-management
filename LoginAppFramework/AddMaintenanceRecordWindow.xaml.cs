using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

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
            if (MaintenanceDatePicker.SelectedDate == null) { MessageBox.Show("Zəhmət olmasa texniki xidmət tarixini seçin.", "Xəta"); return; }
            if (!decimal.TryParse(CostTextBox.Text, NumberStyles.Any, CultureInfo.CurrentCulture, out var cost)) { MessageBox.Show("Xərc düzgün rəqəm formatında olmalıdır.", "Xəta"); return; }
            if (string.IsNullOrWhiteSpace(DescriptionTextBox.Text)) { MessageBox.Show("Zəhmət olmasa təsvir daxil edin.", "Xəta"); return; }

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