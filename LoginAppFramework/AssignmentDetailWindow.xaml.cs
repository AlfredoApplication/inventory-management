using System.Linq;
using System.Windows;

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

            EventTitleTextBlock.Text = historyEntry.Action switch
            {
                AssignmentAction.Assigned => "Vəsait təhkim edildi",
                AssignmentAction.Unassigned => "Təhkim ləğv edildi",
                _ => "Təhkim dəyişdirildi"
            };
            EventSubtitleTextBlock.Text = $"{historyEntry.ChangedBy} tərəfindən • {historyEntry.ChangeDate:g}";
            EventTitleTextBlock.Foreground = historyEntry.Action switch
            {
                AssignmentAction.Assigned => (System.Windows.Media.Brush)FindResource("SuccessBrush"),
                AssignmentAction.Unassigned => (System.Windows.Media.Brush)FindResource("DangerBrush"),
                _ => (System.Windows.Media.Brush)FindResource("PrimaryBrush")
            };
            if (asset != null) { AssetNameTextBlock.Text = asset.Name; AssetCategoryTextBlock.Text = asset.Category; AssetSerialTextBlock.Text = $"Seriya: {asset.SerialNumber}"; }
            if (worker != null) { UserNameTextBlock.Text = worker.per_adiper_soyadi; UserPositionTextBlock.Text = worker.pgk_gorev_adi; UserDepartmentTextBlock.Text = $"Departament: {worker.pdp_adi}"; }
            else { UserNameTextBlock.Text = relevantWorkerName; UserPositionTextBlock.Text = "(İstifadəçi tapılmadı)"; UserDepartmentTextBlock.Text = string.Empty; }
        }
    }
}