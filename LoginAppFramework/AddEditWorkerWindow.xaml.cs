using System.Windows;
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
                MessageBox.Show("Bu əməliyyat üçün icazəniz yoxdur.", "Giriş Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                Close();
                return;
            }

            DepartmentComboBox.ItemsSource = AppData.GetWorkerDepartments();
            if (Worker.Id > 0)
            {
                LogonNameTextBox.IsEnabled = false;
            }
        }
        private void SaveButton_Click(object _, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(Worker.per_adiper_soyadi) ||
                string.IsNullOrWhiteSpace(Worker.pgk_gorev_adi) ||
                string.IsNullOrWhiteSpace(Worker.pdp_adi) ||
                string.IsNullOrWhiteSpace(Worker.per_kod))
            {
                MessageBox.Show("All fields are required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            DialogResult = true;
            Close();
        }
    }
}