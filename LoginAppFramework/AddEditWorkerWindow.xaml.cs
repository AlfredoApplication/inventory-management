using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class AddEditWorkerWindow : Window
    {
        public Worker Worker { get; private set; }

        private string _initialFormFingerprint;
        private bool _isTrackingChanges;
        private bool _allowClose;

        public AddEditWorkerWindow(Worker workerToEdit = null)
        {
            InitializeComponent();
            Worker = workerToEdit?.Clone() ?? new Worker();
            DataContext = Worker;

            AddHandler(
                TextBox.TextChangedEvent,
                new TextChangedEventHandler(FormTextChanged),
                true);

            AddHandler(
                Selector.SelectionChangedEvent,
                new SelectionChangedEventHandler(FormSelectionChanged),
                true);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (!SessionManager.CanEdit())
            {
                DialogService.Warning(
                    this,
                    "Giriş Qadağandır",
                    "Bu əməliyyat üçün icazəniz yoxdur.");
                Close();
                return;
            }

            DepartmentComboBox.ItemsSource =
                AppData.GetWorkerDepartments();

            if (Worker.Id > 0)
                LogonNameTextBox.IsEnabled = false;

            _initialFormFingerprint = BuildFormFingerprint();
            _isTrackingChanges = true;
            UpdateDirtyState();
        }

        private void SaveButton_Click(object _, RoutedEventArgs e)
            => TrySaveAndClose();

        private bool TrySaveAndClose()
        {
            ClearValidation();

            Control firstInvalid = null;

            if (string.IsNullOrWhiteSpace(FullNameTextBox.Text))
            {
                MarkInvalid(FullNameTextBox);
                firstInvalid ??= FullNameTextBox;
            }

            if (string.IsNullOrWhiteSpace(LogonNameTextBox.Text))
            {
                MarkInvalid(LogonNameTextBox);
                firstInvalid ??= LogonNameTextBox;
            }

            if (string.IsNullOrWhiteSpace(PositionTextBox.Text))
            {
                MarkInvalid(PositionTextBox);
                firstInvalid ??= PositionTextBox;
            }

            if (string.IsNullOrWhiteSpace(DepartmentComboBox.Text))
            {
                MarkInvalid(DepartmentComboBox);
                firstInvalid ??= DepartmentComboBox;
            }

            if (firstInvalid != null)
            {
                ValidationTextBlock.Text =
                    "Qırmızı işarələnmiş bütün məcburi sahələri doldurun.";
                ValidationTextBlock.Visibility = Visibility.Visible;
                firstInvalid.Focus();
                return false;
            }

            Worker.per_adiper_soyadi = FullNameTextBox.Text.Trim();
            Worker.per_kod = LogonNameTextBox.Text.Trim();
            Worker.pgk_gorev_adi = PositionTextBox.Text.Trim();
            Worker.pdp_adi = DepartmentComboBox.Text.Trim();

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
                FullNameTextBox,
                LogonNameTextBox,
                PositionTextBox,
                DepartmentComboBox
            })
            {
                control.ClearValue(Control.BorderBrushProperty);
                control.ClearValue(Control.BorderThicknessProperty);
            }
        }

        private void MarkInvalid(Control control)
        {
            control.BorderBrush =
                (Brush)FindResource("DangerBrush");
            control.BorderThickness = new Thickness(2);
        }

        private void FormTextChanged(
            object sender,
            TextChangedEventArgs e)
            => UpdateDirtyState();

        private void FormSelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
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
            => string.Join(
                "\u001F",
                new[]
                {
                    FullNameTextBox.Text ?? string.Empty,
                    LogonNameTextBox.Text ?? string.Empty,
                    PositionTextBox.Text ?? string.Empty,
                    DepartmentComboBox.Text ?? string.Empty
                });

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
                !string.IsNullOrWhiteSpace(FullNameTextBox.Text)
                    ? FullNameTextBox.Text.Trim()
                    : "Yeni işçi";

            var choice = DialogService.ConfirmUnsavedChanges(
                this,
                itemName);

            if (choice == UnsavedChangesChoice.Save)
            {
                Dispatcher.BeginInvoke(
                    new Action(() => TrySaveAndClose()));
                return;
            }

            if (choice == UnsavedChangesChoice.Discard)
            {
                _allowClose = true;
                Dispatcher.BeginInvoke(
                    new Action(Close));
            }
        }
    }
}
