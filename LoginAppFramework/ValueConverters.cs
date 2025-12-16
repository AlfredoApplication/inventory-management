using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;

namespace LoginAppFramework
{
    // ALL CONVERTER CLASSES ARE DEFINED AND PUBLIC IN THIS SINGLE FILE

    public class DeviceTypeToIconConverter : IValueConverter
    {
        private static Dictionary<string, string> _iconCache;
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            _iconCache ??= AppData.GetDeviceCategoryIcons();
            if (value is string category && !string.IsNullOrEmpty(category) && _iconCache.TryGetValue(category, out var icon))
            {
                return icon;
            }
            return "⚙️";
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }

    public class ActionTextConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is AssignmentAction action ? action.ToString() : string.Empty; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class ActionToColorConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is AssignmentAction action ? action switch { AssignmentAction.Assigned => Brushes.Green, AssignmentAction.Unassigned => Brushes.IndianRed, AssignmentAction.Reassigned => Brushes.RoyalBlue, _ => Brushes.Gray } : Brushes.Gray; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class ActionToIconConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is AssignmentAction action ? action switch { AssignmentAction.Assigned => "✅", AssignmentAction.Unassigned => "❌", AssignmentAction.Reassigned => "🔄", _ => "❓" } : "❓"; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class HistoryDescriptionConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { if (value is not HistoryLogEntryViewModel entry) return null; var panel = new TextBlock { TextWrapping = TextWrapping.Wrap }; if (entry.Action == AssignmentAction.Assigned) { panel.Inlines.Add(new Run("Assigned to ") { Foreground = Brushes.Green, FontWeight = FontWeights.SemiBold }); panel.Inlines.Add(new Run(entry.ToWorkerName) { FontWeight = FontWeights.Bold }); } else if (entry.Action == AssignmentAction.Unassigned) { panel.Inlines.Add(new Run("Unassigned from ") { Foreground = Brushes.IndianRed, FontWeight = FontWeights.SemiBold }); panel.Inlines.Add(new Run(entry.FromWorkerName) { FontWeight = FontWeights.Bold }); } else { panel.Inlines.Add(new Run("Reassigned from ") { Foreground = Brushes.Gray }); panel.Inlines.Add(new Run(entry.FromWorkerName) { FontWeight = FontWeights.Bold }); panel.Inlines.Add(new Run(" to ") { Foreground = Brushes.RoyalBlue }); panel.Inlines.Add(new Run(entry.ToWorkerName) { FontWeight = FontWeights.Bold }); } return panel; } public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class DateToBrushConverter : IMultiValueConverter { public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) { if (values.Length < 2 || values[0] is not IEnumerable<HistoryLogEntryViewModel> allEvents || values[1] is not DateTime date) return Brushes.Transparent; int count = allEvents.Count(e => e.ChangeDate.Date == date.Date); if (count == 0) return Brushes.Transparent; if (count <= 5) return new SolidColorBrush(Color.FromArgb(100, 167, 233, 171)); if (count <= 15) return new SolidColorBrush(Color.FromArgb(180, 68, 184, 73)); return new SolidColorBrush(Color.FromArgb(255, 30, 111, 35)); } public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class LoanerActionTextConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is AssignmentHistoryEntry entry ? (entry.ToWorkerName == "Loaner Pool" ? "was checked in from" : "was checked out to") : string.Empty; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class AlertTypeToColorConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is AlertType type ? type switch { AlertType.Info => Brushes.CornflowerBlue, AlertType.Warning => Brushes.Orange, AlertType.Critical => new BrushConverter().ConvertFrom("#D32F2F") as Brush, _ => Brushes.Gray } : Brushes.Gray; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class AlertTypeToIconConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is AlertType type ? type switch { AlertType.Info => "\uE946", AlertType.Warning => "\uE7BA", AlertType.Critical => "\uEA39", _ => "\uE946" } : "\uE946"; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class RelativeTimeConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { if (value is not DateTime dateTime) return value; const int MINUTE = 60; const int HOUR = 60 * MINUTE; var ts = DateTime.Now - dateTime; double delta = Math.Abs(ts.TotalSeconds); if (delta < MINUTE) return ts.Seconds <= 1 ? "just now" : $"{ts.Seconds} seconds ago"; if (delta < HOUR) return ts.Minutes == 1 ? "a minute ago" : $"{ts.Minutes} minutes ago"; if (delta < 24 * HOUR) return ts.Hours == 1 ? "an hour ago" : $"{ts.Hours} hours ago"; if (delta < 48 * HOUR) return "yesterday"; return $"{ts.Days} days ago"; } public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class InvertedBooleanToOpacityConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { double opacityWhenTrue = System.Convert.ToDouble(parameter ?? 0.6, CultureInfo.InvariantCulture); return value is bool b && b ? opacityWhenTrue : 1.0; } public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class StringToIntConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { if (value is string text && int.TryParse(text, out int number)) { return number > 0 ? "GreaterThanZero" : "Zero"; } return "Zero"; } public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); } }
    public class NullToCursorConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value != null ? Cursors.Hand : Cursors.Arrow; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class BooleanToVisibilityCollapsedConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool b && b ? Visibility.Visible : Visibility.Collapsed; public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }
    public class IsNotNullOrEmptyToSeparatorConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { return (value is string s && !string.IsNullOrEmpty(s)) ? " / " : ""; } public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { throw new NotImplementedException(); } }
    public class InvertedBooleanConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { if (value is bool b) { return !b; } return true; } public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) { if (value is bool b) { return !b; } return false; } }
    public class HistoryEntryToIconConverter : IValueConverter { public object Convert(object value, Type targetType, object parameter, CultureInfo culture) { if (value is UnifiedHistoryViewModel vm) { if (vm.EntryType == HistoryEntryType.Assignment) { return "🔄"; } if (vm.OriginalEntry is AssetLog log) { return log.status switch { "Yaradılan" => "✅", "Dəyişdirilən" => "✏️", "Silinən" => "❌", _ => "❓" }; } } return "❓"; } public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException(); }

    public class IsNotNullConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }


        public class HistoryStatusToIconConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                return value as string switch
                {
                    "Yaradılan" => "\uE710", // Add
                    "Dəyişdirilən" => "\uE70F", // Edit
                    "Silinən" => "\uE74D", // Delete
                    _ => "\uE946" // Info
                };
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}