    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations.Schema;

    public class DeviceCategory
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public int? DefaultUsefulLifeYears { get; set; }
        public int? ParentId { get; set; }

        // --- THIS PROPERTY WAS MISSING ---
        public int SortOrder { get; set; }


        [NotMapped]
        public List<DeviceCategory> Subcategories { get; set; } = new List<DeviceCategory>();
    }