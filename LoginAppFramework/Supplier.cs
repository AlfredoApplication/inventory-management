using System.ComponentModel.DataAnnotations;

namespace LoginAppFramework
{
    public class Supplier
    {
        [Key]
        public int Id { get; set; }
        public string Name { get; set; }
        public string ContactPerson { get; set; }
        public string PhoneNumber { get; set; }
        public string Website { get; set; }
    }
}