using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LoginAppFramework
{
    public partial class HistoryLogWindow : Window
    {
        private readonly HistoryLogWindowViewModel _viewModel;
        private bool isMenuOpen;

        public HistoryLogWindow()
        {
            InitializeComponent();

            _viewModel = new HistoryLogWindowViewModel();
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(20);
            await _viewModel.LoadAsync();

            GeneratePageButtons();
            UpdateUserDisplay();
        }

        private void ViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HistoryLogWindowViewModel.CurrentPage) ||
                e.PropertyName == nameof(HistoryLogWindowViewModel.TotalPages) ||
                e.PropertyName == nameof(HistoryLogWindowViewModel.FilteredCount))
            {
                GeneratePageButtons();
            }
        }

        private void GeneratePageButtons()
        {
            if (PageNumbersPanel == null) return;

            PageNumbersPanel.Children.Clear();

            int startPage = Math.Max(1, _viewModel.CurrentPage - 3);
            int endPage = Math.Min(_viewModel.TotalPages, _viewModel.CurrentPage + 3);

            if (startPage > 1)
            {
                PageNumbersPanel.Children.Add(CreatePageButton(1));

                if (startPage > 2)
                {
                    PageNumbersPanel.Children.Add(new TextBlock
                    {
                        Text = "...",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 5, 0)
                    });
                }
            }

            for (int page = startPage; page <= endPage; page++)
            {
                PageNumbersPanel.Children.Add(CreatePageButton(page));
            }

            if (endPage < _viewModel.TotalPages)
            {
                if (endPage < _viewModel.TotalPages - 1)
                {
                    PageNumbersPanel.Children.Add(new TextBlock
                    {
                        Text = "...",
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(5, 0, 5, 0)
                    });
                }

                PageNumbersPanel.Children.Add(CreatePageButton(_viewModel.TotalPages));
            }
        }

        private Button CreatePageButton(int pageNumber)
        {
            var button = new Button
            {
                Content = pageNumber.ToString(),
                Style = (Style)FindResource("PageButtonStyle"),
                Tag = pageNumber
            };

            if (pageNumber == _viewModel.CurrentPage)
            {
                button.Background = (Brush)FindResource("PrimaryBrush");
                button.Foreground = Brushes.White;
                button.IsEnabled = false;
            }

            button.Click += PageButton_Click;
            return button;
        }

        private void PageButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: int pageNumber })
            {
                _viewModel.GoToPage(pageNumber);
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
            => _viewModel.GoToPreviousPage();

        private void NextButton_Click(object sender, RoutedEventArgs e)
            => _viewModel.GoToNextPage();

        private void HistoryListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (HistoryListView.SelectedItem is HistoryLogViewModel vm &&
                vm.Log != null &&
                vm.Log.status == "Dəyişdirilən")
            {
                var detailWindow = new ChangeDetailWindow(vm.Log) { Owner = this };
                detailWindow.ShowDialog();
            }
        }

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ClearFilters();
            DateFilterCalendar.DisplayDate = DateTime.Today;
        }

        private void SelectAllDeletedCheckBox_Checked(object sender, RoutedEventArgs e)
            => _viewModel.SelectAllRestorable(true);

        private void SelectAllDeletedCheckBox_Unchecked(object sender, RoutedEventArgs e)
            => _viewModel.SelectAllRestorable(false);

        private async void RestoreSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            var toRestore = _viewModel.GetSelectedRestoreEntries();
            if (toRestore.Count == 0) return;

            var confirm = MessageBox.Show(
                $"{toRestore.Count} vəsaiti bərpa etmək istəyirsiniz?\n\n" +
                string.Join("\n", toRestore.Select(v => $"  • {v.Log.VesaitinAdi} ({v.Log.VesaitinKodu})")),
                "Bərpanı Təsdiq Et",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            RestoreSelectedButton.IsEnabled = false;

            try
            {
                var result = await _viewModel.RestoreSelectedAsync();

                string message = $"{result.SuccessCount} vəsait uğurla bərpa edildi.";
                if (result.FailureCount > 0)
                {
                    message += $"\n{result.FailureCount} vəsait bərpa edilə bilmədi.";
                    if (result.Failures.Count > 0)
                    {
                        message += $"\n\n{string.Join("\n", result.Failures.Take(10))}";
                    }
                }

                MessageBox.Show(
                    message,
                    "Bərpa Nəticəsi",
                    MessageBoxButton.OK,
                    result.SuccessCount > 0 ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            finally
            {
                SelectAllDeletedCheckBox.IsChecked = false;
            }
        }

        private void CloseTheMenu()
        {
            isMenuOpen = false;
            MenuOverlay.Visibility = Visibility.Collapsed;
            (FindResource("CloseMenu") as Storyboard)?.Begin();
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (isMenuOpen)
            {
                CloseTheMenu();
                return;
            }

            isMenuOpen = true;
            UserSwitchPopup.IsOpen = false;
            MenuOverlay.Visibility = Visibility.Visible;
            (FindResource("OpenMenu") as Storyboard)?.Begin();
        }

        private void CloseMenuButton_Click(object sender, RoutedEventArgs e) => CloseTheMenu();
        private void MenuOverlay_MouseDown(object sender, MouseButtonEventArgs e) => CloseTheMenu();

        private async void DashboardButton_Click(object sender, RoutedEventArgs e)
            => await NavigationManager.GoToDashboard();

        private async void UsersButton_Click(object sender, RoutedEventArgs e)
            => await NavigationManager.GoToWorkerListWindow();

        private async void AssetsButton_Click(object sender, RoutedEventArgs e)
            => await NavigationManager.GoToAssetWindow();

        private async void LifecycleReportButton_Click(object sender, RoutedEventArgs e)
            => await NavigationManager.GoToLifecycleReportWindow();

        private void UpdateUserDisplay()
        {
            if (SessionManager.CurrentUser == null) return;

            UserProfileIcon.Text = SessionManager.CurrentUser.ProfilePicture;
            UserProfileName.Text = SessionManager.CurrentUser.FullName;
        }

        private void UserProfileButton_Click(object sender, RoutedEventArgs e)
            => UserSwitchPopup.IsOpen = true;

        private void ReturnToLogin() => NavigationManager.RestartApplication();
        private void SwitchUserButton_Click(object sender, RoutedEventArgs e) => ReturnToLogin();
        private void LogoutButton_Click(object sender, RoutedEventArgs e) => ReturnToLogin();
    }
}
