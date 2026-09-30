using System;
using System.Collections.Generic;
using System.Globalization;

namespace LoginAppFramework
{
    public static class AssetDeletionMetadata
    {
        public const int RetentionDays = 30;

        private const string DeletedAtKey =
            "__inventory_system_deleted_at_utc";

        private const string DeletedByKey =
            "__inventory_system_deleted_by";

        public static bool IsDeleted(Asset asset)
            => GetDeletedAtUtc(asset).HasValue;

        public static DateTime? GetDeletedAtUtc(Asset asset)
        {
            if (asset?.CustomFields == null ||
                !asset.CustomFields.TryGetValue(
                    DeletedAtKey,
                    out string raw) ||
                string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            return DateTime.TryParse(
                    raw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out DateTime value)
                ? value.ToUniversalTime()
                : null;
        }

        public static string GetDeletedBy(Asset asset)
        {
            if (asset?.CustomFields == null)
                return null;

            return asset.CustomFields.TryGetValue(
                    DeletedByKey,
                    out string value)
                ? value
                : null;
        }

        public static DateTime? GetExpiresAtUtc(Asset asset)
            => GetDeletedAtUtc(asset)?
                .AddDays(RetentionDays);

        public static int GetDaysRemaining(
            Asset asset,
            DateTime nowUtc)
        {
            DateTime? expiresAt = GetExpiresAtUtc(asset);
            if (!expiresAt.HasValue)
                return RetentionDays;

            double remaining =
                (expiresAt.Value - nowUtc.ToUniversalTime()).TotalDays;

            return Math.Max(
                0,
                (int)Math.Ceiling(remaining));
        }

        public static bool ShouldPurge(
            Asset asset,
            DateTime nowUtc)
        {
            DateTime? deletedAt = GetDeletedAtUtc(asset);

            return deletedAt.HasValue &&
                   deletedAt.Value <=
                   nowUtc.ToUniversalTime().AddDays(-RetentionDays);
        }

        public static void MarkDeleted(
            Asset asset,
            string deletedBy,
            DateTime nowUtc)
        {
            if (asset == null)
                throw new ArgumentNullException(nameof(asset));

            asset.CustomFields ??=
                new Dictionary<string, string>();

            if (IsDeleted(asset))
                return;

            asset.CustomFields[DeletedAtKey] =
                nowUtc.ToUniversalTime()
                    .ToString(
                        "O",
                        CultureInfo.InvariantCulture);

            asset.CustomFields[DeletedByKey] =
                string.IsNullOrWhiteSpace(deletedBy)
                    ? "Sistem"
                    : deletedBy.Trim();
        }

        public static void Restore(Asset asset)
        {
            if (asset?.CustomFields == null)
                return;

            asset.CustomFields.Remove(DeletedAtKey);
            asset.CustomFields.Remove(DeletedByKey);
        }
    }
}
