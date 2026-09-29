using System;
using System.Windows;

namespace LoginAppFramework
{
    public static class NotificationService
    {
        public static void Success(
            Window owner,
            string message,
            int milliseconds = 2600)
        {
            var toast = new ToastNotificationWindow(
                owner,
                message,
                TimeSpan.FromMilliseconds(milliseconds));

            toast.Start();
        }
    }
}
