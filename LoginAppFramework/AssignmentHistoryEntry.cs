using System;

namespace LoginAppFramework
{
    public class AssignmentHistoryEntry
    {
        public int Id { get; set; } // Primary Key
        public int AssetId { get; set; } // Foreign Key to Asset
        public AssignmentAction Action { get; set; }
        public string FromWorkerName { get; set; }
        public string ToWorkerName { get; set; }
        public string ChangedBy { get; set; }
        public DateTime ChangeDate { get; set; }

        // This is a "navigation property" that tells EF about the relationship
        public virtual Asset Asset { get; set; }
    }

    // The enum remains the same
    public enum AssignmentAction { Assigned, Unassigned, Reassigned }
}