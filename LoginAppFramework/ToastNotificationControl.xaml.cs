using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
namespace LoginAppFramework
{
    public partial class ToastNotificationControl : UserControl
    {
        public ToastNotificationControl() { InitializeComponent(); }
        public async void Show(string message, bool isError = false)
        {
            MessageText.Text = message;
            if (isError) { ToastBorder.Background = new SolidColorBrush(Colors.IndianRed); IconText.Text = "❌"; }
            else { ToastBorder.Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)); IconText.Text = "✅"; }
            this.Visibility = Visibility.Visible;
            var fadeInAnimation = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.3));
            this.BeginAnimation(OpacityProperty, fadeInAnimation);
            await Task.Delay(3000);
            var fadeOutAnimation = new DoubleAnimation(1, 0, TimeSpan.FromSeconds(0.5));
            fadeOutAnimation.Completed += (s, e) => { this.Visibility = Visibility.Collapsed; this.BeginAnimation(OpacityProperty, null); };
            this.BeginAnimation(OpacityProperty, fadeOutAnimation);
        }
    }
}