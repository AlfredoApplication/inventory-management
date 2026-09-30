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
    public partial class HistoryLogWindow : Window, INavigationRefreshable
    {
        private readonly HistoryLogWindowViewModel _viewModel;

        public HistoryLogWindow()
        {
            InitializeComponent();

            _viewModel = new HistoryLogWindowViewModel();
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DataContext = _viewModel;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadingOverlay.Visibility = Visibility.Visible;
            await Task.Delay(20);

            try
            {
                await _viewModel.LoadAsync();
                GeneratePageButtons();
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Yükləmə Xətası",
                    $"Tarixçə yüklənərkən xəta baş verdi:\n\n{ex.Message}");
            }
            finally
            {
                LoadingOverlay.Visibility = Visibility.Collapsed;
            }
        }

        public async Task RefreshForNavigationAsync()
        {
            await _viewModel.LoadAsync();
            GeneratePageButtons();
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

            bool confirm = DialogService.Confirm(
                this,
                "Bərpanı Təsdiq Et",
                $"{toRestore.Count} vəsaiti bərpa etmək istəyirsiniz?\n\n" +
                string.Join("\n", toRestore.Select(v => $"  • {v.Log.VesaitinAdi} ({v.Log.VesaitinKodu})")),
                "Bərpa et",
                "Ləğv et");

            if (!confirm) return;

            RestoreSelectedButton.SetCurrentValue(UIElement.IsEnabledProperty, false);

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

                if (result.FailureCount > 0)
                    NotificationService.Warning(this, message, title: "Bərpa Nəticəsi");
                else
                    NotificationService.Success(this, message, title: "Bərpa Nəticəsi");
            }
            finally
            {
                SelectAllDeletedCheckBox.IsChecked = false;
                RestoreSelectedButton.SetCurrentValue(
                    UIElement.IsEnabledProperty,
                    _viewModel.CanRestoreSelected);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool control =
                (Keyboard.Modifiers & ModifierKeys.Control) != 0;

            if (control && e.Key == Key.K)
            {
                NavigationManager.GoToGlobalSearch(this);
                e.Handled = true;
                return;
            }

            if (control && e.Key == Key.F)
            {
                SearchBox.Focus();
                SearchBox.SelectAll();
                e.Handled = true;
            }
        }

        private void MenuButton_Click(object sender, RoutedEventArgs e)
            => SharedNavigationMenu.Open();
    }
}
