using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows;

namespace LoginAppFramework
{
    public partial class ChangeDetailWindow : Window
    {
        private readonly AssetLog _logEntry;
        public bool LogWasDeleted { get; private set; } = false;

        public ChangeDetailWindow(AssetLog logEntry)
        {
            InitializeComponent();
            _logEntry = logEntry;

            TitleTextBlock.Text = $"Vəsait Dəyişiklikləri: {logEntry.VesaitinAdi}";
            SubtitleTextBlock.Text = logEntry.ChangeDate.ToString("yyyy-MM-dd HH:mm:ss");

            // Audit records are immutable. They can be inspected and deleted assets can be
            // restored, but the audit evidence itself is never removed from the UI.
            DeleteButton.Visibility = Visibility.Collapsed;

            var changes = new List<ChangeDetailViewModel>();
            try
            {
                if (string.IsNullOrEmpty(logEntry.ChangeDetails))
                {
                    throw new Exception("ChangeDetails JSON is empty.");
                }

                using (JsonDocument doc = JsonDocument.Parse(logEntry.ChangeDetails))
                {
                    if (logEntry.status == "Dəyişdirilən")
                    {
                        string oldValuesJson = doc.RootElement.GetProperty("OldValues").GetString();
                        string newValuesJson = doc.RootElement.GetProperty("NewValues").GetString();

                        using JsonDocument oldDoc = JsonDocument.Parse(oldValuesJson);
                        using JsonDocument newDoc = JsonDocument.Parse(newValuesJson);

                        var oldValues = oldDoc.RootElement.Deserialize<Dictionary<string, JsonElement>>();
                        var newValues = newDoc.RootElement.Deserialize<Dictionary<string, JsonElement>>();
                        var allKeys = oldValues.Keys.Union(newValues.Keys).Distinct();

                        foreach (var key in allKeys)
                        {
                            string oldValueStr = GetStringValue(oldValues, key);
                            string newValueStr = GetStringValue(newValues, key);

                            if (oldValueStr != newValueStr)
                            {
                                changes.Add(new ChangeDetailViewModel
                                {
                                    FieldName = key,
                                    OldValue = oldValueStr,
                                    NewValue = newValueStr
                                });
                            }
                        }
                    }
                    else
                    {
                        var values = doc.RootElement.Deserialize<Dictionary<string, JsonElement>>();
                        foreach (var kvp in values)
                        {
                            string valueStr = GetStringValue(values, kvp.Key);
                            if (!string.IsNullOrEmpty(valueStr))
                            {
                                changes.Add(new ChangeDetailViewModel
                                {
                                    FieldName = kvp.Key,
                                    OldValue = (logEntry.status == "Silinən") ? valueStr : "",
                                    NewValue = (logEntry.status == "Yaradılan") ? valueStr : ""
                                });
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Dəyişiklik detallarını oxumaq mümkün olmadı.\n\nXəta: " + ex.Message);
            }

            ChangesDataGrid.ItemsSource = changes.OrderBy(c => c.FieldName).ToList();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Audit qeydləri dəyişdirilmir və silinmir.",
                "Audit qorunması",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // --- THIS METHOD CONTAINS THE FINAL FIX ---
        private string GetStringValue(Dictionary<string, JsonElement> dict, string key)
        {
            if (dict.TryGetValue(key, out var element))
            {
                if (key == "MaintenanceHistory" || key == "CustomFields")
                {
                    try
                    {
                        // STEP 1: Get the inner JSON as a STRING. This unwraps the first layer of quotes.
                        string innerJson = element.GetString();
                        if (string.IsNullOrEmpty(innerJson) || innerJson == "[]" || innerJson == "{}") return "(Boş)";

                        // STEP 2: Parse and format the INNER JSON string.
                        if (key == "MaintenanceHistory")
                        {
                            var records = JsonSerializer.Deserialize<List<MaintenanceRecord>>(innerJson);
                            if (records == null || !records.Any()) return "(Boş)";
                            return string.Join("\n", records.Select(r => $"{r.MaintenanceType}: {r.Description} ({r.Cost:C})"));
                        }

                        if (key == "CustomFields")
                        {
                            var fields = JsonSerializer.Deserialize<Dictionary<string, string>>(innerJson);
                            if (fields == null || !fields.Any()) return "(Boş)";
                            return string.Join("\n", fields.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
                        }
                    }
                    catch
                    {
                        return element.ToString(); // Fallback if anything goes wrong
                    }
                }

                if (element.ValueKind == JsonValueKind.Null) return string.Empty;
                if (element.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(element.GetString(), out var date))
                    {
                        return date.TimeOfDay == TimeSpan.Zero ? date.ToString("yyyy-MM-dd") : date.ToString("g");
                    }
                    return element.GetString();
                }
                return element.ToString();
            }
            return string.Empty;
        }
    }
}