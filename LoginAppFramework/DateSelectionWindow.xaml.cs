using System;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace LoginAppFramework
{
    public partial class DateSelectionWindow : Window
    {
        private readonly WinForms.MonthCalendar _calendar;

        public DateTime? SelectedDate { get; private set; }

        public DateSelectionWindow(
            string title,
            DateTime? initialDate = null)
        {
            InitializeComponent();

            Title = title;
            TitleText.Text = title;

            var date = initialDate?.Date ?? DateTime.Today;

            _calendar = new WinForms.MonthCalendar
            {
                MaxSelectionCount = 1,
                SelectionStart = date,
                SelectionEnd = date,
                ShowToday = true,
                ShowTodayCircle = true,
                Dock = WinForms.DockStyle.Fill
            };

            CalendarHost.Child = _calendar;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedDate = _calendar.SelectionStart.Date;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
