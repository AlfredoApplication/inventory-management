using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace LoginAppFramework
{
    public sealed class AssetAuditSnapshot
    {
        public Asset Asset { get; init; }
        public int? WorkerId { get; init; }
        public string WorkerName { get; init; }

        public static AssetAuditSnapshot FromDeletedLog(AssetLog log)
        {
            if (log == null) throw new ArgumentNullException(nameof(log));

            var values = ExtractSnapshotValues(log.ChangeDetails, log.status);
            var asset = new Asset
            {
                VesaitinKodu = GetString(values, "VesaitinKodu") ?? log.VesaitinKodu,
                VesaitinAdi = GetString(values, "VesaitinAdi") ?? log.VesaitinAdi,
                ITAvadanliqlarininSeriyaNomresi = GetString(values, "ITAvadanliqlarininSeriyaNomresi") ?? log.ITAvadanliqlarininSeriyaNomresi,
                Kateqoriya = GetString(values, "Kateqoriya") ?? log.Kateqoriya,
                YerleshmeYeri = GetString(values, "YerleshmeYeri") ?? log.YerleshmeYeri,
                Erazi = GetString(values, "Erazi") ?? log.Erazi,
                Status = GetString(values, "Status") ?? GetString(values, "status") ?? "Anbarda",
                PurchaseCost = GetDecimal(values, "PurchaseCost") ?? 0m,
                PurchaseDate = GetDateTime(values, "PurchaseDate") ?? DateTime.MinValue,
                UsefulLifeInYears = GetInt(values, "UsefulLifeInYears") ?? 0,
                Supplier = GetString(values, "Supplier"),
                WarrantyExpirationDate = GetDateTime(values, "WarrantyExpirationDate") ?? DateTime.MinValue,
                LifecycleStatus = GetEnum<AssetLifecycleStatus>(values, "LifecycleStatus") ?? default,
                CustomFields = GetJson<Dictionary<string, string>>(values, "CustomFields") ?? new Dictionary<string, string>(),
                MaintenanceHistory = GetJson<List<MaintenanceRecord>>(values, "MaintenanceHistory") ?? new List<MaintenanceRecord>()
            };

            string workerName =
                GetString(values, "TehkimOlunanEmekdas") ??
                log.TehkimOlunanEmekdas;

            return new AssetAuditSnapshot
            {
                Asset = asset,
                WorkerId = GetInt(values, "WorkerId"),
                WorkerName = workerName
            };
        }

        private static Dictionary<string, JsonElement> ExtractSnapshotValues(string json, string status)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

            using var document = JsonDocument.Parse(json);
            JsonElement root = document.RootElement;

            if (string.Equals(status, "Dəyişdirilən", StringComparison.OrdinalIgnoreCase) &&
                root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("OldValues", out var oldValues))
            {
                root = UnwrapJson(oldValues);
            }
            else
            {
                root = UnwrapJson(root);
            }

            var result = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            if (root.ValueKind != JsonValueKind.Object) return result;

            foreach (var property in root.EnumerateObject())
            {
                result[property.Name] = property.Value.Clone();
            }

            return result;
        }

        private static JsonElement UnwrapJson(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String) return element;

            string inner = element.GetString();
            if (string.IsNullOrWhiteSpace(inner)) return element;

            using var nested = JsonDocument.Parse(inner);
            return nested.RootElement.Clone();
        }

        private static string GetString(Dictionary<string, JsonElement> values, string key)
        {
            if (!values.TryGetValue(key, out var element) || element.ValueKind == JsonValueKind.Null)
                return null;

            return element.ValueKind == JsonValueKind.String ? element.GetString() : element.ToString();
        }

        private static int? GetInt(Dictionary<string, JsonElement> values, string key)
        {
            if (!values.TryGetValue(key, out var element) || element.ValueKind == JsonValueKind.Null)
                return null;

            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out int number))
                return number;

            return int.TryParse(GetString(values, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? parsed
                : null;
        }

        private static decimal? GetDecimal(Dictionary<string, JsonElement> values, string key)
        {
            if (!values.TryGetValue(key, out var element) || element.ValueKind == JsonValueKind.Null)
                return null;

            if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out decimal number))
                return number;

            return decimal.TryParse(
                GetString(values, key),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out decimal parsed)
                ? parsed
                : null;
        }

        private static DateTime? GetDateTime(Dictionary<string, JsonElement> values, string key)
        {
            string value = GetString(values, key);
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTime.TryParse(value, out parsed) ? parsed : null;
        }

        private static TEnum? GetEnum<TEnum>(Dictionary<string, JsonElement> values, string key)
            where TEnum : struct, Enum
        {
            var intValue = GetInt(values, key);
            if (intValue.HasValue && Enum.IsDefined(typeof(TEnum), intValue.Value))
                return (TEnum)Enum.ToObject(typeof(TEnum), intValue.Value);

            string value = GetString(values, key);
            return Enum.TryParse<TEnum>(value, true, out var parsed) ? parsed : null;
        }

        private static T GetJson<T>(Dictionary<string, JsonElement> values, string key)
        {
            if (!values.TryGetValue(key, out var element) || element.ValueKind == JsonValueKind.Null)
                return default;

            try
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    string innerJson = element.GetString();
                    if (string.IsNullOrWhiteSpace(innerJson)) return default;
                    return JsonSerializer.Deserialize<T>(innerJson);
                }

                return element.Deserialize<T>();
            }
            catch
            {
                return default;
            }
        }
    }
}
