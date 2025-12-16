using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ConnectionManager.LoadSettings();
            UpdateUiState();
        }

        private void UpdateUiState()
        {
            if (ConnectionManager.IsConfigured)
            {
                SetupView.Visibility = Visibility.Collapsed;
                LoginView.Visibility = Visibility.Visible;
                SubtitleText.Text = "Davam etmək üçün daxil olun";
                UsernameBox.Focus();
            }
            else
            {
                SetupView.Visibility = Visibility.Visible;
                LoginView.Visibility = Visibility.Collapsed;
                SubtitleText.Text = "Verilənlər bazasının ilkin quraşdırılması";
                ServerBox.Focus();
            }
        }

        private async void TestAndSave_Click(object sender, RoutedEventArgs e)
        {
            TestAndSaveButton.IsEnabled = false;
            TestAndSaveButton.Content = "Yoxlanılır...";
            ErrorMessage.Visibility = Visibility.Collapsed;

            var settings = new ConnectionSettings
            {
                ServerAddress = ServerBox.Text,
                SqlUsername = SqlUsernameBox.Text,
                SqlPassword = SqlPasswordBox.Password
            };

            bool isSuccess = await ConnectionManager.TestConnection(settings);

            if (isSuccess)
            {
                ConnectionManager.SaveSettings(settings);
                ErrorMessage.Text = "Qoşulma uğurlu oldu və yadda saxlanıldı!";
                ErrorMessage.Foreground = Brushes.Green;
                ErrorMessage.Visibility = Visibility.Visible;
                await Task.Delay(1000);
                UpdateUiState();
            }
            else
            {
                ErrorMessage.Text = "Qoşulma Baş Tutmadı. Server detallarını və məlumatları yoxlayın.";
                ErrorMessage.Foreground = Brushes.Red;
                ErrorMessage.Visibility = Visibility.Visible;
            }

            TestAndSaveButton.IsEnabled = true;
            TestAndSaveButton.Content = "Yoxla və Yadda Saxla";
        }

        private async void Login_Click(object _, RoutedEventArgs e)
        {
            LoginButton.IsEnabled = false;
            LoginButton.Content = "Daxil olunur...";
            ErrorMessage.Visibility = Visibility.Collapsed;

            string username = UsernameBox.Text;
            string password = PasswordBox.Password;
            bool loginSuccess = false;

            await Task.Run(() => { loginSuccess = SessionManager.Login(username, password); });

            if (loginSuccess)
            {
                await Task.Run(() => AppData.LoadAllData());
                await NavigationManager.GoToDashboard(this);
            }
            else
            {
                ErrorMessage.Text = "İstifadəçi adı və ya şifrə yanlışdır.";
                ErrorMessage.Foreground = Brushes.Red;
                ErrorMessage.Visibility = Visibility.Visible;
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Daxil Ol";
            }
        }

        private void ChangeServerSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Verilənlər bazası qoşulma ayarlarını dəyişmək istədiyinizə əminsinizmi?", "Dəyişikliyi Təsdiq Et", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                ConnectionManager.IsConfigured = false;
                UpdateUiState();
            }
        }

        private void PasswordBox_KeyDown(object _, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && LoginView.IsVisible)
            {
                Login_Click(null, null);
            }
        }
    }
}