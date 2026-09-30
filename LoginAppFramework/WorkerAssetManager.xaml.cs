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
            EditWorkerButton.IsEnabled = canEdit;
            DeleteWorkerButton.IsEnabled = canEdit;
            // Force re-evaluation of CanEditAssets property for XAML bindings
            OnPropertyChanged(nameof(CanEditAssets));
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        private void EditWorkerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode || _currentWorker == null) return;
            OnEditWorker?.Invoke(this, _currentWorker.GetModel());
        }

        private void DeleteWorkerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isReadOnlyMode || _currentWorker == null) return;
            OnDeleteWorker?.Invoke(this, _currentWorker.GetModel());
        }

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
                DialogService.Warning(
                    Window.GetWindow(this),
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            if (sender is FrameworkElement { DataContext: Asset assetToUnassign } &&
                _currentWorker != null)
            {
                bool confirm = DialogService.Confirm(
                    Window.GetWindow(this),
                    "Təhkimatı Ləğv Et",
                    $"'{_currentWorker.Name}' adlı işçidən təhkimi ləğv etməyə əminsinizmi?",
                    "Təhkimatı ləğv et",
                    "Geri qayıt");

                if (!confirm)
                    return;

                AppServices.Assets.Unassign(assetToUnassign, "İşçi detalları");
                AssetAssignmentChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private void AssignNewAssetButton_Click(object _, RoutedEventArgs e)
        {
            if (_isReadOnlyMode)
            {
                DialogService.Warning(
                    Window.GetWindow(this),
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                return;
            }

            if (_currentWorker == null) return;

            var availableAssets = AppData.GetAssets()
                .Where(a => a.WorkerId == null && a.Status != "Arxivdə")
                .ToList();

            if (!availableAssets.Any())
            {
                NotificationService.Info(
                    Window.GetWindow(this),
                    "Təhkim ediləcək boş vəsait yoxdur.",
                    title: "Vəsait Yoxdur");
                return;
            }

            var selectWindow = new SelectAssetWindow(availableAssets) { Owner = Window.GetWindow(this) };
            if (selectWindow.ShowDialog() == true && selectWindow.SelectedAsset != null)
            {
                AppServices.Assets.Assign(
                    selectWindow.SelectedAsset,
                    _currentWorker.GetModel(),
                    "İşçi detalları");

                AssetAssignmentChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}