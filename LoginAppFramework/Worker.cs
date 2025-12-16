// In Worker.cs

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoginAppFramework
{
    public class Worker
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        public string per_kod { get; set; }
        public string per_adiper_soyadi { get; set; }
        public string pgk_gorev_adi { get; set; }
        public string pdp_adi { get; set; }

        // ADD THIS PROPERTY BACK
        public bool IsActive { get; set; }

        public Worker()
        {
            // Set the default for new workers created *manually* in the app
            IsActive = true;
        }

        public Worker Clone() => (Worker)this.MemberwiseClone();
    }
}