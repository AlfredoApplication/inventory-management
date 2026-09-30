using QuestPDF.Infrastructure;
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace LoginAppFramework
{
    public partial class App : Application
    {
        private bool _handlingFatalError;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            QuestPDF.Settings.License = LicenseType.Community;

            var cultureInfo = new CultureInfo("az-Latn-AZ");
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;

            DiagnosticService.Initialize();

            DispatcherUnhandledException +=
                App_DispatcherUnhandledException;

            AppDomain.CurrentDomain.UnhandledException +=
                CurrentDomain_UnhandledException;

            TaskScheduler.UnobservedTaskException +=
                TaskScheduler_UnobservedTaskException;
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ConnectionHealthService.StopMonitoring();

            DispatcherUnhandledException -=
                App_DispatcherUnhandledException;

            AppDomain.CurrentDomain.UnhandledException -=
                CurrentDomain_UnhandledException;

            TaskScheduler.UnobservedTaskException -=
                TaskScheduler_UnobservedTaskException;

            DiagnosticService.Log(
                "Application",
                $"Application exited. Code={e.ApplicationExitCode}");

            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(
            object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            DiagnosticService.Log(
                "Unhandled",
                "Unhandled UI exception.",
                e.Exception);

            e.Handled = true;

            ShowFatalError(
                e.Exception,
                "WPF Dispatcher");
        }

        private void CurrentDomain_UnhandledException(
            object sender,
            UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception;

            DiagnosticService.Log(
                "Unhandled",
                "Unhandled AppDomain exception.",
                exception);

            if (Dispatcher.CheckAccess())
            {
                ShowFatalError(
                    exception,
                    "AppDomain");
            }
            else
            {
                Dispatcher.BeginInvoke(
                    new Action(() =>
                        ShowFatalError(
                            exception,
                            "AppDomain")));
            }
        }

        private void TaskScheduler_UnobservedTaskException(
            object sender,
            UnobservedTaskExceptionEventArgs e)
        {
            DiagnosticService.Log(
                "Task",
                "Unobserved task exception.",
                e.Exception);

            e.SetObserved();

            Dispatcher.BeginInvoke(
                new Action(() =>
                    NotificationService.Error(
                        null,
                        "Arxa planda gözlənilməz xəta baş verdi. Diaqnostika tarixçəyə yazıldı.",
                        title: "Sistem xətası")));
        }

        private void ShowFatalError(
            Exception exception,
            string context)
        {
            if (_handlingFatalError)
                return;

            _handlingFatalError = true;

            try
            {
                var window = new CrashReportWindow(
                    exception,
                    context);

                window.ShowDialog();

                if (window.RestartRequested)
                {
                    NavigationManager.RestartApplication();
                    return;
                }

                Shutdown();
            }
            catch (Exception crashWindowError)
            {
                DiagnosticService.Log(
                    "CrashHandler",
                    "Crash report window could not be shown.",
                    crashWindowError);

                Shutdown();
            }
        }
    }
}
