using System;
namespace LoginAppFramework
{
    public class BulkAssetChanges
    {
        public static string ResolveTextChange(
            string text,
            bool clearRequested)
        {
            if (clearRequested)
                return string.Empty;

            return string.IsNullOrWhiteSpace(text)
                ? null
                : text.Trim();
        }

        public string VesaitinKodu { get; set; }
        public string VesaitinAdi { get; set; }
        public string ITAvadanliqlarininSeriyaNomresi { get; set; }
        public string Kateqoriya { get; set; }
        public Worker AssignedWorker { get; set; }
        public string YerleshmeYeri { get; set; }
        public string Erazi { get; set; }
        public string Status { get; set; }
        public decimal? PurchaseCost { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public int? UsefulLifeInYears { get; set; }

        // --- NEW PROPERTIES ADDED ---
        public string Supplier { get; set; }
        public DateTime? WarrantyExpirationDate { get; set; }
    }
}