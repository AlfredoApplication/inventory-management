using System.Windows;
using System.Windows.Controls;

namespace LoginAppFramework
{
    public partial class OperationProgressOverlay : UserControl
    {
        public OperationProgressOverlay()
        {
            InitializeComponent();
            Visibility = Visibility.Collapsed;
        }

        public void Show(string title, string detail = null)
        {
            TitleTextBlock.Text = title ?? "Əməliyyat icra olunur...";
            DetailTextBlock.Text = detail ?? string.Empty;
            CounterTextBlock.Text = string.Empty;
            PercentTextBlock.Text = string.Empty;
            ProgressBar.IsIndeterminate = true;
            ProgressBar.Value = 0;
            Visibility = Visibility.Visible;
        }

        public void Report(OperationProgressInfo progress)
        {
            if (progress == null)
                return;

            Visibility = Visibility.Visible;
            ProgressBar.IsIndeterminate = progress.Total <= 0;

            if (progress.Total > 0)
            {
                ProgressBar.Value = progress.Percent;
                CounterTextBlock.Text = $"{progress.Current} / {progress.Total}";
                PercentTextBlock.Text = $"{progress.Percent:0}%";
            }
            else
            {
                CounterTextBlock.Text = string.Empty;
                PercentTextBlock.Text = string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(progress.Message))
                TitleTextBlock.Text = progress.Message;

            DetailTextBlock.Text = progress.Detail ?? string.Empty;
        }

        public void Hide()
        {
            Visibility = Visibility.Collapsed;
            ProgressBar.IsIndeterminate = false;
            ProgressBar.Value = 0;
        }
    }
}
