using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public enum WorkerSelectionKind
    {
        Worker,
        ClearAssignment,
        NoChange,
        Invalid
    }

    public sealed class WorkerSelectionOption
    {
        public WorkerSelectionKind Kind { get; init; }
        public string DisplayName { get; init; }
        public Worker Worker { get; init; }

        public static WorkerSelectionOption ForWorker(Worker worker)
            => new()
            {
                Kind = WorkerSelectionKind.Worker,
                DisplayName = worker?.per_adiper_soyadi ?? string.Empty,
                Worker = worker
            };

        public static WorkerSelectionOption Clear()
            => new()
            {
                Kind = WorkerSelectionKind.ClearAssignment,
                DisplayName = "(Boşdur)"
            };

        public static WorkerSelectionOption NoChange()
            => new()
            {
                Kind = WorkerSelectionKind.NoChange,
                DisplayName = "(Dəyişiklik yoxdur)"
            };
    }

    public sealed class WorkerSelectionResolution
    {
        public WorkerSelectionKind Kind { get; init; }
        public Worker Worker { get; init; }
        public string ErrorMessage { get; init; }
        public bool IsValid => Kind != WorkerSelectionKind.Invalid;
    }

    public static class WorkerSelectionResolver
    {
        public static WorkerSelectionResolution Resolve(
            string typedText,
            WorkerSelectionOption selectedOption,
            IEnumerable<WorkerSelectionOption> options,
            WorkerSelectionKind blankSelectionKind)
        {
            var optionList = options?.ToList() ?? new List<WorkerSelectionOption>();
            string text = typedText?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(text))
                return ResolveBlank(blankSelectionKind);

            if (selectedOption != null &&
                string.Equals(
                    text,
                    selectedOption.DisplayName?.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return FromOption(selectedOption);
            }

            var exactMatches = optionList
                .Where(option =>
                    string.Equals(
                        option.DisplayName?.Trim(),
                        text,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (exactMatches.Count == 1)
                return FromOption(exactMatches[0]);

            if (exactMatches.Count > 1)
            {
                return new WorkerSelectionResolution
                {
                    Kind = WorkerSelectionKind.Invalid,
                    ErrorMessage =
                        "Eyni adlı birdən çox əməkdaş tapıldı. Zəhmət olmasa əməkdaşı siyahıdan seçin."
                };
            }

            return new WorkerSelectionResolution
            {
                Kind = WorkerSelectionKind.Invalid,
                ErrorMessage =
                    "Yazdığınız əməkdaş siyahıda tapılmadı. Zəhmət olmasa siyahıdan düzgün əməkdaş seçin."
            };
        }

        private static WorkerSelectionResolution ResolveBlank(
            WorkerSelectionKind blankSelectionKind)
        {
            if (blankSelectionKind == WorkerSelectionKind.NoChange)
            {
                return new WorkerSelectionResolution
                {
                    Kind = WorkerSelectionKind.NoChange
                };
            }

            return new WorkerSelectionResolution
            {
                Kind = WorkerSelectionKind.ClearAssignment
            };
        }

        private static WorkerSelectionResolution FromOption(
            WorkerSelectionOption option)
        {
            if (option.Kind == WorkerSelectionKind.Worker && option.Worker == null)
            {
                return new WorkerSelectionResolution
                {
                    Kind = WorkerSelectionKind.Invalid,
                    ErrorMessage = "Əməkdaş seçimi etibarsızdır."
                };
            }

            return new WorkerSelectionResolution
            {
                Kind = option.Kind,
                Worker = option.Worker
            };
        }
    }
}
