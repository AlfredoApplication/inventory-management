using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LoginAppFramework
{
    public static class NotificationCenterService
    {
        private const int MaximumItemsPerUser = 300;
        private static readonly object Sync = new();
        private static readonly string FolderPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "InventoryManagement");

        private static readonly string FilePath =
            Path.Combine(
                FolderPath,
                "notifications.json");

        public static event EventHandler Changed;

        public static IReadOnlyList<NotificationCenterItem> GetItems()
        {
            lock (Sync)
            {
                string userKey = CurrentUserKey();

                return LoadRoot()
                    .Items
                    .Where(item =>
                        string.Equals(
                            item.UserKey,
                            userKey,
                            StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.Timestamp)
                    .ToList();
            }
        }

        public static int GetUnreadCount()
            => GetItems().Count(item =>
                !item.IsRead &&
                !item.IsResolved);

        public static void AddActivity(
            ToastType type,
            string title,
            string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            lock (Sync)
            {
                var root = LoadRoot();
                string userKey = CurrentUserKey();

                root.Items.Add(new NotificationCenterItem
                {
                    UserKey = userKey,
                    Category = NotificationCenterCategory.Activity,
                    Type = type,
                    Title = ResolveTitle(type, title),
                    Message = message.Trim(),
                    Timestamp = DateTime.Now,
                    IsRead = false,
                    IsResolved = false
                });

                TrimUserItems(root, userKey);
                SaveRoot(root);
            }

            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void SyncBusinessAlerts(
            IEnumerable<Alert> alerts)
        {
            alerts ??= Array.Empty<Alert>();

            lock (Sync)
            {
                var root = LoadRoot();
                string userKey = CurrentUserKey();

                var currentKeys = alerts
                    .Where(alert =>
                        !string.IsNullOrWhiteSpace(alert.SourceKey))
                    .Select(alert => alert.SourceKey)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var existingBusiness = root.Items
                    .Where(item =>
                        string.Equals(
                            item.UserKey,
                            userKey,
                            StringComparison.OrdinalIgnoreCase) &&
                        item.Category is
                            NotificationCenterCategory.Warranty or
                            NotificationCenterCategory.Maintenance)
                    .ToList();

                foreach (var item in existingBusiness)
                {
                    if (!string.IsNullOrWhiteSpace(item.SourceKey) &&
                        !currentKeys.Contains(item.SourceKey) &&
                        !item.IsResolved)
                    {
                        item.IsResolved = true;
                        item.IsRead = true;
                    }
                }

                foreach (var alert in alerts)
                {
                    if (string.IsNullOrWhiteSpace(alert.SourceKey))
                        continue;

                    var existing = existingBusiness.FirstOrDefault(item =>
                        string.Equals(
                            item.SourceKey,
                            alert.SourceKey,
                            StringComparison.OrdinalIgnoreCase));

                    var category = alert.Category switch
                    {
                        AlertCategory.Warranty =>
                            NotificationCenterCategory.Warranty,
                        AlertCategory.Maintenance =>
                            NotificationCenterCategory.Maintenance,
                        _ => NotificationCenterCategory.System
                    };

                    var type = alert.Type switch
                    {
                        AlertType.Critical => ToastType.Error,
                        AlertType.Warning => ToastType.Warning,
                        _ => ToastType.Info
                    };

                    if (existing == null)
                    {
                        root.Items.Add(new NotificationCenterItem
                        {
                            UserKey = userKey,
                            Category = category,
                            Type = type,
                            SourceKey = alert.SourceKey,
                            Title = alert.Title,
                            Message = alert.Message,
                            Timestamp = alert.Timestamp,
                            IsRead = false,
                            IsResolved = false,
                            TargetType = alert.TargetType,
                            TargetId = alert.TargetId
                        });

                        continue;
                    }

                    bool changed =
                        !string.Equals(
                            existing.Title,
                            alert.Title,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            existing.Message,
                            alert.Message,
                            StringComparison.Ordinal) ||
                        existing.Type != type;

                    existing.Category = category;
                    existing.Type = type;
                    existing.Title = alert.Title;
                    existing.Message = alert.Message;
                    existing.TargetType = alert.TargetType;
                    existing.TargetId = alert.TargetId;
                    existing.IsResolved = false;

                    if (changed)
                    {
                        existing.Timestamp = DateTime.Now;
                        existing.IsRead = false;
                    }
                }

                TrimUserItems(root, userKey);
                SaveRoot(root);
            }

            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void MarkRead(Guid id)
            => UpdateItem(
                id,
                item => item.IsRead = true);

        public static void MarkAllRead()
        {
            lock (Sync)
            {
                var root = LoadRoot();
                string userKey = CurrentUserKey();

                foreach (var item in root.Items.Where(item =>
                    string.Equals(
                        item.UserKey,
                        userKey,
                        StringComparison.OrdinalIgnoreCase)))
                {
                    item.IsRead = true;
                }

                SaveRoot(root);
            }

            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void ClearRead()
        {
            lock (Sync)
            {
                var root = LoadRoot();
                string userKey = CurrentUserKey();

                root.Items.RemoveAll(item =>
                    string.Equals(
                        item.UserKey,
                        userKey,
                        StringComparison.OrdinalIgnoreCase) &&
                    item.IsRead);

                SaveRoot(root);
            }

            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void UpdateItem(
            Guid id,
            Action<NotificationCenterItem> update)
        {
            lock (Sync)
            {
                var root = LoadRoot();
                string userKey = CurrentUserKey();

                var item = root.Items.FirstOrDefault(value =>
                    value.Id == id &&
                    string.Equals(
                        value.UserKey,
                        userKey,
                        StringComparison.OrdinalIgnoreCase));

                if (item == null)
                    return;

                update(item);
                SaveRoot(root);
            }

            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void TrimUserItems(
            NotificationRoot root,
            string userKey)
        {
            var userItems = root.Items
                .Where(item =>
                    string.Equals(
                        item.UserKey,
                        userKey,
                        StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.Timestamp)
                .ToList();

            foreach (var item in userItems.Skip(MaximumItemsPerUser))
                root.Items.Remove(item);
        }

        private static string ResolveTitle(
            ToastType type,
            string title)
        {
            if (!string.IsNullOrWhiteSpace(title))
                return title.Trim();

            return type switch
            {
                ToastType.Success => "Uğurlu əməliyyat",
                ToastType.Error => "Xəta",
                ToastType.Warning => "Xəbərdarlıq",
                _ => "Məlumat"
            };
        }

        private static string CurrentUserKey()
            => string.IsNullOrWhiteSpace(
                SessionManager.CurrentUser?.Username)
                ? "default"
                : SessionManager.CurrentUser.Username.Trim();

        private static NotificationRoot LoadRoot()
        {
            try
            {
                if (!File.Exists(FilePath))
                    return new NotificationRoot();

                string json = File.ReadAllText(FilePath);

                return JsonSerializer.Deserialize<NotificationRoot>(json)
                    ?? new NotificationRoot();
            }
            catch
            {
                return new NotificationRoot();
            }
        }

        private static void SaveRoot(NotificationRoot root)
        {
            try
            {
                Directory.CreateDirectory(FolderPath);

                string json = JsonSerializer.Serialize(
                    root,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                File.WriteAllText(FilePath, json);
            }
            catch
            {
                // Notification history must never block the application.
            }
        }

        private sealed class NotificationRoot
        {
            public List<NotificationCenterItem> Items { get; set; }
                = new();
        }
    }
}
