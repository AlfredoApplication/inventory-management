using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace LoginAppFramework
{
    public enum ToastType { Success, Error, Warning, Info }

    public partial class ToastNotificationControl : UserControl
    {
        private bool _isShowing = false;

        public ToastNotificationControl() { InitializeComponent(); }

        public async void Show(string message, ToastType type = ToastType.Success, string title = null)
        {
            // Set visual style based on type
            switch (type)
            {
                case ToastType.Success:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(22, 160, 133)); // Teal
                    IconVisual.Icon = AppIconKind.Success;
                    TitleText.Text = title ?? "Uğurlu!";
                    break;
                case ToastType.Error:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(192, 57, 43)); // Red
                    IconVisual.Icon = AppIconKind.Error;
                    TitleText.Text = title ?? "Xəta!";
                    break;
                case ToastType.Warning:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(211, 84, 0)); // Orange
                    IconVisual.Icon = AppIconKind.Warning;
                    TitleText.Text = title ?? "Diqqət!";
                    break;
                case ToastType.Info:
                    ToastBorder.Background = new SolidColorBrush(Color.FromRgb(41, 128, 185)); // Blue
                    IconVisual.Icon = AppIconKind.Info;
                    TitleText.Text = title ?? "Məlumat";
                    break;
            }

            MessageText.Text = message;

            if (_isShowing) return;
            _isShowing = true;

            this.Visibility = Visibility.Visible;

            // Slide up + fade in
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var slideIn = new DoubleAnimation(60, 0, TimeSpan.FromSeconds(0.3)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            this.BeginAnimation(OpacityProperty, fadeIn);
            SlideTransform.BeginAnimation(TranslateTransform.YProperty, slideIn);

            await Task.Delay(3500);

            // Slide down + fade out
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.4));
            var slideOut = new DoubleAnimation(0, 60, TimeSpan.FromSeconds(0.4)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
            fadeOut.Completed += (s, e) =>
            {
                this.Visibility = Visibility.Collapsed;
                this.BeginAnimation(OpacityProperty, null);
                SlideTransform.BeginAnimation(TranslateTransform.YProperty, null);
                SlideTransform.Y = 60;
                _isShowing = false;
            };
            this.BeginAnimation(OpacityProperty, fadeOut);
            SlideTransform.BeginAnimation(TranslateTransform.YProperty, slideOut);
        }

        // Convenience overload for backward compatibility
        public void Show(string message, bool isError)
        {
            Show(message, isError ? ToastType.Error : ToastType.Success);
        }
    }
}