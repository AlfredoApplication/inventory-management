using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;

namespace LoginAppFramework
{
    public partial class HighlightTextBlock : UserControl
    {
        public static readonly DependencyProperty TextProperty =
            DependencyProperty.Register(
                nameof(Text),
                typeof(string),
                typeof(HighlightTextBlock),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty HighlightProperty =
            DependencyProperty.Register(
                nameof(Highlight),
                typeof(string),
                typeof(HighlightTextBlock),
                new PropertyMetadata(string.Empty, OnTextChanged));

        public static readonly DependencyProperty TextForegroundProperty =
            DependencyProperty.Register(
                nameof(TextForeground),
                typeof(Brush),
                typeof(HighlightTextBlock),
                new PropertyMetadata(null, OnTextChanged));

        public static readonly DependencyProperty TextFontSizeProperty =
            DependencyProperty.Register(
                nameof(TextFontSize),
                typeof(double),
                typeof(HighlightTextBlock),
                new PropertyMetadata(14d, OnTextChanged));

        public static readonly DependencyProperty TextFontWeightProperty =
            DependencyProperty.Register(
                nameof(TextFontWeight),
                typeof(FontWeight),
                typeof(HighlightTextBlock),
                new PropertyMetadata(FontWeights.Normal, OnTextChanged));

        public HighlightTextBlock()
        {
            InitializeComponent();
            Loaded += (_, _) => RebuildInlines();
        }

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public string Highlight
        {
            get => (string)GetValue(HighlightProperty);
            set => SetValue(HighlightProperty, value);
        }

        public Brush TextForeground
        {
            get => (Brush)GetValue(TextForegroundProperty);
            set => SetValue(TextForegroundProperty, value);
        }

        public double TextFontSize
        {
            get => (double)GetValue(TextFontSizeProperty);
            set => SetValue(TextFontSizeProperty, value);
        }

        public FontWeight TextFontWeight
        {
            get => (FontWeight)GetValue(TextFontWeightProperty);
            set => SetValue(TextFontWeightProperty, value);
        }

        private static void OnTextChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is HighlightTextBlock control &&
                control.IsLoaded)
            {
                control.RebuildInlines();
            }
        }

        private void RebuildInlines()
        {
            string text = Text ?? string.Empty;
            string[] terms = (Highlight ?? string.Empty)
                .Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries |
                    StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .ToArray();

            DisplayTextBlock.FontSize = TextFontSize;
            DisplayTextBlock.FontWeight = TextFontWeight;

            if (TextForeground != null)
                DisplayTextBlock.Foreground = TextForeground;

            DisplayTextBlock.Inlines.Clear();

            if (text.Length == 0 || terms.Length == 0)
            {
                DisplayTextBlock.Inlines.Add(
                    new Run(text));
                return;
            }

            var highlighted = new bool[text.Length];

            foreach (string term in terms)
            {
                if (term.Length == 0)
                    continue;

                int start = 0;
                while (start < text.Length)
                {
                    int index = text.IndexOf(
                        term,
                        start,
                        StringComparison.CurrentCultureIgnoreCase);

                    if (index < 0)
                        break;

                    for (int i = index;
                         i < Math.Min(index + term.Length, text.Length);
                         i++)
                    {
                        highlighted[i] = true;
                    }

                    start = index + Math.Max(1, term.Length);
                }
            }

            Brush highlightBackground =
                TryFindResource("WarningSurfaceBrush") as Brush;

            int segmentStart = 0;
            bool segmentHighlighted = highlighted[0];

            for (int i = 1; i <= text.Length; i++)
            {
                bool currentHighlighted =
                    i < text.Length && highlighted[i];

                if (i < text.Length &&
                    currentHighlighted == segmentHighlighted)
                {
                    continue;
                }

                string segment =
                    text.Substring(segmentStart, i - segmentStart);

                var run = new Run(segment);

                if (segmentHighlighted)
                {
                    run.FontWeight = FontWeights.Bold;
                    if (highlightBackground != null)
                        run.Background = highlightBackground;
                }

                DisplayTextBlock.Inlines.Add(run);

                segmentStart = i;
                if (i < text.Length)
                    segmentHighlighted = currentHighlighted;
            }
        }
    }
}
