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

    public enum UnsavedChangesChoice
    {
        Save,
        Discard,
        Cancel
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

        public static UnsavedChangesChoice ConfirmUnsavedChanges(
            Window owner,
            string itemName = null)
        {
            string subject = string.IsNullOrWhiteSpace(itemName)
                ? "Bu pəncərədə"
                : $"'{itemName}' üçün";

            var dialog = new AppDialogWindow(
                owner,
                "Yadda saxlanılmamış dəyişikliklər",
                $"{subject} yadda saxlanılmamış dəyişikliklər var. Nə etmək istəyirsiniz?",
                AppDialogType.Warning,
                true,
                "Yadda saxla",
                "Dəyişiklikləri at",
                false,
                showTertiary: true,
                tertiaryText: "Geri qayıt");

            dialog.ShowDialog();

            return dialog.SelectedAction switch
            {
                AppDialogAction.Primary => UnsavedChangesChoice.Save,
                AppDialogAction.Secondary => UnsavedChangesChoice.Discard,
                _ => UnsavedChangesChoice.Cancel
            };
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