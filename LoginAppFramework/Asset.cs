using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace LoginAppFramework
{
    public class Asset : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public string VesaitinKodu { get; set; }
        public string VesaitinAdi { get; set; }
        public string ITAvadanliqlarininSeriyaNomresi { get; set; }
        public string Kateqoriya { get; set; }

        // Legacy compatibility snapshots. New application logic must use WorkerId + Worker.
        // The existing database/audit trigger still references these physical columns, so they
        // remain mapped until a dedicated database migration removes them safely.
        [Column("TehkimOlunanEmekdas")]
        public string LegacyAssignedWorkerName { get; set; }

        [Column("Vezifesi")]
        public string LegacyAssignedWorkerPosition { get; set; }

        [Column("BolmeShobeDepartment")]
        public string LegacyAssignedWorkerDepartment { get; set; }

        public string YerleshmeYeri { get; set; }
        public string Erazi { get; set; }
        public string Status { get; set; }
        public decimal PurchaseCost { get; set; }
        public DateTime PurchaseDate { get; set; }
        public int UsefulLifeInYears { get; set; }
        public string Supplier { get; set; }
        public DateTime WarrantyExpirationDate { get; set; }

        // --- NEW FOREIGN KEY AND NAVIGATION PROPERTY ---
        public int? WorkerId { get; set; }
        public virtual Worker Worker { get; set; }

        public virtual ICollection<AssignmentHistoryEntry> History { get; set; } = new List<AssignmentHistoryEntry>();
        public AssetLifecycleStatus LifecycleStatus { get; set; }

        public Dictionary<string, string> CustomFields { get; set; } = new Dictionary<string, string>();
        public List<MaintenanceRecord> MaintenanceHistory { get; set; } = new List<MaintenanceRecord>();

        [NotMapped] public string Name { get => VesaitinAdi; set => VesaitinAdi = value; }
        [NotMapped] public string SerialNumber { get => ITAvadanliqlarininSeriyaNomresi; set => ITAvadanliqlarininSeriyaNomresi = value; }
        [NotMapped] public string Category { get => Kateqoriya; set => Kateqoriya = value; }

        [NotMapped] public string AssignedUser => Worker?.per_adiper_soyadi;
        [NotMapped] public string AssignedPosition => Worker?.pgk_gorev_adi;
        [NotMapped] public string Department => Worker?.pdp_adi;

        public void AssignWorker(Worker worker)
        {
            if (worker == null)
            {
                ClearWorkerAssignment();
                return;
            }

            WorkerId = worker.Id;
            Worker = worker;

            // Keep the old physical columns synchronized only as compatibility snapshots.
            LegacyAssignedWorkerName = worker.per_adiper_soyadi;
            LegacyAssignedWorkerPosition = worker.pgk_gorev_adi;
            LegacyAssignedWorkerDepartment = worker.pdp_adi;

            Status = "İstifadədədir";
            OnPropertyChanged(nameof(AssignedUser));
            OnPropertyChanged(nameof(AssignedPosition));
            OnPropertyChanged(nameof(Department));
        }

        public void ClearWorkerAssignment(string nextStatus = null)
        {
            WorkerId = null;
            Worker = null;

            LegacyAssignedWorkerName = null;
            LegacyAssignedWorkerPosition = null;
            LegacyAssignedWorkerDepartment = null;

            if (!string.IsNullOrWhiteSpace(nextStatus))
                Status = nextStatus;
            else if (Status == "İstifadədədir")
                Status = "Anbarda";

            OnPropertyChanged(nameof(AssignedUser));
            OnPropertyChanged(nameof(AssignedPosition));
            OnPropertyChanged(nameof(Department));
        }

        [NotMapped] public Brush StatusColor => Status switch { "İstifadədədir" => Brushes.Green, "Anbarda" => Brushes.DodgerBlue, "Arxivdə" => Brushes.SlateGray, "İstifadəyə yararsız" => Brushes.Black, _ => Brushes.Gray };
        [NotMapped] public bool HasUsefulLife => UsefulLifeInYears > 0 && PurchaseDate > DateTime.MinValue;
        [NotMapped] public string UsefulLifeDisplay => UsefulLifeInYears > 0 ? $"{UsefulLifeInYears} il" : "Təyin edilməyib";
        [NotMapped] public decimal AnnualDepreciation => UsefulLifeInYears > 0 ? PurchaseCost / UsefulLifeInYears : 0;
        [NotMapped] public decimal MonthlyDepreciation => AnnualDepreciation / 12;
        [NotMapped] public decimal CurrentValue { get { if (UsefulLifeInYears <= 0 || PurchaseCost <= 0) return PurchaseCost; decimal ageInYears = (decimal)(DateTime.Now - PurchaseDate).TotalDays / 365.25m; decimal totalDepreciation = AnnualDepreciation * ageInYears; decimal val = PurchaseCost - totalDepreciation; return val < 0 ? 0 : val; } }
        [NotMapped] public decimal TotalDepreciation => (PurchaseCost > CurrentValue) ? (PurchaseCost - CurrentValue) : 0;

        [NotMapped]
        public DateTime? EndOfLifeDate
        {
            get
            {
                if (!HasUsefulLife) return null;
                try
                {
                    return PurchaseDate.AddYears(UsefulLifeInYears);
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }
        }

        [NotMapped] public string EndOfLifeDateDisplay => EndOfLifeDate?.ToString("yyyy-MM-dd") ?? "Təyin edilməyib";
        [NotMapped] public bool IsEndOfLife => EndOfLifeDate is DateTime endOfLifeDate && DateTime.Today >= endOfLifeDate.Date;

        [NotMapped]
        public string ParentCategory
        {
            get
            {
                static IEnumerable<DeviceCategory> FlattenCategories(IEnumerable<DeviceCategory> categories)
                {
                    return categories.SelectMany(c => new[] { c }.Concat(FlattenCategories(c.Subcategories)));
                }
                var allCategoriesFlat = FlattenCategories(AppData.GetHierarchicalCategories()).ToList();
                var currentNode = allCategoriesFlat.FirstOrDefault(c => c.Name == this.Kateqoriya);
                if (currentNode?.ParentId != null)
                {
                    var parentNode = allCategoriesFlat.FirstOrDefault(p => p.Id == currentNode.ParentId);
                    return parentNode?.Name;
                }
                return null;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public Asset Clone()
        {
            var clone = (Asset)this.MemberwiseClone();
            clone.CustomFields = new Dictionary<string, string>(this.CustomFields);
            clone.History = this.History.Select(h => h).ToList();
            clone.MaintenanceHistory = this.MaintenanceHistory.Select(m => m).ToList();
            return clone;
        }
    }
}