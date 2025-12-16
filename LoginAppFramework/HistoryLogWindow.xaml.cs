using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LoginAppFramework
{
    public partial class HistoryLogWindow : Window
    {
        private List<HistoryLogViewModel> _allHistoryEntries;
        private List<HistoryLogViewModel> _filteredHistoryEntries;
        private bool isMenuOpen;

        private const int PageSize = 100;
        private int _currentPage = 1;
        private int _totalPages;

        public HistoryLogWindow() { InitializeComponent(); }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await Task.Delay(20);
            await Task.Run(() => LoadAllHistory());

            _filteredHistoryEntries = new List<HistoryLogViewModel>(_allHistoryEntries);

            PopulateFilters();
            UpdatePagedView();
            UpdateUserDisplay();
        }

        private void LoadAllHistory()
        {
            var assetLogs = DataAccess.GetAssetLogs();
            var assignmentHistory = DataAccess.GetAssignmentHistoryEntries();

            var assetLogViewModels = assetLogs.Select(log => new HistoryLogViewModel(log));

            var assignmentViewModels = assignmentHistory
                .Where(h => h.Asset != null)
                .Select(h => new HistoryLogViewModel(h, h.Asset.Name));

            _allHistoryEntries = assetLogViewModels
                .Concat(assignmentViewModels)
                .OrderByDescending(vm => vm.Timestamp)
                .ToList();
        }

        private void ApplyFilters()
        {
            if (_allHistoryEntries == null) return;

            IEnumerable<HistoryLogViewModel> filtered = _allHistoryEntries;

            DateTime? selectedDate = DateFilterCalendar.SelectedDate;
            if (selectedDate.HasValue)
            {
                filtered = filtered.Where(e => e.Timestamp.Date == selectedDate.Value.Date);
            }

            if (ActionFilterComboBox.SelectedIndex > 0 && ActionFilterComboBox.SelectedItem is string actionStr)
            {
                filtered = filtered.Where(entry =>
                    (actionStr == "Təhkimat" && entry.Status == "Təhkimat") ||
                    (actionStr != "Təhkimat" && entry.Status == actionStr)
                );
            }

            string searchText = SearchBox.Text;
            if (!string.IsNullOrWhiteSpace(searchText))
            {
                filtered = filtered.Where(entry =>
                    entry.Description?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false);
            }

            _filteredHistoryEntries = filtered.ToList();
            _currentPage = 1;
            UpdatePagedView();
        }

        private void UpdatePagedView()
        {
            if (_filteredHistoryEntries == null) return;

            _totalPages = (int)Math.Ceiling((double)_filteredHistoryEntries.Count / PageSize);
            if (_totalPages == 0) _totalPages = 1;
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            var pagedItems = _filteredHistoryEntries.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
            HistoryListView.ItemsSource = pagedItems;

            PageInfoTextBlock.Text = $"Səhifə {_currentPage} / {_totalPages} ({_filteredHistoryEntries.Count} qeyd)";
            PrevButton.IsEnabled = _currentPage > 1;
            NextButton.IsEnabled = _currentPage < _totalPages;
            GeneratePageButtons();
        }

        private void GeneratePageButtons()
        {
            PageNumbersPanel.Children.Clear();
            int startPage = Math.Max(1, _currentPage - 3);
            int endPage = Math.Min(_totalPages, _currentPage + 3);

            if (startPage > 1)
            {
                PageNumbersPanel.Children.Add(CreatePageButton(1));
                if (startPage > 2) PageNumbersPanel.Children.Add(new TextBlock { Text = "...", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) });
            }

            for (int i = startPage; i <= endPage; i++)
            {
                PageNumbersPanel.Children.Add(CreatePageButton(i));
            }

            if (endPage < _totalPages)
            {
                if (endPage < _totalPages - 1) PageNumbersPanel.Children.Add(new TextBlock { Text = "...", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(5, 0, 5, 0) });
                PageNumbersPanel.Children.Add(CreatePageButton(_totalPages));
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
            if (pageNumber == _currentPage)
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
                _currentPage = pageNumber;
                UpdatePagedView();
            }
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                UpdatePagedView();
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                UpdatePagedView();
            }
        }

        private void HistoryListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (HistoryListView.SelectedItem is HistoryLogViewModel vm && vm.Log != null && vm.Log.status == "Dəyişdirilən")
            {
                var detailWindow = new ChangeDetailWindow(vm.Log) { Owner = this };
                detailWindow.ShowDialog();

                if (detailWindow.LogWasDeleted)
                {
                    LoadAllHistory();
                    ApplyFilters();
                }
            }
        }

        private void PopulateFilters()
        {
            var actionTypes = new List<string> { "Bütün Əməliyyatlar", "Təhkimat", "Yaradılan", "Dəyişdirilən", "Silinən" };
            ActionFilterComboBox.ItemsSource = actionTypes;
            ActionFilterComboBox.SelectedIndex = 0;
        }

        private void Filters_Changed(object sender, RoutedEventArgs e) => ApplyFilters();

        private void ClearFiltersButton_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Clear();
            ActionFilterComboBox.SelectedIndex = 0;
            DateFilterCalendar.SelectedDate = null;
            DateFilterCalendar.DisplayDate = DateTime.Today;
            ApplyFilters();
        }

        private void CloseTheMenu() { isMenuOpen = false; MenuOverlay.Visibility = Visibility.Collapsed; (FindResource("CloseMenu") as Storyboard)?.Begin(); }
        private void MenuButton_Click(object sender, RoutedEventArgs e) { if (isMenuOpen) CloseTheMenu(); else { isMenuOpen = true; UserSwitchPopup.IsOpen = false; MenuOverlay.Visibility = Visibility.Visible; (FindResource("OpenMenu") as Storyboard)?.Begin(); } }
        private void CloseMenuButton_Click(object sender, RoutedEventArgs e) => CloseTheMenu();
        private void MenuOverlay_MouseDown(object sender, MouseButtonEventArgs e) => CloseTheMenu();
        private async void DashboardButton_Click(object sender, RoutedEventArgs e) => await NavigationManager.GoToDashboard();
        private async void UsersButton_Click(object sender, RoutedEventArgs e) => await NavigationManager.GoToWorkerListWindow();
        private async void AssetsButton_Click(object sender, RoutedEventArgs e) => await NavigationManager.GoToAssetWindow();
        private async void LifecycleReportButton_Click(object sender, RoutedEventArgs e) => await NavigationManager.GoToLifecycleReportWindow();
        private void UpdateUserDisplay() { if (SessionManager.CurrentUser != null) { UserProfileIcon.Text = SessionManager.CurrentUser.ProfilePicture; UserProfileName.Text = SessionManager.CurrentUser.FullName; } }
        private void UserProfileButton_Click(object sender, RoutedEventArgs e) => UserSwitchPopup.IsOpen = true;
        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            NavigationManager.GoToReportsWindow();
        }
        private void ReturnToLogin() { NavigationManager.RestartApplication(); }
        private void SwitchUserButton_Click(object sender, RoutedEventArgs e) => ReturnToLogin();
        private void LogoutButton_Click(object sender, RoutedEventArgs e) => ReturnToLogin();
    }
}