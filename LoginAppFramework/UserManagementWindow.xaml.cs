using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class UserManagementWindow : Window
    {
        private List<AppUser> _allAppUsers;

        public UserManagementWindow()
        {
            InitializeComponent();
            LoadUsers();
            NewUserButton_Click(null, null);
        }

        private void LoadUsers()
        {
            _allAppUsers = AppServices.Users.GetAll();
            UsersListView.ItemsSource = _allAppUsers;
        }

        private void UsersListView_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (UsersListView.SelectedItem is not AppUser selectedUser)
                return;

            ClearValidation();

            DetailPanel.DataContext = selectedUser;
            UsernameTextBox.IsReadOnly = true;
            DeleteButton.IsEnabled = true;
            PasswordBox.Password = string.Empty;

            SetRoleComboBox(selectedUser.Role ?? "ReadOnly");
        }

        private void SetRoleComboBox(string role)
        {
            foreach (ComboBoxItem item in RoleComboBox.Items)
            {
                if (item.Tag?.ToString() == role)
                {
                    RoleComboBox.SelectedItem = item;
                    return;
                }
            }

            RoleComboBox.SelectedIndex = -1;
        }

        private void NewUserButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            ClearValidation();

            var newUser = new AppUser
            {
                Role = "ReadOnly"
            };

            DetailPanel.DataContext = newUser;
            UsersListView.SelectedItem = null;
            UsernameTextBox.IsReadOnly = false;
            PasswordBox.Password = string.Empty;
            DeleteButton.IsEnabled = false;

            SetRoleComboBox("ReadOnly");
            UsernameTextBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is not AppUser user)
                return;

            ClearValidation();

            if (string.IsNullOrWhiteSpace(UsernameTextBox.Text))
            {
                ShowValidation(
                    "İstifadəçi adı tələb olunur.",
                    UsernameTextBox);
                return;
            }

            if (string.IsNullOrWhiteSpace(FullNameTextBox.Text))
            {
                ShowValidation(
                    "Ad Soyad tələb olunur.",
                    FullNameTextBox);
                return;
            }

            if (RoleComboBox.SelectedItem is not ComboBoxItem selectedRoleItem)
            {
                ShowValidation(
                    "İstifadəçi rolu seçilməlidir.",
                    RoleComboBox);
                return;
            }

            if (user.Id == 0 &&
                string.IsNullOrWhiteSpace(PasswordBox.Password))
            {
                ShowValidation(
                    "Yeni istifadəçi üçün şifrə tələb olunur.",
                    PasswordBox);
                return;
            }

            user.Role = selectedRoleItem.Tag?.ToString();

            try
            {
                AppServices.Users.Save(user, PasswordBox.Password);
                NotificationService.Success(
                    this,
                    $"'{user.Username}' istifadəçisi yadda saxlanıldı.");

                LoadUsers();
                NewUserButton_Click(null, null);
            }
            catch (Exception ex)
            {
                ShowValidation(ex.Message);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersListView.SelectedItem is not AppUser userToDelete)
                return;

            var confirm = MessageBox.Show(
                $"'{userToDelete.Username}' istifadəçisini silmək istədiyinizə əminsiniz?\n\nBu əməliyyatı geri qaytarmaq mümkün deyil.",
                "Silməni Təsdiq Et",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                AppServices.Users.Delete(userToDelete);
                NotificationService.Success(
                    this,
                    "İstifadəçi silindi.");

                LoadUsers();
                NewUserButton_Click(null, null);
            }
            catch (Exception ex)
            {
                ShowValidation(ex.Message);
            }
        }

        private void ClearValidation()
        {
            ValidationTextBlock.Text = string.Empty;
            ValidationTextBlock.Visibility = Visibility.Collapsed;

            foreach (Control control in new Control[]
            {
                UsernameTextBox,
                FullNameTextBox,
                RoleComboBox,
                PasswordBox
            })
            {
                control.ClearValue(Control.BorderBrushProperty);
                control.ClearValue(Control.BorderThicknessProperty);
            }
        }

        private void ShowValidation(
            string message,
            Control control = null)
        {
            ValidationTextBlock.Text = message;
            ValidationTextBlock.Visibility = Visibility.Visible;

            if (control == null)
                return;

            control.BorderBrush =
                (Brush)FindResource("DangerBrush");
            control.BorderThickness = new Thickness(2);
            control.Focus();
        }
    }
}
