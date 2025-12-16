using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LoginAppFramework
{
    // This is now the ONE AND ONLY definition for this class.
    public class FilterOption<T> : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private bool _isChecked;
        public string DisplayName { get; set; }
        public T Value { get; set; }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged(); // CallerMemberName will automatically use "IsChecked"
                }
            }
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}