using System;
using System.Windows;
using System.Windows.Controls;

namespace LoginAppFramework
{
    public partial class DateSelectorControl : UserControl
    {
        public static readonly DependencyProperty SelectedDateProperty =
            DependencyProperty.Register(
                nameof(SelectedDate),
                typeof(DateTime?),
                typeof(DateSelectorControl),
                new FrameworkPropertyMetadata(
                    null,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnSelectedDateChanged));

        public static readonly DependencyProperty DialogTitleProperty =
            DependencyProperty.Register(
                nameof(DialogTitle),
                typeof(string),
                typeof(DateSelectorControl),
                new PropertyMetadata("Tarix seçin"));

        public static readonly DependencyProperty AllowClearProperty =
            DependencyProperty.Register(
                nameof(AllowClear),
                typeof(bool),
                typeof(DateSelectorControl),
                new PropertyMetadata(true, OnSelectedDateChanged));

        public DateTime? SelectedDate
        {
            get => (DateTime?)GetValue(SelectedDateProperty);
            set => SetValue(SelectedDateProperty, value);
        }

        public string DialogTitle
        {
            get => (string)GetValue(DialogTitleProperty);
            set => SetValue(DialogTitleProperty, value);
        }

        public bool AllowClear
        {
            get => (bool)GetValue(AllowClearProperty);
            set => SetValue(AllowClearProperty, value);
        }

        public DateSelectorControl()
        {
            InitializeComponent();
            UpdateVisualState();
        }

        private static void OnSelectedDateChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is DateSelectorControl control)
                control.UpdateVisualState();
        }

        private void CalendarButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new DateSelectionWindow(
                string.IsNullOrWhiteSpace(DialogTitle) ? "Tarix seçin" : DialogTitle,
                SelectedDate)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() == true && dialog.SelectedDate.HasValue)
                SelectedDate = dialog.SelectedDate.Value.Date;
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            if (AllowClear)
                SelectedDate = null;
        }

        private void UpdateVisualState()
        {
            if (DateTextBox == null || ClearButton == null) return;

            DateTextBox.Text = SelectedDate?.ToString("dd.MM.yyyy") ?? string.Empty;
            ClearButton.Visibility =
                AllowClear && SelectedDate.HasValue
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }
    }
}
