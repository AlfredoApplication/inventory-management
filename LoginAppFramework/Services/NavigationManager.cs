using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace LoginAppFramework
{
    public static class NavigationManager
    {
        private static Window _currentWindow;

        private static async Task NavigateAsync<T>() where T : Window, new()
        {
            Window previousWindow = _currentWindow;
            if (previousWindow == null) return; // Safety check

            // --- ALL ANIMATION AND FADING LOGIC HAS BEEN REMOVED ---

            // Create the next window
            T nextWindow = new T();

            // We use a TaskCompletionSource to wait until the new window's "Loaded" event has fired.
            // This ensures the new window is fully rendered before we close the old one.
            var tcs = new TaskCompletionSource<bool>();
            nextWindow.Loaded += (s, e) => { tcs.SetResult(true); };

            // Show the new window instantly at full opacity
            nextWindow.Show();
            _currentWindow = nextWindow; // Update the current window reference

            // Asynchronously wait for the new window to be fully loaded
            await tcs.Task;

            // Now that the new window is visible and ready, close the previous one instantly.
            previousWindow.Close();
        }

        public static void GoToReportsWindow()
        {
            // Yeni ReportsWindow pəncərəsindən bir obyekt yaradırıq
            var reportsWindow = new ReportsWindow
            {
                // `Owner = Application.Current.MainWindow` təyin etməklə,
                // hesabat pəncərəsinin həmişə əsas pəncərənin üzərində qalmasını təmin edirik.
                Owner = Application.Current.MainWindow
            };
            // `ShowDialog()` metodu pəncərəni modal olaraq açır, yəni
            // bu pəncərə bağlanana qədər arxadakı əsas pəncərəyə keçid etmək olmur.
            reportsWindow.ShowDialog();
        }
        public static async Task GoToDashboard(Window callingWindow = null)
        {
            if (callingWindow != null) _currentWindow = callingWindow;
            await NavigateAsync<DashboardWindow>();
        }

        public static async Task GoToAssetWindow() => await NavigateAsync<AssetWindow>();
        public static async Task GoToWorkerListWindow() => await NavigateAsync<WorkerListWindow>();
        public static async Task GoToHistoryLogWindow() => await NavigateAsync<HistoryLogWindow>();
        public static async Task GoToLifecycleReportWindow() => await NavigateAsync<LifecycleReportWindow>();

        // Restart logic is unchanged and correct.
        public static void RestartApplication()
        {
            SessionManager.Logout();
            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }
    }
}