using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public static class AlertService
    {
        private const int DefaultWarrantyDays = 30;
        private const int DefaultMaintenanceDays = 180;

        public static List<Alert> CurrentAlerts { get; private set; } = new();

        public static int WarrantyAlertCount =>
            CurrentAlerts.Count(alert =>
                alert.Category == AlertCategory.Warranty);

        public static int MaintenanceAlertCount =>
            CurrentAlerts.Count(alert =>
                alert.Category == AlertCategory.Maintenance);

        public static IReadOnlyList<Alert> Refresh(
            IEnumerable<Asset> assets)
        {
            var assetList = assets?
                .Where(asset =>
                    asset != null &&
                    !string.Equals(
                        asset.Status,
                        "Arxivdə",
                        StringComparison.OrdinalIgnoreCase))
                .ToList()
                ?? new List<Asset>();

            var rules = LoadRulesSafely();

            int warrantyDays = ResolveThreshold(
                rules,
                DefaultWarrantyDays,
                "warranty",
                "zəmanət",
                "zemanet");

            int maintenanceDays = ResolveThreshold(
                rules,
                DefaultMaintenanceDays,
                "maintenance",
                "texniki",
                "service");

            var alerts = new List<Alert>();
            DateTime today = DateTime.Today;

            foreach (var asset in assetList)
            {
                AddWarrantyAlert(
                    alerts,
                    asset,
                    today,
                    warrantyDays);

                AddMaintenanceAlert(
                    alerts,
                    asset,
                    today,
                    maintenanceDays);
            }

            CurrentAlerts = alerts
                .OrderBy(alert => alert.Type)
                .ThenBy(alert => alert.Timestamp)
                .ToList();

            NotificationCenterService.SyncBusinessAlerts(
                CurrentAlerts);

            return CurrentAlerts;
        }

        public static IReadOnlyList<Asset> GetWarrantyAssets(
            IEnumerable<Asset> assets)
        {
            var ids = CurrentAlerts
                .Where(alert =>
                    alert.Category == AlertCategory.Warranty &&
                    alert.TargetType == NavigationTargetType.Asset)
                .Select(alert => alert.TargetId)
                .ToHashSet();

            return assets?
                .Where(asset => asset != null && ids.Contains(asset.Id))
                .ToList()
                ?? new List<Asset>();
        }

        public static IReadOnlyList<Asset> GetMaintenanceAssets(
            IEnumerable<Asset> assets)
        {
            var ids = CurrentAlerts
                .Where(alert =>
                    alert.Category == AlertCategory.Maintenance &&
                    alert.TargetType == NavigationTargetType.Asset)
                .Select(alert => alert.TargetId)
                .ToHashSet();

            return assets?
                .Where(asset => asset != null && ids.Contains(asset.Id))
                .ToList()
                ?? new List<Asset>();
        }

        private static void AddWarrantyAlert(
            ICollection<Alert> alerts,
            Asset asset,
            DateTime today,
            int thresholdDays)
        {
            if (asset.WarrantyExpirationDate <= DateTime.MinValue)
                return;

            int days =
                (asset.WarrantyExpirationDate.Date - today).Days;

            if (days > thresholdDays)
                return;

            bool expired = days < 0;

            alerts.Add(new Alert
            {
                Category = AlertCategory.Warranty,
                SourceKey = $"warranty:{asset.Id}",
                Type = expired
                    ? AlertType.Critical
                    : AlertType.Warning,
                TargetType = NavigationTargetType.Asset,
                TargetId = asset.Id,
                Title = expired
                    ? "Zəmanət müddəti bitib"
                    : "Zəmanət müddəti yaxınlaşır",
                Message = expired
                    ? $"{DisplayAsset(asset)} üçün zəmanət {Math.Abs(days)} gün əvvəl bitib."
                    : $"{DisplayAsset(asset)} üçün zəmanətin bitməsinə {days} gün qalıb.",
                Timestamp = DateTime.Now
            });
        }

        private static void AddMaintenanceAlert(
            ICollection<Alert> alerts,
            Asset asset,
            DateTime today,
            int thresholdDays)
        {
            DateTime? lastMaintenance =
                asset.MaintenanceHistory?
                    .Where(record =>
                        record != null &&
                        record.MaintenanceDate > DateTime.MinValue)
                    .OrderByDescending(record =>
                        record.MaintenanceDate)
                    .Select(record =>
                        (DateTime?)record.MaintenanceDate.Date)
                    .FirstOrDefault();

            DateTime? baseline =
                lastMaintenance ??
                (asset.PurchaseDate > DateTime.MinValue
                    ? asset.PurchaseDate.Date
                    : null);

            if (!baseline.HasValue)
                return;

            int daysSince =
                (today - baseline.Value.Date).Days;

            if (daysSince < thresholdDays)
                return;

            bool critical =
                daysSince >= thresholdDays + 90;

            string detail = lastMaintenance.HasValue
                ? $"Son texniki xidmətdən {daysSince} gün keçib."
                : $"Alış tarixindən {daysSince} gün keçib və texniki xidmət qeydi yoxdur.";

            alerts.Add(new Alert
            {
                Category = AlertCategory.Maintenance,
                SourceKey = $"maintenance:{asset.Id}",
                Type = critical
                    ? AlertType.Critical
                    : AlertType.Warning,
                TargetType = NavigationTargetType.Asset,
                TargetId = asset.Id,
                Title = critical
                    ? "Texniki xidmət gecikib"
                    : "Texniki xidmət vaxtıdır",
                Message = $"{DisplayAsset(asset)}. {detail}",
                Timestamp = DateTime.Now
            });
        }

        private static int ResolveThreshold(
            IEnumerable<AlertRule> rules,
            int fallback,
            params string[] keywords)
        {
            var rule = rules.FirstOrDefault(candidate =>
                candidate?.ThresholdValue > 0 &&
                keywords.Any(keyword =>
                    candidate.RuleName?.IndexOf(
                        keyword,
                        StringComparison.OrdinalIgnoreCase) >= 0));

            if (rule?.ThresholdValue is decimal threshold)
                return Math.Max(1, (int)threshold);

            return fallback;
        }

        private static List<AlertRule> LoadRulesSafely()
        {
            try
            {
                return DataAccess.GetAlertRules()
                    ?? new List<AlertRule>();
            }
            catch
            {
                return new List<AlertRule>();
            }
        }

        private static string DisplayAsset(Asset asset)
        {
            string name =
                string.IsNullOrWhiteSpace(asset.VesaitinAdi)
                    ? "Vəsait"
                    : asset.VesaitinAdi.Trim();

            return string.IsNullOrWhiteSpace(asset.VesaitinKodu)
                ? name
                : $"{asset.VesaitinKodu} • {name}";
        }

        public static void MarkAsRead(Guid alertId)
        {
            var alert = CurrentAlerts.FirstOrDefault(
                candidate => candidate.Id == alertId);

            if (alert != null)
                alert.IsRead = true;
        }
    }
}
