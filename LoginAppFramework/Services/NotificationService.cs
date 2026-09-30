using System;
using System.Windows;

namespace LoginAppFramework
{
    public static class NotificationService
    {
        public static void Success(
            Window owner,
            string message,
            int milliseconds = 2600,
            string title = null)
            => Show(owner, message, ToastType.Success, milliseconds, title);

        public static void Error(
            Window owner,
            string message,
            int milliseconds = 3600,
            string title = null)
            => Show(owner, message, ToastType.Error, milliseconds, title);

        public static void Warning(
            Window owner,
            string message,
            int milliseconds = 3400,
            string title = null)
            => Show(owner, message, ToastType.Warning, milliseconds, title);

        public static void Info(
            Window owner,
            string message,
            int milliseconds = 3000,
            string title = null)
            => Show(owner, message, ToastType.Info, milliseconds, title);

        private static void Show(
            Window owner,
            string message,
            ToastType type,
            int milliseconds,
            string title)
        {
            var toast = new ToastNotificationWindow(
                owner,
                message,
                type,
                TimeSpan.FromMilliseconds(milliseconds),
                title);

            toast.Start();
        }
    }
}
