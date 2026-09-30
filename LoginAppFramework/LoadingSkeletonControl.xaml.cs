using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace LoginAppFramework
{
    public partial class LoadingSkeletonControl : UserControl
    {
        public LoadingSkeletonControl()
        {
            InitializeComponent();
            Loaded += LoadingSkeletonControl_Loaded;
            Unloaded += LoadingSkeletonControl_Unloaded;
        }

        private void LoadingSkeletonControl_Loaded(object sender, RoutedEventArgs e)
            => ((Storyboard)Resources["PulseStoryboard"]).Begin(this, true);

        private void LoadingSkeletonControl_Unloaded(object sender, RoutedEventArgs e)
            => ((Storyboard)Resources["PulseStoryboard"]).Stop(this);
    }
}
