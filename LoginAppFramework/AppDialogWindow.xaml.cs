using System.Windows;
using System.Windows.Media;

namespace LoginAppFramework
{
    public enum AppDialogAction
    {
        None,
        Primary,
        Secondary,
        Tertiary
    }

    public partial class AppDialogWindow : Window
    {
        public bool Confirmed { get; private set; }
        public AppDialogAction SelectedAction { get; private set; }

        public AppDialogWindow(
            Window owner,
            string title,
            string message,
            AppDialogType type,
            bool showCancel,
            string primaryText,
            string secondaryText,
            bool destructive,
            bool showTertiary = false,
            string tertiaryText = null)
        {
            InitializeComponent();

            if (owner != null)
                Owner = owner;

            Title = title ?? "Bildiriş";
            TitleTextBlock.Text = title ?? "Bildiriş";
            MessageTextBlock.Text = message ?? string.Empty;
            PrimaryButton.Content = string.IsNullOrWhiteSpace(primaryText)
                ? "OK"
                : primaryText;
            SecondaryButton.Content = string.IsNullOrWhiteSpace(secondaryText)
                ? "Ləğv et"
                : secondaryText;
            SecondaryButton.Visibility =
                showCancel ? Visibility.Visible : Visibility.Collapsed;

            TertiaryButton.Content = string.IsNullOrWhiteSpace(tertiaryText)
                ? "Geri qayıt"
                : tertiaryText;
            TertiaryButton.Visibility =
                showTertiary ? Visibility.Visible : Visibility.Collapsed;

            SecondaryButton.IsCancel = !showTertiary;
            TertiaryButton.IsCancel = showTertiary;

            ApplyType(type);

            if (destructive)
                PrimaryButton.Style = (Style)FindResource("DangerActionButton");
        }

        private void ApplyType(AppDialogType type)
        {
            string surfaceKey;
            string borderKey;
            string textKey;

            switch (type)
            {
                case AppDialogType.Success:
                    IconVisual.Icon = AppIconKind.Success;
                    surfaceKey = "SuccessSurfaceBrush";
                    borderKey = "SuccessBorderBrush";
                    textKey = "SuccessTextBrush";
                    break;
                case AppDialogType.Warning:
                    IconVisual.Icon = AppIconKind.Warning;
                    surfaceKey = "WarningSurfaceBrush";
                    borderKey = "WarningBorderBrush";
                    textKey = "WarningTextBrush";
                    break;
                case AppDialogType.Error:
                    IconVisual.Icon = AppIconKind.Error;
                    surfaceKey = "DangerSurfaceBrush";
                    borderKey = "DangerBorderBrush";
                    textKey = "DangerTextBrush";
                    break;
                case AppDialogType.Question:
                    IconVisual.Icon = AppIconKind.Info;
                    surfaceKey = "InfoSurfaceBrush";
                    borderKey = "InfoBorderBrush";
                    textKey = "InfoTextBrush";
                    break;
                default:
                    IconVisual.Icon = AppIconKind.Info;
                    surfaceKey = "InfoSurfaceBrush";
                    borderKey = "InfoBorderBrush";
                    textKey = "InfoTextBrush";
                    break;
            }

            IconContainer.Background = (Brush)FindResource(surfaceKey);
            IconContainer.BorderBrush = (Brush)FindResource(borderKey);
            IconVisual.Foreground = (Brush)FindResource(textKey);
        }

        private void PrimaryButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = true;
            SelectedAction = AppDialogAction.Primary;
            DialogResult = true;
            Close();
        }

        private void SecondaryButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            SelectedAction = AppDialogAction.Secondary;
            DialogResult = false;
            Close();
        }

        private void TertiaryButton_Click(object sender, RoutedEventArgs e)
        {
            Confirmed = false;
            SelectedAction = AppDialogAction.Tertiary;
            DialogResult = false;
            Close();
        }
    }
}