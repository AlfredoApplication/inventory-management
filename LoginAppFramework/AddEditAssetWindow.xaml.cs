using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace LoginAppFramework
{
    public partial class AddEditAssetWindow : Window
    {
        public Asset Asset { get; }
        private List<Worker> _availableWorkers;

        public AddEditAssetWindow(Asset assetToEdit)
        {
            InitializeComponent();
            Asset = assetToEdit.Clone();
            DataContext = Asset;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            _availableWorkers = AppData.GetWorkers();
            CategoryComboBox.ItemsSource = AppData.GetDeviceCategories();
            DepartmentComboBox.ItemsSource = AppData.GetWorkerDepartments();

            PopulateUserComboBox(_availableWorkers.Where(w => w.IsActive).ToList());

            if (Asset.PurchaseDate <= DateTime.MinValue) Asset.PurchaseDate = DateTime.Today;

            // When editing, find the assigned worker by ID to select them in the ComboBox
            if (Asset.WorkerId.HasValue)
            {
                var assignedWorker = _availableWorkers.FirstOrDefault(w => w.Id == Asset.WorkerId.Value);
                if (assignedWorker != null)
                {
                    UserComboBox.SelectedItem = assignedWorker;
                }
            }
            else
            {
                UserComboBox.SelectedIndex = 0;
            }

            UpdateStatusBehavior();
            GenerateCustomFields(Asset.Kateqoriya);
        }

        private void PopulateUserComboBox(List<Worker> workers)
        {
            var workerList = new List<object> { "(Boşdur)" };
            workerList.AddRange(workers);
            UserComboBox.ItemsSource = workerList;
        }

        private void UserComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateStatusBehavior();
        }

        private void UpdateStatusBehavior()
        {
            if (!this.IsLoaded) return;
            var allStatuses = AppData.GetAssetStatuses();
            if (UserComboBox.SelectedItem is Worker)
            {
                StatusComboBox.ItemsSource = allStatuses;
                StatusComboBox.SelectedItem = "İstifadədədir";
                StatusComboBox.IsEnabled = false;
            }
            else
            {
                var manualStatuses = allStatuses
                    .Where(s => s != "İstifadədədir" && s != "Arxivdə")
                    .ToList();
                StatusComboBox.ItemsSource = manualStatuses;
                if (Asset.Status == "İstifadədədir" || string.IsNullOrEmpty(Asset.Status))
                {
                    StatusComboBox.SelectedItem = "Anbarda";
                }
                else
                {
                    if (manualStatuses.Contains(Asset.Status))
                    {
                        StatusComboBox.SelectedItem = Asset.Status;
                    }
                    else
                    {
                        StatusComboBox.SelectedItem = "Anbarda";
                    }
                }
                StatusComboBox.IsEnabled = true;
            }
        }

        private void StatusComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StatusComboBox.SelectedItem != null)
            {
                Asset.Status = StatusComboBox.SelectedItem.ToString();
            }
        }

        private void SaveButton_Click(object _, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Asset.VesaitinAdi)) { MessageBox.Show("Vəsaitin Adı tələb olunur.", "Xəta"); return; }

            // --- THE FIX: We now set the WorkerId foreign key ---
            if (UserComboBox.SelectedItem is Worker assignedWorker && assignedWorker.Id > 0)
            {
                Asset.WorkerId = assignedWorker.Id;
                // We still set these for immediate display, but they are no longer the source of truth.
                Asset.TehkimOlunanEmekdas = assignedWorker.per_adiper_soyadi;
                Asset.Vezifesi = assignedWorker.pgk_gorev_adi;
                Asset.BolmeShobeDepartment = assignedWorker.pdp_adi;
            }
            else
            {
                Asset.WorkerId = null; // Unassign the asset
                Asset.TehkimOlunanEmekdas = null;
                Asset.Vezifesi = null;
                Asset.BolmeShobeDepartment = null;
            }

            if (StatusComboBox.SelectedItem != null)
            {
                Asset.Status = StatusComboBox.SelectedItem.ToString();
            }

            Asset.Name = Asset.VesaitinAdi;
            Asset.SerialNumber = Asset.ITAvadanliqlarininSeriyaNomresi;

            if (Asset.UsefulLifeInYears <= 0) Asset.UsefulLifeInYears = 0;

            Asset.CustomFields.Clear();
            foreach (var child in CustomFieldsPanel.Children)
            {
                if (child is Grid grid && grid.Children.OfType<TextBox>().FirstOrDefault() is { } textBox)
                {
                    Asset.CustomFields[textBox.Tag.ToString()] = textBox.Text;
                }
            }

            AppData.SaveAndRefreshAsset(Asset);
            DialogResult = true;
            Close();
        }

        private void CategoryComboBox_SelectionChanged(object _, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is string category)
            {
                GenerateCustomFields(category);
                if (Asset.Id <= 0 && Asset.UsefulLifeInYears == 0)
                {
                    var defaultLifecycles = AppData.GetCategoryDefaultLifecycles();
                    if (defaultLifecycles.TryGetValue(category, out int defaultYears))
                    {
                        Asset.UsefulLifeInYears = defaultYears;
                    }
                }
            }
        }

        private void GenerateCustomFields(string category)
        {
            CustomFieldsPanel.Children.Clear();
            CustomFieldsSeparator.Visibility = Visibility.Collapsed;
            var categoryCustomFields = AppData.GetCategoryCustomFields();
            if (category == null || !categoryCustomFields.TryGetValue(category, out var fields))
            {
                return;
            }
            CustomFieldsSeparator.Visibility = Visibility.Visible;
            foreach (var fieldName in fields)
            {
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(200) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                var label = new Label { Content = $"{fieldName}:", VerticalAlignment = VerticalAlignment.Center };
                var textBox = new TextBox { Margin = new Thickness(0, 0, 0, 10), Padding = new Thickness(5), Tag = fieldName };
                if (Asset.CustomFields.TryGetValue(fieldName, out var fieldValue)) { textBox.Text = fieldValue; }
                Grid.SetColumn(label, 0); Grid.SetColumn(textBox, 1);
                grid.Children.Add(label); grid.Children.Add(textBox);
                CustomFieldsPanel.Children.Add(grid);
            }
        }
    }
}