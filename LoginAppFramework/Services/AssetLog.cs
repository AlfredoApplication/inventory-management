using System;

namespace LoginAppFramework
{
    public class AssetLog
    {
        public int Id { get; set; }
        public string VesaitinKodu { get; set; }
        public string VesaitinAdi { get; set; }
        public string ITAvadanliqlarininSeriyaNomresi { get; set; }
        public string Kateqoriya { get; set; }
        public string TehkimOlunanEmekdas { get; set; }
        public string Vezifesi { get; set; }
        public string BolmeShobeDepartment { get; set; }
        public string YerleshmeYeri { get; set; }
        public string Erazi { get; set; }
        public string status { get; set; }
        public DateTime ChangeDate { get; set; }
        public string ChangeDetails { get; set; }
        public string ChangedBy { get; set; }
    }
}