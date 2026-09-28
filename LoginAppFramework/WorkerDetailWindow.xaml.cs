using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LoginAppFramework
{
    public partial class WorkerDetailWindow : Window
    {
        private readonly Worker _workerToShow;
        private readonly List<Asset> _allAssets;
        public event Action OnWorkerUpdated;

        public WorkerDetailWindow(Worker workerToShow, List<Asset> allAssets)
        {
            InitializeComponent();
            _workerToShow = workerToShow;
            _allAssets = allAssets;
            Loaded += Window_Loaded;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (_workerToShow == null)
            {
                MessageBox.Show("Cannot display details for a null worker.", "Error");
                this.Close();
                return;
            }

            // This now populates the correctly named UI elements using your 'per_' property names.
            this.Title = $"{_workerToShow.per_adiper_soyadi} - Details";
            WorkerNameTextBlock.Text = _workerToShow.per_adiper_soyadi ?? "N/A";
            WorkerPositionTextBlock.Text = _workerToShow.pgk_gorev_adi ?? "N/A";
            DepartmentTextBlock.Text = _workerToShow.pdp_adi ?? "N/A";
            PerKodTextBlock.Text = _workerToShow.per_kod ?? "N/A";

            // The asset list logic is updated to use the correct name for comparison
            var assignedAssets = _allAssets
                .Where(a => a.AssignedUser != null &&
                            a.AssignedUser.Equals(_workerToShow.per_adiper_soyadi, StringComparison.OrdinalIgnoreCase))
                .ToList();

            AssignedAssetsHeader.Text = $"Assigned Assets ({assignedAssets.Count})";
            AssignedAssetsListView.ItemsSource = assignedAssets;

            // Apply permission checks
            ApplyRoleBasedPermissions();
        }

        private void ApplyRoleBasedPermissions()
        {
            bool canEdit = SessionManager.CanEdit();
            EditButton.IsEnabled = canEdit;
        }

        private void EditButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var editWindow = new AddEditWorkerWindow(_workerToShow) { Owner = this };
            if (editWindow.ShowDialog() == true)
            {
                AppData.SaveAndRefreshWorker(editWindow.Worker);
                OnWorkerUpdated?.Invoke();
                this.Close();
            }
        }
    }
}