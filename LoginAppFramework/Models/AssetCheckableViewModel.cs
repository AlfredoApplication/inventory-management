using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LoginAppFramework
{
    public class AssetCheckableViewModel : INotifyPropertyChanged
    {
        public Asset Asset { get; }

        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(); // UI-a dəyişiklik barədə məlumat verir
                }
            }
        }

        // DataGrid-də rahat bindinq üçün Asset xüsusiyyətlərini birbaşa əlçatan edirik
        public string VesaitinKodu => Asset.VesaitinKodu;
        public string VesaitinAdi => Asset.VesaitinAdi;
        public string ITAvadanliqlarininSeriyaNomresi => Asset.ITAvadanliqlarininSeriyaNomresi;
        public string Kateqoriya => Asset.Kateqoriya;
        public Worker Worker => Asset.Worker;
        public string YerleshmeYeri => Asset.YerleshmeYeri;
        public string Erazi => Asset.Erazi;


        public AssetCheckableViewModel(Asset asset)
        {
            Asset = asset;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}