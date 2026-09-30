using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class StatusBadgeControl : UserControl
    {
        public static readonly DependencyProperty StatusProperty =
            DependencyProperty.Register(
                nameof(Status),
                typeof(string),
                typeof(StatusBadgeControl),
                new PropertyMetadata(string.Empty, OnStatusChanged));

        public StatusBadgeControl()
        {
            InitializeComponent();
            Loaded += (_, _) => ApplyStatus();
        }

        public string Status
        {
            get => (string)GetValue(StatusProperty);
            set => SetValue(StatusProperty, value);
        }

        private static void OnStatusChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is StatusBadgeControl badge && badge.IsLoaded)
                badge.ApplyStatus();
        }

        private void ApplyStatus()
        {
            string normalized = Status?.Trim() ?? string.Empty;
            StatusTextBlock.Text =
                string.IsNullOrWhiteSpace(normalized)
                    ? "Təyin edilməyib"
                    : normalized;

            string surfaceKey;
            string borderKey;
            string textKey;

            switch (normalized.ToLowerInvariant())
            {
                case "istifadədədir":
                case "aktiv":
                    surfaceKey = "SuccessSurfaceBrush";
                    borderKey = "SuccessBorderBrush";
                    textKey = "SuccessTextBrush";
                    break;

                case "anbarda":
                    surfaceKey = "InfoSurfaceBrush";
                    borderKey = "InfoBorderBrush";
                    textKey = "InfoTextBrush";
                    break;

                case "təmirə göndərilib":
                case "təmir":
                case "servisdə":
                    surfaceKey = "WarningSurfaceBrush";
                    borderKey = "WarningBorderBrush";
                    textKey = "WarningTextBrush";
                    break;

                case "istifadəyə yararsız":
                case "qeyri-aktiv":
                case "qeyri-aktivdir":
                    surfaceKey = "DangerSurfaceBrush";
                    borderKey = "DangerBorderBrush";
                    textKey = "DangerTextBrush";
                    break;

                case "arxivdə":
                    surfaceKey = "SurfaceMutedBrush";
                    borderKey = "ControlBorderBrush";
                    textKey = "SubtleTextBrush";
                    break;

                default:
                    surfaceKey = "SurfaceMutedBrush";
                    borderKey = "ControlBorderBrush";
                    textKey = "BodyTextBrush";
                    break;
            }

            var surface = (Brush)FindResource(surfaceKey);
            var border = (Brush)FindResource(borderKey);
            var text = (Brush)FindResource(textKey);

            BadgeBorder.Background = surface;
            BadgeBorder.BorderBrush = border;
            StatusTextBlock.Foreground = text;
            StatusDot.Fill = text;
        }
    }
}
