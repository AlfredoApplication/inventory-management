using MahApps.Metro.IconPacks;
using System.Windows;

namespace LoginAppFramework
{
    public enum AppIconKind
    {
        Search,
        ChevronDown,
        ChevronUp,
        Calendar,
        Check,
        Menu,
        Close,
        Dashboard,
        Workers,
        Assets,
        History,
        Reports,
        UserManagement,
        User,
        SwitchUser,
        Logout,
        ExportExcel,
        Import,
        Add,
        Assign,
        Filter,
        Location,
        Money,
        Depreciation,
        Chart,
        Success,
        Error,
        Warning,
        Info
    }

    public sealed class AppIcon : PackIconMaterial
    {
        public static readonly DependencyProperty IconProperty =
            DependencyProperty.Register(
                nameof(Icon),
                typeof(AppIconKind),
                typeof(AppIcon),
                new PropertyMetadata(AppIconKind.Dashboard, OnIconChanged));

        public static readonly DependencyProperty SizeProperty =
            DependencyProperty.Register(
                nameof(Size),
                typeof(double),
                typeof(AppIcon),
                new PropertyMetadata(18d, OnSizeChanged));

        public AppIconKind Icon
        {
            get => (AppIconKind)GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public double Size
        {
            get => (double)GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public AppIcon()
        {
            HorizontalAlignment = HorizontalAlignment.Center;
            VerticalAlignment = VerticalAlignment.Center;
            ApplySize(Size);
            ApplyIcon(Icon);
        }

        private static void OnIconChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is AppIcon icon)
                icon.ApplyIcon((AppIconKind)e.NewValue);
        }

        private static void OnSizeChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is AppIcon icon)
                icon.ApplySize((double)e.NewValue);
        }

        private void ApplySize(double size)
        {
            Width = size;
            Height = size;
        }

        private void ApplyIcon(AppIconKind icon)
        {
            Kind = icon switch
            {
                AppIconKind.Search => PackIconMaterialKind.Magnify,
                AppIconKind.ChevronDown => PackIconMaterialKind.ChevronDown,
                AppIconKind.ChevronUp => PackIconMaterialKind.ChevronUp,
                AppIconKind.Calendar => PackIconMaterialKind.Calendar,
                AppIconKind.Check => PackIconMaterialKind.Check,
                AppIconKind.Menu => PackIconMaterialKind.Menu,
                AppIconKind.Close => PackIconMaterialKind.Close,
                AppIconKind.Dashboard => PackIconMaterialKind.ViewDashboard,
                AppIconKind.Workers => PackIconMaterialKind.AccountGroup,
                AppIconKind.Assets => PackIconMaterialKind.Archive,
                AppIconKind.History => PackIconMaterialKind.History,
                AppIconKind.Reports => PackIconMaterialKind.ChartBar,
                AppIconKind.UserManagement => PackIconMaterialKind.AccountGroup,
                AppIconKind.User => PackIconMaterialKind.Account,
                AppIconKind.SwitchUser => PackIconMaterialKind.AccountSwitch,
                AppIconKind.Logout => PackIconMaterialKind.Logout,
                AppIconKind.ExportExcel => PackIconMaterialKind.FileExcel,
                AppIconKind.Import => PackIconMaterialKind.Upload,
                AppIconKind.Add => PackIconMaterialKind.Plus,
                AppIconKind.Assign => PackIconMaterialKind.AccountPlus,
                AppIconKind.Filter => PackIconMaterialKind.Filter,
                AppIconKind.Location => PackIconMaterialKind.MapMarker,
                AppIconKind.Money => PackIconMaterialKind.Cash,
                AppIconKind.Depreciation => PackIconMaterialKind.TrendingDown,
                AppIconKind.Chart => PackIconMaterialKind.ChartLine,
                AppIconKind.Success => PackIconMaterialKind.CheckCircle,
                AppIconKind.Error => PackIconMaterialKind.AlertCircle,
                AppIconKind.Warning => PackIconMaterialKind.Alert,
                AppIconKind.Info => PackIconMaterialKind.Information,
                _ => PackIconMaterialKind.CircleOutline
            };
        }
    }
}
