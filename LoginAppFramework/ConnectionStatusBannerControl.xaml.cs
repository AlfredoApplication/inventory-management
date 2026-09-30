using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class ConnectionStatusBannerControl : UserControl
    {
        public ConnectionStatusBannerControl()
        {
            InitializeComponent();

            Loaded += (_, _) =>
            {
                ConnectionHealthService.Changed +=
                    ConnectionHealthService_Changed;

                UpdateState();
            };

            Unloaded += (_, _) =>
            {
                ConnectionHealthService.Changed -=
                    ConnectionHealthService_Changed;
            };
        }

        private void ConnectionHealthService_Changed(
            object sender,
            EventArgs e)
        {
            if (Dispatcher.CheckAccess())
                UpdateState();
            else
                Dispatcher.BeginInvoke(
                    new Action(UpdateState));
        }

        private void UpdateState()
        {
            bool dbOffline =
                ConnectionHealthService.DatabaseState ==
                ConnectionHealthState.Offline;

            bool hrOffline =
                ConnectionHealthService.HrState ==
                ConnectionHealthState.Offline;

            if (!dbOffline && !hrOffline)
            {
                Visibility = Visibility.Collapsed;
                return;
            }

            Visibility = Visibility.Visible;

            var danger =
                (Brush)FindResource("DangerTextBrush");

            var warning =
                (Brush)FindResource("WarningTextBrush");

            if (dbOffline)
            {
                BannerBorder.Background =
                    (Brush)FindResource("DangerSurfaceBrush");

                BannerBorder.BorderBrush =
                    (Brush)FindResource("DangerBorderBrush");

                StateIcon.Icon = AppIconKind.Error;
                StateIcon.Foreground = danger;

                TitleTextBlock.Text =
                    "Verilənlər bazası bağlantısı yoxdur";

                TitleTextBlock.Foreground = danger;

                MessageTextBlock.Text =
                    "Məlumat oxuma və yazma əməliyyatları uğursuz ola bilər. Bağlantını yoxlayıb yenidən cəhd edin.";

                RetryButton.Visibility = Visibility.Visible;
                return;
            }

            BannerBorder.Background =
                (Brush)FindResource("WarningSurfaceBrush");

            BannerBorder.BorderBrush =
                (Brush)FindResource("WarningBorderBrush");

            StateIcon.Icon = AppIconKind.Warning;
            StateIcon.Foreground = warning;

            TitleTextBlock.Text =
                "HR bağlantısı əlçatan deyil";

            TitleTextBlock.Foreground = warning;

            MessageTextBlock.Text =
                "Əsas inventar bazası işləyir, amma HR sinxronizasiyası son cəhddə uğursuz oldu.";

            RetryButton.Visibility = Visibility.Collapsed;
        }

        private async void RetryButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            RetryButton.IsEnabled = false;

            try
            {
                await ConnectionHealthService.CheckDatabaseAsync();
            }
            finally
            {
                RetryButton.IsEnabled = true;
            }
        }
    }
}
