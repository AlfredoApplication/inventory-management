using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LoginAppFramework
{
    public partial class WorkerAssetManager : UserControl
    {
        public event EventHandler<Worker> OnEditWorker;
        public event EventHandler<Worker> OnDeleteWorker;
        public event EventHandler AssetAssignmentChanged;
        public event EventHandler OnDetailPanelClosed;
        public event EventHandler<Asset> OnAssetDoubleClicked;

        private WorkerViewModel _currentWorker;
        private bool _isReadOnlyMode = false;

        public bool CanEditAssets => !_isReadOnlyMode;

        public WorkerAssetManager()
        {
            InitializeComponent();
        }

        private void CloseDetailPanelButton_Click(object sender, RoutedEventArgs e)
        {
            OnDetailPanelClosed?.Invoke(this, EventArgs.Empty);
        }

        public void SetWorker(WorkerViewModel worker, List<Asset> allAssets)
        {
            _currentWorker = worker;
            DataContext = _currentWorker;
            if (_currentWorker != null)
            {
                SelectWorkerPrompt.Visibility = Visibility.Collapsed;
                DetailViewPanel.Visibility = Visibility.Visible;
                AssignedAssetsDataGrid.ItemsSource = allAssets.Where(a => a.WorkerId == _currentWorker.GetModel().Id).ToList();
            }
            else
            {
                SelectWorkerPrompt.Visibility = Visibility.Visible;
                DetailViewPanel.Visibility = Visibility.Collapsed;
                DataContext = null;
            }
            ApplyReadOnlyPermissions();
        }

        public void SetReadOnlyMode(bool isReadOnly)
        {
            _isReadOnlyMode = isReadOnly;
            ApplyReadOnlyPermissions();
        }

        private void ApplyReadOnlyPermissions()
        {
            bool canEdit = !_isReadOnlyMode;
            AssignNewAssetButton.IsEnabled = canEdit;
            // Force re-evaluation of CanEditAssets property for XAML bindings
            OnPropertyChanged(nameof(CanEditAssets));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private void AssignedAssetsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (AssignedAssetsDataGrid.SelectedItem is Asset selectedAsset)
            {
                OnAssetDoubleClicked?.Invoke(this, selectedAsset);
            }
        }

        private void UnassignButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (sender is FrameworkElement { DataContext: Asset assetToUnassign } && _currentWorker != null &&
                MessageBox.Show($"'{_currentWorker.Name}' adlı işçidən təhkimi ləğv etməyə əminsinizmi?", "Təsdiq", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                string oldUser = assetToUnassign.TehkimOlunanEmekdas;
                assetToUnassign.Status = "Anbarda";
                assetToUnassign.WorkerId = null;
                assetToUnassign.TehkimOlunanEmekdas = null;
                assetToUnassign.Vezifesi = null;
                assetToUnassign.BolmeShobeDepartment = null;

                if (assetToUnassign.History == null) assetToUnassign.History = new List<AssignmentHistoryEntry>();
                assetToUnassign.History.Add(new() { Action = AssignmentAction.Unassigned, FromWorkerName = oldUser, ToWorkerName = "Sistem", ChangedBy = SessionManager.CurrentUser.FullName, ChangeDate = DateTime.Now });
                AppData.SaveAndRefreshAsset(assetToUnassign);
                AssetAssignmentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void AssignNewAssetButton_Click(object _, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_currentWorker == null) return;
            var availableAssets = AppData.GetAssets()
                   .Where(a => a.WorkerId == null && a.Status != "Arxivdə")
                   .ToList();
            if (!availableAssets.Any())
            {
                MessageBox.Show("Təhkim ediləcək boş vəsait yoxdur.", "Vəsait Yoxdur");
                return;
            }
            var selectWindow = new SelectAssetWindow(availableAssets) { Owner = Window.GetWindow(this) };
            if (selectWindow.ShowDialog() == true && selectWindow.SelectedAsset != null)
            {
                var assetToAssign = selectWindow.SelectedAsset;
                var workerModel = _currentWorker.GetModel();

                assetToAssign.Status = "İstifadədədir";
                assetToAssign.WorkerId = workerModel.Id;
                assetToAssign.TehkimOlunanEmekdas = workerModel.per_adiper_soyadi;
                assetToAssign.Vezifesi = workerModel.pgk_gorev_adi;
                assetToAssign.BolmeShobeDepartment = workerModel.pdp_adi;

                if (assetToAssign.History == null) assetToAssign.History = new List<AssignmentHistoryEntry>();
                assetToAssign.History.Add(new() { Action = AssignmentAction.Assigned, FromWorkerName = "Sistem", ToWorkerName = _currentWorker.Name, ChangedBy = SessionManager.CurrentUser.FullName, ChangeDate = DateTime.Now });
                AppData.SaveAndRefreshAsset(assetToAssign);
                AssetAssignmentChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}