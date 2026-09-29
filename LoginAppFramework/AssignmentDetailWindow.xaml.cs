using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class AssignmentDetailWindow : Window
    {
        public AssignmentDetailWindow(HistoryLogEntryViewModel historyEntry)
        {
            InitializeComponent();
            string relevantWorkerName = historyEntry.Action == AssignmentAction.Unassigned ? historyEntry.FromWorkerName : historyEntry.ToWorkerName;

            // FIX: Using AppData cache instead of direct DataAccess calls.
            var asset = AppData.GetAssets().FirstOrDefault(a => a.Name == historyEntry.AssetName);
            var worker = AppData.GetWorkers().FirstOrDefault(w => w.per_adiper_soyadi == relevantWorkerName);

            EventTitleTextBlock.Text = $"Asset {historyEntry.Action}";
            EventSubtitleTextBlock.Text = $"by {historyEntry.ChangedBy} on {historyEntry.ChangeDate:g}";
            if (historyEntry.Action == AssignmentAction.Assigned) EventTitleTextBlock.Foreground = Brushes.Green;
            else if (historyEntry.Action == AssignmentAction.Unassigned) EventTitleTextBlock.Foreground = Brushes.IndianRed;
            else EventTitleTextBlock.Foreground = Brushes.RoyalBlue;
            if (asset != null) { AssetNameTextBlock.Text = asset.Name; AssetCategoryTextBlock.Text = asset.Category; AssetSerialTextBlock.Text = $"SN: {asset.SerialNumber}"; }
            if (worker != null) { UserNameTextBlock.Text = worker.per_adiper_soyadi; UserPositionTextBlock.Text = worker.pgk_gorev_adi; UserDepartmentTextBlock.Text = $"Departament: {worker.pdp_adi}"; }
            else { UserNameTextBlock.Text = relevantWorkerName; UserPositionTextBlock.Text = "(İstifadəçi tapılmadı)"; UserDepartmentTextBlock.Text = string.Empty; }
        }
    }
}