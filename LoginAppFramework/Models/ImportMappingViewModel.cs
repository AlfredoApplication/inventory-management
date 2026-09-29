using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

namespace LoginAppFramework
{
    public class ImportMappingViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public string ExcelUserName { get; set; }
        public List<Worker> AllDbWorkers { get; set; }
        private Worker _selectedDbWorker;

        public Worker SelectedDbWorker
        {
            get => _selectedDbWorker;
            set
            {
                if (ReferenceEquals(_selectedDbWorker, value)) return;
                _selectedDbWorker = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedDbWorker)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUnmapped)));
            }
        }

        // IsUnmapped is true if the user has not selected a valid worker.
        public bool IsUnmapped => SelectedDbWorker == null || SelectedDbWorker.Id == 0;

        public ImportMappingViewModel(string excelName, List<Worker> allDbWorkers)
        {
            ExcelUserName = excelName;

            // Create a new list for the dropdown that includes a "(none)" option.
            var workerOptions = new List<Worker>
            {
                new Worker { per_adiper_soyadi = "(Leave Unassigned)", Id = 0 }
            };
            workerOptions.AddRange(allDbWorkers.OrderBy(w => w.per_adiper_soyadi));
            AllDbWorkers = workerOptions;

            // --- THIS IS THE NEW, TRULY LOGICAL AUTO-MAPPING ---
            SelectedDbWorker = FindBestMatch(excelName, allDbWorkers) ?? workerOptions[0];
        }

        private Worker FindBestMatch(string excelName, List<Worker> dbWorkers)
        {
            if (string.IsNullOrWhiteSpace(excelName)) return null;

            string normalizedExcelName = NormalizeName(excelName);
            if (string.IsNullOrEmpty(normalizedExcelName)) return null;

            Worker bestMatch = null;
            int bestScore = 0;

            foreach (var worker in dbWorkers)
            {
                string normalizedDbName = NormalizeName(worker.per_adiper_soyadi);
                if (string.IsNullOrEmpty(normalizedDbName)) continue;

                int score = 0;
                var excelParts = normalizedExcelName.Split(' ');

                // Score based on matching parts
                foreach (var part in excelParts)
                {
                    if (normalizedDbName.Contains(part))
                    {
                        score++;
                    }
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestMatch = worker;
                }
                // If we get a new best score that is the same, it becomes ambiguous, so reset.
                else if (score == bestScore && bestScore > 0)
                {
                    bestMatch = null;
                }
            }

            // Only return a match if the score is high enough to be confident.
            // This requires at least two name parts to match.
            return bestScore >= 2 ? bestMatch : null;
        }

        // A simpler, more robust normalization function
        private string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            var sb = new StringBuilder();
            foreach (char c in name.ToLowerInvariant())
            {
                if (char.IsLetter(c))
                {
                    sb.Append(c);
                }
                else if (c == ' ')
                {
                    sb.Append(c); // Keep spaces for splitting
                }
            }
            // Remove common suffixes and clean up multiple spaces
            return sb.ToString().Replace("hr", "").Replace("  ", " ").Trim();
        }
    }
}