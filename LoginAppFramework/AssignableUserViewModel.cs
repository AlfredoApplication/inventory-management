using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

namespace LoginAppFramework
{
    // NOTE: This class is defined in UserNodeViewModel.cs, ensure it is public
    // You may need to move this class into its own file if it's not already.
    public class AssignableUserViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public Worker Worker { get; }
        public string Name => Worker.per_adiper_soyadi;
        public string Position => Worker.pgk_gorev_adi;
        public string Department => Worker.pdp_adi;

        // FIX: The IsInactive property has been removed.

        public string TotalAssetValueFormatted { get; private set; }
        public int AssignedAssetsCount { get; private set; }

        private bool _isDragOver;
        public bool IsDragOver
        {
            get => _isDragOver;
            set { _isDragOver = value; OnPropertyChanged(); }
        }

        public AssignableUserViewModel(Worker worker, IReadOnlyCollection<Asset> allAssets)
        {
            Worker = worker;
            UpdateAssetSummary(allAssets);
        }

        public void UpdateAssetSummary(IReadOnlyCollection<Asset> allAssets)
        {
            var assignedAssets = allAssets.Where(a => a.AssignedUser == Name).ToList();
            AssignedAssetsCount = assignedAssets.Count;
            decimal totalValue = assignedAssets.Sum(a => a.CurrentValue);
            TotalAssetValueFormatted = totalValue.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        }

        public void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}