using System;
using System.Windows;
using System.Windows.Threading;

namespace LoginAppFramework
{
    public partial class ToastNotificationWindow : Window
    {
        private readonly DispatcherTimer _timer;

        public ToastNotificationWindow(
            Window owner,
            string message,
            TimeSpan duration)
        {
            InitializeComponent();

            Owner = owner;
            MessageTextBlock.Text = message ?? string.Empty;

            Loaded += (_, _) => PositionWindow();
            if (owner != null)
                owner.LocationChanged += Owner_LocationChanged;

            _timer = new DispatcherTimer
            {
                Interval = duration
            };
            _timer.Tick += (_, _) =>
            {
                _timer.Stop();
                Close();
            };

            Closed += (_, _) =>
            {
                _timer.Stop();
                if (owner != null)
                    owner.LocationChanged -= Owner_LocationChanged;
            };
        }

        public void Start()
        {
            Show();
            _timer.Start();
        }

        private void Owner_LocationChanged(object sender, EventArgs e)
            => PositionWindow();

        private void PositionWindow()
        {
            var owner = Owner;
            if (owner == null)
            {
                Left = SystemParameters.WorkArea.Right - ActualWidth - 20;
                Top = SystemParameters.WorkArea.Bottom - ActualHeight - 20;
                return;
            }

            Left = owner.Left + owner.ActualWidth - ActualWidth - 24;
            Top = owner.Top + owner.ActualHeight - ActualHeight - 48;
        }
    }
}
