using System;
using System.Collections.Generic;
using System.Linq;

namespace LoginAppFramework
{
    public static class FuzzySearchHelper
    {
        public static int Score(
            string title,
            string searchText,
            string query)
        {
            string normalizedQuery = Normalize(query);
            if (string.IsNullOrWhiteSpace(normalizedQuery))
                return 0;

            string normalizedTitle = Normalize(title);
            string normalizedSearch = Normalize(searchText);

            if (normalizedTitle.StartsWith(
                    normalizedQuery,
                    StringComparison.Ordinal))
            {
                return 0;
            }

            if (normalizedTitle.Contains(
                    normalizedQuery,
                    StringComparison.Ordinal))
            {
                return 5;
            }

            if (normalizedSearch.Contains(
                    normalizedQuery,
                    StringComparison.Ordinal))
            {
                return 10;
            }

            string[] terms = normalizedQuery.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

            var haystackWords = Tokenize(normalizedSearch);

            int fuzzyCost = 0;
            foreach (string term in terms)
            {
                if (normalizedSearch.Contains(
                        term,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int bestDistance = int.MaxValue;

                foreach (string word in haystackWords)
                {
                    int lengthDelta =
                        Math.Abs(word.Length - term.Length);

                    int allowed = AllowedDistance(term.Length);
                    if (lengthDelta > allowed)
                        continue;

                    int distance = DamerauLevenshteinDistance(
                        term,
                        word,
                        allowed);

                    if (distance < bestDistance)
                        bestDistance = distance;

                    if (bestDistance == 0)
                        break;
                }

                if (bestDistance > AllowedDistance(term.Length))
                    return int.MaxValue;

                fuzzyCost += bestDistance;
            }

            return 20 + fuzzyCost;
        }

        private static int AllowedDistance(int length)
        {
            if (length <= 3)
                return 0;

            if (length <= 7)
                return 1;

            return 2;
        }

        private static List<string> Tokenize(string value)
            => value
                .Split(
                    new[]
                    {
                        ' ', '•', '-', '_', '/', '\\',
                        '.', ',', ':', ';', '(', ')',
                        '[', ']', '{', '}'
                    },
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();

        private static string Normalize(string value)
            => (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant();

        private static int DamerauLevenshteinDistance(
            string source,
            string target,
            int maximum)
        {
            if (source == target)
                return 0;

            if (Math.Abs(source.Length - target.Length) > maximum)
                return maximum + 1;

            int rows = source.Length + 1;
            int columns = target.Length + 1;
            var matrix = new int[rows, columns];

            for (int i = 0; i < rows; i++)
                matrix[i, 0] = i;

            for (int j = 0; j < columns; j++)
                matrix[0, j] = j;

            for (int i = 1; i < rows; i++)
            {
                int rowMinimum = int.MaxValue;

                for (int j = 1; j < columns; j++)
                {
                    int cost =
                        source[i - 1] == target[j - 1] ? 0 : 1;

                    int value = Math.Min(
                        Math.Min(
                            matrix[i - 1, j] + 1,
                            matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);

                    if (i > 1 &&
                        j > 1 &&
                        source[i - 1] == target[j - 2] &&
                        source[i - 2] == target[j - 1])
                    {
                        value = Math.Min(
                            value,
                            matrix[i - 2, j - 2] + 1);
                    }

                    matrix[i, j] = value;
                    rowMinimum = Math.Min(rowMinimum, value);
                }

                if (rowMinimum > maximum &&
                    i > target.Length + maximum)
                {
                    return maximum + 1;
                }
            }

            int result = matrix[source.Length, target.Length];
            return result > maximum
                ? maximum + 1
                : result;
        }
    }
}
