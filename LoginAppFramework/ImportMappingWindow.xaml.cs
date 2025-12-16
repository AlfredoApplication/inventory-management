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
                if (MessageBox.Show("There are still unmapped users. Assets assigned to these users will be imported as unassigned. Continue?",
                    "Incomplete Mapping", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
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