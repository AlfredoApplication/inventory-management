using Microsoft.Win32;
using System;
using System.Windows;

namespace LoginAppFramework
{
    public partial class CrashReportWindow : Window
    {
        private readonly Exception _exception;
        private readonly string _context;

        public bool RestartRequested { get; private set; }

        public CrashReportWindow(
            Exception exception,
            string context)
        {
            InitializeComponent();

            _exception = exception;
            _context = context;

            ExceptionSummaryTextBox.Text =
                exception == null
                    ? "Naməlum xəta."
                    : $"{exception.GetType().Name}: {exception.Message}";
        }

        private void ExportDiagnosticsButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "ZIP arxiv|*.zip",
                Title = "Diaqnostika paketini yadda saxla",
                FileName =
                    $"Inventory_Crash_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
            };

            if (dialog.ShowDialog(this) != true)
                return;

            try
            {
                DiagnosticService.ExportPackage(
                    dialog.FileName,
                    _exception,
                    _context);

                DialogService.Info(
                    this,
                    "Diaqnostika",
                    "Diaqnostika paketi yaradıldı.");
            }
            catch (Exception ex)
            {
                DialogService.Error(
                    this,
                    "Diaqnostika Xətası",
                    $"Paket yaradıla bilmədi: {ex.Message}");
            }
        }

        private void RestartButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RestartRequested = true;
            DialogResult = true;
            Close();
        }

        private void CloseApplicationButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RestartRequested = false;
            DialogResult = false;
            Close();
        }
    }
}
