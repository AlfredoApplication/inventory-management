using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace LoginAppFramework
{
    public static class NavigationManager
    {
        private static readonly Dictionary<Type, Window> _pageCache = new();

        private static Window _currentWindow;
        private static bool _isShuttingDown;

        private static bool IsCachedPage(Window window)
            => window is DashboardWindow
                or AssetWindow
                or WorkerListWindow
                or HistoryLogWindow;

        private static async Task<T> NavigateAsync<T>(
            Window callingWindow = null,
            Func<T> factory = null)
            where T : Window, new()
        {
            Window previousWindow =
                callingWindow ??
                _currentWindow ??
                Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(window => window.IsActive);

            if (previousWindow is T currentTarget)
            {
                _currentWindow = currentTarget;

                if (!currentTarget.IsVisible)
                    currentTarget.Show();

                currentTarget.Activate();
                return currentTarget;
            }

            if (!_pageCache.TryGetValue(typeof(T), out Window cachedWindow))
            {
                var next = factory != null ? factory() : new T();
                RegisterCachedPage(next);
                cachedWindow = next;
            }

            var nextWindow = (T)cachedWindow;
            bool firstShow = !nextWindow.IsLoaded;

            TaskCompletionSource<bool> loadedTcs = null;
            if (firstShow)
            {
                loadedTcs = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously);

                RoutedEventHandler loadedHandler = null;
                loadedHandler = (_, _) =>
                {
                    nextWindow.Loaded -= loadedHandler;
                    loadedTcs.TrySetResult(true);
                };

                nextWindow.Loaded += loadedHandler;
            }

            if (!nextWindow.IsVisible)
                nextWindow.Show();

            _currentWindow = nextWindow;
            nextWindow.Activate();

            if (loadedTcs != null)
                await loadedTcs.Task;

            if (previousWindow != null &&
                !ReferenceEquals(previousWindow, nextWindow))
            {
                if (IsCachedPage(previousWindow))
                {
                    previousWindow.Hide();
                }
                else
                {
                    previousWindow.Close();
                }
            }

            return nextWindow;
        }

        private static void RegisterCachedPage(Window window)
        {
            _pageCache[window.GetType()] = window;

            window.Closed += (_, _) =>
            {
                _pageCache.Remove(window.GetType());

                if (ReferenceEquals(_currentWindow, window))
                    _currentWindow = null;

                if (!_isShuttingDown &&
                    !Application.Current.Windows
                        .OfType<Window>()
                        .Any(other =>
                            other.IsVisible &&
                            !ReferenceEquals(other, window)))
                {
                    Application.Current.Shutdown();
                }
            };
        }

        public static void GoToReportsWindow(Window owner = null)
        {
            var reportsWindow = new ReportsWindow
            {
                Owner =
                    owner ??
                    _currentWindow ??
                    Application.Current.MainWindow
            };

            reportsWindow.ShowDialog();
        }

        public static async Task GoToDashboard(
            Window callingWindow = null)
            => await NavigateAsync<DashboardWindow>(callingWindow);

        public static async Task GoToAssetWindow(
            Window callingWindow = null)
            => await NavigateAsync<AssetWindow>(callingWindow);

        public static async Task GoToAssetWindow(
            Window callingWindow,
            int assetId)
        {
            var window = await NavigateAsync<AssetWindow>(
                callingWindow,
                () => new AssetWindow(assetId));

            window.NavigateToAsset(assetId);
        }

        public static async Task GoToWorkerListWindow(
            Window callingWindow = null)
            => await NavigateAsync<WorkerListWindow>(callingWindow);

        public static async Task GoToHistoryLogWindow(
            Window callingWindow = null)
            => await NavigateAsync<HistoryLogWindow>(callingWindow);

        public static void SwitchUser()
        {
            SessionManager.Logout();

            var loginWindow = new MainWindow();
            Application.Current.MainWindow = loginWindow;

            _isShuttingDown = true;
            try
            {
                loginWindow.Show();

                var windowsToClose = Application.Current.Windows
                    .OfType<Window>()
                    .Where(window => !ReferenceEquals(window, loginWindow))
                    .ToList();

                foreach (var window in windowsToClose)
                    window.Close();

                _pageCache.Clear();
                _currentWindow = loginWindow;
            }
            finally
            {
                _isShuttingDown = false;
            }

            loginWindow.Activate();
        }

        public static void ExitApplication()
        {
            _isShuttingDown = true;
            SessionManager.Logout();
            Application.Current.Shutdown();
        }

        public static void RestartApplication()
        {
            _isShuttingDown = true;
            SessionManager.Logout();

            Process.Start(
                new ProcessStartInfo
                {
                    FileName =
                        Environment.ProcessPath ??
                        Application.ResourceAssembly.Location,
                    UseShellExecute = true
                });

            Application.Current.Shutdown();
        }
    }
}
