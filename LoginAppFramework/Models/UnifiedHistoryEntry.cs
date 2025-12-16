using System;

namespace LoginAppFramework
{
    public class UnifiedHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public string Description { get; set; }
        public string ChangedBy { get; set; }
        public string Status { get; set; }
        public string Details { get; set; } // This will hold the raw JSON
    }
}