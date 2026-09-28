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
            NewUserButton_Click(null, null); // Start in "New User" mode
        }

        private void LoadUsers()
        {
            // Use the powerful connection string to manage users
            _allAppUsers = DataAccess.GetAllAppUsers();
            UsersListView.ItemsSource = _allAppUsers.OrderBy(u => u.Username);
        }

        private void UsersListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // When a user is selected from the list, show their details
            if (UsersListView.SelectedItem is AppUser selectedUser)
            {
                DetailPanel.DataContext = selectedUser;
                UsernameTextBox.IsReadOnly = true; // Cannot change username after creation
                DeleteButton.IsEnabled = true;

                // Set the role combo box
                SetRoleComboBox(selectedUser.Role ?? "Admin");
            }
        }

        private void SetRoleComboBox(string role)
        {
            foreach (ComboBoxItem item in RoleComboBox.Items)
            {
                if (item.Tag?.ToString() == role)
                {
                    RoleComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void NewUserButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear the form to prepare for a new user
            var newUser = new AppUser { Role = "Admin" }; // Default to Admin
            DetailPanel.DataContext = newUser;
            UsersListView.SelectedItem = null;
            UsernameTextBox.IsReadOnly = false;
            PasswordBox.Password = string.Empty;
            DeleteButton.IsEnabled = false;
            SetRoleComboBox("Admin"); // Set default role
            UsernameTextBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is not AppUser user) return;

            // Validation
            if (string.IsNullOrWhiteSpace(user.Username))
            {
                MessageBox.Show("İstifadəçi adı tələb olunur.", "Doğrulama Xətası");
                return;
            }

            // Get selected role from ComboBox
            if (RoleComboBox.SelectedItem is ComboBoxItem selectedRoleItem)
            {
                user.Role = selectedRoleItem.Tag?.ToString();
            }

            // Validate role is selected
            if (string.IsNullOrWhiteSpace(user.Role))
            {
                MessageBox.Show("Rol seçilməlidir.", "Doğrulama Xətası");
                return;
            }

            // Ensure at least one Admin exists
            if (user.Role != "Admin")
            {
                var allUsers = DataAccess.GetAllAppUsers();
                var otherAdmins = allUsers.Where(u => u.Id != user.Id && u.Role == "Admin").ToList();
                if (otherAdmins.Count == 0)
                {
                    MessageBox.Show("Sistem ən azı bir Admin istifadəçisi tələb edir. Bu istifadəçinin rolunu dəyişməzdən əvvəl başqa bir Admin yaradın.",
                        "Əməliyyat Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            string newPassword = PasswordBox.Password;
            if (user.Id == 0 && string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show("Yeni istifadəçi üçün şifrə tələb olunur.", "Doğrulama Xətası");
                return;
            }

            try
            {
                // If a new password was typed, hash it.
                if (!string.IsNullOrWhiteSpace(newPassword))
                {
                    user.PasswordHash = PasswordHasher.HashPassword(newPassword);
                }

                // Save the changes (either a new user or updated details/password hash)
                DataAccess.SaveAppUser(user);

                MessageBox.Show($"İstifadəçi '{user.Username}' uğurla saxlanıldı.", "Uğur");

                // Reload the list and clear the form for the next operation
                LoadUsers();
                NewUserButton_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"İstifadəçi saxlanarkən xəta: {ex.Message}", "Verilənlər Bazası Xətası", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersListView.SelectedItem is AppUser userToDelete)
            {
                // Prevent deleting the last admin
                if (userToDelete.Role == "Admin")
                {
                    var allUsers = DataAccess.GetAllAppUsers();
                    var adminCount = allUsers.Count(u => u.Role == "Admin");
                    if (adminCount <= 1)
                    {
                        MessageBox.Show("Sistem ən azı bir Admin istifadəçisi tələb edir. Son Admin istifadəçisini silə bilməzsiniz.",
                            "Əməliyyat Qadağandır", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                if (MessageBox.Show($"'{userToDelete.Username}' istifadəçisini silmək istədiyinizə əminsiniz?\n\nBu əməliyyatı geri ala bilməzsiniz.",
                    "Silinməni Təsdiq et", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        DataAccess.DeleteAppUser(userToDelete);
                        MessageBox.Show("İstifadəçi uğurla silindi.", "Uğur");
                        LoadUsers();
                        NewUserButton_Click(null, null);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"İstifadəçi silinərkən xəta: {ex.Message}", "Verilənlər Bazası Xətası");
                    }
                }
            }
        }
    }
}