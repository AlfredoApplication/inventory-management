using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace LoginAppFramework
{
    public class HistoryLogViewModel : INotifyPropertyChanged
    {
        public AssetLog Log { get; }
        public AssignmentHistoryEntry Assignment { get; }

        public string Description { get; private set; }
        public string ChangedBy { get; set; }
        public DateTime Timestamp { get; private set; }
        public AppIconKind Icon { get; private set; }
        public string Status { get; private set; }

        public bool IsDeleted => Log?.status == "Silinən";
        public bool CanRestore { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        public HistoryLogViewModel(AssetLog log, bool canRestore = true)
        {
            Log = log;
            CanRestore = log?.status == "Silinən" && canRestore;
            Timestamp = log.ChangeDate;
            ChangedBy = log.ChangedBy ?? "Sistem/Trigger";
            Status = log.status;
            GenerateAssetLogSummary();
        }

        public HistoryLogViewModel(AssignmentHistoryEntry assignment, string assetName)
        {
            Assignment = assignment;
            Timestamp = assignment.ChangeDate;
            ChangedBy = assignment.ChangedBy;
            Status = "Təhkimat";
            Description = assignment.Action switch
            {
                AssignmentAction.Assigned => $"{assetName} təhkim olundu: {assignment.ToWorkerName}",
                AssignmentAction.Unassigned => $"{assetName} təhkimdən çıxarıldı: {assignment.FromWorkerName}",
                AssignmentAction.Reassigned => $"{assetName} yenidən təhkim olundu: {assignment.FromWorkerName} -> {assignment.ToWorkerName}",
                _ => "Naməlum təhkimat əməliyyatı"
            };
            Icon = AppIconKind.History;
        }

        private void GenerateAssetLogSummary()
        {
            Icon = Log.status switch
            {
                "Yaradılan" => AppIconKind.Add,
                "Dəyişdirilən" => AppIconKind.Edit,
                "Silinən" => AppIconKind.Delete,
                _ => AppIconKind.Info
            };

            // --- THIS IS THE CORRECTED LOGIC ---
            if (Log.status == "Dəyişdirilən" && !string.IsNullOrEmpty(Log.ChangeDetails))
            {
                var changes = new List<string>();
                try
                {
                    using (JsonDocument doc = JsonDocument.Parse(Log.ChangeDetails))
                    {
                        var oldValues = doc.RootElement.GetProperty("OldValues").Deserialize<Dictionary<string, JsonElement>>();
                        var newValues = doc.RootElement.GetProperty("NewValues").Deserialize<Dictionary<string, JsonElement>>();
                        var allKeys = oldValues.Keys.Union(newValues.Keys).Distinct();

                        foreach (var key in allKeys)
                        {
                            string oldValueStr = GetStringValue(oldValues, key);
                            string newValueStr = GetStringValue(newValues, key);
                            if (oldValueStr != newValueStr)
                            {
                                changes.Add(key); // Just add the name of the field that changed
                            }
                        }
                    }
                }
                catch
                {
                    // If parsing fails for any reason, just show the simple description.
                    Description = $"{Log.VesaitinAdi} - {Log.status}";
                    return;
                }

                if (changes.Any())
                {
                    Description = $"{Log.VesaitinAdi} - Dəyişdirilən: {string.Join(", ", changes)}";
                }
                else
                {
                    Description = $"{Log.VesaitinAdi} - {Log.status} (dəyişiklik yoxdur)";
                }
            }
            else
            {
                Description = $"{Log.VesaitinAdi} - {Log.status}";
            }
        }

        private string GetStringValue(Dictionary<string, JsonElement> dict, string key)
        {
            if (dict.TryGetValue(key, out var element))
            {
                if (element.ValueKind == JsonValueKind.Null) return "";
                return element.ToString();
            }
            return "";
        }
    }
}