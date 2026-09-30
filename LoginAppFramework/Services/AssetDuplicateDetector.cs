using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public enum AssetDuplicateField
    {
        Code,
        SerialNumber
    }

    public sealed class AssetDuplicateConflict
    {
        public AssetDuplicateField Field { get; init; }
        public Asset Asset { get; init; }
        public bool IsDeleted =>
            AssetDeletionMetadata.IsDeleted(Asset);

        public string Message
        {
            get
            {
                string fieldName =
                    Field == AssetDuplicateField.Code
                        ? "Vəsait kodu"
                        : "Seriya nömrəsi";

                string value =
                    Field == AssetDuplicateField.Code
                        ? Asset?.VesaitinKodu
                        : Asset?.ITAvadanliqlarininSeriyaNomresi;

                string assetName =
                    string.IsNullOrWhiteSpace(Asset?.VesaitinAdi)
                        ? "adsız vəsait"
                        : Asset.VesaitinAdi;

                return IsDeleted
                    ? $"{fieldName} '{value}' Silinənlər bölməsindəki '{assetName}' vəsaitində istifadə olunur. Həmin vəsaiti bərpa edin və ya həmişəlik silin."
                    : $"{fieldName} '{value}' artıq '{assetName}' vəsaitində istifadə olunur.";
            }
        }
    }

    public static class AssetDuplicateDetector
    {
        public static AssetDuplicateConflict FindCodeConflict(
            IEnumerable<Asset> assets,
            string code,
            int currentAssetId = 0)
            => Find(
                assets,
                code,
                currentAssetId,
                AssetDuplicateField.Code,
                asset => asset.VesaitinKodu);

        public static AssetDuplicateConflict FindSerialConflict(
            IEnumerable<Asset> assets,
            string serial,
            int currentAssetId = 0)
            => Find(
                assets,
                serial,
                currentAssetId,
                AssetDuplicateField.SerialNumber,
                asset => asset.ITAvadanliqlarininSeriyaNomresi);

        private static AssetDuplicateConflict Find(
            IEnumerable<Asset> assets,
            string candidate,
            int currentAssetId,
            AssetDuplicateField field,
            Func<Asset, string> selector)
        {
            string normalized = Normalize(candidate);

            if (normalized == null)
                return null;

            var match = assets?
                .Where(asset =>
                    asset != null &&
                    (currentAssetId <= 0 ||
                     asset.Id != currentAssetId))
                .FirstOrDefault(asset =>
                    string.Equals(
                        Normalize(selector(asset)),
                        normalized,
                        StringComparison.OrdinalIgnoreCase));

            return match == null
                ? null
                : new AssetDuplicateConflict
                {
                    Field = field,
                    Asset = match
                };
        }

        private static string Normalize(string value)
            => string.IsNullOrWhiteSpace(value)
                ? null
                : value.Trim();
    }
}
