// NEW FILE: Alert.cs
using System;

namespace LoginAppFramework
{
    public class Alert
    {
        public Guid Id { get; } = Guid.NewGuid();
        public AlertType Type { get; set; }
        public AlertCategory Category { get; set; }
        public string SourceKey { get; set; }
        public NavigationTargetType? TargetType { get; set; }
        public int TargetId { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; } = false;
    }

    public enum AlertType
    {
        Info,
        Warning,
        Critical
    }

    public enum AlertCategory
    {
        Warranty,
        Maintenance,
        Lifecycle,
        General
    }

    public enum NavigationTargetType
    {
        Asset,
        StockItem,
        User
    }
}