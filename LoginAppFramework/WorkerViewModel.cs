using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LoginAppFramework
{
    public class WorkerViewModel : INotifyPropertyChanged
    {
        private readonly Worker _worker;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public string Name => _worker.per_adiper_soyadi;
        public string Position => _worker.pgk_gorev_adi;
        public string Department => _worker.pdp_adi;
        public bool IsActive => _worker.IsActive;
        public string StatusText => _worker.IsActive ? "Aktiv" : "Qeyri-Aktiv";
        public int AssignedAssetsCount { get; private set; }

        // Formatted String Properties (for display)
        public string TotalAssetValue { get; private set; }
        public string TotalPurchaseCost { get; private set; }
        public string TotalDepreciation { get; private set; }
        public string MonthlyDepreciation { get; private set; }

        // --- NEW RAW DECIMAL PROPERTIES (for calculations) ---
        public decimal RawTotalCurrentValue { get; private set; }
        public decimal RawTotalPurchaseCost { get; private set; }
        public decimal RawTotalDepreciation { get; private set; }
        public decimal RawMonthlyDepreciation { get; private set; }

        public WorkerViewModel(Worker worker, List<Asset> assignedAssets)
        {
            _worker = worker;
            AssignedAssetsCount = assignedAssets.Count;
            var culture = CultureInfo.GetCultureInfo("az-Latn-AZ");

            if (assignedAssets.Any())
            {
                RawTotalCurrentValue = assignedAssets.Sum(a => a.CurrentValue);
                RawTotalPurchaseCost = assignedAssets.Sum(a => a.PurchaseCost);
                RawTotalDepreciation = RawTotalPurchaseCost - RawTotalCurrentValue;
                RawMonthlyDepreciation = assignedAssets.Sum(a => a.MonthlyDepreciation);

                TotalAssetValue = RawTotalCurrentValue.ToString("C", culture);
                TotalPurchaseCost = RawTotalPurchaseCost.ToString("C", culture);
                TotalDepreciation = RawTotalDepreciation.ToString("C", culture);
                MonthlyDepreciation = RawMonthlyDepreciation.ToString("C", culture);
            }
            else
            {
                // Set both raw and formatted values to 0 if no assets
                RawTotalCurrentValue = 0;
                RawTotalPurchaseCost = 0;
                RawTotalDepreciation = 0;
                RawMonthlyDepreciation = 0;
                TotalAssetValue = 0.ToString("C", culture);
                TotalPurchaseCost = 0.ToString("C", culture);
                TotalDepreciation = 0.ToString("C", culture);
                MonthlyDepreciation = 0.ToString("C", culture);
            }
        }
        public Worker GetModel() => _worker;
    }
}