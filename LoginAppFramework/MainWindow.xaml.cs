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
            string serverAddress = ServerBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(serverAddress))
            {
                ShowError("SQL Server ünvanını daxil edin.");
                ServerBox.Focus();
                return;
            }

            TestAndSaveButton.IsEnabled = false;
            TestAndSaveButton.Content = "Yoxlanılır...";
            ErrorMessage.Visibility = Visibility.Collapsed;

            try
            {
                var settings = new ConnectionSettings
                {
                    ServerAddress = serverAddress,
                    SqlUsername = SqlUsernameBox.Text?.Trim(),
                    SqlPassword = SqlPasswordBox.Password
                };

                bool isSuccess = await ConnectionManager.TestConnection(settings);

                if (isSuccess)
                {
                    ConnectionManager.SaveSettings(settings);
                    ErrorMessage.Text = "Qoşulma uğurlu oldu və yadda saxlanıldı.";
                    ErrorMessage.Foreground = (Brush)FindResource("SuccessBrush");
                    ErrorMessage.Visibility = Visibility.Visible;
                    UpdateUiState();
                }
                else
                {
                    ShowError("Qoşulma baş tutmadı. Server ünvanını və giriş məlumatlarını yoxlayın.");
                }
            }
            catch (Exception ex)
            {
                ShowError($"Qoşulma yoxlanarkən xəta baş verdi: {ex.Message}");
            }
            finally
            {
                TestAndSaveButton.IsEnabled = true;
                TestAndSaveButton.Content = "Yoxla və Yadda Saxla";
            }
        }

        private async void Login_Click(object _, RoutedEventArgs e)
        {
            string username = UsernameBox.Text?.Trim();
            string password = PasswordBox.Password;

            if (string.IsNullOrWhiteSpace(username))
            {
                ShowError("İstifadəçi adını daxil edin.");
                UsernameBox.Focus();
                return;
            }

            LoginButton.IsEnabled = false;
            LoginButton.Content = "Daxil olunur...";
            ErrorMessage.Visibility = Visibility.Collapsed;

            try
            {
                LoginResult loginResult = await Task.Run(
                    () => SessionManager.Login(username, password));

                if (!loginResult.Success)
                {
                    ShowError(loginResult.Message);
                    PasswordBox.SelectAll();
                    PasswordBox.Focus();
                    return;
                }

                await Task.Run(() => AppData.LoadAllData());
                await NavigationManager.GoToDashboard(this);
            }
            catch (Exception ex)
            {
                ShowError($"Giriş zamanı xəta baş verdi: {ex.Message}");
            }
            finally
            {
                LoginButton.IsEnabled = true;
                LoginButton.Content = "Daxil Ol";
            }
        }

        private void ChangeServerSettings_Click(object sender, RoutedEventArgs e)
        {
            bool result = DialogService.Confirm(
                this,
                "Dəyişikliyi Təsdiq Et",
                "Verilənlər bazası qoşulma ayarlarını dəyişmək istədiyinizə əminsinizmi?",
                "Dəyiş",
                "Ləğv et");
            if (result)
            {
                ConnectionManager.BeginReconfiguration();
                UpdateUiState();
            }
        }

        private void ShowError(string message)
        {
            ErrorMessage.Text = message;
            ErrorMessage.Foreground = (Brush)FindResource("DangerBrush");
            ErrorMessage.Visibility = Visibility.Visible;
        }

        private void PasswordBox_KeyDown(object _, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && LoginView.IsVisible)
            {
                Login_Click(null, null);
                e.Handled = true;
            }
        }

        private void SqlPasswordBox_KeyDown(object _, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && SetupView.IsVisible)
            {
                TestAndSave_Click(null, null);
                e.Handled = true;
            }
        }
    }
}