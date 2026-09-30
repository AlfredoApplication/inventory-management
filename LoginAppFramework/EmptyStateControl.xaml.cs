using System.Windows;
using System.Windows.Controls;

namespace LoginAppFramework
{
    public partial class EmptyStateControl : UserControl
    {
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                nameof(Icon),
                typeof(AppIconKind),
                typeof(EmptyStateControl),
                new PropertyMetadata(AppIconKind.Info));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register(
                nameof(Title),
                typeof(string),
                typeof(EmptyStateControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty MessageProperty =
            DependencyProperty.Register(
                nameof(Message),
                typeof(string),
                typeof(EmptyStateControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty ActionTextProperty =
            DependencyProperty.Register(
                nameof(ActionText),
                typeof(string),
                typeof(EmptyStateControl),
                new PropertyMetadata(string.Empty));

        public event RoutedEventHandler ActionClick;

        public EmptyStateControl()
        {
            InitializeComponent();
        }

        public AppIconKind Icon
        {
            get => (AppIconKind)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public string Message
        {
            get => (string)GetValue(MessageProperty);
            set => SetValue(MessageProperty, value);
        }

        public string ActionText
        {
            get => (string)GetValue(ActionTextProperty);
            set => SetValue(ActionTextProperty, value);
        }

        private void ActionButton_Click(object sender, RoutedEventArgs e)
            => ActionClick?.Invoke(this, e);
    }
}
