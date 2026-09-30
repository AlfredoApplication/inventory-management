using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace LoginAppFramework
{
    public partial class ImportMappingWindow : Window
    {
        public Dictionary<string, Worker> ConfirmedMappings { get; private set; }

        public ImportMappingWindow(List<string> unmappedNames, List<Worker> allDbWorkers)
        {
            InitializeComponent();
            var viewModels = unmappedNames
                .Select(name => new ImportMappingViewModel(name, allDbWorkers))
                .ToList();
            MappingDataGrid.ItemsSource = viewModels;
            ConfirmedMappings = new Dictionary<string, Worker>();
        }

        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            var viewModels = MappingDataGrid.ItemsSource as List<ImportMappingViewModel>;

            if (viewModels.Any(vm => vm.IsUnmapped))
            {
                bool confirm = DialogService.Confirm(
                    this,
                    "Natamam uyğunlaşdırma",
                    "Hələ uyğunlaşdırılmamış işçilər var. Bu işçilərə təhkim edilmiş vəsaitlər təhkimsiz import olunacaq. Davam edilsin?",
                    "Davam et",
                    "Geri qayıt");

                if (!confirm)
                    return;
            }

            // We now build a dictionary of the confirmed mappings to send back.
            foreach (var vm in viewModels.Where(vm => !vm.IsUnmapped))
            {
                ConfirmedMappings[vm.ExcelUserName] = vm.SelectedDbWorker;
            }

            DialogResult = true;
            Close();
        }
    }
}