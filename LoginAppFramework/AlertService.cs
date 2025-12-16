// FILE: AlertService.cs

using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public static class AlertService
    {
        public static List<Alert> CurrentAlerts { get; private set; } = new();

        

        

        

        private static void ProcessAssetEolRule(AlertRule rule)
        {
            if (!rule.ThresholdValue.HasValue) return;
            var assets = AppData.GetAssets().Where(a => a.PurchaseDate < DateTime.Now.AddYears(-(int)rule.ThresholdValue.Value));
            foreach (var asset in assets)
            {
                CurrentAlerts.Add(new Alert
                {
                    Type = (AlertType)rule.AlertType,
                    TargetType = (NavigationTargetType)rule.TargetType,
                    TargetId = asset.Id,
                    Title = rule.AlertTitle,
                    Message = rule.AlertMessageTemplate
                                    .Replace("{AssetName}", asset.Name)
                                    .Replace("{ThresholdValue}", rule.ThresholdValue.Value.ToString())
                });
            }
        }

        public static void MarkAsRead(Guid alertId)
        {
            var alert = CurrentAlerts.FirstOrDefault(a => a.Id == alertId);
            if (alert != null)
            {
                alert.IsRead = true;
            }
        }
    }
}