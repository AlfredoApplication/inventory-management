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
        public IReadOnlyList<AuditDiffItem> Changes { get; private set; }
            = Array.Empty<AuditDiffItem>();
        public bool HasChanges => Changes.Count > 0;

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
                AssignmentAction.Assigned =>
                    $"{assetName} təhkim olundu",
                AssignmentAction.Unassigned =>
                    $"{assetName} təhkimdən çıxarıldı",
                AssignmentAction.Reassigned =>
                    $"{assetName} yenidən təhkim olundu",
                _ => "Naməlum təhkimat əməliyyatı"
            };

            Changes = assignment.Action switch
            {
                AssignmentAction.Assigned => new[]
                {
                    new AuditDiffItem
                    {
                        Field = "Təhkim olunan əməkdaş",
                        OldValue = "—",
                        NewValue = DisplayValue(assignment.ToWorkerName)
                    }
                },
                AssignmentAction.Unassigned => new[]
                {
                    new AuditDiffItem
                    {
                        Field = "Təhkim olunan əməkdaş",
                        OldValue = DisplayValue(assignment.FromWorkerName),
                        NewValue = "—"
                    }
                },
                AssignmentAction.Reassigned => new[]
                {
                    new AuditDiffItem
                    {
                        Field = "Təhkim olunan əməkdaş",
                        OldValue = DisplayValue(assignment.FromWorkerName),
                        NewValue = DisplayValue(assignment.ToWorkerName)
                    }
                },
                _ => Array.Empty<AuditDiffItem>()
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

            if (Log.status == "Dəyişdirilən" &&
                !string.IsNullOrWhiteSpace(Log.ChangeDetails))
            {
                Changes = ParseChanges(Log.ChangeDetails);

                Description = Changes.Count > 0
                    ? $"{Log.VesaitinAdi} - {Changes.Count} sahə dəyişdirildi"
                    : $"{Log.VesaitinAdi} - Dəyişdirilən (fərq yoxdur)";

                return;
            }

            Description = $"{Log.VesaitinAdi} - {Log.status}";
            Changes = Array.Empty<AuditDiffItem>();
        }

        private static IReadOnlyList<AuditDiffItem> ParseChanges(
            string changeDetails)
        {
            try
            {
                using var document = JsonDocument.Parse(changeDetails);
                JsonElement root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object ||
                    !root.TryGetProperty("OldValues", out var oldElement) ||
                    !root.TryGetProperty("NewValues", out var newElement))
                {
                    return Array.Empty<AuditDiffItem>();
                }

                oldElement = UnwrapJson(oldElement);
                newElement = UnwrapJson(newElement);

                var oldValues = ToDictionary(oldElement);
                var newValues = ToDictionary(newElement);

                var allKeys = oldValues.Keys
                    .Union(newValues.Keys, StringComparer.OrdinalIgnoreCase)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(GetFieldOrder)
                    .ThenBy(GetFieldLabel)
                    .ToList();

                var changes = new List<AuditDiffItem>();

                foreach (string key in allKeys)
                {
                    oldValues.TryGetValue(key, out JsonElement oldValue);
                    newValues.TryGetValue(key, out JsonElement newValue);

                    string oldText = FormatValue(key, oldValue);
                    string newText = FormatValue(key, newValue);

                    if (string.Equals(
                            oldText,
                            newText,
                            StringComparison.Ordinal))
                    {
                        continue;
                    }

                    changes.Add(new AuditDiffItem
                    {
                        Field = GetFieldLabel(key),
                        OldValue = oldText,
                        NewValue = newText
                    });
                }

                return changes;
            }
            catch
            {
                return Array.Empty<AuditDiffItem>();
            }
        }

        private static JsonElement UnwrapJson(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.String)
                return element;

            string json = element.GetString();
            if (string.IsNullOrWhiteSpace(json))
                return element;

            using var nested = JsonDocument.Parse(json);
            return nested.RootElement.Clone();
        }

        private static Dictionary<string, JsonElement> ToDictionary(
            JsonElement element)
        {
            var values = new Dictionary<string, JsonElement>(
                StringComparer.OrdinalIgnoreCase);

            if (element.ValueKind != JsonValueKind.Object)
                return values;

            foreach (var property in element.EnumerateObject())
                values[property.Name] = property.Value.Clone();

            return values;
        }

        private static string FormatValue(
            string key,
            JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Undefined ||
                element.ValueKind == JsonValueKind.Null)
            {
                return "—";
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                string value = element.GetString();

                if (string.IsNullOrWhiteSpace(value))
                    return "—";

                if (key.Contains(
                        "Date",
                        StringComparison.OrdinalIgnoreCase) &&
                    DateTime.TryParse(value, out DateTime date))
                {
                    return date.ToString(
                        date.TimeOfDay == TimeSpan.Zero
                            ? "dd.MM.yyyy"
                            : "dd.MM.yyyy HH:mm");
                }

                return value;
            }

            if (element.ValueKind == JsonValueKind.True)
                return "Bəli";

            if (element.ValueKind == JsonValueKind.False)
                return "Xeyr";

            if (element.ValueKind == JsonValueKind.Object)
            {
                var properties = element
                    .EnumerateObject()
                    .Select(property =>
                        $"{property.Name}: {FormatValue(property.Name, property.Value)}")
                    .Take(8)
                    .ToList();

                return properties.Count == 0
                    ? "—"
                    : string.Join("; ", properties);
            }

            if (element.ValueKind == JsonValueKind.Array)
            {
                int count = element.GetArrayLength();
                return count == 0 ? "—" : $"{count} element";
            }

            return element.ToString();
        }

        private static string DisplayValue(string value)
            => string.IsNullOrWhiteSpace(value) ? "—" : value;

        private static int GetFieldOrder(string key)
            => key switch
            {
                "VesaitinKodu" => 1,
                "VesaitinAdi" => 2,
                "ITAvadanliqlarininSeriyaNomresi" => 3,
                "Kateqoriya" => 4,
                "Status" or "status" => 5,
                "WorkerId" or "TehkimOlunanEmekdas" or "AssignedUser" => 6,
                "YerleshmeYeri" => 7,
                "Erazi" => 8,
                "PurchaseCost" => 9,
                "PurchaseDate" => 10,
                "Supplier" => 11,
                "WarrantyExpirationDate" => 12,
                "UsefulLifeInYears" => 13,
                _ => 100
            };

        private static string GetFieldLabel(string key)
            => key switch
            {
                "VesaitinKodu" => "Vəsaitin kodu",
                "VesaitinAdi" => "Vəsaitin adı",
                "ITAvadanliqlarininSeriyaNomresi" => "Seriya nömrəsi",
                "Kateqoriya" => "Kateqoriya",
                "Status" or "status" => "Status",
                "WorkerId" => "İşçi ID",
                "TehkimOlunanEmekdas" or "AssignedUser" =>
                    "Təhkim olunan əməkdaş",
                "Vezifesi" or "AssignedPosition" => "Vəzifə",
                "BolmeShobeDepartment" or "Department" => "Departament",
                "YerleshmeYeri" => "Yerləşmə yeri",
                "Erazi" => "Ərazi",
                "PurchaseCost" => "Alış qiyməti",
                "PurchaseDate" => "Alınma tarixi",
                "Supplier" => "Təchizatçı",
                "WarrantyExpirationDate" => "Zəmanət bitmə tarixi",
                "UsefulLifeInYears" => "İstifadə müddəti",
                "LifecycleStatus" => "Həyat dövrü statusu",
                "CustomFields" => "Kateqoriyaya xas məlumatlar",
                "MaintenanceHistory" => "Texniki xidmət tarixçəsi",
                _ => key
            };

    }
}