using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace LoginAppFramework
{
    public partial class ToastNotificationWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly Window _owner;
        private readonly Action _action;
        private bool _actionInvoked;
        private int _stackIndex;

        public event EventHandler ToastClosed;

        public ToastNotificationWindow(
            Window owner,
            string message,
            ToastType type,
            TimeSpan duration,
            string title = null,
            string actionText = null,
            Action action = null)
        {
            InitializeComponent();

            _owner = owner;
            if (owner != null)
                Owner = owner;

            MessageTextBlock.Text = message ?? string.Empty;
            _action = action;

            if (_action != null &&
                !string.IsNullOrWhiteSpace(actionText))
            {
                ActionButton.Content = actionText;
                ActionButton.Visibility = Visibility.Visible;
            }

            ApplyType(type, title);

            Loaded += (_, _) => PositionWindow();
            if (owner != null)
            {
                owner.LocationChanged += Owner_LocationChanged;
                owner.SizeChanged += Owner_SizeChanged;
            }

            _timer = new DispatcherTimer
            {
                Interval = duration
            };
            _timer.Tick += (_, _) => CloseToast();

            Closed += (_, _) =>
            {
                _timer.Stop();

                if (_owner != null)
                {
                    _owner.LocationChanged -= Owner_LocationChanged;
                    _owner.SizeChanged -= Owner_SizeChanged;
                }

                ToastClosed?.Invoke(this, EventArgs.Empty);
            };
        }

        public void Start()
        {
            Show();
            _timer.Start();
        }

        public void SetStackIndex(int stackIndex)
        {
            _stackIndex = Math.Max(0, stackIndex);
            if (IsLoaded)
                PositionWindow();
        }

        private void ApplyType(ToastType type, string title)
        {
            string surfaceKey;
            string borderKey;
            string textKey;

            switch (type)
            {
                case ToastType.Success:
                    IconVisual.Icon = AppIconKind.Success;
                    TitleTextBlock.Text = title ?? "Uğurlu";
                    surfaceKey = "SuccessSurfaceBrush";
                    borderKey = "SuccessBorderBrush";
                    textKey = "SuccessTextBrush";
                    break;
                case ToastType.Error:
                    IconVisual.Icon = AppIconKind.Error;
                    TitleTextBlock.Text = title ?? "Xəta";
                    surfaceKey = "DangerSurfaceBrush";
                    borderKey = "DangerBorderBrush";
                    textKey = "DangerTextBrush";
                    break;
                case ToastType.Warning:
                    IconVisual.Icon = AppIconKind.Warning;
                    TitleTextBlock.Text = title ?? "Diqqət";
                    surfaceKey = "WarningSurfaceBrush";
                    borderKey = "WarningBorderBrush";
                    textKey = "WarningTextBrush";
                    break;
                default:
                    IconVisual.Icon = AppIconKind.Info;
                    TitleTextBlock.Text = title ?? "Məlumat";
                    surfaceKey = "InfoSurfaceBrush";
                    borderKey = "InfoBorderBrush";
                    textKey = "InfoTextBrush";
                    break;
            }

            ToastBorder.Background = (Brush)FindResource(surfaceKey);
            ToastBorder.BorderBrush = (Brush)FindResource(borderKey);
            IconVisual.Foreground = (Brush)FindResource(textKey);
        }

        private void Owner_LocationChanged(object sender, EventArgs e)
            => PositionWindow();

        private void Owner_SizeChanged(object sender, SizeChangedEventArgs e)
            => PositionWindow();

        private void PositionWindow()
        {
            const double gap = 8;
            double offset = _stackIndex * (ActualHeight + gap);

            if (_owner == null)
            {
                Left = SystemParameters.WorkArea.Right - ActualWidth - 20;
                Top = SystemParameters.WorkArea.Bottom - ActualHeight - 20 - offset;
                return;
            }

            Left = Math.Max(
                _owner.Left + 16,
                _owner.Left + _owner.ActualWidth - ActualWidth - 24);

            Top = Math.Max(
                _owner.Top + 16,
                _owner.Top + _owner.ActualHeight - ActualHeight - 48 - offset);
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (_actionInvoked || _action == null)
                return;

            _actionInvoked = true;
            _timer.Stop();
            Close();

            try
            {
                _action();
            }
            catch (Exception ex)
            {
                NotificationService.Error(
                    _owner,
                    $"Əməliyyatı geri qaytarmaq mümkün olmadı: {ex.Message}",
                    title: "Geri qaytarma xətası");
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
            => CloseToast();

        private void CloseToast()
        {
            _timer.Stop();
            Close();
        }
    }
}
