using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LoginAppFramework
{
    public class FilterOption : INotifyPropertyChanged
    {
        private bool _isChecked; public string DisplayName { get; set; }
        public object Value { get; set; }
        public bool IsChecked { get => _isChecked; set { _isChecked = value; OnPropertyChanged(); } }
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public class FilterViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged; public event Action FiltersChanged;
        public ObservableCollection<FilterOption> Options { get; private set; }
        public string Title { get; private set; }
        public FilterViewModel(string title, IEnumerable<FilterOption> options)
        {
            Title = title; Options = new ObservableCollection<FilterOption>();
            foreach (var option in options) { option.PropertyChanged += OnOptionCheckedChanged; Options.Add(option); }
        }
        private void OnOptionCheckedChanged(object sender, PropertyChangedEventArgs e) { if (e.PropertyName == "IsChecked") { FiltersChanged?.Invoke(); } }
        public void Clear()
        {
            foreach (var option in Options) { option.PropertyChanged -= OnOptionCheckedChanged; }
            foreach (var option in Options) { option.IsChecked = false; }
            foreach (var option in Options) { option.PropertyChanged += OnOptionCheckedChanged; }
            FiltersChanged?.Invoke();
        }
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}