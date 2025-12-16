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
            }
        }

        private void NewUserButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear the form to prepare for a new user
            var newUser = new AppUser();
            DetailPanel.DataContext = newUser;
            UsersListView.SelectedItem = null;
            UsernameTextBox.IsReadOnly = false;
            PasswordBox.Password = string.Empty;
            DeleteButton.IsEnabled = false;
            UsernameTextBox.Focus();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (DetailPanel.DataContext is not AppUser user) return;

            // Validation
            if (string.IsNullOrWhiteSpace(user.Username))
            {
                MessageBox.Show("Username is required.", "Validation Error");
                return;
            }

            string newPassword = PasswordBox.Password;
            if (user.Id == 0 && string.IsNullOrWhiteSpace(newPassword))
            {
                MessageBox.Show("Password is required for a new user.", "Validation Error");
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

                MessageBox.Show($"User '{user.Username}' saved successfully.", "Success");

                // Reload the list and clear the form for the next operation
                LoadUsers();
                NewUserButton_Click(null, null);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving user: {ex.Message}", "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            if (UsersListView.SelectedItem is AppUser userToDelete)
            {
                if (MessageBox.Show($"Are you sure you want to delete the user '{userToDelete.Username}'?\n\nThis cannot be undone.",
                    "Confirm Deletion", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
                {
                    try
                    {
                        DataAccess.DeleteAppUser(userToDelete);
                        MessageBox.Show("User deleted successfully.", "Success");
                        LoadUsers();
                        NewUserButton_Click(null, null);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Error deleting user: {ex.Message}", "Database Error");
                    }
                }
            }
        }
    }
}