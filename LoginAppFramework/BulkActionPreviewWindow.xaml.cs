using System.Windows;

namespace LoginAppFramework
{
    public partial class BulkActionPreviewWindow : Window
    {
        public BulkActionPreviewWindow(BulkActionPreview preview)
        {
            InitializeComponent();
            DataContext = preview;
        }

        private void ConfirmButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
