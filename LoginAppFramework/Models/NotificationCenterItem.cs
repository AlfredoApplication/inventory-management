using System;

namespace LoginAppFramework
{
    public enum NotificationCenterCategory
    {
        Activity,
        Warranty,
        Maintenance,
        System
    }

    public sealed class NotificationCenterItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserKey { get; set; }
        public NotificationCenterCategory Category { get; set; }
        public ToastType Type { get; set; }
        public string SourceKey { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public bool IsRead { get; set; }
        public bool IsResolved { get; set; }
        public NavigationTargetType? TargetType { get; set; }
        public int TargetId { get; set; }

        public string CategoryText => Category switch
        {
            NotificationCenterCategory.Warranty => "Zəmanət",
            NotificationCenterCategory.Maintenance => "Texniki xidmət",
            NotificationCenterCategory.System => "Sistem",
            _ => "Əməliyyat"
        };

        public string StateText =>
            IsResolved
                ? "Həll olunub"
                : IsRead
                    ? "Oxunub"
                    : "Yeni";

        public AppIconKind Icon => Category switch
        {
            NotificationCenterCategory.Warranty => AppIconKind.Warning,
            NotificationCenterCategory.Maintenance => AppIconKind.Maintenance,
            NotificationCenterCategory.System => AppIconKind.Info,
            _ => Type switch
            {
                ToastType.Success => AppIconKind.Success,
                ToastType.Error => AppIconKind.Error,
                ToastType.Warning => AppIconKind.Warning,
                _ => AppIconKind.Info
            }
        };
    }
}
