using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace LoginAppFramework
{
    public enum HistoryEntryType
    {
        AssetChange,
        Assignment
    }

    public class UnifiedHistoryViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public DateTime Timestamp { get; set; }
        public HistoryEntryType EntryType { get; set; }
        public string Description { get; set; }
        public string ChangedBy { get; set; }
        public object OriginalEntry { get; set; }

        private bool _isExpanded;
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

        public ObservableCollection<ChangeDetailViewModel> ChangeDetails { get; }
        public ICommand ToggleExpandCommand { get; }

        public UnifiedHistoryViewModel()
        {
            ChangeDetails = new ObservableCollection<ChangeDetailViewModel>();
            ToggleExpandCommand = new RelayCommand(ToggleExpand);
        }

        private void ToggleExpand()
        {
            IsExpanded = !IsExpanded;

            // Lazy load the details only when the item is expanded for the first time.
            if (IsExpanded && ChangeDetails.Count == 0 && OriginalEntry is AssetLog logEntry)
            {
                ParseAndLoadDetails(logEntry);
            }
        }

        private void ParseAndLoadDetails(AssetLog logEntry)
        {
            var changes = new List<ChangeDetailViewModel>();
            try
            {
                if (string.IsNullOrEmpty(logEntry.ChangeDetails)) return;

                using (JsonDocument doc = JsonDocument.Parse(logEntry.ChangeDetails))
                {
                    if (logEntry.status == "Dəyişdirilən")
                    {
                        var oldValues = doc.RootElement.GetProperty("OldValues").Deserialize<Dictionary<string, JsonElement>>();
                        var newValues = doc.RootElement.GetProperty("NewValues").Deserialize<Dictionary<string, JsonElement>>();
                        var allKeys = oldValues.Keys.Union(newValues.Keys).Distinct().OrderBy(k => k);
                        foreach (var key in allKeys)
                        {
                            string oldValueStr = GetStringValue(oldValues, key);
                            string newValueStr = GetStringValue(newValues, key);
                            if (oldValueStr != newValueStr) { changes.Add(new ChangeDetailViewModel { FieldName = key, OldValue = oldValueStr, NewValue = newValueStr }); }
                        }
                    }
                    else
                    {
                        var values = doc.RootElement.Deserialize<Dictionary<string, JsonElement>>();
                        foreach (var kvp in values.OrderBy(k => k.Key))
                        {
                            string valueStr = GetStringValue(values, kvp.Key);
                            if (!string.IsNullOrEmpty(valueStr) && kvp.Key != "Id") { changes.Add(new ChangeDetailViewModel { FieldName = kvp.Key, OldValue = (logEntry.status == "Silinən") ? valueStr : "", NewValue = (logEntry.status == "Yaradılan") ? valueStr : "" }); }
                        }
                    }
                }
            }
            catch { /* Fail silently if JSON is malformed */ }

            Application.Current.Dispatcher.Invoke(() =>
            {
                ChangeDetails.Clear();
                foreach (var change in changes) { ChangeDetails.Add(change); }
            });
        }

        private string GetStringValue(Dictionary<string, JsonElement> dict, string key)
        {
            if (dict.TryGetValue(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.Null) return string.Empty;
                if (element.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(element.GetString(), out var date)) { return date.TimeOfDay == TimeSpan.Zero ? date.ToString("yyyy-MM-dd") : date.ToString("g"); }
                    return element.GetString();
                }
                return element.ToString();
            }
            return string.Empty;
        }
    }

    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        public RelayCommand(Action execute) { _execute = execute; }
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute();
        public event EventHandler CanExecuteChanged { add { } remove { } }
    }
}