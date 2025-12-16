using System.Globalization;
using System.Threading;
using System.Windows;
using QuestPDF.Infrastructure;

namespace LoginAppFramework
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            QuestPDF.Settings.License = LicenseType.Community;

            // This sets the application's culture for formatting dates, currency, etc.
            var cultureInfo = new CultureInfo("az-Latn-AZ");
            Thread.CurrentThread.CurrentCulture = cultureInfo;
            Thread.CurrentThread.CurrentUICulture = cultureInfo;
        }
    }
}