using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace LoginAppFramework
{
    public class CategoryFilterNodeViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event System.Action FilterChanged;

        private bool? _isChecked = false;
        private bool _isExpanded;
        private readonly CategoryFilterNodeViewModel _parent;

        public string Name { get; }
        public ObservableCollection<CategoryFilterNodeViewModel> Subcategories { get; }

        public bool? IsChecked
        {
            get => _isChecked;
            set => SetIsChecked(value, true, true);
        }

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
                }
            }
        }

        public CategoryFilterNodeViewModel(string name, CategoryFilterNodeViewModel parent = null)
        {
            Name = name;
            _parent = parent;
            Subcategories = new ObservableCollection<CategoryFilterNodeViewModel>();
        }

        private void SetIsChecked(bool? value, bool updateChildren, bool updateParent)
        {
            if (_isChecked == value) return;

            _isChecked = value;

            if (updateChildren && _isChecked.HasValue)
            {
                foreach (var child in Subcategories)
                {
                    child.SetIsChecked(_isChecked, true, false); // Don't let children update the parent
                }
            }

            if (updateParent && _parent != null)
            {
                _parent.VerifyCheckState();
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            FilterChanged?.Invoke();
        }

        private void VerifyCheckState()
        {
            bool? state = null;
            if (Subcategories.All(c => c.IsChecked == true))
                state = true;
            else if (Subcategories.All(c => c.IsChecked == false))
                state = false;
            // Otherwise, it remains indeterminate (null)

            SetIsChecked(state, false, true); // Don't update children, do update parent
        }
    }
}