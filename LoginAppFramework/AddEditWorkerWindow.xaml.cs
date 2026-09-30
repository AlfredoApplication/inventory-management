using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class AddEditWorkerWindow : Window
    {
        public Worker Worker { get; private set; }

        public AddEditWorkerWindow(Worker workerToEdit = null)
        {
            InitializeComponent();
            Worker = workerToEdit?.Clone() ?? new Worker();
            DataContext = Worker;
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
        }

        private void SaveButton_Click(object _, RoutedEventArgs e)
        {
            ClearValidation();

            Control firstInvalid = null;

            if (string.IsNullOrWhiteSpace(Worker.per_adiper_soyadi))
            {
                MarkInvalid(FullNameTextBox);
                firstInvalid ??= FullNameTextBox;
            }

            if (string.IsNullOrWhiteSpace(Worker.per_kod))
            {
                MarkInvalid(LogonNameTextBox);
                firstInvalid ??= LogonNameTextBox;
            }

            if (string.IsNullOrWhiteSpace(Worker.pgk_gorev_adi))
            {
                MarkInvalid(PositionTextBox);
                firstInvalid ??= PositionTextBox;
            }

            if (string.IsNullOrWhiteSpace(Worker.pdp_adi))
            {
                MarkInvalid(DepartmentComboBox);
                firstInvalid ??= DepartmentComboBox;
            }

            if (firstInvalid != null)
            {
                ValidationTextBlock.Text =
                    "Qırmızı işarələnmiş bütün sahələri doldurun.";
                ValidationTextBlock.Visibility = Visibility.Visible;
                firstInvalid.Focus();
                return;
            }

            DialogResult = true;
            Close();
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
    }
}
