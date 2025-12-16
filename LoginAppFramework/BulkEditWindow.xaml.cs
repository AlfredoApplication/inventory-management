using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LoginAppFramework
{
    public partial class BulkEditWindow : Window
    {
        public BulkAssetChanges Changes { get; private set; }

        public BulkEditWindow(int assetCount)
        {
            InitializeComponent();
            InstructionText.Text = $"Seçilmiş {assetCount} element üçün dəyişdirmək istədiyiniz sahələrə YENİ dəyər daxil edin. Sahələri boş buraxmaq onların orijinal dəyərlərini saxlayacaq.";
            PopulateComboBoxes();
        }

        private void PopulateComboBoxes()
        {
            var noChangeOption = "(Dəyişiklik yoxdur)";

            var categories = new List<string> { noChangeOption };
            categories.AddRange(AppData.GetDeviceCategories());
            CategoryComboBox.ItemsSource = categories;
            CategoryComboBox.SelectedIndex = 0;

            var departments = new List<string> { noChangeOption };
            departments.AddRange(AppData.GetWorkerDepartments());
            DepartmentComboBox.ItemsSource = departments;
            DepartmentComboBox.SelectedIndex = 0;

            var statuses = new List<string> { noChangeOption };
            statuses.AddRange(AppData.GetAssetStatuses().Where(s => s != "Arxivdə"));
            StatusComboBox.ItemsSource = statuses;
            StatusComboBox.SelectedIndex = 0;

            var workers = new List<object> { noChangeOption, "(Boşdur)" };
            workers.AddRange(AppData.GetWorkers().Where(w => w.IsActive).ToList());
            UserComboBox.ItemsSource = workers;
            UserComboBox.SelectedIndex = 0;
        }

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            Changes = new BulkAssetChanges();

            if (!string.IsNullOrWhiteSpace(VesaitinKoduTextBox.Text)) Changes.VesaitinKodu = VesaitinKoduTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(VesaitinAdiTextBox.Text)) Changes.VesaitinAdi = VesaitinAdiTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(SeriyaNomresiTextBox.Text)) Changes.ITAvadanliqlarininSeriyaNomresi = SeriyaNomresiTextBox.Text.Trim();
            if (CategoryComboBox.SelectedIndex > 0) Changes.Kateqoriya = CategoryComboBox.SelectedItem.ToString();
            if (!string.IsNullOrWhiteSpace(DepartmentComboBox.Text) && DepartmentComboBox.Text != "(Dəyişiklik yoxdur)") Changes.BolmeShobeDepartment = DepartmentComboBox.Text;
            if (!string.IsNullOrWhiteSpace(LocationTextBox.Text)) Changes.YerleshmeYeri = LocationTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(AreaTextBox.Text)) Changes.Erazi = AreaTextBox.Text.Trim();
            if (StatusComboBox.SelectedIndex > 0) Changes.Status = StatusComboBox.SelectedItem.ToString();
            if (UserComboBox.SelectedIndex > 0)
            {
                if (UserComboBox.SelectedItem is Worker worker) { Changes.AssignedWorker = worker; }
                else if (UserComboBox.SelectedItem.ToString() == "(Boşdur)") { Changes.AssignedWorker = new Worker { Id = 0 }; }
            }
            if (!string.IsNullOrWhiteSpace(PurchaseCostTextBox.Text))
            {
                if (decimal.TryParse(PurchaseCostTextBox.Text, out decimal cost)) Changes.PurchaseCost = cost;
                else { MessageBox.Show("Alış qiyməti düzgün rəqəm formatında deyil.", "Xəta"); return; }
            }
            if (PurchaseDatePicker.SelectedDate.HasValue) Changes.PurchaseDate = PurchaseDatePicker.SelectedDate.Value;
            if (!string.IsNullOrWhiteSpace(UsefulLifeTextBox.Text))
            {
                if (int.TryParse(UsefulLifeTextBox.Text, out int life)) Changes.UsefulLifeInYears = life;
                else { MessageBox.Show("İstifadə müddəti düzgün rəqəm formatında deyil.", "Xəta"); return; }
            }

            // --- NEW LOGIC ADDED ---
            if (!string.IsNullOrWhiteSpace(SupplierTextBox.Text)) Changes.Supplier = SupplierTextBox.Text.Trim();
            if (WarrantyDatePicker.SelectedDate.HasValue) Changes.WarrantyExpirationDate = WarrantyDatePicker.SelectedDate.Value;

            this.DialogResult = true;
            this.Close();
        }
    }
}