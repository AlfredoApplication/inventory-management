using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Windows.Input;

namespace LoginAppFramework
{
    public static class NavigationManager
    {
        private static Window _currentWindow;

        private static async Task NavigateAsync<T>(Window callingWindow = null) where T : Window, new()
        {
            Window previousWindow =
                callingWindow ??
                _currentWindow ??
                Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(window => window.IsActive);

            if (previousWindow == null) return;

            if (previousWindow is T)
            {
                _currentWindow = previousWindow;
                return;
            }

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

        public static void GoToReportsWindow(Window owner = null)
        {
            var reportsWindow = new ReportsWindow
            {
                Owner = owner ?? _currentWindow ?? Application.Current.MainWindow
            };

            reportsWindow.ShowDialog();
        }
        public static async Task GoToDashboard(Window callingWindow = null)
            => await NavigateAsync<DashboardWindow>(callingWindow);

        public static async Task GoToAssetWindow(Window callingWindow = null)
            => await NavigateAsync<AssetWindow>(callingWindow);

        public static async Task GoToWorkerListWindow(Window callingWindow = null)
            => await NavigateAsync<WorkerListWindow>(callingWindow);

        public static async Task GoToHistoryLogWindow(Window callingWindow = null)
            => await NavigateAsync<HistoryLogWindow>(callingWindow);

        // Restart logic is unchanged and correct.
        public static void RestartApplication()
        {
            SessionManager.Logout();
            Process.Start(Application.ResourceAssembly.Location);
            Application.Current.Shutdown();
        }
    }
}