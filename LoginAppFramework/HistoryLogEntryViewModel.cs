using System;

namespace LoginAppFramework
{
    public class HistoryLogEntryViewModel : AssignmentHistoryEntry
    {
        public string AssetName { get; set; }
        public string AssetSerialNumber { get; set; }
        public DateTime ChangeDateOnly => ChangeDate.Date;
    }
}