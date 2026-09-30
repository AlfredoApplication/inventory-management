using System;
using System.Globalization;
using System.Linq;

namespace LoginAppFramework
{
    public static class AssetFormValidator
    {
        public static bool TryParsePurchaseCost(
            string text,
            out decimal value,
            out string errorMessage)
        {
            value = 0;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(text))
                return true;

            string normalized = text
                .Trim()
                .Replace(" ", string.Empty)
                .Replace(',', '.');

            if (normalized.Count(ch => ch == '.') > 1 ||
                !decimal.TryParse(
                    normalized,
                    NumberStyles.AllowDecimalPoint |
                    NumberStyles.AllowLeadingSign,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                errorMessage =
                    "Alış qiyməti düzgün rəqəm formatında deyil. Məsələn: 1250,50";
                return false;
            }

            if (value < 0)
            {
                errorMessage = "Alış qiyməti mənfi ola bilməz.";
                return false;
            }

            return true;
        }

        public static bool TryParseUsefulLife(
            string text,
            out int value,
            out string errorMessage)
        {
            value = 0;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(text))
                return true;

            if (!int.TryParse(
                    text.Trim(),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out value))
            {
                errorMessage =
                    "İstifadə müddəti tam ədəd olmalıdır. Məsələn: 3";
                return false;
            }

            if (value < 0)
            {
                errorMessage =
                    "İstifadə müddəti mənfi ola bilməz. 0 dəyəri 'təyin edilməyib' kimi qəbul olunur.";
                return false;
            }

            return true;
        }

        public static bool ValidateDates(
            DateTime? purchaseDate,
            DateTime? warrantyExpirationDate,
            out string errorMessage)
        {
            errorMessage = null;

            if (!purchaseDate.HasValue)
            {
                errorMessage = "Alınma tarixi tələb olunur.";
                return false;
            }

            if (warrantyExpirationDate.HasValue &&
                warrantyExpirationDate.Value.Date < purchaseDate.Value.Date)
            {
                errorMessage =
                    "Zəmanət bitmə tarixi alınma tarixindən əvvəl ola bilməz.";
                return false;
            }

            return true;
        }

        public static bool IsPotentialPurchaseCostText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return true;

            string normalized = text.Replace(" ", string.Empty);

            if (normalized.Any(ch =>
                    !char.IsDigit(ch) &&
                    ch != ',' &&
                    ch != '.'))
            {
                return false;
            }

            return normalized.Count(ch => ch == ',' || ch == '.') <= 1;
        }

        public static bool IsPotentialUsefulLifeText(string text)
            => string.IsNullOrEmpty(text) || text.All(char.IsDigit);
    }
}
