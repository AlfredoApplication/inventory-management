using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

        private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (UsersListView.SelectedItem is not AppUser selectedUser)
                return;

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

        private void NewUserButton_Click(object sender, RoutedEventArgs e)
        {
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

            if (RoleComboBox.SelectedItem is ComboBoxItem selectedRoleItem)
            {
                user.Role = selectedRoleItem.Tag?.ToString();
            }

            try
            {
                AppServices.Users.Save(user, PasswordBox.Password);

                MessageBox.Show(
                    $"İstifadəçi '{user.Username}' uğurla saxlanıldı.",
                    "Uğur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                LoadUsers();
                NewUserButton_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "İstifadəçi Saxlanılmadı",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersListView.SelectedItem is not AppUser userToDelete)
                return;

            var confirm = MessageBox.Show(
                $"'{userToDelete.Username}' istifadəçisini silmək istədiyinizə əminsiniz?\n\nBu əməliyyatı geri ala bilməzsiniz.",
                "Silinməni Təsdiq et",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes)
                return;

            try
            {
                AppServices.Users.Delete(userToDelete);

                MessageBox.Show(
                    "İstifadəçi uğurla silindi.",
                    "Uğur",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                LoadUsers();
                NewUserButton_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "İstifadəçi Silinmədi",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
