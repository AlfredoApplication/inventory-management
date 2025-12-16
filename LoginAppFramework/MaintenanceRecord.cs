using System;

namespace LoginAppFramework
{
    public class MaintenanceRecord
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime MaintenanceDate { get; set; }
        public MaintenanceType MaintenanceType { get; set; }
        public string Description { get; set; }
        public decimal Cost { get; set; }
        public string PerformedBy { get; set; }
    }
}