using System.Windows;

namespace LoginAppFramework
{
    public enum AppDialogType
    {
        Info,
        Success,
        Warning,
        Error,
        Question
    }

    public static class DialogService
    {
        public static void Info(Window owner, string title, string message)
            => Show(owner, title, message, AppDialogType.Info);

        public static void Success(Window owner, string title, string message)
            => Show(owner, title, message, AppDialogType.Success);

        public static void Warning(Window owner, string title, string message)
            => Show(owner, title, message, AppDialogType.Warning);

        public static void Error(Window owner, string title, string message)
            => Show(owner, title, message, AppDialogType.Error);

        public static bool Confirm(
            Window owner,
            string title,
            string message,
            string confirmText = "Təsdiq et",
            string cancelText = "Ləğv et",
            bool destructive = false)
        {
            var dialog = new AppDialogWindow(
                owner,
                title,
                message,
                destructive ? AppDialogType.Warning : AppDialogType.Question,
                true,
                confirmText,
                cancelText,
                destructive);

            return dialog.ShowDialog() == true && dialog.Confirmed;
        }

        private static void Show(
            Window owner,
            string title,
            string message,
            AppDialogType type)
        {
            var dialog = new AppDialogWindow(
                owner,
                title,
                message,
                type,
                false,
                "Bağla",
                null,
                false);

            dialog.ShowDialog();
        }
    }
}