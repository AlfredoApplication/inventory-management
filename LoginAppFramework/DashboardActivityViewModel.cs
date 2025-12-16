// NEW FILE: DashboardActivityViewModel.cs
namespace LoginAppFramework
{
    /// <summary>
    /// A ViewModel used to represent a single line item
    /// in the "Recent Activity" feed on the Dashboard.
    /// </summary>
    public class DashboardActivityViewModel
    {
        public string AssetName { get; set; }
        public AssignmentHistoryEntry HistoryEntry { get; set; }
        public int AssetId { get; set; }

        public string Description
        {
            get
            {
                // Using a modern C# switch expression
                return HistoryEntry.Action switch
                {
                    AssignmentAction.Assigned => $"was assigned to {HistoryEntry.ToWorkerName}",
                    AssignmentAction.Unassigned => $"was unassigned from {HistoryEntry.FromWorkerName}",
                    AssignmentAction.Reassigned => $"was reassigned from {HistoryEntry.FromWorkerName} to {HistoryEntry.ToWorkerName}",
                    _ => "had an unknown status change",
                };
            }
        }
    }
}